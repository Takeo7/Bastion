using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The combat played without a network or a map (Gameplay 24/10): a state recorded by the TypeScript engine
    /// (engine-cs/golden/&lt;Step&gt;/&lt;Golden&gt;.json) laid out roughly (the floor grey, walls dark blocks, things in the
    /// way brown, the units capsules, registered as figures), the camera, the marks, the controller and the HUD. Its orders
    /// go to the engine's rules here, as the host's would (Rules.ExecuteCommand on the host's state), and the events back
    /// to this player's copy (Events.ApplyEvent), with no animation yet. For a capture, a soldier is picked, and either the
    /// pointer held on a tile at mid reach or an ability armed (Arm). Throwaway: Arte's map and figures replace the floor
    /// and the capsules.
    /// </summary>
    public sealed class CombatLab : MonoBehaviour
    {
        /// <summary>BASTION_LENTO=ms: each frame that much longer (Ajuste auto's test: App.QualityGuard takes the level down).</summary>
        void Update()
        {
            if (App.GameSettings.SlowForTest > 0) System.Threading.Thread.Sleep(App.GameSettings.SlowForTest);
            App.GameSettings.WatchScreen();
            // The touches' clock: the game's time as it would run at ×1, on through the pause (timeScale 0) and «Rápida».
            clock += Time.timeScale > 0 ? Time.deltaTime / Time.timeScale : Time.captureDeltaTime > 0 ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        }

        float clock;

        /// <summary>A folder of engine-cs/golden (step1, step2).</summary>
        public string Step = "step2";

        /// <summary>A file of that folder, without .json.</summary>
        public string Golden = "city-3-elimination";

        /// <summary>An ability to arm on the first soldier, for a capture (shoot, grenade…); empty, the pointer on a tile instead.</summary>
        public string Arm = "";

        /// <summary>The recorded orders played up to the first shot, and its soldier aiming it at its target (a capture of the aim).</summary>
        public bool ToShot;

        /// <summary>With ToShot, the first shot that kills its target (the shot over the shoulder).</summary>
        public bool Kills;

        /// <summary>With ToShot, the pointer on the target instead of the shot armed (what it sees, its flag's line).</summary>
        public bool HoverTarget;

        /// <summary>
        /// In the scene «Partida»: its map (Arte's Battlefield) and figures (UnitViews) instead of the rough floor and the
        /// capsules, and the events played by the event player into View, as in the game.
        /// </summary>
        public bool InScene;

        /// <summary>What is armed fired by itself a moment after the start (a strip of a shot played).</summary>
        public bool Fire;

        /// <summary>
        /// The recorded orders played up to the first one after which several of the enemy move (the order that ended the
        /// squad's turn), and its recorded events then played by the event player (a strip of the enemy's turn).
        /// </summary>
        public bool ToEnemyTurn;

        /// <summary>
        /// The game's menu open once it is set up, on a page (menu, ajustes, controles) or at a question (volver, abandonar,
        /// salir); with a question, the partner is in too.
        /// </summary>
        public string Menu = "";

        /// <summary>The pointer on a tile a soldier reaches up (or down) a flight of stairs: the path on the treads.</summary>
        public bool Stairs;

        /// <summary>Ana in too, with a plan for one of the soldiers, and a ping of hers on a tile ahead.</summary>
        public bool Plans;

        /// <summary>
        /// The squad put on the evacuation's tiles as the mission starts, and a moment later the call to leave (alone: La
        /// Lata goes at once): the event player's evacDeparted and evacuated, the camera on La Lata and it pulling away.
        /// </summary>
        public bool Evacuate;

        /// <summary>The HUD, if the lab has one (HudShots.Combat makes it).</summary>
        public HudRoot Hud;

        sealed class GoldenFile
        {
            public GameState State { get; set; }
            public int RngSeed { get; set; }
            public List<GoldenOp> Ops { get; set; } = new List<GoldenOp>();
        }

        /// <summary>A recorded order (record-step2.ts): what was asked, the events it gave, and the dice before it.</summary>
        sealed class GoldenOp
        {
            public string Op { get; set; }
            public Command Cmd { get; set; }
            public List<GameEvent> Events { get; set; } = new List<GameEvent>();
            public List<long> Rng { get; set; }
        }

        static readonly Color FloorColor = new Color(0.29f, 0.31f, 0.32f);
        static readonly Color WallColor = new Color(0.2f, 0.22f, 0.23f);
        static readonly Color ThingColor = new Color(0.42f, 0.35f, 0.27f);

        GameState host;
        Rng rng;
        CombatController control;
        EventPlayer player;
        readonly Dictionary<string, Transform> bodies = new Dictionary<string, Transform>();
        Material squad;
        Material enemy;

        /// <summary>
        /// BASTION_TOQUES: keys and clicks at moments of the game (seconds), for strips of what they do: «0:intro» the
        /// arrival card, «lejos» the camera off to a corner, «tuturno» the squad's turn opening, «space», «enter», «esc»,
        /// «click» and «rclick» (on the middle of the map), «hold=d/1.5» a key held that long (the camera's WASD, as a player
        /// taking it), and PackSteps' (the backpack, the bar, the pointer, «end»): «1.5:space,4:click,6:slot=0».
        /// </summary>
        public string Touches;

        void Start()
        {
            if (string.IsNullOrEmpty(Touches)) Touches = System.Environment.GetEnvironmentVariable("BASTION_TOQUES") ?? "";
            // BASTION_TINTE=1: «Ropa con el color del jugador» on for this capture (Arte's figures), not kept.
            if (System.Environment.GetEnvironmentVariable("BASTION_TINTE") == "1") Bastion.Client.Units.SquadTint.On = true;
            // BASTION_INTERFAZ=1.3: Ajustes' «Interfaz» for this capture (the panel put back as play stops).
            App.GameSettings.ApplyInterface();
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../engine-cs/golden", Step, Golden + ".json"));
            if (!File.Exists(path))
            {
                Debug.LogError("Combat lab: no " + path);
                return;
            }
            var file = Json.Read<GoldenFile>(File.ReadAllText(path));
            host = file.State;
            // BASTION_LAB_NOCHE=1: the recorded mission at night (its sky and light, side by side with the 0.10.1's same save).
            if (System.Environment.GetEnvironmentVariable("BASTION_LAB_NOCHE") == "1") host.Mission.Night = true;
            rng = new Rng(file.RngSeed);
            AbilityCommand shot = null;
            GoldenOp turn = null;
            // BASTION_LAB_OP=<n>: the recorded order n's events played (those before it on the state), instead of the first big
            // enemy turn (a small map's turn has fewer than four of the dead walking: arena-siege's blows on its gate, 9).
            var opAt = int.TryParse(System.Environment.GetEnvironmentVariable("BASTION_LAB_OP"), out var at) ? at : -1;
            if (ToEnemyTurn)
                for (var i = 0; i < file.Ops.Count; i++)
                {
                    var op = file.Ops[i];
                    // (Whatever order ended the turn: since 28/10 the recordings have no «end turn», the dead's turn comes
                    // after a soldier's last order, ability, move or pass.)
                    if (opAt >= 0 ? i == opAt : op.Events.Count(x => x is MovedEvent mv && mv.Unit.StartsWith("a")) >= 4)
                    {
                        turn = op;
                        break;
                    }
                    foreach (var e in op.Events) Events.ApplyEvent(host, e);
                }
            if (ToShot)
            {
                // Each recorded order's events, on the host's state, up to the first shot.
                foreach (var op in file.Ops)
                {
                    if (op.Cmd is AbilityCommand a && (a.Ability == AbilityIds.Shoot || a.Ability == AbilityIds.Pistol) && (!Kills || op.Events.Any(x => x is DiedEvent d && d.Unit == a.Target)))
                    {
                        shot = a;
                        if (op.Rng != null) rng = new Rng(op.Rng);
                        break;
                    }
                    foreach (var e in op.Events) Events.ApplyEvent(host, e);
                }
            }
            if (Evacuate) IntoZone(host);
            // BASTION_LAB_ESTORBO=1: another of the squad behind the shooter, in the way of the shot over the shoulder (ClearView).
            if (shot != null && System.Environment.GetEnvironmentVariable("BASTION_LAB_ESTORBO") == "1") InTheWay(host, shot);
            var mine = Events.CloneState(host);
            InScene &= Bastion.Client.Battlefield.Current != null;
            if (InScene) Bastion.Client.Battlefield.Current.Show(mine);
            else
            {
                LayOut(mine);
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                squad = new Material(lit) { color = new Color(0.35f, 0.55f, 0.95f) };
                enemy = new Material(lit) { color = new Color(0.85f, 0.25f, 0.2f) };
                Bodies(mine);
            }

            var cam = Camera.main;
            var rig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            rig.Bounds = new Rect(0, -mine.Height * Board.Tile, mine.Width * Board.Tile, mine.Height * Board.Tile);
            var marks = new GameObject("Marcas");
            control = gameObject.AddComponent<CombatController>();
            control.State = mine;
            control.MySlot = 0;
            control.Players = new List<PlayerInfo> { new PlayerInfo { Slot = 0, Name = "Arturo", Color = Content.PlayerColors[0], Connected = true } };
            control.Rig = rig;
            control.Overlay = marks.AddComponent<MoveOverlay>();
            control.AimMarks = marks.AddComponent<AimOverlay>();
            control.Marks = marks.AddComponent<MissionMarks>();
            control.Hud = Hud;
            control.Order += Play;
            // BASTION_LAB_CAMPANA=1: the sample campaign (SampleCampaign) as the squad's, its first soldier as the first
            // of ours, for the soldier's sheet with its board.
            if (System.Environment.GetEnvironmentVariable("BASTION_LAB_CAMPANA") == "1")
            {
                var campaign = Bastion.UI.Strategy.SampleCampaign.Load();
                control.Campaign = () => campaign;
                var first = mine.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive);
                if (first != null) first.CampaignId = campaign.Soldiers[0].Id;
            }
            if (Hud != null)
            {
                Hud.MenuChose += control.MenuChose;
                Hud.SettingChanged += control.SettingChanged;
            }
            if (Plans) PlansSoon();
            if (!string.IsNullOrEmpty(Menu))
            {
                if (Menu != "menu" && Menu != "ajustes" && Menu != "controles") control.Players.Add(new PlayerInfo { Slot = 1, Name = "Ana", Color = Content.PlayerColors[1], Connected = true });
                MenuSoon();
            }
            if (InScene)
            {
                player = new EventPlayer(control, destroyCancellationToken);
                player.Load(mine);
                control.Halt = player.SetHalted;
                control.Busy = player.IsBusy;
                control.CloseFocus = () => player.CloseFocus;
            }

            if (Touches != "") TouchesSoon();
            if (turn != null && player != null)
            {
                PlaySoon(turn.Events);
                return;
            }
            if (Evacuate)
            {
                EvacSoon(mine);
                return;
            }
            if (Stairs && UpStairs(mine, rig)) return;
            var u = shot != null ? mine.Units.First(x => x.Id == shot.Unit) : mine.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive && x.Ap > 0);
            if (u == null) return;
            control.Pick(u.Id, false);
            rig.Focus(Board.TileToWorld(mine, u.Pos), true);
            if (shot != null && HoverTarget)
            {
                control.Hover(mine.Units.First(x => x.Id == shot.Target).Pos);
                return;
            }
            if (shot != null) control.ArmAt(shot.Ability, shot.Target);
            else if (!string.IsNullOrEmpty(Arm)) control.OnAbility(Arm);
            if (control.Armed)
            {
                if (Fire) FireSoon();
                return;
            }
            // The pointer on a tile it can end its move on, on its floor, at 70 % of its two-action reach (with a throw armed, its area there).
            var reach = Paths.ComputeReach(mine, u, Paths.MaxMoveCost(u));
            var ends = Paths.Destinations(mine, u, reach).Where(d => d.Pos.Z == u.Pos.Z).ToList();
            if (ends.Count == 0) return;
            var max = ends.Max(d => d.Cost);
            control.Hover(ends.OrderBy(d => Mathf.Abs(d.Cost - max * 0.7f)).First().Pos);
        }

        /// <summary>A soldier and a tile it reaches over a flight of stairs, picked and pointed at, the camera between; false if none.</summary>
        bool UpStairs(GameState s, CameraRig rig, bool moved = false)
        {
            foreach (var u in s.Units.Where(x => x.Team == Teams.Xcom && x.Alive && x.Ap > 0))
            {
                var reach = Paths.ComputeReach(s, u, Paths.MaxMoveCost(u));
                foreach (var d in Paths.Destinations(s, u, reach))
                {
                    var path = Paths.BuildPath(s, u, reach, d.Pos);
                    if (path == null || !path.Any(p => Flights.FlightAt(s, p) != null)) continue;
                    control.Pick(u.Id, false);
                    rig.Focus(Vector3.Lerp(Board.TileToWorld(s, u.Pos), Board.TileToWorld(s, d.Pos), 0.5f), true);
                    // From the soldier's floor, close: the flight under the storey above, which that floor's view takes away.
                    control.OnFloor(u.Pos.Z - control.Level);
                    rig.CinematicZoom(12 * Board.Tile);
                    control.Hover(d.Pos);
                    Debug.Log($"[lab] stairs: {u.Id} at {u.Pos.X},{u.Pos.Y},{u.Pos.Z} to {d.Pos.X},{d.Pos.Y},{d.Pos.Z}");
                    return true;
                }
            }
            if (moved)
            {
                Debug.Log("[lab] stairs: none in reach");
                return false;
            }
            // None in reach: the first soldier put at the foot of a flight of several tiles (the lab's own state only).
            var first = s.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive && x.Ap > 0);
            for (var z = 0; first != null && z < 4; z++)
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        var on = Flights.LongFlightAt(s, new Pos { X = x, Y = y, Z = z });
                        if (on == null || on.Step != 0) continue;
                        var foot = Flights.FlightFoot(on.Flight);
                        if (!Grid.InBounds(s, foot.X, foot.Y) || !Flights.CanStopAt(s, foot) || Grid.UnitAt(s, foot) != null) continue;
                        first.Pos = foot;
                        player?.Load(s);
                        Debug.Log($"[lab] stairs: {first.Id} put at the foot {foot.X},{foot.Y},{foot.Z}");
                        return UpStairs(s, rig, true);
                    }
            Debug.Log("[lab] stairs: no flight on the map");
            return false;
        }

        /// <summary>Ana's plan for the second soldier, to a tile it reaches 70 % of the way, and her ping by the first.</summary>
        async void PlansSoon()
        {
            control.Players.Add(new PlayerInfo { Slot = 1, Name = "Ana", Color = Content.PlayerColors[1], Connected = true });
            await Awaitable.WaitForSecondsAsync(0.4f, destroyCancellationToken);
            Hud?.CloseIntro();
            var s = control.State;
            var squad = s.Units.Where(x => x.Team == Teams.Xcom && x.Alive).ToList();
            if (squad.Count < 2) return;
            var u = squad[1];
            var reach = Paths.ComputeReach(s, u, Paths.MaxMoveCost(u));
            var ends = Paths.Destinations(s, u, reach).Where(d => d.Pos.Z == u.Pos.Z).ToList();
            if (ends.Count == 0) return;
            var max = ends.Max(d => d.Cost);
            control.PartnerPlan(1, u.Id, ends.OrderBy(d => Mathf.Abs(d.Cost - max * 0.7f)).First().Pos);
            var first = squad[0];
            control.OnPing(1, new Pos { X = first.Pos.X + 2, Y = first.Pos.Y + 1, Z = first.Pos.Z }, "point");
        }

        async void MenuSoon()
        {
            await Awaitable.WaitForSecondsAsync(0.3f, destroyCancellationToken);
            Hud?.CloseIntro();
            control.OnMenu();
            if (Menu == "ajustes") Hud?.MenuPage("settings");
            else if (Menu == "controles") Hud?.MenuPage("controls");
            else if (Menu == "volver") control.MenuChose(Bastion.UI.Hud.GameMenu.Leave);
            else if (Menu == "abandonar") control.MenuChose(Bastion.UI.Hud.GameMenu.Abandon);
            else if (Menu == "salir") control.MenuChose(Bastion.UI.Hud.GameMenu.Quit);
        }

        /// <summary>«llegada»: a SpawnedEvent pushed into the player, its four on the tiles in the squad's sight nearest the map's edge (`inside`: farthest).</summary>
        void ArriveInSight(bool inside = false)
        {
            var v = control.View ?? control.State;
            var sight = Los.TeamVisibility(v, Teams.Xcom);
            int Edge(Pos p) => Mathf.Min(Mathf.Min(p.X, p.Y), Mathf.Min(v.Width - 1 - p.X, v.Height - 1 - p.Y));
            var spots = Enumerable.Range(0, v.Width * v.Height).Where(i => i < sight.Length && sight[i])
                .Select(i => new Pos { X = i % v.Width, Y = i / v.Width, Z = 0 })
                .Where(p => Grid.IsWalkable(Grid.KindAt(v, p)) && Grid.UnitAt(v, p) == null)
                .OrderBy(p => inside ? -Edge(p) : Edge(p)).Take(4).ToList();
            var living = Content.Templates.First(t => t.Value.Team == Teams.Alien && !t.Value.Undead && !t.Value.Robotic).Key;
            var units = spots.Select((p, k) => Setup.MakeUnit($"lab{k}", k < 3 ? "zombie" : living, Content.Templates[k < 3 ? "zombie" : living].Name, null, p, "plab")).ToList();
            Debug.Log($"[lab] llegada: {string.Join(", ", units.Select(u => $"{u.Template} ({u.Pos.X},{u.Pos.Y}) a {Edge(u.Pos)} del borde"))}");
            if (units.Count == 0) return;
            control.Rig.Focus(Board.TileToWorld(v, spots[0]), true);
            var pod = new Pod { Id = "plab", UnitIds = units.Select(u => u.Id).ToList(), Patrol = new List<Pos>(), Active = true };
            player?.Push(new List<GameEvent> { new SpawnedEvent { Units = units, Pod = pod } }, null);
        }

        /// <summary>«botin»: the first soldier (in the view the player shows) put two tiles from the nearest shut loot box, the fog and the HUD again from there.</summary>
        void LootNear()
        {
            var v = control.View ?? control.State;
            var by = v.Units.FirstOrDefault(u => u.Alive && u.Team == Teams.Xcom);
            if (by == null) return;
            Pos box = null;
            for (var i = 0; i < v.Width * v.Height; i++)
            {
                var p = new Pos { X = i % v.Width, Y = i / v.Width, Z = 0 };
                if (Looting.SearchSpot(v, p) == null || Looting.WasSearched(v, p)) continue;
                if (box == null || Mathf.Abs(p.X - by.Pos.X) + Mathf.Abs(p.Y - by.Pos.Y) < Mathf.Abs(box.X - by.Pos.X) + Mathf.Abs(box.Y - by.Pos.Y)) box = p;
            }
            if (box == null) return;
            var near = Grid.Dirs8.Select(d => new Pos { X = box.X + d.X * 2, Y = box.Y + d.Y * 2, Z = 0 }).FirstOrDefault(p => Grid.InBounds(v, p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(v, p)) && Grid.UnitAt(v, p) == null);
            Debug.Log($"[lab] botín: caja ({box.X},{box.Y}) {Looting.SearchSpot(v, box)}, {by.Id} a {(near == null ? "ningún sitio" : $"({near.X},{near.Y})")}");
            if (near == null) return;
            by.Pos = near;
            Bastion.Client.Map.Fog.Current?.Show(v, true);
            control.Rig.Focus(Board.TileToWorld(v, box), true);
            control.Refresh();
        }

        /// <summary>«caja», «sala»: the camera on the nearest loot box or room to the first soldier, and a second later its search pushed into the player.</summary>
        async Awaitable SearchSoon(bool room)
        {
            var s = control.State;
            var by = s.Units.FirstOrDefault(u => u.Alive && u.Team == Teams.Xcom);
            if (by == null) return;
            float Far(Pos p) => Mathf.Abs(p.X - by.Pos.X) + Mathf.Abs(p.Y - by.Pos.Y);
            Pos at = null;
            LootRoom found = null;
            if (!room)
            {
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        var p = new Pos { X = x, Y = y, Z = 0 };
                        if (Looting.SearchSpot(s, p) != null && (at == null || Far(p) < Far(at))) at = p;
                    }
            }
            else
            {
                var counted = new HashSet<int>();
                var rooms = new List<LootRoom>();
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        if (counted.Contains(y * s.Width + x)) continue;
                        var r = Looting.RoomAt(s, new Pos { X = x, Y = y, Z = 0 });
                        if (r == null) continue;
                        foreach (var t in r.Tiles) counted.Add(t.Y * s.Width + t.X);
                        rooms.Add(r);
                    }
                found = rooms.OrderBy(r => r.Spot == Spots.Shop ? 0 : 1).ThenBy(r => r.Tiles.Min(Far)).FirstOrDefault();
                at = found?.Tiles.OrderBy(t => t.X + t.Y).ElementAtOrDefault(found.Tiles.Count / 2);
            }
            Debug.Log($"[lab] {(room ? "sala" : "caja")}: {(at == null ? "ninguna" : $"({at.X},{at.Y})" + (found != null ? $", {found.Spot}, {found.Tiles.Count} casillas" : ""))}");
            if (at == null) return;
            // In sight without fog, and the pointer there so the building's roof opens (Cutaway).
            control.Rig.Focus(Board.TileToWorld(s, at), true);
            control.Hover(at);
            Bastion.Client.Map.Fog.Current?.SeeAll();
            await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
            GameEvent e = room ? new RoomSearchedEvent { Unit = by.Id, Room = found.Id } : new SearchedEvent { Unit = by.Id, Tile = at };
            player?.Push(new List<GameEvent> { e }, null);
            // Once it is played (the player paces its events), without fog again: it fogs what the squad doesn't see.
            var node = Grid.NodeOf(s, at);
            for (var wait = 0f; wait < 5 && !(room ? control.View.RoomsSearched?.Contains(found.Id) == true : control.View.Searched?.Contains(node) == true); wait += Time.deltaTime)
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            control.Hover(at);
            Bastion.Client.Map.Fog.Current?.SeeAll();
        }

        /// <summary>BASTION_TOQUES played: the keyboard's and the mouse's states queued as a player's would come (Input System).</summary>
        async void TouchesSoon()
        {
#if UNITY_EDITOR
            // In the editor the keyboard and the pointer go to the game only with its view in focus, and a batch run has no
            // view: a copy of the settings that sends them all (the project's own asset untouched).
            var settings = ScriptableObject.Instantiate(InputSystem.settings);
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
#endif
            // Nor is a batch run ever in focus: its devices go on all the same.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keys = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            // (Off from the start: the app had no focus.)
            if (!keys.enabled) InputSystem.EnableDevice(keys);
            if (!mouse.enabled) InputSystem.EnableDevice(mouse);
            // (Its own clock: the pause stops the battle's time, and Esc to close the menu must still come.)
            var start = clock;
            var steps = Touches.Split(',').Select(t => t.Split(':')).Where(t => t.Length == 2).Select(t => (at: float.Parse(t[0], System.Globalization.CultureInfo.InvariantCulture), what: t[1].Trim())).OrderBy(t => t.at).ToList();
            try
            {
                foreach (var (at, what) in steps)
                {
                    while (clock - start < at) await Awaitable.NextFrameAsync(destroyCancellationToken);
                    var cam = control.Rig != null ? control.Rig.Camera : Camera.main;
                    var middle = new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.45f);
                    switch (what)
                    {
                        case "intro":
                            control.Intro();
                            break;
                        case "lejos":
                            // The camera off to a corner of the map, as a player who looked elsewhere (no TakeControl: not the player's).
                            control.Rig.Focus(Board.TileToWorld(control.State, new Pos { X = 4, Y = 4 }), true);
                            break;
                        case "ajustes":
                            // The game's menu open on Ajustes, later than BASTION_MENU (Ajuste auto's note, once the guard took a level).
                            control.OnMenu();
                            Hud?.MenuPage("settings");
                            break;
                        case "autono":
                            // Ajustes' «Ajuste auto» turned off on its own key (what it took given back, its note gone).
                            foreach (var doc in FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None))
                            {
                                if (doc.rootVisualElement == null) continue;
                                var row = UnityEngine.UIElements.UQueryExtensions.Query(doc.rootVisualElement, className: "setting").ToList()
                                    .FirstOrDefault(r => UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(r, className: "setting__name")?.text == "AJUSTE AUTO");
                                var no = row == null ? null : UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Button>(row, className: "setting__choice").ToList().FirstOrDefault(b => b.text == "No");
                                if (no == null) continue;
                                using (var e = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled())
                                {
                                    e.target = no;
                                    no.SendEvent(e);
                                }
                                Debug.Log($"[lab] Ajuste auto: {App.GameSettings.AutoQuality}, {App.GameSettings.Summary()}");
                            }
                            break;
                        case "llegada":
                        case "llegada-dentro":
                            // Three of the dead and one of the living arriving where the squad sees them, nearest the edge
                            // (Arrivals: in on foot from it); «-dentro», farthest from it (up out of the dust where they are).
                            ArriveInSight(what == "llegada-dentro");
                            break;
                        case "botin":
                            // The first soldier two tiles from the nearest shut loot box, and the fog shown again from there:
                            // the box seen and a soldier near, its mark over it (CombatController.LootMarks).
                            LootNear();
                            break;
                        case "caja":
                        case "sala":
                            // The nearest loot box to the squad, or the nearest room (a shop's first), seen and then searched a
                            // second later, as the host's event would come (MapBuilder draws it searched: mapView.ts searchedSeen).
                            await SearchSoon(what == "sala");
                            break;
                        case "giro":
                            // The enemy nearest the squad turned round and round for 3 s, the camera on it: its flag must
                            // stay over it (Ana, 28/10: the bar spun with the head).
                            Spin();
                            break;
                        case "pausa-otro":
                        case "pausa-otro-fin":
                            // The partner opening the game's menu (or closing it), as the room would say (PlayerInfo.Paused).
                            var me = control.Players.FirstOrDefault(p => p.Slot == control.MySlot) ?? new PlayerInfo { Slot = control.MySlot, Name = "Arturo", Color = Content.PlayerColors[control.MySlot], Connected = true };
                            control.SetPlayers(new List<PlayerInfo> { me, new PlayerInfo { Slot = 1 - control.MySlot, Name = "Ana", Color = Content.PlayerColors[1 - control.MySlot], Connected = true, Paused = what == "pausa-otro" ? true : (bool?)null } });
                            break;
                        case "turnoenemigo":
                            // The enemy's stamp held up, as the event player puts it (its red plate).
                            Hud?.Banner("TURNO ENEMIGO", "alien", null, true);
                            break;
                        case "tuturno":
                            // The squad's turn opening, as the host's event would come (the stamp held, the camera to its soldier).
                            player?.Push(new List<GameEvent> { new TurnBeganEvent { Team = Teams.Xcom, Turn = control.State.Turn, Command = 0 } }, null);
                            break;
                        case "space":
                        case "enter":
                        case "esc":
                            var key = what == "space" ? Key.Space : what == "enter" ? Key.Enter : Key.Escape;
                            InputSystem.QueueStateEvent(keys, new KeyboardState(key));
                            await Awaitable.NextFrameAsync(destroyCancellationToken);
                            await Awaitable.NextFrameAsync(destroyCancellationToken);
                            InputSystem.QueueStateEvent(keys, new KeyboardState());
                            break;
                        case "click":
                        case "rclick":
                            InputSystem.QueueStateEvent(mouse, new MouseState { position = middle, buttons = (ushort)(what == "click" ? 1 : 2) });
                            await Awaitable.NextFrameAsync(destroyCancellationToken);
                            await Awaitable.NextFrameAsync(destroyCancellationToken);
                            InputSystem.QueueStateEvent(mouse, new MouseState { position = middle });
                            break;
                        default:
                            // «ayuda=horde» the help of the first element of that class pinned open (TooltipLayer.Pin), whatever
                            // the editor's own pointer does.
                            if (what.StartsWith("ayuda="))
                            {
                                Hud?.PinTip(what.Substring("ayuda=".Length));
                                break;
                            }
                            // «wheel=1» a notch of the wheel up (-1 down) over the screen's middle; «wheelx=1» sideways, as macOS turns it with Shift.
                            if (what.StartsWith("wheel=") || what.StartsWith("wheelx="))
                            {
                                if (float.TryParse(what.Substring(what.IndexOf('=') + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var notch))
                                {
                                    var turn = what.StartsWith("wheelx=") ? new Vector2(notch * 120, 0) : new Vector2(0, notch * 120);
                                    InputSystem.QueueStateEvent(mouse, new MouseState { position = middle, scroll = turn });
                                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                                    InputSystem.QueueStateEvent(mouse, new MouseState { position = middle });
                                }
                                break;
                            }
                            if (what.StartsWith("quedan="))
                            {
                                // The mission's turns left set so (a turn later than the first), for the warnings as a turn opens.
                                var n = int.Parse(what.Substring(7));
                                foreach (var st in new[] { control.State, control.Shown })
                                {
                                    if (st == null) continue;
                                    st.Mission.TurnsLeft = n;
                                    st.Turn = System.Math.Max(st.Turn, 2);
                                }
                                break;
                            }
                            if (what.StartsWith("sin-granadas="))
                            {
                                // A soldier with no grenades (none in the bag: the engine lists no Granada), for Lanzar's menu.
                                foreach (var st in new[] { control.State, control.Shown })
                                    st?.Units.FirstOrDefault(x => x.Id == what.Substring(13))?.Charges.Remove(ChargeIds.Grenade);
                                control.Refresh();
                                break;
                            }
                            if (what.StartsWith("keep="))
                            {
                                // «keep=LeftMeta+Digit4»: those keys down, and left so (a release that never came); «keep=» none.
                                var down = what.Substring(5).Split('+').Select(n => System.Enum.TryParse<Key>(n, true, out var one) ? one : Key.None).Where(one => one != Key.None).ToArray();
                                InputSystem.QueueStateEvent(keys, new KeyboardState(down));
                                await Awaitable.NextFrameAsync(destroyCancellationToken);
                                await Awaitable.NextFrameAsync(destroyCancellationToken);
                                Debug.Log($"[lab] tras {what}: {control.LabState()}; pulsadas: {string.Join(" ", keys.allKeys.Where(x => x.isPressed).Select(x => x.keyCode))}");
                                break;
                            }
                            if (what.StartsWith("key="))
                            {
                                // «key=Digit4», or several held together: «key=LeftMeta+LeftShift+Digit4» (a screenshot's).
                                var pressed = what.Substring(4).Split('+').Select(n => System.Enum.TryParse<Key>(n, true, out var one) ? one : Key.None).Where(one => one != Key.None).ToArray();
                                InputSystem.QueueStateEvent(keys, new KeyboardState(pressed));
                                await Awaitable.NextFrameAsync(destroyCancellationToken);
                                await Awaitable.NextFrameAsync(destroyCancellationToken);
                                InputSystem.QueueStateEvent(keys, new KeyboardState());
                                await Awaitable.NextFrameAsync(destroyCancellationToken);
                                Debug.Log($"[lab] tras {what}: {control.LabState()}");
                                break;
                            }
                            if (what.StartsWith("hold="))
                            {
                                var hold = what.Substring(5).Split('/');
                                if (hold.Length == 2 && System.Enum.TryParse<Key>(hold[0], true, out var held) && float.TryParse(hold[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                                    HoldKey(keys, held, seconds);
                                break;
                            }
                            // The backpack's: «pick=s1», «slot=0», «act=Curar», «floor=0», «fallen», «offer», «x»; the map's.
                            PackSteps.Do(control, what);
                            break;
                    }
                    Debug.Log($"[lab] toque {what} a {clock - start:0.0} s (tiempo del juego {Time.time:0.0}, escala {Time.timeScale:0.##})");
                }
            }
            catch (System.OperationCanceledException)
            {
                // The lab went first.
            }
        }

        /// <summary>The «giro» touch: the enemy nearest the first soldier turned round fast for 3 s, the camera on it.</summary>
        async void Spin()
        {
            var s = control.Shown;
            var views = Bastion.Client.Units.UnitViews.Current;
            var one = s.Units.FirstOrDefault(u => u.Alive && u.Team == Teams.Xcom);
            var enemy = one == null ? null : s.Units.Where(u => u.Alive && u.Team == Teams.Alien && views != null && views.Has(u.Id))
                // (One brought into sight by «llegada» first, if any.)
                .OrderBy(u => u.Id.StartsWith("lab") ? 0 : 1).ThenBy(u => System.Math.Abs(u.Pos.X - one.Pos.X) + System.Math.Abs(u.Pos.Y - one.Pos.Y)).FirstOrDefault();
            if (enemy == null) return;
            Debug.Log($"[lab] giro: {enemy.Id} ({enemy.Template}) en {enemy.Pos.X},{enemy.Pos.Y}");
            control.Rig.Focus(views.Feet(enemy.Id), true);
            try
            {
                var until = Time.time + 3;
                float most = 0, sum = 0;
                var frames = 0;
                while (Time.time < until)
                {
                    var a = Time.time * 9;
                    views.Face(enemy.Id, views.Feet(enemy.Id) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3);
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                    // How far across its flag's point is from its feet (m).
                    if (Hud?.HeadOf?.Invoke(enemy.Id) is Vector3 flag)
                    {
                        var off = Vector2.Distance(new Vector2(flag.x, flag.z), new Vector2(views.Feet(enemy.Id).x, views.Feet(enemy.Id).z));
                        most = Mathf.Max(most, off);
                        sum += off;
                        frames++;
                    }
                }
                Debug.Log($"[lab] giro: la barra, de sus pies, {sum / Mathf.Max(1, frames):0.00} m de media y {most:0.00} como mucho en {frames} fotogramas");
            }
            catch (System.OperationCanceledException)
            {
                // The lab went first.
            }
        }

        /// <summary>`key` held for `seconds` (BASTION_TOQUES «hold=d/1.5»), while the other touches go on.</summary>
        async void HoldKey(Keyboard keys, Key key, float seconds)
        {
            try
            {
                var until = Time.time + seconds;
                while (Time.time < until)
                {
                    InputSystem.QueueStateEvent(keys, new KeyboardState(key));
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                InputSystem.QueueStateEvent(keys, new KeyboardState());
            }
            catch (System.OperationCanceledException)
            {
                // The lab went first.
            }
        }

        /// <summary>Recorded events, a moment after the start: on the host's and this player's states, and to the event player.</summary>
        async void PlaySoon(List<GameEvent> events)
        {
            var first = control.State.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive);
            if (first != null) control.Rig.Focus(Board.TileToWorld(control.State, first.Pos), true);
            await Awaitable.WaitForSecondsAsync(0.5f, destroyCancellationToken);
            foreach (var e in events)
            {
                Events.ApplyEvent(host, e);
                Events.ApplyEvent(control.State, e);
            }
            player.Push(events, null);
            control.Refresh();
        }

        /// <summary>The squad's living on the evacuation's free tiles (the ground's), one each, as far as there are tiles.</summary>
        static void IntoZone(GameState s)
        {
            var free = s.Mission.EvacZone.Where(z => Grid.SurfaceAt(s, z.X, z.Y, Grid.ElevAt(s, z.X, z.Y)) is Pos p && p.Z == 0 && !s.Units.Any(u => u.Alive && u.Team != Teams.Xcom && Grid.SameTile(u.Pos, z))).ToList();
            var i = 0;
            foreach (var u in s.Units.Where(x => x.Team == Teams.Xcom && x.Alive))
            {
                if (i >= free.Count) break;
                u.Pos = new Pos { X = free[i].X, Y = free[i].Y, Z = 0 };
                i++;
            }
            Debug.Log($"[lab] evac: {i} soldiers on the zone's {free.Count} free tiles");
        }

        /// <summary>The call to leave, from the first in the zone, a second in (the map and the camera settled).</summary>
        async void EvacSoon(GameState s)
        {
            await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
            var u = s.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive && Abilities.InEvacZone(s, x.Pos));
            if (u == null)
            {
                Debug.Log("[lab] evac: nobody in the zone");
                return;
            }
            Play(new EvacCommand { Unit = u.Id });
        }

        /// <summary>Another soldier of the squad put on the line of fire of `shot`, the first free tile two or more from its shooter and short of its target.</summary>
        static void InTheWay(GameState s, AbilityCommand shot)
        {
            var by = s.Units.FirstOrDefault(u => u.Id == shot.Unit);
            var at = s.Units.FirstOrDefault(u => u.Id == shot.Target);
            var other = s.Units.FirstOrDefault(u => u.Alive && u.Team == Teams.Xcom && u.Id != shot.Unit);
            if (by == null || at == null || other == null) return;
            var far = Mathf.Max(Mathf.Abs(at.Pos.X - by.Pos.X), Mathf.Abs(at.Pos.Y - by.Pos.Y));
            var on = Enumerable.Range(2, Mathf.Max(0, far - 3))
                .Select(k => new Pos { X = by.Pos.X + Mathf.RoundToInt((at.Pos.X - by.Pos.X) * (float)k / far), Y = by.Pos.Y + Mathf.RoundToInt((at.Pos.Y - by.Pos.Y) * (float)k / far), Z = by.Pos.Z })
                .FirstOrDefault(p => Grid.InBounds(s, p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(s, p)) && Grid.UnitAt(s, p) == null);
            Debug.Log($"[lab] estorbo: {other.Name} a {(on == null ? "ningún sitio" : $"({on.X},{on.Y})")}, entre {by.Name} y {at.Name}");
            if (on != null) other.Pos = on;
        }

        async void FireSoon()
        {
            await Awaitable.WaitForSecondsAsync(0.6f, destroyCancellationToken);
            control.OnConfirmTarget();
        }

        /// <summary>BASTION_LAB_TARDA=0.3: the host's answer that late (seconds), as over the network: what shows between an order and its events.</summary>
        static readonly float Late = float.TryParse(System.Environment.GetEnvironmentVariable("BASTION_LAB_TARDA"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var late) ? late : 0;

        void Play(Command cmd)
        {
            if (Late > 0) PlayLate(cmd);
            else Answer(cmd);
        }

        async void PlayLate(Command cmd)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(Late, destroyCancellationToken);
                Answer(cmd);
            }
            catch (System.OperationCanceledException)
            {
                // The lab went first.
            }
        }

        /// <summary>
        /// An order to the rules, as the host would take it; its events to this player's copy, and then either to the event
        /// player (in the scene) or straight to the capsules.
        /// </summary>
        void Answer(Command cmd)
        {
            var result = Rules.ExecuteCommand(host, cmd, 0, new[] { 0 }, rng);
            if (!result.Ok)
            {
                Debug.Log($"[lab] orden rechazada ({cmd.Type}): {result.Error}");
                Hud?.Toast(result.Error);
                return;
            }
            foreach (var e in result.Events) Events.ApplyEvent(control.State, e);
            if (player != null) player.Push(result.Events, 0);
            else Bodies(control.State);
            control.Refresh();
        }

        /// <summary>
        /// The ground roughly: a grey quad on each tile one can stand on, a dark block waist high for a wall (city's solid
        /// blocks a storey high hid the marks; Arte's map cuts them away), a brown one for a thing. Solid and lit, so the
        /// marks on a tile behind a wall stay behind it.
        /// </summary>
        void LayOut(GameState s)
        {
            var floor = new MeshBuilder();
            var walls = new MeshBuilder();
            var things = new MeshBuilder();
            for (var y = 0; y < s.Height; y++)
            for (var x = 0; x < s.Width; x++)
            {
                var kind = Grid.TileAt(s, x, y);
                var h = Grid.ElevAt(s, x, y) * Board.Storey;
                var a = Board.Corner(x, y, h);
                if (Grid.IsWalkable(kind) || kind == "floor")
                {
                    var t = Board.Tile;
                    floor.Face(a, a + new Vector3(t, 0, 0), a + new Vector3(t, 0, -t), a + new Vector3(0, 0, -t), Vector3.up, Color.white);
                    continue;
                }
                var tall = kind == "wall" ? 1.2f : Mathf.Max(0.4f, (float)Grid.ObstacleHeight(kind) * Board.Storey);
                Box(kind == "wall" ? walls : things, a, tall);
            }
            Solid("Suelo (laboratorio)", floor, FloorColor);
            Solid("Muros (laboratorio)", walls, WallColor);
            Solid("Obstáculos (laboratorio)", things, ThingColor);
        }

        static void Solid(string name, MeshBuilder b, Color color)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            go.AddComponent<MeshFilter>().sharedMesh = b.Build(null, true);
        }

        /// <summary>A block on a tile, `nw` its north-west corner at its floor, `tall` high.</summary>
        static void Box(MeshBuilder b, Vector3 nw, float tall)
        {
            var t = Board.Tile;
            var up = Vector3.up * tall;
            var ne = nw + new Vector3(t, 0, 0);
            var se = nw + new Vector3(t, 0, -t);
            var sw = nw + new Vector3(0, 0, -t);
            var c = (Color32)Color.white;
            b.Face(nw + up, ne + up, se + up, sw + up, Vector3.up, c);
            b.Face(nw, ne, ne + up, nw + up, Vector3.forward, c);
            b.Face(sw, se, se + up, sw + up, Vector3.back, c);
            b.Face(nw, sw, sw + up, nw + up, Vector3.left, c);
            b.Face(ne, se, se + up, ne + up, Vector3.right, c);
        }

        /// <summary>A capsule for each unit alive, the squad blue and the rest red, on its tile and registered as its figure; the fallen gone.</summary>
        void Bodies(GameState s)
        {
            foreach (var u in s.Units)
            {
                bodies.TryGetValue(u.Id, out var body);
                if (!u.Alive)
                {
                    if (body == null) continue;
                    Figures.Unregister(u.Id);
                    Destroy(body.gameObject);
                    bodies.Remove(u.Id);
                    continue;
                }
                if (body == null)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    c.name = "Cápsula";
                    Destroy(c.GetComponent<Collider>());
                    var mesh = c.transform;
                    mesh.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                    mesh.GetComponent<Renderer>().sharedMaterial = u.Team == Teams.Xcom ? squad : enemy;
                    // The root on the feet, as a figure's; the capsule over it.
                    body = new GameObject(u.Name + " (" + u.Id + ")").transform;
                    mesh.SetParent(body, false);
                    mesh.localPosition = Vector3.up * 0.9f;
                    var head = new GameObject("Cabeza").transform;
                    head.SetParent(body, false);
                    head.localPosition = Vector3.up * 1.65f;
                    bodies[u.Id] = body;
                    Figures.Register(u.Id, body, head);
                }
                body.position = Board.TileToWorld(s, u.Pos);
            }
        }
    }
}
