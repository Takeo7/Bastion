using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;
using Bastion.UI.Start;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// What the HUD shows (game.ts refreshHud and refreshStatus, and what hud.ts read off the units; UI's: docs/UNITY-UI.md
    /// §3): the views built from the state. What the player reads (turn, objective, players, squad, the picked soldier)
    /// comes from Shown, what has been played out: a death or a new turn must not show before it is seen. Orders and their
    /// checks (the bar, the aim, ending the turn, the command) go by State.
    /// </summary>
    public sealed partial class CombatController
    {
        const string Enemy = "#d64545";

        /// <summary>Everything on the HUD again (game.ts refreshHud): each piece draws only what changed (Ui.Key).</summary>
        static readonly Unity.Profiling.ProfilerMarker MStatus = new Unity.Profiling.ProfilerMarker("Bastion.UI.Hud.Status");
        static readonly Unity.Profiling.ProfilerMarker MBar = new Unity.Profiling.ProfilerMarker("Bastion.UI.Hud.Bar");
        static readonly Unity.Profiling.ProfilerMarker MFlags = new Unity.Profiling.ProfilerMarker("Bastion.UI.Hud.Flags");
        static readonly Unity.Profiling.ProfilerMarker MMarks = new Unity.Profiling.ProfilerMarker("Bastion.UI.Hud.Marks");

        /// <summary>The picked one's keys the last time this player could act: kept, locked, while the screen is behind (RefreshHud).</summary>
        (string id, List<AbilityView> keys) shownKeys;

        void RefreshHud()
        {
            if (Hud == null || State == null) return;
            using (MStatus.Auto()) RefreshStatus();
            var s = State;
            var busy = AbilityTexts.BusySlots(s);
            var partner = PartnerHere();
            var mine = HasCommand();
            ActionBarView bar;
            using (MBar.Auto()) bar = new ActionBarView { Abilities = BarKeys() };
            // The host ahead of the screen (the enemy's turn worked out, to the end even, and not played yet: R23 A1): one of
            // mine still standing on screen keeps the keys it had, under the lock, till the screen gets there. An empty row
            // told of their fall before it was seen.
            if (CanAct()) shownKeys = (picked, bar.Abilities);
            else if (bar.Abilities.Count == 0 && Shown.Outcome == null && shownKeys.id != null && shownKeys.id == picked && UnitById(Shown, picked) is Unit seen && seen.Alive)
                bar.Abilities = shownKeys.keys;
            var u = Unit();
            // One inside La Lata too (T24): its Bajar waits for the command as any key.
            if ((u ?? Inside()) is Unit commanded && Commands(commanded) && CanAct() && !mine) bar.Lock = HudText.Turn.PartnerLock(PlayerName(s.Command));
            // Why the cards rest in the enemy's turn: the bar says it over itself (hud.ts setTurn).
            // (Till the end is on screen: the host may know it while the enemy's turn still plays.)
            else if (Shown.Outcome == null && (s.ActiveTeam == Teams.Alien || Shown.ActiveTeam == Teams.Alien)) bar.Lock = HudText.Turn.EnemyLock;
            // The picked soldier's movement left, before the keys (Arturo, 5/11 #14).
            var shownUnit = u != null ? UnitById(Shown, u.Id) ?? u : null;
            if (shownUnit != null && shownUnit.Team == Teams.Xcom && shownUnit.Automaton == null && Paths.FullMoves(Shown, shownUnit) > 0)
            {
                bar.Moves = Paths.MoveBudget(Shown, shownUnit);
                bar.MovesMax = Paths.FullMoves(Shown, shownUnit);
                bar.MovesTip = HudText.Moves.Tip(bar.Moves, bar.MovesMax, Paths.ConcealedBonus(Shown, shownUnit));
            }
            Hud.SetAbilities(bar);
            Hud.SetCall(CallView());
            Hud.SetTarget(TargetPanel());

            var ready = s.Ready[MySlot];
            var waiting = partner != null && busy.Contains(partner.Slot) && !s.Ready[partner.Slot] ? partner.Name : null;
            var idle = IdleSoldiers();
            // Without the command too (Arturo, 7/11): the key ends one's own turn, «A punto» until the partner's is over.
            // One of mine in play: the key ends its turn first (Arturo, 7/11, Ana's #5).
            var playing = InPlayMine();
            // The stage over: no key to end a turn (Producto, 18/11: one left alone under the card); over on screen, not
            // only for the host (R23 A1: gone while the last enemy turn still played, the row empty).
            var endMode = Shown.Outcome != null ? "over" : !CanAct() ? (s.ActiveTeam == Teams.Alien || Shown.ActiveTeam == Teams.Alien ? "enemy" : "disabled") : ready ? "ready" : (EndArmed && idle > 0) || (playing != null && WarnedLeft) ? "confirm" : playing != null && !EndArmed ? "done" : "act";
            // (With actions or movement left: the turn ends by itself only when none has either, Arturo 5/11 #14; Lore's words.)
            var detail = endMode == "confirm" ? $"{idle} {(idle == 1 ? "soldado" : "soldados")} sin terminar" : waiting;
            Hud.SetEndTurn(new EndTurnView
            {
                Mode = endMode, Detail = detail, Holder = ready && !mine && s.Command != null ? PlayerName(s.Command) : null,
                InPlay = endMode == "done" ? playing.Name : null, GoesTo = endMode == "done" ? CommandGoesTo() : null,
            });
        }

        /// <summary>What the player reads about the fight, from what has been seen (game.ts refreshStatus).</summary>
        void RefreshStatus()
        {
            var v = Shown;
            var sharing = PartnerHere() != null && v.ActiveTeam == Teams.Xcom && v.Outcome == null;
            var holder = v.Command == null || !Players.Any(p => p.Slot == v.Command && p.Connected) ? MySlot : v.Command.Value;
            // Whose turn it is, unmistakably (playtest): mine, the partner's while they hold the command, or the enemy's.
            // Alone, with no partner here or just gone (ActiveSlots: within the server's grace), no frame nor players (Ana, 28/10).
            var alone = ActiveSlots().Count(s => s != MySlot) == 0;
            var turn = new TurnView { Alone = alone };
            if (v.Outcome != null) turn.Owner = "over";
            else if (v.ActiveTeam == Teams.Alien)
            {
                turn.Owner = "enemy";
                turn.Color = Enemy;
            }
            else if (sharing && holder != MySlot)
            {
                turn.Owner = "partner";
                turn.OwnerName = PlayerName(holder);
                turn.Color = PlayerColor(holder);
            }
            else
            {
                turn.Owner = "mine";
                turn.Color = PlayerColor(MySlot);
            }
            if (v.Mission.Night == true)
            {
                turn.NightLabel = Lore.NightText.Label;
                turn.NightRules = string.Join("\n", Lore.NightText.Rules);
            }
            // One soldier at a time with a partner (playtest 15/10 #9): the one in play, by the label and on its card.
            var playing = sharing ? UnitById(v, v.ActiveUnit) : null;
            // In play while it has anything left, its movement too (Arturo, 7/11).
            var inPlay = playing != null && playing.Alive && Paths.HasTurnLeft(playing) ? playing : null;
            turn.InPlay = inPlay?.Name;
            Hud.SetTurn(turn);
            Hud.SetMission(MissionView());
            Hud.SetBoss(BossOf(v));
            Hud.SetCompass(CompassOf(v) is var rose && !rose.Shown && Bastion.Client.GameInput.Touch ? new CompassView { Shown = true, Edges = false } : rose);
            Hud.SetPlayers(alone ? null : PlayersView(v, sharing ? holder : (int?)null));
            Hud.SetSquad(SquadView(v, inPlay?.Id));
            var shown = UnitById(v, picked);
            // One inside La Lata too (T24, Producto 18/11): its name, weapon and health stay over the cards.
            Hud.SetSelected(shown != null && (shown.Alive || (shown.Aboard == true && v.Outcome == null)) ? UnitPanel(shown) : null);
            RefreshFlags();
            RefreshMarks();
            // The enemies' sight follows what is seen (the button on: who comes into sight, who moves); worked out per enemy only when it changed.
            ShowVision();
        }

        // ------------------------------------------------------- the mission's marks

        /// <summary>The mission's marks on the map (made with the controller: CombatSession, the lab).</summary>
        public MissionMarks Marks;

        /// <summary>Where the partner just made something happen (the event player's notice), pointed at for a while.</summary>
        (Vector3 at, string who)? moment;
        float momentUntil;
        int markCycle;

        /// <summary>A partner's moment, pointed at from the screen's edge for 8 s (game.ts moment); null clears it.</summary>
        public void SetMoment(Vector3? at, string who)
        {
            moment = at != null ? (at.Value, who) : ((Vector3, string)?)null;
            momentUntil = Time.time + 8;
            RefreshMarks();
        }

        /// <summary>
        /// The mission's marks from what has been seen (game.ts refreshView and marks): the evacuation zone, the beams over
        /// the equipment to fix and the one to reach, the item lying loose, the flares; the ring's arrow to the objective (or
        /// the evacuation); and the arrows at the screen's edge to the partner's moment and the soldier pointed at.
        /// </summary>
        void RefreshMarks()
        {
            using var marksMark = MMarks.Auto();
            var v = Shown;
            var m = v.Mission;
            var beacons = new List<(Pos, int)>();
            if (m.Terminal != null && !m.ObjectiveDone) beacons.Add((m.Terminal, 0x54d1ff));
            var objective = UnitById(v, m.ObjectiveUnit);
            if (objective != null && objective.Alive && !m.ObjectiveDone && !Unfound(objective)) beacons.Add((objective.Pos, objective.Team == Teams.Alien ? 0xff4a6a : 0x4dff9a));
            var item = m.Item != null && m.Item.Carrier == null && m.Item.Evacuated != true ? m.Item.Pos : null;
            // La llave's key or Mordido's antiviral on the floor (U22, U23): seen from afar by its beam and the ring at its
            // foot, in the item's green; not the item's core, which hid Arte's key and flask lying there (Producto, 19/11).
            if (MissionGearOnFloor(v) is Pos lying) beacons.Add((lying, 0x4dff9a));
            // A siege's breaches: an orange beam each, and an arrow a side at the screen's edge (CombatController.Siege).
            var breaches = new EdgeMarksView();
            BreachMarks(v, beacons, breaches);
            // La Lata's open tiles only (Gameplay's Rules.OpenTiles: one a seat still free; Arturo, by Producto, 18/11).
            Marks?.Show(v, Rules.OpenTiles(v), beacons, item, m.Reinforcements.Where(r => r.Pos != null && !r.Landed).Select(r => r.Pos).ToList(), SearchZone(v), AlightArmed() ? Rules.AlightTiles(v) : null);
            if (Hud == null) return;
            Hud.SetWorldTags(WorldTags(v));
            // Where to go, the ring's arrow (UI-COMBATE UI-06): the objective, else the evacuation, as the O key's.
            var goals = MissionGoals();
            Hud.SetGoal(goals.Count > 0 ? Board.TileToWorld(v, goals[0].pos, Board.Tile) : (Vector3?)null);
            var view = new EdgeMarksView();
            view.Marks.AddRange(breaches.Marks);
            // El herido on the floor off the screen (U25), found and alive: «El herido · 12 casillas», amber as Mordido's, with
            // their stage's crutch (U26: Producto, after the R10).
            if (Rules.WoundedOf(v) is Unit down && down.Alive && down.CarriedBy == null && !down.Evacuated && !Unfound(down))
                view.Marks.Add(new EdgeMarkView { Kind = "wounded", Icon = "node:wounded", Name = Lore.StageName(NodeKinds.Wounded), World = Board.TileToWorld(v, down.Pos, Board.Tile) });
            // Mordido's bitten off the screen (U23), from their finding till cured: «Mordido · 5 turnos», an alert at 2 and 1.
            if (UnitById(v, m.ObjectiveUnit) is Unit bitten && BiteLeft(v, bitten) is int left && !Unfound(bitten))
                view.Marks.Add(new EdgeMarkView { Kind = left <= 2 ? "alert" : "bite", Name = $"{Lore.StageName(NodeKinds.Bitten)} · {left} {(left == 1 ? "turno" : "turnos")}", World = Board.TileToWorld(v, bitten.Pos, Board.Tile) });
            // Separados (U27): the other group off the screen, «Pierre y Ana · 40 casillas» (Lore's SplitText.Group, its living),
            // in its player's colour with the stage's icon; to the one of them nearest the picked soldier. None once it is all
            // in La Lata or fallen.
            foreach (var g in OtherGroups(v))
            {
                var group = SplitField(v, g);
                if (group.Count == 0) continue;
                var at = Unit()?.Pos ?? group[0].Pos;
                var near = group.OrderBy(u => Math.Abs(u.Pos.X - at.X) + Math.Abs(u.Pos.Y - at.Y)).First();
                view.Marks.Add(new EdgeMarkView
                {
                    Kind = "split",
                    Icon = "node:split",
                    Color = PlayerColor(near.Owner ?? MySlot),
                    Name = Lore.SplitText.Group(group.Select(u => u.Name).ToList()),
                    World = FeetOf(near.Id) is Vector3 foot ? foot + Vector3.up * Board.Tile : Board.TileToWorld(v, near.Pos, Board.Tile),
                });
            }
            if (moment != null && Time.time < momentUntil) view.Marks.Add(new EdgeMarkView { Kind = "alert", Name = moment.Value.who, World = moment.Value.at + Vector3.up * Board.Tile });
            // The soldier pointed at from the list, when it is off the screen (#11): an arrow at the edge, as the marks'.
            var pointed = UnitById(v, listPointed);
            if (pointed != null && pointed.Alive && !pointed.Evacuated && FeetOf(pointed.Id) is Vector3 feet)
                view.Marks.Add(new EdgeMarkView { Kind = "soldier", Name = pointed.Name, World = feet + Vector3.up * Board.Tile });
            // Each says how far it is from the picked soldier (edgeMarks.ts): «Brecha · sur · 7 casillas», on the board's plane.
            var from = Unit()?.Pos;
            var here = from != null ? Board.TileToWorld(v, from) : Vector3.zero;
            foreach (var mark in view.Marks)
            {
                var far = Mathf.RoundToInt(Vector2.Distance(new Vector2(here.x, here.z), new Vector2(mark.World.x, mark.World.z)) / Board.Tile);
                mark.Label = from != null ? $"{mark.Name} · {far} {(far == 1 ? "casilla" : "casillas")}" : mark.Name;
            }
            Hud.SetEdgeMarks(view);
        }

        /// <summary>
        /// A run's Rescate (N2, 17/11): the circle where the survivor is to be looked for, while nobody of the squad has
        /// seen them (MissionState.SearchArea); null once found, dead or anywhere else.
        /// </summary>
        SearchArea SearchZone(GameState v)
        {
            var area = v.Mission.SearchArea;
            var vip = UnitById(v, v.Mission.ObjectiveUnit);
            return area != null && vip != null && vip.Alive && Unfound(vip) ? area : null;
        }

        /// <summary>Where the squad has to go (game.ts missionMarks): the objective while it is still to do, else the evacuation's middle.</summary>
        List<(string kind, string name, Pos pos)> MissionGoals()
        {
            var s = Shown;
            var m = s.Mission;
            Pos goal = null;
            if (!m.ObjectiveDone)
            {
                if (m.Kind == MissionKinds.Recovery && m.Item != null && m.Item.Carrier == null && m.Item.Evacuated != true) goal = m.Item.Pos;
                if (m.Kind == MissionKinds.Hack) goal = m.Terminal;
                if (m.Kind == MissionKinds.Sabotage || m.Kind == MissionKinds.Rescue)
                {
                    var target = UnitById(s, m.ObjectiveUnit);
                    // A run's survivor not found yet: the middle of the zone to look in (N2), not where they are.
                    if (SearchZone(s) is SearchArea look) goal = new Pos { X = look.Center.X, Y = look.Center.Y };
                    else if (target != null && target.Alive && (m.Kind == MissionKinds.Sabotage || target.Captive)) goal = target.Pos;
                }
            }
            // El herido (U25): found and on the floor, to them (as the Rescate's, joined too); carried, La Lata (below); not
            // found, the zone (above).
            if (Rules.WoundedOf(s) is Unit hurt && hurt.Alive && hurt.CarriedBy == null && !hurt.Evacuated && !Unfound(hurt)) goal = hurt.Pos;
            // Mordido (U23): the antiviral on the floor; in a pack, the bitten, to give it to them.
            if (m.Bitten == true && m.Cured != true && UnitById(s, m.ObjectiveUnit) is Unit bitten && bitten.Alive && !Unfound(bitten))
                goal = AntiviralOnFloor(s) ?? (Looting.AntiviralWhere(s) == AntiviralPlaces.Pack ? bitten.Pos : goal);
            else if (m.Bitten == true && AntiviralOnFloor(s) is Pos lying) goal = lying;
            // La llave (U22): the key on the floor, then, in a pack, the armoury's door; on its holder still, nowhere to go.
            if (m.KeyArmory == true && !Looting.ArmoryOpen(s))
                goal = KeyOnFloor(s) ?? (Looting.KeyWhere(s) == KeyPlaces.Pack && m.Locked is List<int> locked && locked.Count > 0 ? Grid.PosOf(s, locked[0]) : null);
            // Saqueadores (U28): the stash's crate still shut nearest the picked soldier, and La Lata after it (O goes to one and
            // then the other): stealing it is up to the squad.
            var raid = false;
            if (goal == null && m.EvacDone != true && StashLeft(s) is List<Pos> stash && stash.Count > 0)
            {
                var from = Unit()?.Pos ?? stash[0];
                goal = stash.OrderBy(p => Grid.Distance(from, p)).First();
                raid = true;
            }
            if (goal != null && !raid) return new List<(string, string, Pos)> { ("objective", "Objetivo", goal) };
            var goals = new List<(string, string, Pos)>();
            if (goal != null) goals.Add(("objective", "Objetivo", goal));
            var zone = m.EvacZone;
            if (zone.Count == 0) return goals;
            var mx = zone.Average(t => t.X);
            var my = zone.Average(t => t.Y);
            var centre = zone.OrderBy(t => (t.X - mx) * (t.X - mx) + (t.Y - my) * (t.Y - my)).First();
            goals.Add(("evac", "Evacuación", new Pos { X = centre.X, Y = centre.Y }));
            return goals;
        }

        /// <summary>O: the camera to the mission's marks, one after another.</summary>
        void NextMark()
        {
            var marks = MissionGoals();
            if (marks.Count == 0)
            {
                Toast("Esta misión no tiene un punto al que ir: abatid a todo lo que haya.");
                return;
            }
            LookAt(Board.TileToWorld(Shown, marks[markCycle++ % marks.Count].pos));
        }

        /// <summary>The player asked to look somewhere (O, an arrow at the edge): it ends any camera pass.</summary>
        void LookAt(Vector3 at)
        {
            if (Rig == null) return;
            Rig.TakeControl();
            Rig.Focus(at);
        }

        // ------------------------------------------------------- sight and flags

        /// <summary>The enemies in the squad's sight, from what has been seen (game.ts refreshView: visibleAliens).</summary>
        readonly HashSet<string> seen = new HashSet<string>();

        void See()
        {
            var v = Shown;
            seen.Clear();
            // A siege too, by day as by night: only those the squad sees (Arturo, 31/10 #5; it was all of them, 18/10 #18).
            // Los.VisibleEnemies, with each viewer's farther than its sight left out before CanSee (its line would be
            // refused for that, SightOrigin's first test: sight at night and behind cover is less, never more), and the
            // buildings' and the cone's tests and the lines walked only for the rest: 15 ms a refresh with eighty of the
            // dead in a city (Red and Arte, 7/11).
            var viewers = v.Units.Where(u => Los.IsViewer(u, Teams.Xcom)).Select(u => (u, sight: (double)Los.SightIn(v, u))).ToList();
            foreach (var t in v.Units)
                if (t.Alive && t.Team != Teams.Xcom && viewers.Any(w => Grid.Distance(w.u.Pos, t.Pos) <= w.sight && Los.CanSee(v, w.u, t))) seen.Add(t.Id);
        }

        /// <summary>
        /// The flags over the units in sight (unitViews.ts renderFlag): health, armour, what is on them, and the soldiers'
        /// cover (the enemies' without it: Ana, Arturo 31/10 #7); the name on the hovered one's only (#9); over the enemies,
        /// what the pointer's tile means (game.ts enemyTags).
        /// </summary>
        /// <summary>Who has boarded La Lata as the event player plays it (EvacuatedEvent), before the view has it: no flag.</summary>
        public readonly HashSet<string> Aboard = new HashSet<string>();

        /// <summary>
        /// Whether `u` is indoors on a storey below the one in view: the storey over it is drawn (the cutaway's), its
        /// ceiling hiding it. Out in the street, nothing is over it and it shows from any storey.
        /// </summary>
        bool UnderView(GameState s, Unit u)
        {
            if (u.Pos.Z >= Level || s.Storeys == null) return false;
            var i = u.Pos.Y * s.Width + u.Pos.X;
            return i >= 0 && i < s.Storeys.Count && s.Storeys[i] > u.Pos.Z;
        }

        void RefreshFlags()
        {
            using var flagsMark = MFlags.Auto();
            if (Hud == null) return;
            var v = Shown;
            var view = new FlagsView();
            // Those that shoot, and the dead too now: cover hides from them (Arturo, 5/11 #4; it meant nothing against a bite).
            var watchers = v.Units.Where(a => a.Alive && a.Team == Teams.Alien && Visible(a)).ToList();
            var hovered = hover != null ? Grid.UnitAt(v, hover) : null;
            var tags = EnemyTags();
            var sees = SeeingPicked();
            // Aiming: the other targets, each with its odds (over the shoulder, their only flag; a click there aims at it: #11).
            var options = mode == Mode.Target && modeAbility != null ? TargetOptions(modeAbility) : null;
            var attack = modeAbility != null && Content.IsAttack(modeAbility);
            // Those the picked one can take down now, a tag over each (Golpe silencioso, Gameplay 16/11); aiming it, why not
            // over the others beside it.
            var quiet = QuietTargets();
            var takedown = mode == Mode.Target && modeAbility == AbilityIds.Takedown;
            var me = takedown ? Unit() : null;
            var carrier = v.Mission.Item?.Carrier;
            foreach (var u in v.Units)
            {
                // Aboard La Lata (evacuated, or boarding as it is played): out of sight, and so its flag (unitViews.ts board).
                if (!u.Alive || u.Evacuated || Aboard.Contains(u.Id) || !Marked(u)) continue;
                // Under the storey in view: no flag over the slab that hides it (Arte, 16/11: looking at the hospital's
                // upper storeys, the squad's flags floated in the black).
                if (UnderView(v, u)) continue;
                var f = new FlagView { Id = u.Id, Enemy = u.Team != Teams.Xcom, Hp = u.Hp, MaxHp = u.MaxHp, Armor = u.Armor, Cover = -1 };
                // Pointed at from the list (Arturo, 31/10 #11): its name too, and the arrow over it.
                f.Pointed = u.Id == listPointed;
                if (u.Id == hovered?.Id || f.Pointed) f.Name = u.Team == Teams.Xcom && u.Template != TemplateIds.Vip && !string.IsNullOrEmpty(u.Nickname) ? $"«{u.Nickname}»" : u.Name;
                if (u.Team == Teams.Xcom)
                {
                    // (Its cover is on the picked one's panel now, as a shield: Arturo, 7/11. CoverOf.)
                }
                void Tag(bool on, string text, string kind, string tip = null)
                {
                    if (on) f.Tags.Add(new TagView { Text = text, Kind = kind, Tip = tip });
                }
                Tag(u.ControlledBy != null, UnitWords.Agree(u, "CONTROLADO"), "psi");
                Tag(u.Overwatch, u.KillZone ? "ZONA LETAL" : u.WatchMelee == true ? "HOJA EN ALTO" : "VIGILANCIA", "ow");
                Tag(u.Hunkered, "CUBRIÉNDOSE", "hk");
                Tag(u.Hidden == true, UnitWords.Agree(u, "ESCONDIDO"), "hide");
                Tag(u.Aided > 0, UnitWords.Agree(u, "PROTEGIDO"), "aid");
                Tag(u.Stunned, UnitWords.Agree(u, "ATURDIDO"), "stun");
                // Knocked down by El Toro's charge (H6): up at the squad's next turn, an action spent. Lore's «EN EL SUELO»
                // (masculine «tumbado» would go over anyone).
                Tag(u.Down == true, Lore.BossText.DownTag, "stun", Lore.Glossary["En el suelo"]);
                Tag(u.Disoriented > 0, UnitWords.Agree(u, "DESORIENTADO"), "psi");
                Tag(u.Panicked, "PÁNICO", "psi");
                Tag(u.SuppressedBy != null, UnitWords.Agree(u, "SUPRIMIDO"), "stun");
                Tag(u.Marked > 0, "MARCADO", "aid");
                Tag(u.Ruptured > 0, "VULNERABLE", "stun");
                Tag(carrier == u.Id, Lore.ItemTag(RecoveryItem()), "data");
                // A run's survivor alone and hidden is «ESCONDIDO»; «CAUTIVO», the Cuervos' prisoner (Arturo, 18/11; Lore).
                // A run's survivor alone is hiding; only the Cuervos' prisoner is held (Arturo, in the cuadro, 18/11; Lore's).
                var hiding = u.Captive && v.Run != null && v.Mission.Guarded != true;
                Tag(u.Captive && u.Lame != true, UnitWords.Agree(u, hiding ? "ESCONDIDO" : "CAUTIVO"), hiding ? "hide" : "data");
                // El herido (U25; Lore's): on the floor, their broken leg; whoever carries them, «CARGANDO».
                // (La Camilla's downed, 0.14.11: Lore's «MALHERIDO», what to do on hover.)
                Tag(u.Lame == true && u.CarriedBy == null, Lore.WoundedText.FloorTagOf(u), "stun", u.Downed == true ? Lore.StretcherText.TagHelp(UnitWords.Female(u) ? Sexes.F : Sexes.M) : null);
                Tag(u.Carrying != null, Lore.WoundedText.CarrierTag, "data");
                Tag(u.Objective, "OBJETIVO", "stun");
                // Saqueadores' guards (U28; Lore's words): «DESPREVENIDO» while their camp sleeps (the Golpe silencioso's
                // word, Paths.OnPatrol), «ALERTA» once it woke; on a line of their own over the health (FlagView.State: in
                // the row, two guards two tiles apart covered each other's bar). None with «GOLPE SILENCIOSO», which says as much.
                if (u.Guard != null && u.Team == Teams.Alien)
                {
                    var unaware = Paths.OnPatrol(v, u);
                    if (!unaware) f.State = new TagView { Text = Lore.RaidersText.AwakeTag, Kind = "awake" };
                    else if (!quiet.Contains(u.Id)) f.State = new TagView { Text = UnitWords.Agree(u, Lore.RaidersText.UnawareTag), Kind = "unaware" };
                }
                // Mordido's count over the bitten (U23): from their finding till cured; red and beating at 2 and 1.
                if (BiteLeft(v, u) is int left && !Unfound(u))
                {
                    f.Countdown = left;
                    f.CountdownTip = Lore.BiteText.Countdown(left);
                    f.CountdownUrgent = left <= 2;
                }
                Tag(quiet.Contains(u.Id), HudText.Takedown.Tag, "quiet");
                var would = false;
                if (tags.TryGetValue(u.Id, out var t))
                {
                    f.HoverKind = t.kind;
                    f.HoverText = t.text;
                    would = t.eye == "would";
                }
                // An enemy's eye in its gauge's circle (Ana, 7/11 #1): open if it would see the picked soldier on the move
                // under the pointer (Arturo, 5/11 #5), or, with none, if it sees it now (#6); else closed.
                if (f.Enemy) f.Eye = (hoverDest ? would : sees.Contains(u.Id)) ? "open" : "closed";
                f.Target = mode == Mode.Target && targetId == u.Id && u.Team != Teams.Xcom;
                if (options != null && !f.Target && u.Team != Teams.Xcom && options.FirstOrDefault(o => o.Unit.Id == u.Id) is TargetOption other)
                {
                    f.Other = true;
                    f.HoverKind = "aim";
                    f.HoverText = attack ? $"{other.Hit} %" : takedown ? HudText.Takedown.Kills : u.Name;
                    // What takes most off it, by its % (U1, 25/11: «dentro 34 %, fuera 72 %» read at a glance).
                    if (attack && other.Worst != null) f.HoverIcon = other.Worst.Icon;
                }
                else if (me != null && f.Enemy && !f.Target && UnitById(State, u.Id) is Unit them && Aim.InMeleeReach(State, me.Pos, them.Pos) && Abilities.TakedownRefusal(State, me, them) is string no)
                {
                    f.Why = true;
                    f.HoverKind = "flank";
                    f.HoverText = no.TrimEnd('.');
                }
                view.Flags.Add(f);
            }
            Hud.SetFlags(view);
        }

        /// <summary>One enemy's field of view as last worked out (SightOf): its key, the tiles it sees and those where it would notice one of ours.</summary>
        readonly Dictionary<string, (string key, List<Pos> seen, List<Pos> noticed)> sights = new Dictionary<string, (string, List<Pos>, List<Pos>)>();
        /// <summary>What is on the ground now (ShowVision): the enemies' keys.</summary>
        string visionKey;

        /// <summary>The enemies' sight's button on (Arturo, 5/11 #6): every one in sight's at once. It stays on, mission after mission.</summary>
        static bool sightAll;

        /// <summary>The enemies' sight's button (or V): on, off; the ground and the flags again.</summary>
        void ToggleSight()
        {
            sightAll = !sightAll;
            Debug.Log($"[vista] zombis {(sightAll ? "sí" : "no")}");
            ShowVision();
            RefreshFlags();
        }

        /// <summary>
        /// What the enemies see on the ground: the one under the pointer's (Arturo, 17/10: the dead see in a cone; game.ts
        /// visionOf), or, the button on and none under it, every one in sight's at once in our turn (Arturo, 5/11 #6). Not
        /// aiming: its red over the ground drowned the target's own mark (Arturo, 23/10 #2). No legend over the button (Ana,
        /// 7/11 #10: «Lo que ven» went; its help says what the red is).
        /// </summary>
        void ShowVision()
        {
            if (AimMarks == null) return;
            // Saqueadores' guards on watch with it (U28): what leaves the pointer's sight off leaves theirs too.
            ShowPosts();
            var v = Shown;
            var a = hover != null ? Grid.UnitAt(v, hover) : null;
            var who = new List<Unit>();
            // Nor over the shoulder (0.12.26: the blow at the dead one under the pointer was played on its red, all the ground).
            if (mode != Mode.Target && v.Outcome == null && !shoulder)
            {
                if (a != null && a.Alive && a.Team == Teams.Alien && Marked(a)) who.Add(a);
                else if (sightAll && CanAct()) who.AddRange(v.Units.Where(e => e.Alive && e.Team == Teams.Alien && Marked(e)));
            }
            if (who.Count == 0)
            {
                if (visionKey != null) AimMarks.ShowVision(null, null, null);
                visionKey = null;
                return;
            }
            var world = SightWorld(v);
            var parts = who.Select(e => SightOf(v, e, world)).ToList();
            var key = string.Join("|", parts.Select(x => x.key));
            if (key == visionKey) return;
            visionKey = key;
            // Several at once: each tile once (their red doesn't pile up), and the border round all of it.
            var seen = new List<Pos>();
            var noticed = new List<Pos>();
            var inSeen = new HashSet<(int, int, int)>();
            var inNoticed = new HashSet<(int, int, int)>();
            foreach (var x in parts)
            {
                foreach (var p in x.seen)
                    if (inSeen.Add((p.X, p.Y, p.Z))) seen.Add(p);
                foreach (var p in x.noticed)
                    if (inNoticed.Add((p.X, p.Y, p.Z))) noticed.Add(p);
            }
            AimMarks.ShowVision(v, seen, noticed, who.Count > 1);
        }

        /// <summary>
        /// What a field of view hangs on besides its viewer's place and facing: the walls, doors and boards of every storey,
        /// the lights, the dark, the weather, whether the squad is hidden, and which of ours hide where. While it stands, no
        /// enemy's is worked out again (#6: a horde of 54 took some 50 ms).
        /// </summary>
        static string SightWorld(GameState v)
        {
            unchecked
            {
                var h = 17;
                void Add(int x) => h = h * 31 + x;
                foreach (var t in v.Tiles) Add(t?.GetHashCode() ?? 0);
                foreach (var d in v.Decor ?? new List<int>()) Add(d);
                foreach (var level in v.Levels ?? new List<Level>())
                {
                    foreach (var t in level.Tiles) Add(t?.GetHashCode() ?? 0);
                    foreach (var d in level.Decor ?? new List<int>()) Add(d);
                }
                Add(v.Lights?.Count ?? 0);
                Add(v.Weather?.GetHashCode() ?? 0);
                Add(v.Mission.Night == true ? 1 : 0);
                Add(v.Concealed ? 1 : 0);
                foreach (var u in v.Units)
                    if (u.Alive && u.Hidden == true) Add(u.Pos.X * 4096 + u.Pos.Y * 8 + u.Pos.Z);
                return h.ToString();
            }
        }

        /// <summary>
        /// One enemy's field of view (`world`: SightWorld's): the tiles in its cone and its sight, and, the squad hidden, where
        /// it would notice one of ours; worked out again only when it moves, turns or the world changes.
        /// </summary>
        (string key, List<Pos> seen, List<Pos> noticed) SightOf(GameState v, Unit a, string world)
        {
            // (Sitting by a camp's fire, it notices only close: U28, Rules.Sitting.)
            var who = Unit() is Unit picked && picked.Team == Teams.Xcom ? picked : null;
            var sitting = Rules.Sitting(v, a);
            var key = $"{a.Id}:{world}:{a.Pos.X},{a.Pos.Y},{a.Pos.Z}:{a.Facing}:{sitting}:{who?.Id}";
            if (sights.TryGetValue(a.Id, out var was) && was.key == key) return was;
            // As far as it sees in this weather (Los.SightIn: a storm cuts it); SightTo below, tile by tile, at night too.
            var range = Los.SightIn(v, a);
            var seen = new List<Pos>();
            var noticed = new List<Pos>();
            for (var y = Math.Max(0, a.Pos.Y - range); y <= Math.Min(v.Height - 1, a.Pos.Y + range); y++)
            for (var x = Math.Max(0, a.Pos.X - range); x <= Math.Min(v.Width - 1, a.Pos.X + range); x++)
            {
                if (x == a.Pos.X && y == a.Pos.Y) continue;
                var p = new Pos { X = x, Y = y, Z = a.Pos.Z };
                // Behind cover from it, less of it (Arturo, 5/11 #4: Los.CoverShare), and one of ours hiding there, whichever
                // hides more (Los.ConcealShare, as the rules have it).
                var there = Grid.UnitAt(v, p) is Unit standing && standing.Team == Teams.Xcom ? standing : null;
                var share = there != null ? Los.ConcealShare(v, a, there, p) : Los.CoverShare(v, a, p, Teams.Xcom);
                if (!Grid.CanStandOn(Grid.KindAt(v, p)) || !Los.InViewCone(a, p) || Los.SightOrigin(v, a.Pos, p, Los.SightTo(v, a, p) * share, Los.WindowReachOf(a.Team)) == null) continue;
                seen.Add(p);
                // Where it would notice one of ours, as the rules do (Rules.Notices: hidden, one of the dead only ahead of it;
                // one sitting by a camp's fire only close, U28): the squad hidden, or one sitting, which sees far but notices
                // near. Whoever stands there, else the picked soldier as if there (its hiding counts).
                if ((v.Concealed || sitting) && Rules.Notices(v, a, there ?? who, p)) noticed.Add(p);
            }
            var made = (key, seen, noticed);
            sights[a.Id] = made;
            return made;
        }

        /// <summary>The enemies' sight's button as things stand (each frame: SightButton draws only what changed).</summary>
        SightView SightNow()
        {
            var battle = State != null && Shown.Outcome == null && !Film && Rig != null && !Rig.Overhead;
            return new SightView { Button = battle, Resting = battle && (Rig.CloseUp || Rig.Away), On = sightAll };
        }

        /// <summary>
        /// The enemies in sight that see the picked soldier now (Arturo, 5/11 #6), whose eye is open in their gauge (Ana,
        /// 7/11 #1); those farther than they see left out before the line is walked (SightOrigin's first test).
        /// </summary>
        HashSet<string> SeeingPicked()
        {
            var ids = new HashSet<string>();
            var v = Shown;
            var u = UnitById(v, picked);
            if (u == null || !u.Alive || u.Team != Teams.Xcom || v.Outcome != null) return ids;
            foreach (var a in v.Units)
                if (a.Alive && a.Team == Teams.Alien && Visible(a) && Grid.Distance(a.Pos, u.Pos) <= Los.SightIn(v, a) && Los.CanSee(v, a, u)) ids.Add(a.Id);
            return ids;
        }

        /// <summary>Who would see whom where, worked out for the pointer's tile (WatchersAt): its key and the enemies' ids.</summary>
        (string key, HashSet<string> ids) watchersAt;

        /// <summary>
        /// The enemies in sight that would see `u` standing on `at` (Arturo, 5/11 #5: a grey eye over each as its move is
        /// picked): the rules' (Los.Watchers: in their cone, as far as they see there or, the squad hidden, notice it; cut
        /// by cover and hiding, #4), of those the squad sees.
        /// </summary>
        HashSet<string> WatchersAt(Unit u, Pos at)
        {
            var s = State;
            var key = $"{s.Seq}:{u.Id}:{at.X},{at.Y},{at.Z}";
            if (watchersAt.key == key) return watchersAt.ids;
            var ids = new HashSet<string>(Los.Watchers(s, u, at).Where(Visible).Select(a => a.Id));
            watchersAt = (key, ids);
            return ids;
        }

        /// <summary>
        /// Over the enemies' flags, what the pointer's tile means (game.ts enemyTags): on an enemy, the odds of a shot at it;
        /// on a tile the picked one can move to, whom it would see from there and the odds if it could still shoot;
        /// with a throw armed, what it takes off each one caught (blastTags).
        /// </summary>
        /// <summary>The pointer on a tile the picked one can move to (EnemyTags): the eyes over the enemies are that tile's.</summary>
        bool hoverDest;

        Dictionary<string, (string kind, string text, string eye)> EnemyTags()
        {
            var tags = new Dictionary<string, (string, string, string)>();
            hoverDest = false;
            var u = Unit();
            var tile = hover;
            if (u == null || tile == null || !CanAct() || !Commands(u) || u.Id == sent) return tags;
            if (mode == Mode.Tile && HandArmed() == null) return BlastTags(u, modeAbility, tile);
            if (mode != Mode.Move) return tags;
            var shoots = Abilities.AbilitiesOf(u).Contains(AbilityIds.Shoot);
            var occupant = Grid.UnitAt(State, tile);
            if (occupant != null)
            {
                if (occupant.Team != Teams.Alien || !Visible(occupant)) return tags;
                // Beside it and unaware: the Golpe silencioso's word instead of the odds (Gameplay, 16/11).
                if (QuietLine(u, occupant) is { } quietLine)
                {
                    tags[occupant.Id] = (quietLine.kind, quietLine.text, null);
                    return tags;
                }
                if (!shoots) return tags;
                var shot = Aim.PreviewShot(State, u, occupant, new ShotOptions { Ability = AbilityIds.Shoot });
                tags[occupant.Id] = shot != null ? ("aim", $"{shot.Hit} % · crít. {Math.Min(shot.Crit, shot.Hit)} %", null) : ("none", "Sin línea de tiro", null);
                return tags;
            }
            var dest = tiles?.FirstOrDefault(t => Same(t.Pos, tile));
            if (dest == null) return tags;
            hoverDest = true;
            // Moving takes no actions now (Arturo, 5/11 #14): the odds from there while it has the shot's.
            var canShoot = shoots && u.Ap >= Abilities.ApNeeded(u, AbilityIds.Shoot);
            // Those that would see it there, a grey eye (Arturo, 5/11 #5), in place of «A la vista» over those it would
            // see (#8); the odds stay while it could still shoot.
            var watching = WatchersAt(u, tile);
            foreach (var a in State.Units)
            {
                if (!a.Alive || a.Team != Teams.Alien || !Visible(a)) continue;
                var shot = canShoot ? Aim.PreviewShot(State, u, a, new ShotOptions { Ability = AbilityIds.Shoot, From = tile }) : null;
                var eye = watching.Contains(a.Id) ? "would" : null;
                if (shot == null && eye == null) continue;
                // No «Flanco» over anyone (Ana, Arturo 01/11 #2; over the dead it went on 31/10 #13): the shot's panel says it.
                tags[a.Id] = ("sight", shot != null ? $"{shot.Hit} %" : null, eye);
            }
            return tags;
        }

        /// <summary>
        /// Those the picked one can take down now (Golpe silencioso: Abilities.TakedownTargets, Gameplay 16/11), in our turn,
        /// with the command and its key on; none, empty.
        /// </summary>
        HashSet<string> QuietTargets()
        {
            var u = Unit();
            if (u == null || !CanAct() || !Commands(u) || u.Id == sent || !Abilities.AbilitiesOf(u).Contains(AbilityIds.Takedown) || Abilities.AbilityBlocker(State, u, AbilityIds.Takedown) != null) return new HashSet<string>();
            return new HashSet<string>(Abilities.TakedownTargets(State, u).Select(t => t.Id));
        }

        /// <summary>
        /// The pointer on one beside `u` that is still unaware (Paths.OnPatrol), one the Golpe silencioso takes (a Zombi, a
        /// Corredor, a Saqueador or a Cazador: Abilities.TakenDown) or the Cabecilla (U28, G23.5: its refusal said): whether
        /// it takes it down, or why not («Te tiene delante…», the chainsaw); null, the shot's odds as for anyone.
        /// </summary>
        (string kind, string text)? QuietLine(Unit u, Unit t)
        {
            if (!Abilities.TakenDown.Contains(t.Template) && t.Template != TemplateIds.Officer || !Aim.InMeleeReach(State, u.Pos, t.Pos) || !Paths.OnPatrol(State, t)) return null;
            if (!Abilities.AbilitiesOf(u).Contains(AbilityIds.Takedown)) return null;
            // The key's own why (the chainsaw, no actions) as it is; this one's, after the action's name.
            if (Abilities.AbilityBlocker(State, u, AbilityIds.Takedown) is string off) return ("flank", off.TrimEnd('.'));
            var no = Abilities.TakedownRefusal(State, u, t);
            // A why that names it already (the Cabecilla's, U28) goes alone, not «Golpe silencioso · … golpe silencioso».
            if (no != null && no.IndexOf(HudText.Takedown.Name, StringComparison.OrdinalIgnoreCase) >= 0) return ("flank", no.TrimEnd('.'));
            return no == null ? ("aim", HudText.Takedown.On(HudText.Takedown.Kills)) : ("flank", HudText.Takedown.On(no.TrimEnd('.')));
        }

        /// <summary>A crit modifier's words for `target`: the engine's «Flanqueo» on one of the dead is being in the open (#13).</summary>
        static string CritLabel(Unit target, string label) => label == "Flanqueo" && Content.Templates[target.Template].Undead ? "Al descubierto" : label;

        /// <summary>
        /// A grenade aimed: what it would take off everyone caught, over their flags (playtest: «no sé dónde está el daño
        /// que hago»); enemies with whether it kills them, our own in warning.
        /// </summary>
        Dictionary<string, (string kind, string text, string eye)> BlastTags(Unit u, string ability, Pos tile)
        {
            var tags = new Dictionary<string, (string, string, string)>();
            var centre = AbilityTexts.ThrowTile(u, ability, tile);
            if (!Abilities.CanTargetTile(State, u, ability, centre)) return tags;
            // Controlled detonation leaves the grenadier's squad out of the blast.
            var grenade = ability == AbilityIds.Grenade || ability == AbilityIds.Launch;
            foreach (var p in Abilities.BlastTiles(State, ability, centre, u))
            {
                var t = Grid.UnitAt(State, p);
                if (t == null || (grenade && Content.HasPerk(u, PerkIds.SafeBlast) && t.Team == u.Team) || (t.Team == Teams.Alien && !Visible(t))) continue;
                var d = Rules.AbilityDamage(State, u, ability, t);
                if (d == null) return tags;
                var text = $"Daño {AbilityTexts.Span(d.Hit)}";
                if (t.Team == Teams.Xcom) tags[t.Id] = ("flank", $"¡Aliado! {text}", null);
                else tags[t.Id] = ("aim", string.Join(" · ", new[] { text, AbilityTexts.Lethality(d, t) }.Where(x => x != null)), null);
            }
            return tags;
        }

        // ------------------------------------------------------- players

        /// <summary>The players on the strip (hud.ts setPlayers): alone, the turn's state is the end-turn button's business.</summary>
        PlayersView PlayersView(GameState v, int? commander)
        {
            var view = new PlayersView();
            var busy = AbilityTexts.BusySlots(v);
            var acting = AbilityTexts.ActingSlots(v);
            var alone = !Players.Any(p => p.Slot != MySlot && p.Connected);
            foreach (var p in Players)
            {
                var holds = commander == p.Slot;
                var status = !p.Connected ? HudText.Players.Offline : p.Away == true ? HudText.Players.Away : alone || holds ? "" : v.Ready[p.Slot] ? HudText.Players.Ready : busy.Contains(p.Slot) ? (acting.Contains(p.Slot) ? HudText.Players.WithActions : HudText.Players.WithMoves) : HudText.Players.NoActions;
                view.Players.Add(new PlayerView { Slot = p.Slot, Name = p.Name, Color = p.Color, Me = p.Slot == MySlot, Connected = p.Connected, Ready = v.Ready[p.Slot], Command = holds, Status = status });
            }
            // With the finger, «Ceder» by the partner, as the menu's «Ceder el mando» (CombatController.Menu: PassOn).
            var partner = PartnerHere();
            // And with the mouse when only soldiers inside La Lata are left (T24: OnlyInside), where it is what is pressed.
            view.CanPass = (Bastion.Client.GameInput.Touch || OnlyInside()) && partner != null && v.ActiveTeam == Teams.Xcom && v.Outcome == null && CanAct()
                && HasCommand() && !v.Ready[partner.Slot] && busy.Contains(partner.Slot);
            return view;
        }

        // ------------------------------------------------------- squad

        /// <summary>The soldiers whose busts were asked for (SquadView).</summary>
        readonly HashSet<string> busts = new HashSet<string>();

        /// <summary>A bust drawn (Portraits.Changed): the squad list again with it.</summary>
        void OnBust(string id) => Hud?.PicturesChanged();

        void OnDestroy() => Bastion.Client.Portraits.Changed -= OnBust;

        /// <summary>
        /// The squad's cards (hud.ts setSquad; UI-COMBATE UI-02): this player's soldiers (Arturo, 01/11), the fallen and the
        /// gone too, then the partner's (Ana, 7/11 #12: a card with their name and bust each, which takes the camera there;
        /// picked, only while this player commands them); not a siege's turrets (their flags say how they are). Each bust
        /// on its owner's colour. A run's survivor only once they join, among the soldiers of whoever talked to them (N2).
        /// </summary>
        SquadView SquadView(GameState v, string inPlay)
        {
            var view = new SquadView();
            var units = v.Units.Where(OnCard).ToList();
            // Each soldier's bust for its card, asked for once (Arte's Portraits draws one a frame).
            foreach (var u in units)
                if (u.Team == Teams.Xcom && busts.Add(u.Id))
                    // Dressed by the figure's own seed (UnitViews: the campaign's id), or each card showed someone else (Arte, 15/11).
                    Bastion.Client.Portraits.Know(u.Id, u.Appearance, u.Name, Bastion.Client.Units.SquadTint.ColourOf(u.Owner ?? MySlot), u.CampaignId ?? u.Name);
            int Rank(Unit u) => u.Owner == MySlot || u.Owner == null ? 0 : 1;
            // Separados (U27): each group together, a gap between them (playing alone the cards said nothing of who was in
            // which); with two, the same as by owner. A survivor who joined is of neither: last.
            int Group(Unit u) => v.Mission.Split?.FindIndex(g => g.Contains(u.Id)) is int g && g >= 0 ? g : int.MaxValue;
            var carrier = v.Mission.Item?.Carrier;
            int? lastGroup = null;
            foreach (var u in units.OrderBy(Rank).ThenBy(Group))
            {
                var mine = Commands(u);
                var theirs = u.Owner != null && u.Owner != MySlot;
                var owner = u.Owner ?? MySlot;
                var card = new SquadCardView
                {
                    Id = u.Id,
                    Color = Players.FirstOrDefault(p => p.Slot == owner)?.Color ?? Content.PlayerColors[Math.Clamp(owner, 0, Content.PlayerColors.Count - 1)],
                    Name = (u.Template == TemplateIds.Rookie ? "" : Content.Templates[u.Template].Short + " ") + (!string.IsNullOrEmpty(u.Nickname) ? $"«{u.Nickname}»" : u.Name),
                    // The bust's: the nickname with no quotes (Producto, 18/11: «Chispa» didn't fit eight busts), else the first name.
                    Short = !string.IsNullOrEmpty(u.Nickname) ? u.Nickname : u.Name.Split(' ')[0],
                    Mine = mine,
                    Selected = u.Id == picked,
                    // (El herido carried, U25: not alive in the engine's sense while on someone's back, never fallen.) Fallen as
                    // soon as their health is gone, with the bar, not once their death has played (R19 of Red, 23/11: a jump that
                    // kills, «CAÍDO» 2.6 s after the bar emptied); only mid-playback is anyone alive at 0.
                    // (La Camilla's downed lies alive at 0, 0.14.11: not fallen till they die.)
                    Dead = (!u.Alive || (u.Hp <= 0 && u.Downed != true && !Downing(u.Id))) && !u.Evacuated && u.CarriedBy == null,
                    Female = u.Appearance?.Sex == "f",
                    // Inside La Lata (T24) it plays on (Bajar, Evacuar): not greyed as one already gone.
                    Evacuated = u.Evacuated && u.Aboard != true,
                    InPlay = u.Id == inPlay,
                    Bust = u.Team == Teams.Xcom,
                    Hp = u.Hp,
                    MaxHp = u.MaxHp,
                    Title = !theirs ? HudText.Squad.Select : mine ? HudText.Squad.TheirsCommanded : HudText.Squad.TheirsLook(Players.FirstOrDefault(p => p.Slot == owner)?.Name),
                    Ap = u.Ap,
                    ApMax = Math.Max(Content.ApPerTurnOf(u), u.Ap),
                    // The movement left, a piece a tile (Arturo, 5/11 #14); not a turret's.
                    Moves = u.Automaton == null && u.Alive ? Paths.MoveBudget(v, u) / Paths.CostOrtho : 0,
                    MovesMax = u.Automaton == null && u.Alive ? Paths.FullMoves(v, u) / Paths.CostOrtho : 0,
                };
                // «EN JUEGO» is the card's own (SquadList, from InPlay), first.
                card.Tags.AddRange(StateTags(u, carrier));
                var group = Group(u);
                card.GroupStart = v.Mission.Split != null && lastGroup != null && group != lastGroup && group != int.MaxValue;
                lastGroup = group;
                view.Cards.Add(card);
            }
            return view;
        }

        /// <summary>
        /// A soldier's states for its card, in order, each with its icon for a narrow bust (Producto, 18/11: with six busts
        /// or more the words don't fit, and shortening them changes their names everywhere; SquadList.StateIcons). The
        /// picked one's sheet says them in full (UnitPanel), as its flag over the figure does.
        /// </summary>
        List<TagView> StateTags(Unit u, string carrier)
        {
            var list = new List<TagView>();
            void Tag(bool on, string text, string kind, string icon, string tip = null)
            {
                if (on) list.Add(new TagView { Text = text, Kind = kind, Tip = tip, Icon = icon });
            }
            Tag(u.ControlledBy != null, UnitWords.Agree(u, "CONTROLADO"), "psi", "state:controlled");
            // A run's survivor alone is hiding; only the Cuervos' prisoner is held (Arturo, in the cuadro, 18/11; Lore's).
            var hiding = u.Captive && Shown.Run != null && Shown.Mission.Guarded != true;
            Tag(u.Captive && u.Lame != true, UnitWords.Agree(u, hiding ? "ESCONDIDO" : "CAUTIVO"), hiding ? "hide" : "data", hiding ? "state:hide" : "state:captive");
            // El herido (U25): on their bust, «PIERNA ROTA» on the floor; «CARGANDO» on whoever carries them.
            // (The bust's short ones, Lore's: «A BORDO»'s width at most.)
            // (La Camilla's downed too, 0.14.11: «NO ANDA» on both, Lore's; what to do on hover.)
            Tag(u.Lame == true && u.CarriedBy == null && u.Alive, Lore.WoundedText.FloorTagShort, "stun", "state:down", u.Downed == true ? Lore.StretcherText.TagHelp(UnitWords.Female(u) ? Sexes.F : Sexes.M) : null);
            Tag(u.Carrying != null, Lore.WoundedText.CarrierTagShort, "data", "state:carry");
            Tag(u.Disoriented > 0, UnitWords.Agree(u, "DESORIENTADO"), "psi", "state:disoriented");
            Tag(u.Panicked, "PÁNICO", "psi", "state:panic");
            // Inside La Lata, or gone in it: Lore's «A BORDO» (T24; «EVACUADO» came out cut, and agreed in gender).
            Tag(u.Evacuated || u.Aboard == true, Lore.BoardText.AboardTag, "evac", "state:aboard");
            Tag(carrier == u.Id, Lore.ItemTag(RecoveryItem()), "data", "state:carry");
            Tag(u.Overwatch, u.KillZone ? "ZONA LETAL" : u.WatchMelee == true ? "HOJA EN ALTO" : "VIGILANCIA", "ow", u.KillZone ? "state:killZone" : u.WatchMelee == true ? "state:watchMelee" : "state:overwatch");
            Tag(u.Hunkered, "CUBRIÉNDOSE", "hk", "state:hunker");
            Tag(u.Hidden == true, UnitWords.Agree(u, "ESCONDIDO"), "hide", "state:hide");
            Tag(u.Stunned, UnitWords.Agree(u, "ATURDIDO"), "stun", "state:stun");
            // Knocked down by El Toro's charge (H6): up at the squad's next turn, an action spent. Lore's «EN EL SUELO»
            // (masculine «tumbado» would go over anyone).
            Tag(u.Down == true, Lore.BossText.DownTag, "stun", "state:down", Lore.Glossary["En el suelo"]);
            return list;
        }

        /// <summary>
        /// Whether `u` has a card under the sheet: a soldier of the squad's, this player's (or one with no owner, alone) or
        /// the partner's (Ana, 7/11 #12); a living one or not.
        /// </summary>
        bool OnCard(Unit u) => u.Team == Teams.Xcom && u.Automaton == null && !(Shown.Run != null && u.Captive && u.Owner == null);

        /// <summary>What a recovery carries out: the chapter's own name for it, or the default (game.ts recoveryItem).</summary>
        string RecoveryItem() => Shown.CampaignMission?.Item ?? Lore.DefaultItem;

        /// <summary>
        /// The six lines' values (SkillIds.All order) for a soldier's hexagon: the cells bought of each line of its board
        /// (Arturo, 29/10), as the campaign had it at take-off; with no campaign, the cells that give its perks.
        /// </summary>
        float[] SkillValues(Unit u)
        {
            if (u.Lines == null) return Array.Empty<float>();
            var soldier = u.CampaignId != null ? Campaign?.Invoke()?.Soldiers.FirstOrDefault(s => s.Id == u.CampaignId) : null;
            return soldier != null ? SkillHex.Branches(soldier.Board) : SkillHex.BranchesOfPerks(u.Perks);
        }

        // ------------------------------------------------------- the picked one

        /// <summary>
        /// The soldier's panel (hud.ts setSelected): symbols, pips and numbers, readable at a glance (full words in the
        /// tooltips); a partner's soldier says whose.
        /// </summary>
        /// <summary>
        /// The weapon in `u`'s hand by its picture (U29, Producto 21/11: «Ballesta» had a gun's by it): the found weapon's, else
        /// the pistol's, else the blade's; an enemy's, its template's (the Cazador's crossbow). One with no picture of its own
        /// (Ui.WeaponIcon's blade for what is no blade), the rifle's, as all had before.
        /// </summary>
        static string WeaponIconOf(Unit u)
        {
            var arms = u.Arms;
            var id = arms == null ? Content.Templates[u.Template].Weapon
                : !string.IsNullOrEmpty(arms.Primary) ? Content.FoundWeapons[arms.Primary].Weapon
                : Content.HasPistol(u) ? WeaponIds.Pistol
                : !string.IsNullOrEmpty(arms.Melee) ? Content.FoundWeapons[arms.Melee].Weapon : WeaponIds.Knife;
            var icon = Ui.WeaponIcon(id);
            return icon == "overwatchMelee" && !(id != null && Content.Weapons.TryGetValue(id, out var w) && w.Melee) ? "stat:weapon" : icon;
        }

        /// <summary>
        /// A soldier's cover as its shield shows it (Arturo, 7/11; the flag's square before): the worst against whoever sees
        /// it (0, flanked), else the best round it; -1, no cover, or cover doesn't count. With no shooter in sight nobody
        /// flanks anyone: the shield only shows cover, never the red «flanked».
        /// </summary>
        int CoverOf(GameState v, Unit u)
        {
            if (!CoverMatters() || u.Team != Teams.Xcom) return -1;
            var watchers = v.Units.Where(a => a.Alive && a.Team == Teams.Alien && Visible(a)).ToList();
            if (watchers.Count == 0) return Cover.HasAnyCover(v, u.Pos) ? Cover.CoverSides(v, u.Pos).Max() : -1;
            return watchers.Min(a => Cover.CoverAgainst(v, u.Pos, a.Pos));
        }

        UnitPanelView UnitPanel(Unit u)
        {
            var t = Content.Templates[u.Template];
            // A soldier's own arms (docs/FASE8.md §A): what is in the hand (the pistol if nothing), the pistol beside it, the knife or machete.
            var arms = u.Arms;
            var pistol = Content.Weapons[WeaponIds.Pistol].Name;
            var (weapon, extras) = ArmsOf(u);
            // Without a gun (Arturo, 01/11 #10: the knife alone), no rounds nor aim with one.
            var gun = Content.HasGun(u);
            var clip = gun ? Content.ClipOf(u, AbilityIds.Shoot) : 0;
            var sidearm = !string.IsNullOrEmpty(arms?.Primary) && Content.HasPistol(u);
            // A soldier's veterancy, in the feminine for a woman.
            var soldier = u.Team == Teams.Xcom && u.Template == TemplateIds.Rookie;
            var step = soldier && u.Rank < Bars.Veterancy.Count ? Bars.Veterancy[u.Rank] : null;
            var rank = step != null ? (u.Appearance?.Sex == "f" ? step.Female : step.Name) : "";
            var view = new UnitPanelView
            {
                Id = u.Id,
                Soldier = soldier,
                Rank = soldier ? u.Rank : 0,
                RankName = soldier ? rank : t.Short,
                Skills = SkillValues(u),
                Name = !string.IsNullOrEmpty(u.Nickname) ? $"{u.Name} «{u.Nickname}»" : u.Name,
                Weapon = weapon,
                WeaponIcon = WeaponIconOf(u),
                WeaponTip = string.Join(" · ", new[] { weapon }.Concat(extras)) + (Content.BareHanded(u) ? "\n" + HudText.Bare.Tip : ""),
                WeaponBare = weapon == HudText.Bare.Name,
            };
            // Its cover, a shield in its colour (Arturo, 7/11).
            var shown = UnitById(Shown, u.Id) ?? u;
            view.Cover = CoverOf(Shown, shown);
            if (view.Cover >= 0)
            {
                view.CoverTitle = HudText.CoverShield.Title;
                view.CoverTip = HudText.CoverShield.Tip(u.Name);
            }
            // A partner's soldier says whose, in the partner's colour (Arturo, 17/10).
            if (u.Team == Teams.Xcom && u.Owner != null && u.Owner != MySlot)
            {
                view.Whose = Players.FirstOrDefault(p => p.Slot == u.Owner)?.Name ?? "";
                view.WhoseColor = PlayerColor(u.Owner.Value);
            }
            var aim = t.Aim + u.Mods.Aim;
            // Health and actions are on its card (UI-COMBATE UI-02); one with none (the partner's, a turret) has them here.
            if (!OnCard(u))
            {
                view.Stats.Add(new StatView { Kind = "hp", Tip = $"Salud {u.Hp}/{u.MaxHp}", Filled = u.Hp, Total = u.MaxHp });
                view.Stats.Add(new StatView { Kind = "ap", Tip = $"Acciones {u.Ap}/{Content.ApPerTurnOf(u)}", Filled = u.Ap, Total = Math.Max(Content.ApPerTurnOf(u), u.Ap) });
                if (u.Team == Teams.Xcom && u.Automaton == null && Paths.FullMoves(State, u) > 0)
                    view.Stats.Add(new StatView { Kind = "moves", Tip = HudText.Moves.Tip(Paths.MoveBudget(State, u), Paths.FullMoves(State, u), Paths.ConcealedBonus(State, u)), Filled = Paths.MoveBudget(State, u) / Paths.CostOrtho, Total = Paths.FullMoves(State, u) / Paths.CostOrtho });
            }
            // Arco y ballesta (U29): the bolt loaded (the crossbow's clip of one; the bow has none) and the arrows in the pack,
            // which both shoot: «7», red at none; the enemies' are not counted (Unit.Arrows null), nor shown.
            var arrows = gun && Content.AttackWeapon(u, AbilityIds.Shoot) is WeaponDef shot && shot.Arrows;
            if (arrows)
            {
                if (clip > 0) view.Stats.Add(new StatView { Kind = "ammo", Tip = $"{weapon}: {u.Ammo}/{clip}", Filled = u.Ammo, Total = clip });
                if (u.Arrows is int left) view.Stats.Add(new StatView { Kind = "arrows", Tip = HudText.Arrows.Tip(left), Value = left.ToString(), ValueBad = left == 0 });
            }
            // The spare magazines after the clip (docs/FASE7.md): «+3», red at none; the enemies don't count theirs.
            else if (clip > 0)
            {
                var spares = u.Mags == null ? "" : u.Mags > 0 ? $" · {Lore.MagsCount(u.Mags.Value)} de repuesto" : " · sin cargadores de repuesto";
                view.Stats.Add(new StatView { Kind = "ammo", Tip = $"{weapon}: {u.Ammo}/{clip}{spares}", Filled = u.Ammo, Total = clip, Value = u.Mags != null ? $"+{u.Mags}" : null, ValueBad = u.Mags == 0 });
            }
            // The pistol carried beside a main gun counts its own rounds; the spares serve both.
            if (sidearm) view.Stats.Add(new StatView { Kind = "sidearm", Tip = $"{pistol}: {u.PistolAmmo ?? 0}/{Content.PistolClip}", Filled = u.PistolAmmo ?? 0, Total = Content.PistolClip });
            // Their words always in sight (B24, Producto 23/11): «PUNT. 55», «CaC 58»; the blade's the spare one with a gun in hand.
            if (gun) view.Stats.Add(new StatView { Kind = "aim", Tip = $"Puntería al disparar {aim + u.Mods.GunAim}", Value = (aim + u.Mods.GunAim).ToString(), Word = HudText.Stats.Aim });
            if (soldier) view.Stats.Add(new StatView { Kind = "melee", Tip = $"Puntería cuerpo a cuerpo {aim + u.Mods.MeleeAim}", Value = (aim + u.Mods.MeleeAim).ToString(), Word = HudText.Stats.Melee, Spare = gun });
            if (u.Charges.TryGetValue(ChargeIds.Grenade, out var grenades)) view.Stats.Add(new StatView { Kind = "grenade", Tip = $"Granadas: {grenades}", Value = $"×{grenades}" });
            if (u.Charges.TryGetValue(ChargeIds.Molotov, out var molotovs)) view.Stats.Add(new StatView { Kind = "molotov", Tip = $"Cócteles molotov: {molotovs}", Value = $"×{molotovs}" });
            if (u.Armor > 0) view.Stats.Add(new StatView { Kind = "armor", Tip = $"Blindaje {u.Armor}", Value = u.Armor.ToString() });
            // A vest found and put on out there (playtest 16/10): its health is already in the pips.
            foreach (var item in u.Worn ?? new List<string>())
            {
                var def = Content.Items[item];
                view.Stats.Add(new StatView { Kind = "worn", Tip = $"Lleva puesto: {def.Name} ({def.Description.TrimEnd('.')})", Value = $"+{def.Hp ?? 0}" });
            }
            // Its state and improvements (docs/FASE9.md §5), where it carries a weapon of the Armería.
            if (!string.IsNullOrEmpty(arms?.Primary)) view.WeaponMarks = WeaponMarks(Content.ConditionOf(u), arms.Mods);
            view.Pack = PackOf(u);
            // With the busts' states as icons, the picked one's in full beside its name.
            if (SquadList.StateIcons && u.Team == Teams.Xcom) view.States = StateTags(u, Shown.Mission.Item?.Carrier);
            return view;
        }

        // ------------------------------------------------------- the objective

        /// <summary>The objective's card (game.ts missionView), in Lore's words for each moment of it.</summary>
        MissionView MissionView()
        {
            var s = Shown;
            var m = s.Mission;
            if (m.Siege != null) return SiegeView();
            var objective = "Abatid a todo lo que haya";
            string tip = null;
            switch (m.Kind)
            {
                case MissionKinds.Recovery:
                {
                    var carrier = UnitById(s, m.Item?.Carrier);
                    var item = RecoveryItem();
                    objective = m.Item?.Evacuated == true ? Lore.ItemObjective(item, "out")
                        : carrier != null && carrier.Alive && Abilities.InEvacZone(s, carrier.Pos) ? Lore.ItemObjective(item, "aboard")
                        : carrier != null ? Lore.ItemObjective(item, "carried", carrier.Name)
                        : Lore.ItemObjective(item, "loose");
                    break;
                }
                case MissionKinds.Sabotage:
                    objective = m.ObjectiveDone ? Lore.ObjectiveDone[MissionKinds.Sabotage] : Lore.AlarmObjective(m.Alarm);
                    break;
                case MissionKinds.Hack:
                    objective = m.ObjectiveDone ? Lore.ObjectiveDone[MissionKinds.Hack] : "Arreglad el equipo (junto a él)";
                    break;
                case MissionKinds.Rescue:
                {
                    var vip = UnitById(s, m.ObjectiveUnit);
                    var who = vip?.Name ?? "la persona retenida";
                    // A run's (N2, 17/11): looked for in the marked zone till someone sees them, then talked to (Hablar);
                    // dead, the stage goes on without a recruit. Lore's phases.
                    var run = s.Run != null && vip != null;
                    objective = run && !vip.Alive && !vip.Evacuated ? Lore.VipObjective(who, "dead")
                        // Gone in La Lata (T24: the recruit counts if inside as it leaves): «…, a salvo».
                        : m.ObjectiveDone || (run && vip.Evacuated && m.EvacDone == true) ? Lore.VipObjective(who, "out")
                        : run && Unfound(vip) ? Lore.VipObjective(who, "search")
                        : run && vip.Captive ? Lore.VipObjective(who, "talk")
                        // Inside La Lata (T24) as in its zone: «…, en La Lata: que suba el resto».
                        // Inside with La Lata full and some of the squad still out (Lore, 18/11): go, or someone gets down.
                        : vip != null && vip.Aboard == true && Rules.SeatsFree(s) == 0 && s.Units.Any(u => u.Alive && u.Team == Teams.Xcom && !u.Captive && u.Automaton == null && u.Id != vip.Id) ? Lore.VipObjective(who, "full")
                        : vip != null && (vip.Aboard == true || (vip.Alive && !vip.Captive && Abilities.InEvacZone(s, vip.Pos))) ? Lore.VipObjective(who, "aboard")
                        : vip != null && vip.Captive ? Lore.VipObjective(who, "captive")
                        : Lore.VipObjective(who, "loose");
                    break;
                }
                case MissionKinds.Scavenge:
                {
                    // What the squad carries counts once it is out (or standing at the end); a fallen soldier's is lost.
                    var n = s.Units.Where(u => u.Team == Teams.Xcom && (u.Alive || u.Evacuated)).Sum(u => u.Backpack?.Count ?? 0);
                    objective = n > 0 ? Lore.Scavenge.Carrying.Replace("{n}", n.ToString()) : Lore.Scavenge.Objective;
                    if (n > 0) tip = Lore.Scavenge.CarryingTip.Replace("{n}", n.ToString());
                    break;
                }
            }
            // The hordes on the way are not said on the card (playtest 19/10 #20); a flare drop, still.
            var alert = m.Reinforcements.Any(r => !r.Landed && r.Horde != true && r.Pos != null) ? "VIENEN MÁS" : null;
            // La Camilla's downed soldier still outside La Lata (0.14.11): what to do, first, in El herido's words (Lore's).
            if (Rules.DownedOf(s) is Unit downed && downed.Aboard != true) alert = Lore.WoundedText.Bring(downed.Name.Split(' ')[0]);
            // The noise meter (Ana's notes, Arturo 29/10): the horde's next wave as dB, 40 quiet and 90 as it comes (Math.round).
            var fill = Rules.HordeFill(s);
            var turnsLeft = m.TurnsLeft ?? -1;
            // A stage of an expedition (H5, H6): its name; the Asedio's count to La Lata; El Toro to kill.
            if (m.HoldUntil is int until)
            {
                turnsLeft = Math.Max(0, until - s.Turn);
                // Come and full with some of the squad still out (Red's pass, 18/11): go, or someone gets down.
                var outside = s.Units.Any(u => u.Alive && u.Team == Teams.Xcom && !u.Captive && !u.Objective && u.Automaton == null);
                objective = HudText.Mission.Hold(until, turnsLeft, Rules.SeatsFree(s) == 0 && outside);
                tip = HudText.Mission.HoldTip;
            }
            if (m.Boss != null && UnitById(s, m.Boss) is Unit boss)
                objective = boss.Alive ? HudText.Mission.Boss(boss.Name) : HudText.Mission.BossDead(boss.Name);
            // A Guarida's box to open first, then out; a Travesía, across (H5): Lore's line of the stage, the tip.
            else if (m.Lair != null)
            {
                objective = m.ObjectiveDone ? HudText.Mission.LairOpen : HudText.Mission.Lair;
                tip = Lore.StageLine(NodeKinds.Elite);
            }
            // A run's Rescate (N2): its line of the stage, with the Cuervos when they hold the survivor.
            else if (s.Run != null && m.Kind == MissionKinds.Rescue)
                tip = Lore.StageLine(NodeKinds.Rescue, crows: m.Guarded == true);
            else if (s.Run?.NodeKind == NodeKinds.Crossing)
            {
                objective = HudText.Mission.Crossing;
                tip = Lore.StageLine(NodeKinds.Crossing);
            }
            // A Sigilo (H5): its Recuperación's, unseen while the squad is hidden (Lore's words, StageLine; the pass of 17/11:
            // the card said the Recuperación's alone); its rule, the double unseen and the half seen, the tip.
            else if (m.Stealth == true)
            {
                if (s.Concealed && m.Item?.Evacuated != true) objective += " sin que os vean";
                tip = Lore.StageLine(NodeKinds.Stealth, RecoveryItem());
            }
            var steps = KeySteps(s);
            if (steps != null)
            {
                objective = steps.First(x => !x.Done).Text;
                tip = Lore.KeyText.ObjectiveTip;
            }
            else if (BiteSteps(s, objective) is List<GoalStepView> bite)
            {
                steps = bite;
                objective = (bite.FirstOrDefault(x => !x.Done) ?? bite.Last()).Text;
                tip = Lore.StageLine(NodeKinds.Bitten, biteTurns: Setup.BiteTurns);
            }
            else if (WoundedSteps(s) is List<GoalStepView> hurt)
            {
                steps = hurt;
                objective = (hurt.FirstOrDefault(x => !x.Done) ?? hurt.Last()).Text;
                tip = Lore.WoundedText.ObjectiveTip;
            }
            else if (SplitSteps(s) is List<GoalStepView> split)
            {
                steps = split;
                objective = (split.FirstOrDefault(x => !x.Done) ?? split.Last()).Text;
                tip = Lore.SplitText.ObjectiveTip;
            }
            else if (RaidersSteps(s) is List<GoalStepView> raid)
            {
                steps = raid;
                objective = (raid.FirstOrDefault(x => !x.Done) ?? raid.Last()).Text;
                tip = Lore.RaidersText.ObjectiveTip;
            }
            // Saqueadores with no room on its map for the camp (Mission.Raiders null, about 1 in 90): a Saqueo's objective, and
            // Lore's line that says so (Producto, 21/11).
            else if (NoCamp(s)) tip = Lore.RaidersText.NoCamp;
            return new MissionView
            {
                Label = s.Run != null ? Bastion.UI.Strategy.StrategyText.Runs.NodeName(s.Run.NodeKind) : MissionName(m.Kind),
                Area = s.MapName,
                Objective = objective,
                ObjectiveTip = tip,
                TurnsLeft = turnsLeft,
                // A Limpieza's (O8): won with none of them left (Rules' Combatants), so all of them, seen or not.
                Left = m.Kind == MissionKinds.Elimination ? Bastion.Engine.Grid.Combatants(s, Teams.Alien).Count : -1,
                Alert = alert,
                HordeFill = fill.HasValue ? (float)fill.Value : -1,
                HordeDb = fill.HasValue ? 40 + (int)Math.Floor(50 * fill.Value + 0.5) : 0,
                // With the Motor silencioso (cuadro 165): the turn it can come from, till then, on the meter and in its help.
                HordeFrom = m.HordeFrom is int from && s.Turn < from ? from : 0,
                HordeTitle = !fill.HasValue ? null : m.HordeFrom is int comes && s.Turn < comes ? HudText.Mission.HordeFromTip(comes) : HudText.Mission.NoiseTip,
                Weather = s.Weather,
                Steps = steps,
            };
        }

        /// <summary>
        /// Saqueadores' objective in two steps (U28, G23.5; Lore's words): the stash, with its crates still shut («quedan 2 de
        /// 3 cajas»), done once none is; and La Lata. Stealing it is up to the squad (without it, the stage is a Saqueo), so
        /// the second can be done with the first not. Null in another fight.
        /// </summary>
        static List<GoalStepView> RaidersSteps(GameState s)
        {
            var camp = s.Mission.Raiders;
            if (camp == null || camp.Crates.Count == 0) return null;
            var left = StashLeft(s).Count;
            return new List<GoalStepView>
            {
                new GoalStepView { Text = Lore.RaidersText.Stash(left, camp.Crates.Count), Done = left == 0 },
                new GoalStepView { Text = Lore.RaidersText.Leave, Done = s.Mission.EvacDone == true },
            };
        }

        /// <summary>Saqueadores on a map with no room for its camp (Mission.Raiders null): played as a Saqueo (U28).</summary>
        static bool NoCamp(GameState s) => s.Run?.NodeKind == NodeKinds.Raiders && s.Mission.Raiders == null;

        /// <summary>Saqueadores' crates still shut (U28): the stash's (MissionState.Raiders) not searched yet; none in another fight.</summary>
        static List<Pos> StashLeft(GameState s) =>
            (s.Mission.Raiders?.Crates ?? new List<int>()).Select(n => Grid.PosOf(s, n)).Where(p => !Looting.WasSearched(s, p)).ToList();

        /// <summary>
        /// How Saqueadores' guards on watch show where they look (U28, two looks for Producto's cuadro): «abanico», a short
        /// fan at each one's feet; «vista», the tiles where each would notice one of ours, as the pointer's enemy's sight.
        /// </summary>
        public static string PostsLook = "abanico";

        /// <summary>What is on the ground now for the guards (ShowPosts): their keys.</summary>
        string postsKey;

        /// <summary>
        /// Saqueadores' guards on watch (U28; Gameplay's Ai.KeepPost: at the fire or by the stash they look one way, and what
        /// is behind them they don't see): where each one in sight looks, on the ground (PostsLook), while its camp sleeps
        /// (Paths.OnPatrol); the round's too, which looks where it walks. Once the camp woke, none: they look all about. Not
        /// over the shoulder, nor aiming (its target's marks), nor in a film or once over.
        /// </summary>
        void ShowPosts()
        {
            if (AimMarks == null) return;
            var v = Shown;
            var guards = v.Mission.Raiders == null || v.Outcome != null || Film || shoulder || mode == Mode.Target ? new List<Unit>()
                : v.Units.Where(a => a.Alive && a.Guard != null && a.Team == Teams.Alien && a.Facing != null && Marked(a) && Paths.OnPatrol(v, a)).ToList();
            var fan = PostsLook != "vista";
            var world = fan || guards.Count == 0 ? "" : SightWorld(v);
            var parts = fan ? null : guards.Select(a => SightOf(v, a, world)).ToList();
            var key = fan ? string.Join("|", guards.Select(a => $"{a.Id}:{a.Pos.X},{a.Pos.Y},{a.Pos.Z}:{a.Facing}")) : string.Join("|", parts.Select(x => x.key)) + (v.Concealed ? ":c" : "");
            if (key == postsKey) return;
            postsKey = key;
            if (guards.Count == 0)
            {
                AimMarks.ShowPosts(null, null, null);
                return;
            }
            if (fan)
            {
                AimMarks.ShowPosts(v, guards.Select(a => (a.Pos, a.Facing.Value)).ToList(), null);
                return;
            }
            // Where they would notice one of ours: hidden or sitting by the fire, as each notices (SightOf's, the rules'); seen,
            // as far as each sees. Each tile once.
            var tiles = new List<Pos>();
            var taken = new HashSet<(int, int, int)>();
            for (var i = 0; i < parts.Count; i++)
                foreach (var p in v.Concealed || Rules.Sitting(v, guards[i]) ? parts[i].noticed : parts[i].seen)
                    if (taken.Add((p.X, p.Y, p.Z))) tiles.Add(p);
            AimMarks.ShowPosts(v, null, tiles);
        }

        /// <summary>
        /// La llave's objective in three steps (U22, G23.1; Lore's words): the key, the armoury, La Lata. The key's done once
        /// in a pack or used, the armoury's once open, with the key or by a hole in its walls (Looting.ArmoryOpen, which
        /// does the first too: the key isn't needed then); the first says the key lies on the floor while it does. Null in
        /// another fight.
        /// </summary>
        static List<GoalStepView> KeySteps(GameState s)
        {
            if (s.Mission.KeyArmory != true) return null;
            var where = Looting.KeyWhere(s);
            var open = Looting.ArmoryOpen(s);
            return new List<GoalStepView>
            {
                new GoalStepView { Text = where == KeyPlaces.Floor && !open ? Lore.KeyText.KeyOnFloor : Lore.KeyText.FindKey, Done = open || where == KeyPlaces.Pack || where == KeyPlaces.Used },
                new GoalStepView { Text = Lore.KeyText.OpenArmory, Done = open },
                new GoalStepView { Text = Lore.KeyText.Leave },
            };
        }

        /// <summary>
        /// Mordido's objective in steps (U23, G23.2; Lore's words, the Rescate's for finding, talking and La Lata): the one who
        /// asks for help (found and talked to), the antiviral (in a pack or used; on the floor, its line says so), giving it
        /// to them with the count under it (from their finding, red at 2 and 1), and La Lata (the Rescate's line for it).
        /// Till they are found only the first two: the others name them, and nobody knows who they are yet. Turned (or
        /// fallen), what was done stays and the rest is Lore's one line, in red. Null in another fight.
        /// </summary>
        List<GoalStepView> BiteSteps(GameState s, string rescueLine)
        {
            var m = s.Mission;
            if (m.Bitten != true || UnitById(s, m.ObjectiveUnit) is not Unit vip) return null;
            var found = !Unfound(vip);
            var joined = found && !vip.Captive;
            var where = Looting.AntiviralWhere(s);
            var cured = m.Cured == true;
            var steps = new List<GoalStepView>
            {
                new GoalStepView { Text = Lore.VipObjective(vip.Name, found ? "talk" : "search"), Done = joined },
                new GoalStepView { Text = where == AntiviralPlaces.Floor ? Lore.BiteText.AntiviralOnFloor : Lore.BiteText.FindAntiviral, Done = where == AntiviralPlaces.Pack || where == AntiviralPlaces.Used },
            };
            if (!vip.Alive && !vip.Evacuated)
            {
                steps.RemoveAll(x => !x.Done);
                steps.Add(new GoalStepView { Text = Lore.BiteText.TurnedObjective, Lost = true });
                return steps;
            }
            if (!found) return steps;
            var cure = new GoalStepView { Text = Lore.BiteText.GiveAntiviral(vip.Name), Done = cured };
            if (BiteLeft(s, vip) is int left)
            {
                cure.Note = Lore.BiteText.Countdown(left);
                cure.NoteUrgent = left <= 2;
            }
            steps.Add(cure);
            steps.Add(new GoalStepView { Text = joined ? rescueLine : Lore.VipObjective(vip.Name, "loose"), Done = m.ObjectiveDone });
            return steps;
        }

        /// <summary>
        /// El herido's objective in steps (U25, G23.3; Lore's words, which name them, never «el herido»): to find them in the
        /// marked zone (the Rescate's), to carry them (again if set down or fallen with their carrier), to bring them to La
        /// Lata, to leave. Till found only the first: the others name them. Dead, what was done stays and the rest is Lore's
        /// one line, in red. Null in another fight.
        /// </summary>
        List<GoalStepView> WoundedSteps(GameState s)
        {
            if (Rules.WoundedOf(s) is not Unit w) return null;
            var found = !Unfound(w);
            var aboard = w.Evacuated || w.Aboard == true;
            var carried = w.CarriedBy != null;
            var steps = new List<GoalStepView> { new GoalStepView { Text = Lore.WoundedText.Search, Done = found } };
            if (!w.Alive && !carried && !aboard)
            {
                steps.RemoveAll(x => !x.Done);
                steps.Add(new GoalStepView { Text = Lore.WoundedText.DeadObjective, Lost = true });
                return steps;
            }
            if (!found) return steps;
            steps.Add(new GoalStepView { Text = Lore.WoundedText.Carry(w.Name), Done = carried || aboard });
            steps.Add(new GoalStepView { Text = Lore.WoundedText.Bring(w.Name), Done = aboard });
            steps.Add(new GoalStepView { Text = Lore.WoundedText.Leave, Done = s.Mission.ObjectiveDone && w.Evacuated });
            return steps;
        }

        // ------------------------------------------------------- Separados (U27, G23.4)

        /// <summary>
        /// Separados' objective in steps (U27; Lore's words, SplitText): to La Lata with one of each group, done as
        /// Rules.SplitShort is null, the group still missing named under it once only that one is (none while both are); out,
        /// done as La Lata has gone. A group all fallen (Rules.SplitLost): what was done stays, and Lore's line in red. Gone
        /// (EvacDone), those left behind are no fallen group: the first done only with one of each in it. Null in another fight.
        /// </summary>
        static List<GoalStepView> SplitSteps(GameState s)
        {
            var split = s.Mission.Split;
            if (split == null) return null;
            var gone = s.Mission.EvacDone == true;
            var lost = !gone && Rules.SplitLost(s);
            var reach = new GoalStepView { Text = Lore.SplitText.Reach, Done = gone ? Rules.SplitSafe(s) : !lost && Rules.SplitShort(s) == null };
            if (!reach.Done && !lost && !gone)
            {
                var noSeat = Rules.NoSeat(s);
                var missing = Enumerable.Range(0, split.Count).Where(g => !SplitGoes(s, g, noSeat)).ToList();
                if (missing.Count == 1) reach.Note = Lore.SplitText.Missing(SplitField(s, missing[0]).Select(u => u.Name).ToList());
            }
            var steps = new List<GoalStepView> { reach };
            if (lost)
            {
                steps.RemoveAll(x => !x.Done);
                steps.Add(new GoalStepView { Text = Lore.SplitText.LostObjective, Lost = true });
                return steps;
            }
            steps.Add(new GoalStepView { Text = Lore.SplitText.Leave, Done = gone });
            return steps;
        }

        /// <summary>Separados' group `g`, all of it: fallen and in La Lata too (the names of one fallen, or none of whom got out).</summary>
        public static List<Unit> SplitAll(GameState v, int g) => v.Mission.Split != null && g >= 0 && g < v.Mission.Split.Count
            ? v.Mission.Split[g].Select(id => UnitById(v, id)).Where(u => u != null).ToList()
            : new List<Unit>();

        /// <summary>Separados' group `g` out on the field: its living not in La Lata (T24: inside is Alive false, Evacuated).</summary>
        static List<Unit> SplitField(GameState v, int g) => SplitAll(v, g).Where(u => u.Alive && !u.Evacuated).ToList();

        /// <summary>
        /// Whether someone of Separados' group `g` would go if La Lata went now, as Rules.SplitShort counts them: inside it,
        /// or in its zone with a seat (`noSeat`: Rules.NoSeat).
        /// </summary>
        static bool SplitGoes(GameState s, int g, List<Unit> noSeat) => SplitAll(s, g).Any(u => u.Evacuated || (u.Alive && Abilities.InEvacZone(s, u.Pos) && !noSeat.Contains(u)));

        /// <summary>
        /// This player's group in Separados (U27): the first with one of theirs (two players: by owner, as the engine splits
        /// them); playing alone, or with none of theirs, the first. −1 outside the stage.
        /// </summary>
        int MyGroup(GameState v)
        {
            var split = v.Mission.Split;
            if (split == null) return -1;
            for (var g = 0; g < split.Count; g++)
                if (split[g].Any(id => UnitById(v, id)?.Owner == MySlot)) return g;
            return 0;
        }

        /// <summary>
        /// Separados' groups other than the one being looked after (U27), for their arrows: those without the picked soldier;
        /// with none of them picked, those other than this player's.
        /// </summary>
        IEnumerable<int> OtherGroups(GameState v)
        {
            var split = v.Mission.Split;
            if (split == null) yield break;
            var picked = Unit();
            var at = picked != null ? split.FindIndex(g => g.Contains(picked.Id)) : -1;
            var mine = at >= 0 ? at : MyGroup(v);
            for (var g = 0; g < split.Count; g++)
                if (g != mine) yield return g;
        }

        /// <summary>Mordido's turns left for `u` (U23): the bitten survivor found, alive and not cured; else null.</summary>
        static int? BiteLeft(GameState s, Unit u) => s.Mission.Bitten == true && s.Mission.Cured != true && s.Mission.BiteTurns is int left && u != null && u.Id == s.Mission.ObjectiveUnit && u.Alive && !u.Evacuated ? left : (int?)null;

        /// <summary>La llave's key lying on the floor (U22: marked as a mission's loose item, its beam and core), or null.</summary>
        static Pos KeyOnFloor(GameState s) => s.Mission.KeyArmory == true && !Looting.ArmoryOpen(s)
            ? (s.FloorGear ?? new List<DroppedGear>()).FirstOrDefault(d => d.Gear.Kind == GearKinds.Key)?.Pos : null;

        /// <summary>Mordido's antiviral lying on the floor (U23: marked as the key on the floor), or null; once cured, none.</summary>
        static Pos AntiviralOnFloor(GameState s) => s.Mission.Bitten == true && s.Mission.Cured != true
            ? (s.FloorGear ?? new List<DroppedGear>()).FirstOrDefault(d => d.Gear.Kind == GearKinds.Antiviral)?.Pos : null;

        /// <summary>The mission's own thing on the floor (U22, U23): La llave's key or Mordido's antiviral.</summary>
        static Pos MissionGearOnFloor(GameState s) => KeyOnFloor(s) ?? AntiviralOnFloor(s);

        /// <summary>
        /// The chapter's boss (H6; MissionState.Boss) for the bar at the top: once seen, or once it has roared (all the map
        /// heard it), till it dies; its charge ready or in how many turns (Cooldowns).
        /// </summary>
        BossView BossOf(GameState s)
        {
            var id = s.Mission?.Boss;
            var u = id != null ? UnitById(s, id) : null;
            if (u == null || !u.Alive) return null;
            var roared = u.Charges != null && u.Charges.TryGetValue(AbilityIds.Roar, out var left) && left <= 0;
            if (!Visible(u) && !roared) return null;
            var soon = ChargeSoon(s, u);
            return new BossView
            {
                Id = u.Id, Name = u.Name, Hp = u.Hp, Max = u.MaxHp, Armor = u.Armor,
                Charge = soon ? HudText.Boss.ChargeReady : HudText.Boss.ChargeIn(ChargeWait(u)), ChargeReady = soon,
                Tip = HudText.Boss.Tip,
            };
        }

        /// <summary>The turns El Toro's charge still waits (Cooldowns; gone, none).</summary>
        static int ChargeWait(Unit boss) => boss.Cooldowns != null && boss.Cooldowns.TryGetValue(AbilityIds.Charge, out var n) ? n : 0;

        /// <summary>
        /// Whether El Toro can charge at his next turn, or now in his: the wait gone, or going as his turn starts
        /// (TickCooldowns; MissionBot's «charging»). In the squad's turn, a wait of 1 is ready.
        /// </summary>
        static bool ChargeSoon(GameState s, Unit boss) => ChargeWait(boss) <= (s.ActiveTeam == Teams.Alien ? 0 : 1);

        /// <summary>
        /// The pointer on El Toro (Gameplay, 16/11): his charge to each soldier it would reach from where he stands
        /// (Toro.ChargeTargets), faint while it isn't his next turn's.
        /// </summary>
        void ShowBossCharges(Unit boss)
        {
            var v = Shown;
            if (AimMarks == null || !boss.Alive || !Visible(boss) || v.Outcome != null) return;
            var lines = Toro.ChargeTargets(v, boss).Select(c => (boss.Pos, (IList<Pos>)c.Path, c.Target.Pos)).ToList();
            AimMarks.ShowCharges(v, lines, ChargeSoon(v, boss));
        }

        /// <summary>
        /// `u` moving to `at` (Gameplay, 16/11): whether it would stand in El Toro's line with his charge ready at his next
        /// turn; if so, the line drawn to it. `u` is put there a moment for Toro.ChargePath (its old tile mustn't block
        /// the line it steps back along) and back.
        /// </summary>
        bool ChargeLineTo(Unit u, Pos at)
        {
            var s = State;
            var boss = UnitById(s, s.Mission?.Boss);
            if (AimMarks == null || boss == null || !boss.Alive || !Visible(boss) || !ChargeSoon(s, boss)) return false;
            var was = u.Pos;
            List<Pos> path;
            try
            {
                u.Pos = new Pos { X = at.X, Y = at.Y, Z = at.Z };
                path = Toro.ChargePath(s, boss, u);
            }
            finally
            {
                u.Pos = was;
            }
            if (path == null) return false;
            AimMarks.ShowCharges(s, new List<(Pos, IList<Pos>, Pos)> { (boss.Pos, path, at) }, true);
            return true;
        }

        /// <summary>A mission's name (setup.ts MISSION_NAMES, as the room lists them, until setup is in C#).</summary>
        static string MissionName(string kind) => RoomText.Missions.FirstOrDefault(x => x.Id == kind).Name ?? kind;

        /// <summary>
        /// A siege's strip (docs/FASE12.md §10, Lore's words): «Asedio», kill them all (Ana, 23/10 #36) and when the next wave
        /// comes; or that none are coming any more.
        /// </summary>
        MissionView SiegeView()
        {
            var s = Shown;
            var waves = s.Mission.Reinforcements.GroupBy(r => r.Turn).Select(g => (Turn: g.Key, Landed: g.All(r => r.Landed))).OrderBy(w => w.Turn).ToList();
            var next = waves.FirstOrDefault(w => !w.Landed);
            var view = new MissionView { Label = Lore.SiegeText.Name, Area = s.MapName, Objective = Lore.SiegeObjective(), ObjectiveTip = Lore.SiegeObjectiveTip() };
            if (next.Turn > 0 || waves.Any(w => !w.Landed))
            {
                view.WaveText = $"OLEADA · {Lore.WaveDueShort(next.Turn, s.Turn).ToUpperInvariant()}";
                view.WaveTip = Lore.WaveDueLine(next.Turn, s.Turn);
                view.WaveUrgent = next.Turn <= s.Turn;
            }
            else
            {
                view.WaveText = Lore.SiegeText.NoMoreWaves.ToUpperInvariant();
                view.WaveTip = Lore.SiegeText.NoMoreWaves;
            }
            return view;
        }

        // ------------------------------------------------------- the aim's panel

        /// <summary>
        /// The aim's panel (game.ts targetPanel): by hand, what it acts on; a self ability armed; aiming, the odds and the
        /// damage of an attack, or what a support ability does. Null with nothing armed.
        /// </summary>
        ShotView TargetPanel()
        {
            var u = Unit();
            if (u != null && HandArmed() != null) return HandPanel(u);
            if (u != null && mode == Mode.Self)
            {
                var self = Content.Abilities[modeAbility];
                return new ShotView { Kind = "self", Title = self.Name, Action = self.Name, Locked = LockLabel(), Cost = AbilityTexts.CostText(u, modeAbility), Limits = AbilityTexts.LimitsText(u, modeAbility), Effect = self.Description, Detail = SelfDetail(u, modeAbility), Noise = AbilityTexts.NoiseText(State, u, modeAbility) };
            }
            var option = CurrentTarget();
            if (u == null || option == null || mode != Mode.Target) return null;
            var ability = modeAbility;
            var def = Content.Abilities[ability];
            var list = TargetOptions(ability);
            var index = list.FindIndex(o => o.Unit.Id == option.Unit.Id);
            var target = option.Unit;
            if (def.Target == AbilityTargets.Ally || !Content.IsAttack(ability))
            {
                var friendly = def.Target == AbilityTargets.Ally;
                // (Golpe silencioso: no roll and no figures, it kills; its panel says what it does.)
                var dmg = friendly || ability == AbilityIds.Takedown ? null : Rules.AbilityDamage(State, u, ability, target);
                var noise = Abilities.NoiseOf(State, u, ability);
                var support = new ShotView
                {
                    Kind = "support",
                    Title = def.Name,
                    Action = def.Name,
                    Locked = LockLabel(),
                    TargetName = target.Name,
                    Effect = AbilityTexts.SupportEffects.TryGetValue(ability, out var effect) ? effect : def.Description,
                    Index = index,
                    Total = list.Count,
                    Noise = noise > 0 ? AbilityTexts.NoiseEstimate(State, u, noise, true) : null,
                };
                if (dmg != null)
                {
                    support.HasDamage = true;
                    support.Damage = new DamageView { Hit = AbilityTexts.Span(dmg.Hit), Notes = AbilityTexts.ArmorNotes(dmg, target), Kill = AbilityTexts.Lethality(dmg, target) };
                }
                return support;
            }
            var shot = Aim.PreviewShot(State, u, target, new ShotOptions { Ability = ability, From = option.Tile });
            if (shot == null) return null;
            var d = Rules.AbilityDamage(State, u, ability, target, shot);
            var loud = Abilities.NoiseOf(State, u, ability);
            var view = new ShotView
            {
                Kind = "attack",
                Title = $"{def.Name} · {AbilityTexts.WeaponLabel(u, ability)}",
                Action = def.Name,
                TargetName = target.Name,
                Hit = shot.Hit,
                Crit = Math.Min(shot.Crit, shot.Hit),
                Graze = shot.Graze,
                Mods = shot.Mods.Select(m => OddsMod(m, shot, target, false)).ToList(),
                CritMods = shot.CritMods.Select(m => OddsMod(m, shot, target, true)).ToList(),
                // Why no crit (cuadro 158): behind cover, nothing gives it; else none of the soldier's does.
                CritNote = shot.Cover > 0 ? HudText.Shot.NoCritCovered : HudText.Shot.NoCrit,
                Index = index,
                Total = list.Count,
                Note = AbilityTexts.AttackNotes.TryGetValue(ability, out var note) ? note : AbilityTexts.BladeNote(u, ability),
                Locked = LockLabel(),
                Noise = loud > 0 ? AbilityTexts.NoiseEstimate(State, u, loud, true) : "Sin ruido",
            };
            // Arco y ballesta (U29, cuadro 102): the squad hidden, whether a kill with arrows keeps it so (Gameplay's
            // Rules.SilentKill: nobody else of the enemy notices the shooter where it stands), or who would see it.
            if (State.Concealed && target.Team != Teams.Xcom && Content.FiresArrows(u, ability))
            {
                var seer = Rules.SilentKill(State, u, target) ? null : Los.Watchers(State, u, u.Pos).FirstOrDefault(w => w.Id != target.Id && Visible(w));
                var silent = seer == null && Rules.SilentKill(State, u, target) ? HudText.Silent.Kept : HudText.Silent.Seen(seer != null ? OneOf(seer) : null);
                view.Note = string.IsNullOrEmpty(view.Note) ? silent : $"{view.Note} {silent}";
            }
            if (d != null)
            {
                view.HasDamage = true;
                view.Damage = new DamageView
                {
                    Hit = AbilityTexts.Span(d.Hit),
                    Crit = d.Crit != null && shot.Crit > 0 ? AbilityTexts.Span(d.Crit) : null,
                    Graze = d.Graze != null && shot.Graze > 0 ? AbilityTexts.Span(d.Graze) : null,
                    Notes = AbilityTexts.ArmorNotes(d, target),
                    Kill = AbilityTexts.Lethality(d, target, shot.Crit > 0),
                };
            }
            return view;
        }

        /// <summary>The armed hand's panel (game.ts handPanel): «Registrar el maletero» with the room left in the backpack, «Abrir la puerta».</summary>
        ShotView HandPanel(Unit u)
        {
            var id = HandArmed();
            var tile = HandTarget();
            if (id == null || tile == null) return null;
            var def = Content.Abilities[id];
            var others = HandTiles().Count - 1;
            var view = new ShotView { Kind = "self", Action = def.Name, Locked = LockLabel(), Cost = AbilityTexts.CostText(u, id), Noise = AbilityTexts.NoiseText(State, u, id), Effect = def.Description, Title = def.Name };
            // El herido's Dejar (U25): on La Lata's tile its name says they go in (Lore's, with their name; Producto, 19/11:
            // seen before confirming), as the tile's green frame does.
            if (id == AbilityIds.SetDown)
            {
                var hurt = Rules.WoundedOf(State);
                if (hurt != null && AboardIfSetDown(tile)) view.Action = view.Title = Lore.WoundedText.SetDownAboardAction(hurt.Name);
                view.Hint = Bastion.Client.GameInput.Touch ? HudText.Touch.Hand("la casilla", view.Action.ToLowerInvariant())
                    : $"Otra vez, Espacio o clic en la casilla{(others > 0 ? " · Tab: otra casilla" : "")} · Esc: cancelar";
                return view;
            }
            // The crowbar on a boarded window or door (Arturo, 02/11 A11).
            if (id == AbilityIds.Pry)
            {
                var what = Abilities.Boards(State, tile) == DecorKinds.DoorBoards ? "la puerta" : "la ventana";
                view.Hint = Bastion.Client.GameInput.Touch ? HudText.Touch.Hand(what, "quitar las tablas")
                    : $"Otra vez, Espacio o clic en {what}: quitar las tablas{(others > 0 ? $" · Tab: {(others == 1 ? "la otra" : "otra")} tapiada" : "")} · Esc: cancelar";
                return view;
            }
            if (id != AbilityIds.Search)
            {
                var what = id == AbilityIds.OpenDoor ? "abrir" : "cerrar";
                view.Hint = Bastion.Client.GameInput.Touch ? HudText.Touch.Hand("la puerta", what)
                    : $"Otra vez, Espacio o clic en la puerta: {what}{(others > 0 ? $" · Tab: {(others == 1 ? "la otra" : "otra")} puerta" : "")} · Esc: cancelar";
                return view;
            }
            var decor = Looting.SearchSpot(State, tile);
            if (decor == null) return null;
            var slots = u.PackSlots ?? Content.BackpackSlots;
            var room = slots - (u.Backpack?.Count ?? 0);
            view.Title = Lore.BoxTitle(decor);
            view.Detail = $"Mochila: {(room == 1 ? "queda 1 hueco" : $"quedan {room} huecos")} de {slots}";
            // A body or a car's boot (Arturo, 02/11 A9, A13): its own button and words, not a box's.
            if (!Looting.LootBoxes.Contains(decor))
            {
                view.Action = view.Title;
                view.Effect = AbilityTexts.SearchWords(decor).effect;
            }
            // Tab to the next names it (the body, the boot, the box of weapons, the rack…); what it does, a piece's own verb (U15).
            var next = others == 1 ? HandTiles().FirstOrDefault(t => !Same(t, tile)) : null;
            var tab = next != null && Looting.SearchSpot(State, next) is string after ? SpotName(after) : "el siguiente";
            view.Hint = Bastion.Client.GameInput.Touch ? HudText.Touch.Hand(SpotName(decor), SearchVerb(decor))
                : $"Otra vez, Espacio o clic en {SpotName(decor)}: {SearchVerb(decor)}{(others > 0 ? $" · Tab: {tab}" : "")} · Esc: cancelar";
            return view;
        }

        /// <summary>What an armed self ability would do right now: whom it reaches, the odds, the clip.</summary>
        string SelfDetail(Unit u, string id)
        {
            switch (id)
            {
                case AbilityIds.Faceoff:
                {
                    var n = VisibleShots(u, AbilityIds.Faceoff).Count;
                    return n > 0 ? $"{n} {(n == 1 ? "enemigo" : "enemigos")} a su alcance" : "Ningún enemigo a su alcance";
                }
                case AbilityIds.Hack:
                    return $"Probabilidad: {Abilities.HackChance(State, u)} %";
                case AbilityIds.Reload:
                    return $"Cargador: {u.Ammo}/{Content.AttackWeapon(u, AbilityIds.Shoot).Clip}{(u.Mags == null ? "" : u.Mags > 0 ? $" · {u.Mags} de repuesto" : " · sin repuesto")}";
            }
            return null;
        }

        // ------------------------------------------------------- the arrival

        /// <summary>The log's colour for the living and the dead fighting each other (game.ts FEUD_COLOR).</summary>
        const string FeudColor = "#e8d8c0";

        /// <summary>
        /// A fresh mission's start, as the loading sheet goes: the log keeps a night's rules (Arturo, 22/10 #57), and says
        /// when the living are among the dead (18/10 #47). No arrival card since the cuadro 139 (Arturo: «Entendido» out, the
        /// sheet says the stage already); the objective's card opens it, to read again (IntroNow).
        /// </summary>
        public void Intro()
        {
            var s = State;
            if (Hud == null || s.Outcome != null) return;
            if (s.Units.Any(u => u.Alive && u.Team == Teams.Alien && !u.Objective && !Content.Templates[u.Template].Undead))
            {
                Hud.Log(Lore.LivingTip, FeudColor);
            }
            if (s.Mission.Night == true) Hud.Log(string.Join(" ", Lore.NightText.Rules), "#2a4a7a");
            // Saqueadores with no camp (U28): said once, as it starts.
            if (NoCamp(s)) Hud.Log(Lore.RaidersText.NoCamp, "#b4bec6");
        }

        /// <summary>
        /// The mission's window as it stands now: its name, the objective and the turns left, the briefing; null once it is
        /// over. Also what the objective's card opens again (HudRoot.IntroNow).
        /// </summary>
        IntroView IntroNow()
        {
            var s = State;
            if (s == null || s.Outcome != null) return null;
            var mission = MissionView();
            var cm = s.CampaignMission;
            return new IntroView
            {
                Kicker = Kicker() + (s.Mission.Night == true ? $" · {Lore.NightText.Label.ToUpperInvariant()}" : "") + (HudText.Weather.Name(s.Weather) is string sky ? $" · {sky.ToUpperInvariant()}" : ""),
                Title = cm?.Name ?? (s.Mission.Siege != null ? Lore.SiegeText.Name : MissionName(s.Mission.Kind)),
                Objective = mission.Objective,
                Turns = mission.TurnsLeft >= 0 ? $"{mission.TurnsLeft} turnos" : null,
                Briefing = cm?.Briefing ?? (s.Mission.Siege != null ? Lore.SiegeText.Briefing : null),
            };
        }

        /// <summary>The loading veil's words (game.ts raiseVeil: kicker, missionName): the kicker and the operation's name.</summary>
        public (string kicker, string title) VeilWords()
        {
            var s = State;
            var cm = s.CampaignMission;
            return (Kicker(), cm?.Name ?? (s.Mission.Siege != null ? Lore.SiegeText.Name : MissionName(s.Mission.Kind)));
        }

        /// <summary>
        /// The briefing's kicker: the operation's, the story chapter's, or what kind of fight it is; a stage of an
        /// expedition, «EXPEDICIÓN · ETAPA 3» (the row of the map: it said the old campaign's «MISIÓN EN CURSO»).
        /// </summary>
        string Kicker()
        {
            if (State.Run is RunFight run) return $"{Lore.ExpeditionText.Name} · {Lore.ExpeditionText.Stage} {run.Row}".ToUpperInvariant();
            var cm = State.CampaignMission;
            var chapter = cm?.Story;
            if (chapter == 1) return "";
            return cm?.Kicker ?? (chapter != null ? $"DIARIO · HOJA {chapter}" : cm != null ? "MISIÓN EN CURSO" : "ESCARAMUZA");
        }

        /// <summary>
        /// The opening shot (game.ts openingShot, UX-REVISION-2 #32): the squad framed with the map, or the enemies already
        /// in sight, in front of it, at most six tiles towards them; the player's first move of the camera ends it.
        /// </summary>
        public void OpeningShot()
        {
            if (Rig == null) return;
            var v = Shown;
            var squad = v.Units.Where(u => u.Alive && u.Team == Teams.Xcom && !u.Captive && !u.Objective).ToList();
            // Separados (U27): each player's own group, the first playing alone; the whole squad's middle is La Lata, between
            // the two. The other one, by its arrow at the edge.
            if (v.Mission.Split != null && SplitField(v, MyGroup(v)) is List<Unit> group && group.Count > 0) squad = group;
            if (squad.Count == 0) return;
            var here = new UnityEngine.Vector2((float)squad.Average(u => u.Pos.X), (float)squad.Average(u => u.Pos.Y));
            var foes = v.Units.Where(a => a.Alive && a.Team == Teams.Alien && Visible(a)).ToList();
            var toward = foes.Count > 0 ? new UnityEngine.Vector2((float)foes.Average(a => a.Pos.X), (float)foes.Average(a => a.Pos.Y)) : new UnityEngine.Vector2(v.Width / 2f, v.Height / 2f);
            var gap = UnityEngine.Vector2.Distance(here, toward);
            // Inside a building only (the hospital), the squad in the middle and its door behind: two tiles towards it.
            var k = gap > 0 ? Math.Min(v.Mission?.Inside == true ? 2 : 6, gap / 2) / gap : 0;
            var at = here + (toward - here) * k;
            // Only the building (the hospital, H7: Mission.Inside): looked at from the strip the squad starts on, its door
            // ahead, not behind the squad; the quarter of the four that faces it most.
            if (v.Mission?.Inside == true)
            {
                var from = Board.TileToWorld(v, new Pos { X = (int)Math.Round(here.x), Y = (int)Math.Round(here.y) });
                var to = Board.TileToWorld(v, new Pos { X = v.Width / 2, Y = v.Height / 2 });
                var best = 45f;
                var most = float.NegativeInfinity;
                foreach (var yaw in new[] { 45f, 135f, 225f, 315f })
                {
                    var r = yaw * Mathf.Deg2Rad;
                    var ahead = -Mathf.Sin(r) * (to.x - from.x) + Mathf.Cos(r) * (to.z - from.z);
                    if (ahead > most)
                    {
                        most = ahead;
                        best = yaw;
                    }
                }
                Rig.FaceYaw(best, true);
            }
            Rig.Focus(Board.TileToWorld(v, new Pos { X = (int)Math.Round(at.x), Y = (int)Math.Round(at.y) }), true);
        }

        /// <summary>
        /// The host's events, just applied to the state (CombatSession): what the controller keeps from them. The partner's
        /// last soldier moved, for «Ver» (game.ts partnerLast).
        /// </summary>
        public void Played(IList<GameEvent> events, int? by)
        {
            // This player's answer (even with no events): what waited for it comes back once they are seen (Red, 29/10:
            // the partner's, between the confirm and these, release nothing).
            if (by == MySlot) sent = null;
            foreach (var e in events)
                if (e is MovedEvent moved && UnitById(State, moved.Unit) is Unit u && u.Owner != null && u.Owner != MySlot) PartnerLast = u.Id;
        }

        // ------------------------------------------------------- calls

        /// <summary>
        /// The partner's call waiting for this player's answer (Ana, 22/10 #39): to leave in the bus or to abandon, in the
        /// middle of the screen with its clock; none for one's own call (game.ts callView).
        /// </summary>
        CallView CallView()
        {
            var s = State;
            if (s.Outcome != null) return null;
            // The question about the one in play (LeaveCall), unless the partner's call is waiting for an answer.
            var leave = LeaveCall();
            var partnerCall = (s.EvacCall != null && s.EvacCall.By != MySlot) || (s.AbandonCall != null && s.AbandonCall.By != MySlot);
            if (leave != null && !partnerCall) return leave;
            var evac = s.EvacCall;
            if (evac != null && evac.By != MySlot)
            {
                var ask = Lore.EvacAsk(PlayerName(evac.By), EvacSituation(s));
                return new CallView
                {
                    Kind = "evac",
                    Kicker = s.Mission.Kind == MissionKinds.Elimination ? "Retirada" : "Evacuación",
                    Title = ask.Title,
                    Text = ask.Text,
                    ClockNote = $"Sin respuesta, {Lore.Bus.Name} sale",
                    Accept = Lore.EvacBar.Accept,
                    Refuse = Lore.EvacBar.Wait,
                };
            }
            var abandon = s.AbandonCall;
            if (abandon != null && abandon.By != MySlot)
                return new CallView
                {
                    Kind = "abandon",
                    Kicker = "",
                    Title = Lore.AbandonAsk(PlayerName(abandon.By)),
                    Text = AbandonStakes,
                    ClockNote = "Sin respuesta, se acepta sola",
                    Accept = Lore.AbandonText.Accept,
                    Refuse = Lore.AbandonText.Refuse,
                };
            return null;
        }
    }
}
