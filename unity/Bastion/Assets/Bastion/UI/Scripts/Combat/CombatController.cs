using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.InputSystem;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The combat's hands (game.ts of the 0.10.1, UI's; docs/UNITY-UI.md §6), in four files:
    /// - this one: who plays and who holds the command, which soldier is picked (Tab, or a click on it), the floor shown,
    ///   the tile under the pointer and the marks of a move there (MoveOverlay), the keys;
    /// - Orders: what is armed (an aim, a throw, something by hand, a self ability) and the orders that go out;
    /// - Bar: the action bar's keys;
    /// - Hud: what the HUD shows, from the state (the presenter).
    /// Orders go out as the engine's commands (Order) and the network's messages (Message), which the game sends.
    /// Clicks on the HUD stay the HUD's.
    /// </summary>
    public sealed partial class CombatController : MonoBehaviour, IHudHandlers
    {
        /// <summary>The latest state: orders and their checks go by it.</summary>
        public GameState State;

        /// <summary>What has been played out on screen so far (the event player's); null, the same as State.</summary>
        public GameState View;

        /// <summary>What the player reads (turn, squad, health): what has been seen.</summary>
        public GameState Shown => View ?? State;

        /// <summary>This player's seat.</summary>
        public int MySlot;

        /// <summary>Who is in the room (the server's `room`: protocol.ts PlayerInfo); empty, a lone player.</summary>
        public List<PlayerInfo> Players = new List<PlayerInfo>();

        public CameraRig Rig;
        public MoveOverlay Overlay;

        /// <summary>The combat's sound (Red's CombatSound, 0.10.1's sfx.ts): the event player calls it as game.ts called sfx.</summary>
        public readonly Bastion.Client.Audio.CombatSound Sfx = new Bastion.Client.Audio.CombatSound();

        /// <summary>The HUD over the map, if any: its views, and a click on it isn't the map's.</summary>
        public HudRoot Hud;

        /// <summary>The floor shown (the floor buttons, PgUp/PgDn): the pointer picks on it.</summary>
        public int Level;

        /// <summary>Whether the squad sees a unit (the fog of war: game.ts visibleAliens); by default, the squad's own and the enemies in its sight (Seen).</summary>
        public Func<Unit, bool> Visible;

        /// <summary>
        /// Where a unit's body stands now (its feet, mid-walk included), to pick it with a click and ring it; by default
        /// its figure's (Arte's Figures), else the middle of its tile.
        /// </summary>
        public Func<string, Vector3?> BodyOf;

        /// <summary>An order for the engine (move, ability, end turn…), for the game to send.</summary>
        public event Action<Command> Order;

        /// <summary>A message for the room (abandon, a new mission…), for the game to send.</summary>
        public event Action<ClientMessage> Message;

        /// <summary>The pick changed (null: none).</summary>
        public event Action<string> Picked;

        /// <summary>F on a partner's moment (the event player's): the camera there; false, none.</summary>
        public Func<bool> SeeMoment;

        /// <summary>Whether a unit still has events to be seen (the event player's): its move's marks wait for them.</summary>
        public Func<string, bool> Busy = id => false;

        /// <summary>The enemy's turn on screen (the event player's): the space bar hurries it then (Hurry), and gives no orders.</summary>
        public Func<bool> EnemyTurnPlaying = () => false;
        public Action<bool> Hurry;

        string picked;
        string partnerPicked;
        Color partnerColor;
        Reach reach;
        List<MoveTile> tiles;
        Pos hover;
        bool hoverHeld;

        public string PickedId => picked;
        public Pos Hovered => hover;

        void Awake()
        {
            if (BodyOf == null) BodyOf = id => Figures.FeetOf(id) is Transform t ? t.position : (Vector3?)null;
            if (Visible == null) Visible = u => u.Team == Teams.Xcom || seen.Contains(u.Id);
        }

        void Start()
        {
            if (Hud != null)
            {
                Hud.Bind(this);
                Hud.Look += at => LookAt(at - Vector3.up * Board.Tile);
                if (Hud.HeadOf == null) Hud.HeadOf = FlagAt;
            }
        }

        void Send(Command c)
        {
            // In the pause the host turns orders down; said here at once (PauseTick).
            if (HeldByPause(c))
            {
                Toast(HudText.Menu.PausedOrder);
                return;
            }
            Order?.Invoke(c);
        }
        void Send(ClientMessage m) => Message?.Invoke(m);

        readonly List<Vector3> enemyFeet = new List<Vector3>();

        /// <summary>Where the enemies in sight stand now, for their rings (alive, on a floor not cut away over the one shown).</summary>
        List<Vector3> EnemyFeet()
        {
            enemyFeet.Clear();
            foreach (var u in Shown.Units)
                if (u.Alive && u.Team == Teams.Alien && !u.Evacuated && u.Pos.Z <= Level && (Visible?.Invoke(u) ?? true) && FeetOf(u.Id) is Vector3 feet)
                    enemyFeet.Add(feet);
            return enemyFeet;
        }

        void Toast(string text)
        {
            if (Hud != null) Hud.Toast(text);
        }

        /// <summary>«Ajuste auto» watching this battle's frame rate (App.QualityGuard), from its start.</summary>
        App.QualityGuard guard;
        bool qualityNoticed;

        /// <summary>The guard took the level down: said once, quietly (game.ts qualityLowered; the details are in Ajustes).</summary>
        void QualityLowered()
        {
            if (qualityNoticed) return;
            qualityNoticed = true;
            Toast("Calidad ajustada para ir más fluido.");
            Hud?.Log("Calidad ajustada para ir más fluido (Menú → Ajustes).");
        }

        // ------------------------------------------------------- who plays

        /// <summary>When each player who dropped did (unscaled: the enemy turn's speed doesn't shorten it): within the server's grace they still count as here (game.ts droppedAt).</summary>
        readonly Dictionary<int, float> droppedAt = new Dictionary<int, float>();

        /// <summary>The partner whose soldiers this player commands too, gone past the grace (game.ts commandingFor); null, none.</summary>
        string commandingFor;

        /// <summary>When the next dropped player's grace runs out (unscaled), to say so then; none waiting, +∞.</summary>
        float graceEnds = float.PositiveInfinity;

        /// <summary>
        /// The room's players, as the server says (game.ts setPlayers): one who drops still counts as here for the server's
        /// grace (Room.ReconnectGraceMs; AwayGraceMs if their game was out of sight); past it this player commands their
        /// soldiers too, and is told so once (CommandChanged).
        /// </summary>
        public void SetPlayers(List<PlayerInfo> players)
        {
            var now = Time.unscaledTime;
            foreach (var p in players)
            {
                if (p.Connected) droppedAt.Remove(p.Slot);
                else if (Players.FirstOrDefault(q => q.Slot == p.Slot)?.Connected == true)
                {
                    droppedAt[p.Slot] = now;
                    graceEnds = Mathf.Min(graceEnds, now + Grace(p) + 0.2f);
                }
            }
            Players = players;
            if (!CommandChanged()) Refresh();
        }

        static float Grace(PlayerInfo p) => (p.Away == true ? Bastion.Net.Room.AwayGraceMs : Bastion.Net.Room.ReconnectGraceMs) / 1000f;

        /// <summary>The players who count as here (game.ts activeSlots): connected, or dropped within the server's grace.</summary>
        List<int> ActiveSlots()
        {
            var now = Time.unscaledTime;
            return Players.Where(p => p.Connected || droppedAt.TryGetValue(p.Slot, out var at) && now - at < Grace(p)).Select(p => p.Slot).ToList();
        }

        /// <summary>The partner who is gone and has soldiers on the field (game.ts partnerGone): this player commands them too.</summary>
        PlayerInfo PartnerGone()
        {
            if (State == null) return null;
            var active = ActiveSlots();
            return Players.FirstOrDefault(p => p.Slot != MySlot && !active.Contains(p.Slot) && State.Units.Any(u => u.Alive && u.Team == Teams.Xcom && u.Owner == p.Slot));
        }

        /// <summary>
        /// The partner's soldiers come under this player's orders (the partner is gone) or go back to them (game.ts
        /// commandChanged): said once, and everything drawn again. Whether it changed.
        /// </summary>
        bool CommandChanged()
        {
            var gone = PartnerGone()?.Name;
            if (gone == commandingFor) return false;
            var before = commandingFor;
            commandingFor = gone;
            if (gone != null) Toast($"{gone} no está: mandas también sus soldados.");
            else if (before != null) Toast($"{before} ha vuelto: cada uno manda en lo suyo.");
            Refresh();
            return true;
        }

        /// <summary>
        /// For tests (PackSteps «gone»): the dropped players' grace over now. A capture's game time runs far ahead of the
        /// unscaled one, so it never sees 30 s go by.
        /// </summary>
        public void EndGraces()
        {
            foreach (var slot in droppedAt.Keys.ToList()) droppedAt[slot] = float.NegativeInfinity;
            GraceOver();
        }

        /// <summary>A dropped player's grace ran out: the next one's to wait for, and whether the command changed.</summary>
        void GraceOver()
        {
            var now = Time.unscaledTime;
            graceEnds = float.PositiveInfinity;
            foreach (var p in Players)
                if (!p.Connected && droppedAt.TryGetValue(p.Slot, out var at) && at + Grace(p) + 0.2f > now) graceEnds = Mathf.Min(graceEnds, at + Grace(p) + 0.2f);
            CommandChanged();
        }

        /// <summary>The partner, when connected: only then is there a command to share.</summary>
        PlayerInfo PartnerHere() => Players.FirstOrDefault(p => p.Slot != MySlot && p.Connected);

        string PlayerName(int? slot) => Players.FirstOrDefault(p => p.Slot == slot)?.Name ?? "el otro jugador";

        string PlayerColor(int slot) => Players.FirstOrDefault(p => p.Slot == slot)?.Color ?? Content.PlayerColors[slot];

        /// <summary>
        /// Whether this player gives orders to `u` (rules.ts commandsFor): its own, the shared ones, and the partner's while
        /// the partner is gone (Arturo, 17/10). A siege's turret fires on its own: nobody's.
        /// </summary>
        public bool Commands(Unit u) => u.Team == Teams.Xcom && u.Automaton == null && Content.CommandsFor(MySlot, u.Owner, ActiveSlots());

        /// <summary>Whether this player holds the command (el mando); a holder who left blocks nobody.</summary>
        public bool HasCommand()
        {
            var holder = State.Command;
            return holder == null || holder == MySlot || !Players.Any(p => p.Slot == holder && p.Connected);
        }

        /// <summary>Orders are taken once the enemy's turn has played out on screen, not just on the server.</summary>
        public bool CanAct() => State != null && State.ActiveTeam == Teams.Xcom && Shown.ActiveTeam == Teams.Xcom && State.Outcome == null;

        List<Unit> MyUnits() => State.Units.Where(u => u.Alive && Commands(u)).ToList();

        static Unit UnitById(GameState s, string id) => id == null ? null : s.Units.FirstOrDefault(x => x.Id == id);

        Unit Unit() => UnitById(State, picked) is Unit u && u.Alive ? u : null;

        // ------------------------------------------------------- picking

        /// <summary>Picks a soldier (null: none), its reach worked out and shown, the camera to it (`look`).</summary>
        public void Pick(string id, bool look = true)
        {
            if (Rig != null) Rig.TakeControl();
            if (picked != id)
            {
                picked = id;
                CancelModeSilently();
            }
            var u = Unit();
            looking = u != null && u.Ap <= 0 && CanAct() ? u.Id : null;
            if (u != null && look && Rig != null) Rig.Focus(Board.TileToWorld(State, u.Pos));
            // Picking another of one's own while one is in play is allowed, to look: it says why it can't act yet.
            var waiting = u != null && Commands(u) && u.Ap > 0 ? WaitingFor(u.Id) : null;
            if (waiting != null) Toast(waiting);
            Refresh();
            Picked?.Invoke(id);
        }

        /// <summary>The soldier the partner has picked, ringed in their colour (null: none).</summary>
        public void PartnerPicked(string id, Color color)
        {
            partnerPicked = id;
            partnerColor = color;
        }

        /// <summary>
        /// Turns go one soldier at a time (playtest 15/10 #9; rules.ts `inPlay`): while one of mine is in play with actions
        /// and the partner can still play, another of mine waits. Why, or null.
        /// </summary>
        string WaitingFor(string id)
        {
            var s = State;
            var busy = s.ActiveUnit != null && s.ActiveUnit != id ? UnitById(s, s.ActiveUnit) : null;
            if (busy == null || !busy.Alive || busy.Ap <= 0 || busy.Owner != MySlot || !HasCommand()) return null;
            var partner = PartnerHere();
            if (partner == null || s.Ready[partner.Slot] || !AbilityTexts.BusySlots(s).Contains(partner.Slot)) return null;
            return $"Primero termina con {busy.Name}, o cede el turno.";
        }

        /// <summary>Keeps a valid pick (game.ts autoSelect): the one in play if it is mine, else the current one if it can still act, else the next that can.</summary>
        void AutoSelect()
        {
            var current = Unit();
            var playing = UnitById(State, State.ActiveUnit);
            var mineInPlay = playing != null && playing.Alive && playing.Ap > 0 && Commands(playing) ? playing : null;
            var turned = State.ActiveUnit != lastInPlay || State.Command != lastCommand;
            lastInPlay = State.ActiveUnit;
            lastCommand = State.Command;
            if (mineInPlay != null && turned && CanAct() && HasCommand())
            {
                looking = null;
                if (current?.Id == mineInPlay.Id) return;
                picked = mineInPlay.Id;
                TurnCamera(mineInPlay);
                return;
            }
            // The partner's soldier comes into play: the camera to it too, if wanted (#47).
            if (turned && playing != null && playing.Alive && !Commands(playing) && playing.Team == Teams.Xcom) TurnCamera(playing);
            if (current != null && current.Id == looking)
            {
                if (current.Ap <= 0) return;
                looking = null;
            }
            if (current != null && Commands(current) && (current.Ap > 0 || !CanAct())) return;
            var next = mineInPlay ?? MyUnits().FirstOrDefault(x => x.Ap > 0) ?? (current != null && Commands(current) ? current : MyUnits().FirstOrDefault());
            picked = next?.Id;
            if (next != null && current != null && next.Id != current.Id && CanAct()) TurnCamera(next);
        }

        /// <summary>The camera keeps off a turn's soldier this long after the player moved it (game.ts TURN_CAMERA_QUIET_MS).</summary>
        const float TurnCameraQuiet = 4f;

        /// <summary>
        /// The camera to the soldier whose turn it is (playtest 17/10 #47; game.ts turnCamera): as the turn begins, the
        /// command passes or a soldier comes into play, if the HUD's switch («Cámara al soldado») says so; never fighting
        /// the player (not if they moved the camera in the last few seconds).
        /// </summary>
        void TurnCamera(Unit u)
        {
            if (u == null || !u.Alive || Hud == null || !Hud.FollowsTurn || Rig == null || Time.unscaledTime - Rig.TakenAt < TurnCameraQuiet) return;
            SetLevel(u.Pos.Z);
            Rig.Focus(FeetOf(u.Id) ?? Board.TileToWorld(State, u.Pos));
        }

        /// <summary>The camera to `slot`'s soldier whose turn it is in `v` (the event player: a turn opens, the command passes).</summary>
        public void TurnCamera(GameState v, int slot, bool opening = false) => TurnCamera(TurnSoldier(v, slot, opening));

        string lastInPlay;
        int? lastCommand;

        /// <summary>One picked with no actions left, to look at its sheet (playtest 18/10 #2): it stays until another is picked.</summary>
        string looking;

        /// <summary>The next (or previous) of one's own that can still act (game.ts cycleUnit).</summary>
        void CycleUnit(int dir)
        {
            var list = MyUnits().Where(x => x.Ap > 0 || !CanAct()).ToList();
            if (list.Count == 0) return;
            var i = list.FindIndex(x => x.Id == picked);
            var next = list[((i < 0 ? (dir > 0 ? -1 : 0) : i) + dir + list.Count) % list.Count];
            Pick(next.Id);
        }

        // ------------------------------------------------------- floors

        /// <summary>The highest storey of the map anyone can stand on (game.ts topLevel): 0 without storeys.</summary>
        int MaxLevel
        {
            get
            {
                var levels = State.Levels;
                if (levels == null) return 0;
                for (var k = levels.Count; k >= 1; k--)
                    if (levels[k - 1].Tiles.Any(t => t != null && Grid.CanStandOn(t))) return k;
                return 0;
            }
        }

        /// <summary>
        /// The floor shown (game.ts setLevel): the camera rises or drops to it, the map shows it (Arte's ShowLevel), and the
        /// pointer, the range and the previews move to it.
        /// </summary>
        void SetLevel(int k)
        {
            var level = Mathf.Clamp(k, 0, MaxLevel);
            if (level == Level && (MaxLevel == 0 || MapView.Current == null || MapView.Current.Level == level)) return;
            Level = level;
            if (MaxLevel > 0 && MapView.Current != null) MapView.Current.ShowLevel(level);
            if (Rig != null)
            {
                var at = Rig.Target;
                Rig.Focus(new Vector3(at.x, level * Board.Storey, at.z));
            }
            hover = null;
            Refresh();
        }

        /// <summary>The floor follows the picked soldier: when it is picked, and when it climbs or comes down.</summary>
        void FollowLevel()
        {
            var u = Unit();
            var key = u != null ? $"{u.Id}@{u.Pos.Z}" : null;
            if (key == levelFor) return;
            levelFor = key;
            if (u == null) return;
            Level = Mathf.Clamp(u.Pos.Z, 0, MaxLevel);
            if (MaxLevel > 0 && MapView.Current != null) MapView.Current.ShowLevel(Level);
        }

        string levelFor;

        // ------------------------------------------------------- refresh

        /// <summary>
        /// Everything that hangs on the state and the pick (game.ts refreshInteractive): the pick kept valid, the floor,
        /// what is armed checked, the reach and the pointer's marks again, the HUD.
        /// </summary>
        public void Refresh()
        {
            if (State == null) return;
            See();
            AutoSelect();
            FollowLevel();
            CheckMode();
            var u = Unit();
            var moving = mode == Mode.Move && u != null && CanAct() && Commands(u) && u.Ap > 0 && WaitingFor(u.Id) == null && !Busy(u.Id);
            reach = moving ? Paths.ComputeReach(State, u, Paths.MaxMoveCost(u)) : null;
            tiles = reach != null ? Tiles(u) : null;
            // Without the command the range is still shown, fainter: it is for planning (#35). Only the floor shown's.
            Overlay?.ShowRange(State, tiles?.Where(t => t.Pos.Z == Level).ToList(), Planning, Soft);
            ShowWay();
            DrawPlans();
            RefreshHud();
        }

        bool Planning => !HasCommand();

        /// <summary>
        /// Where `u` can end its move (game.ts reach): past one action's cost, the dash; with only the last action left,
        /// all of it drawn as the dash (Arturo, 21/10 #10, as XCOM's); where an enemy the squad sees would notice it while
        /// the squad is hidden.
        /// </summary>
        List<MoveTile> Tiles(Unit u)
        {
            var perAp = Paths.CostPerAp(u);
            var last = LastAction();
            var watchers = State.Concealed ? State.Units.Where(a => a.Alive && a.Team == Teams.Alien && Visible(a)).ToList() : new List<Unit>();
            return Paths.Destinations(State, u, reach).Select(d => new MoveTile
            {
                Pos = d.Pos,
                Dash = last || d.Cost > perAp,
                Detected = watchers.Any(a => Los.InViewCone(a, d.Pos) && Los.SightOrigin(State, a.Pos, d.Pos, Los.DetectionTo(State, a, d.Pos)) != null),
            }).ToList();
        }

        /// <summary>Whether the picked soldier has only its last action left (one gone of two): its move drawn as a dash's (#10).</summary>
        bool LastAction()
        {
            var u = Unit();
            return u != null && u.Ap == 1 && Content.ApPerTurnOf(u) > 1;
        }

        /// <summary>A thing in the way, not a wall (a car or a crate: cover; the objective's console; someone standing): the border leaves it alone (Ana, 21/10 #18).</summary>
        bool Soft(int x, int y, int z)
        {
            var p = new Pos { X = x, Y = y, Z = z };
            var kind = Grid.KindAt(State, p);
            if (kind == "high" || kind == "low" || kind == "console") return true;
            return Grid.UnitAt(State, p) != null;
        }

        /// <summary>The pointer on `place`, as if it were there (for captures; the real pointer takes over when it moves).</summary>
        public void Hover(Pos place)
        {
            hover = place;
            hoverHeld = true;
            ShowWay();
            RefreshHud();
        }

        /// <summary>A click on `place`, as the pointer's there (tests: PackSteps «click=x,y»).</summary>
        public void ClickAt(Pos place)
        {
            Hover(place);
            var at = Rig != null ? (Vector2)Rig.Camera.WorldToScreenPoint(Board.TileToWorld(State, place)) : Vector2.zero;
            Click(at);
        }

        // ------------------------------------------------------- input

        void Update()
        {
            guard ??= new App.QualityGuard(QualityLowered);
            guard.Frame();
            if (State == null) return;
            if (Time.unscaledTime >= graceEnds) GraceOver();
            if (moment != null && Time.time >= momentUntil)
            {
                moment = null;
                RefreshMarks();
            }
            if (Overlay != null)
            {
                Overlay.ShowPicked(Unit() != null ? FeetOf(picked) : null);
                Overlay.ShowPartner(partnerPicked != picked ? FeetOf(partnerPicked) : null, partnerColor);
                Overlay.ShowEnemies(EnemyFeet());
            }
            Keys();
            PauseTick();
            Cutaway();
            ClearView();
            CloseFlags();
            var mouse = Mouse.current;
            if (mouse == null || Rig == null) return;
            var at = mouse.position.ReadValue();
            if (mouse.delta.ReadValue() != Vector2.zero) hoverHeld = false;
            var overHud = Hud != null && Hud.Over(at);
            // Any click or the wheel in the game lets «TU TURNO» go and does what it does (Arturo, 23/10 #8: aiming, it
            // stayed in the middle); one on the stamp itself only lets it go (Banner, with its sound).
            if (Hud != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame || mouse.scroll.ReadValue().y != 0) && !Hud.OverBanner(at))
                Hud.ReleaseBanner(true);
            // A press on the map closes the backpack's menu (one on the HUD, its root: Backpack.PressedAt).
            if (Hud != null && !overHud && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) Hud.ClosePackMenu();
            RightClick(mouse, at, overHud);
            if (!hoverHeld)
            {
                var place = !overHud && Board.RayToPlace(State, Rig.Camera.ScreenPointToRay(at), Level, out var p) ? p : null;
                if (!Same(place, hover))
                {
                    hover = place;
                    ShowWay();
                    if (mode != Mode.Move) RefreshHud();
                    else RefreshFlags();
                }
            }
            // (Not with Cmd or Ctrl too: a screenshot's Cmd+Mayús+4 is no signal. Red, 28/10.)
            var shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed && !App.Shortcuts.Elsewhere(Keyboard.current);
            if (!overHud && (Hud == null || !Hud.MenuOpen)) App.GameCursor.Ask(CursorOn(hover, shift));
            if (Hud != null && Hud.MenuOpen) return;
            // Shift and the click, or the wheel's click, mark the tile for both (playtest 19/10 #14); Shift or Alt and the wheel, the floor.
            if (!overHud && mouse.leftButton.wasPressedThisFrame)
            {
                if (shift) Ping();
                else Click(at);
            }
            if (!overHud && mouse.middleButton.wasPressedThisFrame) Ping();
            // Shift turns the wheel sideways on a Mac (game.ts: deltaY || deltaX); a trackpad sends a burst of these, one floor a gesture.
            var scroll = mouse.scroll.ReadValue();
            var wheel = scroll.y != 0 ? scroll.y : scroll.x;
            var alt = Keyboard.current != null && Keyboard.current.altKey.isPressed;
            if ((shift || alt) && wheel != 0 && !overHud)
            {
                if (Time.unscaledTime - lastFloorWheel > 0.3f) SetLevel(Level + (wheel > 0 ? 1 : -1));
                lastFloorWheel = Time.unscaledTime;
            }
        }

        float lastFloorWheel = -10;

        /// <summary>Where the right button went down off the HUD (a drag of it moves the camera: CameraRig); null, it isn't down.</summary>
        Vector2? rightFrom;
        bool rightMoved;

        /// <summary>
        /// A right click, not a drag (game.ts onUp): planning without the command, it takes the picked soldier's plan away
        /// (playtest 18/10 #25); elsewhere it backs out of aiming, throwing or a door. (On a known enemy the 0.10.1 opened
        /// its file: that comes with the file.)
        /// </summary>
        void RightClick(Mouse mouse, Vector2 at, bool overHud)
        {
            if (mouse.rightButton.wasPressedThisFrame && !overHud)
            {
                rightFrom = at;
                rightMoved = false;
            }
            if (rightFrom is Vector2 from && Mathf.Abs(at.x - from.x) + Mathf.Abs(at.y - from.y) > 4 * Mathf.Max(1, Screen.height / 900f)) rightMoved = true;
            if (!mouse.rightButton.wasReleasedThisFrame || rightFrom == null) return;
            rightFrom = null;
            if (rightMoved || (Hud != null && Hud.MenuOpen)) return;
            // On an enemy in sight, its file (game.ts: a right click, not a drag, while moving); planning, it takes the
            // picked one's plan away; elsewhere it backs out of what is armed.
            var enemy = mode == Mode.Move && modeAbility == null && hover != null ? Grid.UnitAt(Shown, hover) : null;
            var planned = Unit();
            if (enemy != null && enemy.Alive && enemy.Team == Teams.Alien && Visible(enemy)) ShowEnemyFile(enemy);
            else if (mode == Mode.Move && !HasCommand() && planned != null && plans.Any(p => p.unit == planned.Id && p.slot == MySlot)) WithdrawPlan(planned);
            else if (mode != Mode.Move || modeAbility != null) CancelMode();
        }

        /// <summary>
        /// The map's cursor (game.ts updateCursor, #5): the brackets with Mayús held (a click signals, #14), the crosshair
        /// on an enemy in sight while there is a soldier to give orders to, the yellow arrow on another of this player's
        /// soldiers (a click picks it), else the arrow.
        /// </summary>
        App.GameCursor.Kind CursorOn(Pos place, bool shift)
        {
            if (shift) return App.GameCursor.Kind.Signal;
            var hovered = place != null ? Grid.UnitAt(State, place) : null;
            var u = Unit();
            if (hovered != null && hovered.Alive && hovered.Team == Teams.Alien && Visible(hovered) && u != null && CanAct()) return App.GameCursor.Kind.Aim;
            if (hovered != null && hovered.Alive && hovered.Id != u?.Id && Commands(hovered)) return App.GameCursor.Kind.Hot;
            return App.GameCursor.Kind.Arrow;
        }

        /// <summary>The shot over a shoulder on now, and who is in it (the event player's).</summary>
        public System.Func<(string attacker, string target)?> CloseFocus;

        /// <summary>
        /// What the buildings' cutaway opens for (Arte's Cutaway; game.ts mapView.watch): the picked soldier, the tile
        /// under the pointer, the target, and in the shot over a shoulder who is in it. Field by field: its level is
        /// MapView.ShowLevel's.
        /// </summary>
        void Cutaway()
        {
            var cut = Bastion.Client.Map.Cutaway.Current;
            if (cut == null) return;
            cut.Focus.selected = picked;
            cut.Focus.hover = hover;
            cut.Focus.target = mode == Mode.Target ? targetId : null;
            var close = CloseFocus?.Invoke();
            cut.Focus.closeAttacker = close?.attacker;
            cut.Focus.closeTarget = close?.target;
            // Over a shoulder, aiming too (game.ts: `close` while rig.closeUp): the buildings stay whole round the shot.
            cut.Focus.close = Rig != null && Rig.CloseUp;
        }

        /// <summary>The keys (game.ts onKey), but the camera's (CameraRig: WASD, arrows, Q/E).</summary>
        void Keys()
        {
            var k = Keyboard.current;
            if (k == null) return;
            // F12: a capture of the screen, whatever is open (Red, 28/10).
            if (k.f12Key.wasPressedThisFrame) App.Screenshots.Take(Toast);
            // Cmd or Ctrl held, the keys are the system's (a screenshot's Cmd+Mayús+4 picked the pistol: Ana, 28/10); the
            // space let go still ends «Rápida».
            if (Bastion.UI.App.Shortcuts.Elsewhere(k))
            {
                if (k.spaceKey.wasReleasedThisFrame) Hurry?.Invoke(false);
                return;
            }
            var enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
            // The game's menu or a question up: the keys are theirs, but Esc, which closes them.
            if (Hud != null && Hud.MenuOpen)
            {
                if (k.escapeKey.wasPressedThisFrame) Hud.MenuBack();
                if (k.spaceKey.wasReleasedThisFrame) Hurry?.Invoke(false);
                return;
            }
            // A sheet or an enemy's file over the battle: only to look at; Esc or H closes it, the other keys wait.
            if (Hud != null && Hud.WindowOpen)
            {
                if (k.escapeKey.wasPressedThisFrame || k.hKey.wasPressedThisFrame) Hud.CloseWindow();
                return;
            }
            // H: the picked soldier's sheet (hud.ts CONTROLS «H · clic en su nombre»).
            if (k.hKey.wasPressedThisFrame && picked != null)
            {
                OpenSheet(picked);
                return;
            }
            // The mission's briefing stays until «Entendido» (playtest 14/10 #3; Ana, 20/10 #32): Intro and Space are that
            // button; Esc does nothing over it.
            if (Hud != null && Hud.IntroOpen && (enter || k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
            {
                if (!k.escapeKey.wasPressedThisFrame) Hud.CloseIntro();
                return;
            }
            // «TU TURNO» held till it is seen (#50): Intro or Esc let it go and do nothing else; any other key goes on (Arturo, 23/10 #8).
            if ((enter || k.escapeKey.wasPressedThisFrame) && Hud != null && Hud.ReleaseBanner()) return;
            // (Mayús, Ctrl, Alt and Cmd alone don't: game.ts.)
            if (Hud != null && Bastion.UI.App.Shortcuts.AnyOther(k)) Hud.ReleaseBanner(true);
            for (var n = 0; n <= 9; n++)
            {
                // (The Input System's keys run Digit1…Digit9, Digit0, then Mayús, Alt, Ctrl and Cmd: Digit0 + n made Mayús
                // the 1 and left the numbers dead. Ana, 28/10.)
                if (!k[n == 0 ? Key.Digit0 : Key.Digit1 + (n - 1)].wasPressedThisFrame) continue;
                NumberKey(n);
                return;
            }
            var shift = k.shiftKey.isPressed;
            if (k.tabKey.wasPressedThisFrame)
            {
                if (mode == Mode.Target || HandArmed() != null) CycleTarget(shift ? -1 : 1);
                else CycleUnit(shift ? -1 : 1);
            }
            // In the enemy's turn the space bar, held, plays it «Rápida» (docs/UX-RITMO.md); let go, the chosen speed.
            var space = k.spaceKey.wasPressedThisFrame;
            if (k.spaceKey.wasReleasedThisFrame) Hurry?.Invoke(false);
            if (space && EnemyTurnPlaying() && (Hud == null || !Hud.Covering))
            {
                Hurry?.Invoke(true);
                space = false;
            }
            if (enter || space) ConfirmOrAttack();
            if (k.escapeKey.wasPressedThisFrame) Escape();
            if (k.cKey.wasPressedThisFrame) PassCommand();
            // M: all the sound off or back (game.ts KeyM; Red's Volumes, kept as Ajustes' «Sonido»).
            if (k.mKey.wasPressedThisFrame) Toast(Bastion.Client.Audio.Volumes.ToggleMute() ? "Sonido desactivado (M)" : "Sonido activado (M)");
            if (k.oKey.wasPressedThisFrame) NextMark();
            if (k.gKey.wasPressedThisFrame) Ping();
            if (k.backspaceKey.wasPressedThisFrame) ToggleReady();
            if (k.pageUpKey.wasPressedThisFrame) SetLevel(Level + 1);
            if (k.pageDownKey.wasPressedThisFrame) SetLevel(Level - 1);
            if (k.fKey.wasPressedThisFrame)
            {
                // A partner's moment on screen first (its notice's «Ver»), then what the partner holding the command does.
                if (SeeMoment != null && SeeMoment()) return;
                if (PartnerHere() != null && !HasCommand() && CanAct()) WatchPartner();
                else GoToSoldier();
            }
        }

        /// <summary>The detected tile clicked once (breaking concealment can't be undone: the first click only warns).</summary>
        string concealWarn;

        /// <summary>
        /// A click on the map (game.ts clickTile): a target picked, or acted on if picked already; one of the squad picked
        /// (a partner's says whose); with something armed, it is done there; an enemy aimed at with the first attack that
        /// reaches it; a closed door or a box beside, opened; a tile of the reach, moved to (planned without the command;
        /// warned first where the squad would be seen).
        /// </summary>
        void Click(Vector2 screen)
        {
            var u = Unit();
            var body = BodyUnder(screen);
            var tile = hover;
            // Aiming: a click on one of the targets picks it; again, it acts.
            if (mode == Mode.Target && body != null && TargetOptions(modeAbility).Any(o => o.Unit.Id == body.Id))
            {
                if (targetId == body.Id) ConfirmTarget();
                else
                {
                    targetId = body.Id;
                    Refresh();
                }
                return;
            }
            // With an area armed, a soldier's tile is a place to throw at like any other (the preview warns «¡Aliado!»).
            if (body != null && body.Team == Teams.Xcom && mode != Mode.Tile)
            {
                // A turret is nobody's to pick.
                if (body.Automaton != null) return;
                if (Commands(body)) Pick(body.Id);
                else Toast($"{body.Name} es de la escuadra de {(body.Owner != null ? PlayerName(body.Owner) : "el otro jugador")}.");
                return;
            }
            if (!CanAct() || u == null || tile == null) return;
            var waiting = WaitingFor(u.Id);
            if (waiting != null)
            {
                Toast(waiting);
                return;
            }
            // Without the command a click plans instead of ordering (#35): aiming and areas only preview.
            var planning = !HasCommand();
            // By hand: a click on what is marked does it, anywhere else backs out.
            if (HandArmed() != null)
            {
                if (HandTiles().Any(t => Same(t, tile))) ConfirmHand(tile);
                else CancelMode();
                return;
            }
            if (mode == Mode.Tile)
            {
                ThrowAt(tile);
                return;
            }
            var enemy = body ?? Grid.UnitAt(State, tile);
            if (enemy != null && enemy.Team == Teams.Alien && Visible(enemy))
            {
                var attack = AbilityTexts.PrimaryAttacks.FirstOrDefault(id => BarView(id)?.Enabled == true && TargetOptions(id).Any(o => o.Unit.Id == enemy.Id));
                if (attack != null) EnterTarget(attack, enemy.Id);
                else Toast("No puede atacar a ese enemigo desde aquí.");
                return;
            }
            // A click on the ground backs out of aiming, or of an armed self ability.
            if (mode == Mode.Target || mode == Mode.Self)
            {
                CancelMode();
                return;
            }
            // A closed door beside the soldier: the click opens it, for free (playtest 13/10 #2). Furniture beside: it searches it (18/10 #29).
            foreach (var id in new[] { AbilityIds.OpenDoor, AbilityIds.Search })
            {
                if (!HandTiles(id).Any(t => Same(t, tile))) continue;
                var view = BarView(id);
                if (view?.Enabled == true) UseDoor(id, tile);
                else if (view?.Reason != null) Toast(view.Reason);
                return;
            }
            var dest = tiles?.FirstOrDefault(t => Same(t.Pos, tile));
            if (dest == null || !reach.Cost.TryGetValue(Grid.NodeOf(State, tile), out var cost) || Paths.ApForCost(u, cost) > u.Ap) return;
            if (planning)
            {
                TogglePlan(u, tile);
                return;
            }
            var key = $"{u.Id}|{tile.X},{tile.Y}";
            if (dest.Detected && concealWarn != key)
            {
                concealWarn = key;
                Toast("Desde ahí os ven: se rompe la ocultación. Haz clic otra vez para mover.");
                return;
            }
            concealWarn = null;
            Send(new MoveCommand { Unit = u.Id, To = tile });
        }

        /// <summary>The unit whose body is nearest the pointer, within a little of it (BodyOf), or on the pointer's tile.</summary>
        Unit BodyUnder(Vector2 screen)
        {
            Unit best = null;
            var bestD = 28f;
            foreach (var u in State.Units)
            {
                if (!u.Alive || (u.Team != Teams.Xcom && !Visible(u))) continue;
                var w = FeetOf(u.Id);
                if (w == null) continue;
                var sp = Rig.Camera.WorldToScreenPoint(w.Value + Vector3.up * 0.9f);
                if (sp.z <= 0) continue;
                var d = Vector2.Distance(screen, sp);
                if (d < bestD)
                {
                    bestD = d;
                    best = u;
                }
            }
            if (best != null || hover == null) return best;
            return State.Units.FirstOrDefault(u => u.Alive && Same(u.Pos, hover) && (u.Team == Teams.Xcom || Visible(u)));
        }

        /// <summary>
        /// What the pointer's tile shows (game.ts updateHover): moving, on an enemy the squad sees a red ring, and on a tile
        /// the picked one can end its move on the way there, a ring in the band's colour (red where it would be noticed)
        /// and the cover there; aiming, throwing or by hand, their own marks (Orders: ShowArmed).
        /// </summary>
        void ShowWay()
        {
            Overlay?.ShowPath(State, null, null);
            Overlay?.ShowCover(State, null);
            // On any tile a white ring (overlays.ts setHover), blue planning; a move or an enemy colour it below, a throw has none.
            Overlay?.ShowHover(State, mode == Mode.Tile ? null : hover, Planning ? MoveOverlay.PlanRing : MoveOverlay.PlainRing);
            ShowVision();
            ShowArmed();
            // At night, a lit tile says what the light does there and how long it lasts (#50); over one of the dead, whom
            // it sees and where it goes next (21/10 #11). A move, a door or a box beside say theirs instead (below).
            var hovered = hover != null ? Grid.UnitAt(Shown, hover) : null;
            Note(hover, Shown.Mission.Night == true && hover != null && Los.IsLit(Shown, hover) ? Lore.LitNote(LightTurns(hover)) : null, BreakableNote(hover), IntentNote(hovered));
            if (mode == Mode.Tile) ThrowNote();
            if (mode == Mode.Target) AimLitNote();
            if (mode != Mode.Move) return;
            var u = Unit();
            if (u != null && hover != null && CanAct() && Commands(u))
            {
                // A closed door beside the soldier: the click opens it, for free.
                if (HandTiles(AbilityIds.OpenDoor).Any(t => Same(t, hover)))
                {
                    AimMarks?.ShowSpots(State, new List<Pos> { hover }, hover);
                    Note(hover, "Puerta · clic: abrir (no gasta acciones)");
                    return;
                }
                // A box the squad has seen: its name, and whether it can be opened from here (docs/FASE8.md §B).
                var decor = HasSpots ? Looting.SearchSpot(State, hover) : null;
                if (decor != null)
                {
                    var name = Capitalize(Lore.SearchSpots.TryGetValue(decor, out var spot) ? spot.Name : "la caja");
                    if (Looting.WasSearched(State, hover))
                    {
                        Note(hover, $"{name} · ya está abierta");
                        return;
                    }
                    var near = HandTiles(AbilityIds.Search).Any(t => Same(t, hover));
                    var blocked = near ? Abilities.AbilityBlocker(State, u, AbilityIds.Search) : null;
                    AimMarks?.ShowSpots(State, new List<Pos> { hover }, near && blocked == null ? hover : null);
                    Note(hover, !near ? $"{name} · se puede abrir" : blocked != null ? $"{name} · {blocked.TrimEnd('.').ToLowerInvariant()}" : $"{name} · clic: abrir");
                    return;
                }
            }
            var dest = u != null && hover != null ? tiles?.FirstOrDefault(t => Same(t.Pos, hover)) : null;
            var enemy = hover != null ? Grid.UnitAt(State, hover) : null;
            if (enemy != null && (enemy.Team != Teams.Alien || !Visible(enemy))) enemy = null;
            if (enemy != null)
            {
                Overlay?.ShowHover(State, enemy.Pos, MoveOverlay.EnemyRing);
                return;
            }
            if (u == null || dest == null) return;
            var path = Paths.BuildPath(State, u, reach, hover);
            Overlay?.ShowPath(State, u.Pos, path, dest.Dash, Planning);
            Overlay?.ShowCover(State, CoverMatters() ? hover : null);
            Overlay?.ShowHover(State, hover, dest.Detected ? MoveOverlay.NoticedRing : dest.Dash ? MoveOverlay.DashRing : MoveOverlay.WalkRing);
            // Running, a door or a window is heard (docs/FASE2.md): said under the destination; and at night, whether it ends in the light.
            Note(hover, path != null ? AbilityTexts.MoveNoise(State, path, dest.Dash) : null, LitNote(hover));
        }

        /// <summary>At night a tile in the light is seen from as far as by day (los.ts sightTo, detectionTo): the previews say so.</summary>
        string LitNote(Pos tile) => State.Mission.Night == true && tile != null && Los.IsLit(State, tile) ? "A la luz: os ven de lejos" : null;

        /// <summary>Aiming at night from a lit tile (where it strikes from, or where it stands): seen from far, said there instead of the pointer's line.</summary>
        void AimLitNote()
        {
            var u = Unit();
            var option = CurrentTarget();
            if (u == null || option == null || !CanAct()) return;
            var from = option.Tile ?? u.Pos;
            var lit = LitNote(from);
            if (lit != null) Note(from, lit);
        }

        /// <summary>The line on `tile`: its parts that say something, joined; none, no line.</summary>
        void Note(Pos tile, params string[] parts)
        {
            if (Hud == null) return;
            var text = string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
            Hud.SetNote(tile != null && text.Length > 0 ? Board.TileToWorld(Shown, tile, 0.15f * Board.Tile) : (Vector3?)null, text);
        }

        static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>The turns left of the light on `p` (a flare's, a fire's); none for a street lamp, which never goes out.</summary>
        int? LightTurns(Pos p)
        {
            var s = Shown;
            var node = Grid.NodeOf(s, p);
            int? lasting = null;
            foreach (var l in s.Lights ?? new List<Bastion.Engine.Light>())
            {
                if (!(l.Tiles != null ? l.Tiles.Contains(node) : Grid.Distance(l.Tile, p) <= l.Radius)) continue;
                if (l.Turns == null) return null;
                lasting = Math.Max(lasting ?? 0, l.Turns.Value);
            }
            return lasting;
        }

        /// <summary>
        /// Over one of the dead under the pointer: whom it sees and where it goes next (playtest 21/10 #11; ai.ts
        /// takeZombieTurn). The Chillón screams and keeps away: only whom it sees.
        /// </summary>
        string IntentNote(Unit a)
        {
            var v = Shown;
            if (a == null || !a.Alive || a.Team != Teams.Alien || !Content.Templates[a.Template].Undead || !Visible(a) || v.Concealed || v.Outcome != null) return null;
            var seen = Grid.Combatants(v, Teams.Xcom).Where(u => Los.CanSee(v, a, u)).Select(u => !string.IsNullOrEmpty(u.Nickname) ? $"«{u.Nickname}»" : u.Name).ToList();
            var who = seen.Count > 1 ? $"{string.Join(", ", seen.Take(seen.Count - 1))} y {seen[seen.Count - 1]}" : seen.FirstOrDefault();
            if (Abilities.AbilitiesOf(a).Contains(AbilityIds.Mindspin)) return who != null ? $"Ve a {who}" : null;
            if (who != null) return $"Ve a {who}: va a por {(seen.Count == 1 ? "ese soldado" : "el más cercano")}";
            if (v.Mission.Siege != null) return "No os ve: va a por el soldado más cercano";
            return a.Heard != null ? "No os ve: va al último ruido que oyó" : "No os ve: deambula";
        }

        /// <summary>A live unit's feet: its body's (BodyOf), or the middle of its tile.</summary>
        /// <summary>
        /// Each figure's top over its feet (m): the highest its head has stood, between 1.2 and 2.4.
        /// </summary>
        readonly Dictionary<string, float> tops = new Dictionary<string, float>();

        /// <summary>
        /// Where a unit's flag hangs: over its feet, at its top and a little more. 0.10.1's hangs from the figure's root
        /// (unitViews.ts: rig.top + 0.45), so it never moves with the body; hung from the head bone, a figure turning
        /// about in the fog swung it round and round (Ana, 28/10: «la barra de vida gira»).
        /// </summary>
        Vector3? FlagAt(string id)
        {
            if (FeetOf(id) is not Vector3 feet) return null;
            var top = Figures.HeadOf(id) is Transform head ? Mathf.Clamp(head.position.y - feet.y, 1.2f, 2.4f) : 1.65f;
            if (tops.TryGetValue(id, out var was) && was >= top) top = was;
            else tops[id] = top;
            return feet + Vector3.up * (top + 0.35f);
        }

        /// <summary>Someone who shoots has been seen in this mission, or the arrival card said the living are about.</summary>
        bool shotsSeen;

        /// <summary>
        /// Whether the squad's cover is worth showing (Arturo, 28/10; Ana: «si no hay enemigos con pistolas…»): a bite
        /// doesn't look at it, a shot does (raiders, turrets). So its shields, over the tile under the pointer and on the
        /// soldiers' flags, show once an enemy that shoots has been seen in the mission, or from the start when the
        /// arrival card announces the living (Lore.LivingTip); never giving away one not yet seen.
        /// </summary>
        bool CoverMatters()
        {
            if (shotsSeen || Shown == null) return shotsSeen;
            foreach (var u in Shown.Units)
                if (u.Alive && u.Team == Teams.Alien && Shoots(u) && Visible(u)) return shotsSeen = true;
            return false;
        }

        /// <summary>Whether `u` has an attack from afar (a gun, a turret's), not only bites and blows.</summary>
        static bool Shoots(Unit u) =>
            Content.Templates.TryGetValue(u.Template, out var t) && t.Abilities.Any(a => Content.IsAttack(a) && Content.Abilities[a].Target == AbilityTargets.Enemy);

        Vector3? FeetOf(string id)
        {
            var u = id != null ? Shown.Units.FirstOrDefault(x => x.Id == id && x.Alive) : null;
            if (u == null) return null;
            return BodyOf?.Invoke(id) ?? Board.TileToWorld(Shown, u.Pos);
        }

        static bool Same(Pos a, Pos b)
        {
            if (a == null || b == null) return a == b;
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }
    }
}
