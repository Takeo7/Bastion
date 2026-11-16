using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// A player that plays a mission by itself, with the engine's own functions, for the tests between two apps
    /// (BASTION_CAMPANA, the app's CampaignRun): never in a game of the players. Each seat plays its own soldiers while it
    /// holds the command, one at a time: it reloads, shoots or strikes what it is likely to hit or what is close, and
    /// otherwise goes for the objective (a Botín's item, the radio to fix, the one to free, the alarm to silence; with the
    /// job done, La Lata, everyone) or the nearest enemy, in stretches (Arturo, 5/11 #14: moving apart from the actions),
    /// never at a run (all the movement at once, heard: Arturo, 6/11); with its actions spent, while the partner plays, it
    /// cedes the command and keeps its movement for later; in La Lata with the job done and everyone aboard who can get
    /// there, it calls the bus; the partner's call it answers yes.
    /// Without a gun (the campaign starts with two pistols for four: Arturo, 2/11) it goes for one first, in a police car's
    /// boot or on the floor, and puts it on; and with the blade it walks up to the dead and strikes. Walled off from where it
    /// goes (a boarded door: Arturo, 2/11), it takes the boards off with the crowbar or goes round. With nothing to do, it
    /// keeps watch or takes cover, and with nothing left for any soldier it ends its turn, movement left or not (the turn
    /// doesn't end by itself while some is left: Arturo, 5/11 #14). Deterministic: no dice of its own.
    /// In a run's fight (H3; Gameplay, 16/11) it uses the pack: a medicine for one at half health or less, what lies at its
    /// feet picked up, a gun in the pack put on, and the reload spends a magazine; with the gun empty and none left, the
    /// blade. A run's Saqueo (the normal node) has no objective: the squad searches the rooms nearest by the walk, never
    /// hunting the dead, and goes for La Lata once RunLeaveTurn comes, once one of it is at half health or less, or once no
    /// room is left (RunLeaving). Hidden, it walks the squad's extra too (Paths with the state).
    /// </summary>
    public static class MissionBot
    {
        /// <summary>Shots at least this likely are taken whatever else there is to do.</summary>
        public const int GoodShot = 50;

        /// <summary>An enemy this close (tiles) is shot at worse odds.</summary>
        public const double Close = 7;

        /// <summary>A run's Saqueo: the turn the squad goes for La Lata, whatever is left to search (RunLeaving).</summary>
        public const int RunLeaveTurn = 6;

        /// <summary>A run's Saqueo: rooms farther than this many turns' walk aren't gone for (RoomGoal); with none nearer, the squad goes.</summary>
        public const int RoomTurns = 2;

        /// <summary>A run's Saqueo: the rooms the squad searches before it goes (Gameplay, 16/11: «las 3–4 más cercanas»).</summary>
        public const int RoomsWanted = 4;

        /// <summary>A run's Saqueo: the dead the squad sees this near (tiles) to one of it, and this many, and it goes (RunLeaving).</summary>
        public const double ThreatNear = 5;
        public const int ThreatMany = 3;

        /// <summary>The next order of `slot`, or null: nothing for it now (the partner holds the command, or it is done).</summary>
        public static Command? Next(GameState s, int slot, IReadOnlyList<int> connected)
        {
            if (s.Outcome != null || s.ActiveTeam != Teams.Xcom) return null;
            // The partner calls La Lata: yes. One's own call waits for the partner.
            if (s.EvacCall != null) return s.EvacCall.By != slot ? new EvacAnswerCommand { Accept = true } : null;
            var holds = s.Command == slot || s.Command == null || !connected.Contains(s.Command.Value);
            if (!holds || s.Ready[slot]) return null;
            // Movement left counts too: the turn doesn't end by itself while there is some (Arturo, 5/11 #14).
            var able = Grid.Combatants(s, Teams.Xcom).Where((u) => !u.Evacuated && Paths.HasTurnLeft(u) && Content.CommandsFor(slot, u.Owner, connected)).ToList();
            if (able.Count == 0) return new EndTurnCommand { Ready = true };
            // Each soldier's walks, past the dead too (they don't block it here): the way to its goal, the nearest of them by
            // the walk, and in a Saqueo whether any is left to walk to.
            var walks = new Dictionary<string, Reach>();
            Reach WalksOf(Unit x) => walks.TryGetValue(x.Id, out var r) ? r : walks[x.Id] = Paths.ComputeReach(s, x, double.MaxValue, null, (o) => o.Team != x.Team);
            // A run's Saqueo: each soldier's nearest room left to search, the rooms found on the way kept for the next one.
            var known = new Dictionary<int, LootRoom?>();
            var rooms = new Dictionary<string, Pos?>();
            Pos? RoomOf(Unit x) => rooms.TryGetValue(x.Id, out var r) ? r : rooms[x.Id] = RoomGoal(s, x, WalksOf(x), known);
            var leaving = Leaving(s, WalksOf, RoomOf);
            var goals = able.ToDictionary((x) => x.Id, (x) => Goal(s, x, leaving, WalksOf, RoomOf));
            // The soldier in play first: it is to be finished before another (Rules InPlay). Then the others, the one
            // nearest its goal first; one with nothing to do lets the next one play, and only with none left is the turn over,
            // movement left or not.
            var order = able.OrderBy((x) => x.Id == s.ActiveUnit ? 0 : 1)
                .ThenBy((x) => goals[x.Id] is Pos g ? Grid.Distance(x.Pos, g) : 0)
                .ThenBy((x) => x.Id, System.StringComparer.Ordinal);
            foreach (var u in order)
            {
                // In play with its actions spent (Arturo, 7/11: in play till it has nothing, or is left), while the partner
                // plays: the partner's go, its movement kept for later in the turn, as the command went on by itself before.
                // Left (DoneCommand), that movement was lost: of seeds 1 to 120 of the test apps' campaign, the first
                // mission was won as often either way (23), and both 18 times so against 20, but 364 lost its siege.
                if (u.Id == s.ActiveUnit && u.Ap <= 0 && PartnerPlays(s, slot, connected)) return new PassCommand();
                var cmd = Choose(s, u, leaving, goals[u.Id], WalksOf(u), slot, connected);
                if (cmd != null) return cmd;
                // In play with nothing to do, its actions kept, it holds the others back while the partner plays (Rules
                // InPlay): the partner's go.
                if (u.Id == s.ActiveUnit && PartnerPlays(s, slot, connected)) return new PassCommand();
            }
            return new EndTurnCommand { Ready = true };
        }

        /// <summary>Whether the partner takes turns with `slot` (Sim.Alternates): here, not done, and with someone to move.</summary>
        private static bool PartnerPlays(GameState s, int slot, IReadOnlyList<int> connected)
        {
            var partner = slot == 0 ? 1 : 0;
            return connected.Contains(partner) && !s.Ready[partner]
                && Grid.Combatants(s, Teams.Xcom).Any((u) => !u.Evacuated && Paths.HasTurnLeft(u) && (u.Owner == partner || u.Owner == null));
        }

        /// <summary>
        /// The job is done and the squad is to go: the item carried, the VIP freed (it comes along), the radio fixed, the
        /// alarm silenced; in a Saqueo, none of the dead left that a soldier can walk to (it searches nothing, and with no
        /// clock it would stay for ever: 5/11).
        /// </summary>
        private static bool Leaving(GameState s, System.Func<Unit, Reach> walksOf, System.Func<Unit, Pos?> roomOf)
        {
            var m = s.Mission;
            if (m.Kind == MissionKinds.Recovery) return !string.IsNullOrEmpty(m.Item?.Carrier);
            if (m.Kind == MissionKinds.Rescue && Vip(s) is Unit vip && !vip.Captive) return true;
            if (s.Run != null)
            {
                // A run's Asedio and Travesía: La Lata from the start, where the Asedio holds till it comes; a Guarida, once
                // its box is open; El Toro, once he is dead; either of these given up when the squad is broken (H5, H6).
                if (m.HoldUntil != null || s.Run.NodeKind == NodeKinds.Crossing) return true;
                if (m.Lair != null || !string.IsNullOrEmpty(m.Boss)) return m.ObjectiveDone || Routed(s);
            }
            if (RunSaqueo(s)) return RunLeaving(s, roomOf);
            if (m.Kind == MissionKinds.Scavenge && m.EvacZone.Count > 0)
            {
                var enemies = Enemies(s).Select((e) => Grid.NodeOf(s, e.Pos)).ToList();
                return !Grid.Combatants(s, Teams.Xcom).Any((o) => !o.Evacuated && enemies.Any((n) => walksOf(o).Cost.TryGetValue(n, out _)));
            }
            return m.Kind != MissionKinds.Elimination && m.ObjectiveDone;
        }

        /// <summary>The one to free in a rescue, alive; null in other missions.</summary>
        private static Unit? Vip(GameState s)
        {
            var m = s.Mission;
            return m.Kind == MissionKinds.Rescue && m.ObjectiveUnit != null && Grid.GetUnit(s, m.ObjectiveUnit) is Unit u && u.Alive && !u.Evacuated ? u : null;
        }

        /// <summary>The sabotage's alarm, still ringing; null in other missions.</summary>
        private static Unit? Alarm(GameState s)
        {
            var m = s.Mission;
            return m.Kind == MissionKinds.Sabotage && !m.ObjectiveDone && m.ObjectiveUnit != null && Grid.GetUnit(s, m.ObjectiveUnit) is Unit u && u.Alive ? u : null;
        }

        private static Command? Choose(GameState s, Unit u, bool leaving, Pos? target, Reach walks, int slot, IReadOnlyList<int> connected)
        {
            // In La Lata with the job done: the bus, once everyone is aboard who can get there. No clock hurries it: only the
            // sabotage's alarm has one now (Arturo, 5/11 #1), and it stops once the alarm is silenced (Events.CompleteObjective).
            if (leaving && Abilities.InEvacZone(s, u.Pos) && BusHere(s))
            {
                var all = AllAboard(s);
                // The one with the item aboard and the dead on it: now, with whoever is in.
                var cornered = s.Mission.Item?.Carrier is string c && Grid.GetUnit(s, c) is Unit holder && Abilities.InEvacZone(s, holder.Pos)
                    && Enemies(s).Any((e) => Grid.Distance(holder.Pos, e.Pos) <= 2);
                if (all || cornered) return new EvacCommand { Unit = u.Id };
            }
            if (leaving && Abilities.InEvacZone(s, u.Pos))
            {
                var room = MakeRoom(s, u);
                if (room != null) return room;
            }
            var armed = Armed(s, u);
            var carrier = s.Mission.Item?.Carrier == u.Id;
            if (s.Run != null)
            {
                var pack = PackOrder(s, u, armed, slot, connected);
                if (pack != null) return pack;
            }
            if (!armed)
            {
                var gun = PackedGun(u);
                if (gun != null && u.Ap > 0) return new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Index = gun };
                // One at its feet is picked up (free); a police car's boot beside it, searched.
                var dropped = s.Dropped?.FirstOrDefault((d) => IsGun(d.Loot.Weapon) && Grid.SamePos(d.Pos, u.Pos));
                if (dropped != null && Looting.Fits(u, dropped.Loot)) return new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Id = dropped.Id };
                var boot = Usable(s, u, AbilityIds.Search) ? Looting.SearchableTiles(s, u).FirstOrDefault((p) => Looting.SearchSpot(s, p) == Looting.PoliceBoot) : null;
                if (boot != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = boot };
            }
            if (u.Ammo <= 0 && Usable(s, u, AbilityIds.Reload)) return Ability(u, AbilityIds.Reload);
            // The job, where it is a thing to do: the radio fixed from beside it, the alarm hit at any fair odds.
            if (Usable(s, u, AbilityIds.Hack)) return Ability(u, AbilityIds.Hack);
            var alarm = Alarm(s);
            var silence = alarm != null ? BestShot(s, u, double.MaxValue, alarm.Id) : null;
            if (silence != null && silence.Value.hit >= 15) return silence.Value.cmd;
            // The silent takedown (Arturo, 16/11): one of the dead beside it that hasn't seen it, killed for sure and unheard.
            var unaware = Usable(s, u, AbilityIds.Takedown) ? Abilities.TakedownTargets(s, u).OrderBy((t) => t.Id, System.StringComparer.Ordinal).FirstOrDefault() : null;
            if (unaware != null) return Ability(u, AbilityIds.Takedown, unaware.Id);
            // A Guarida's box beside it: opened, the job done (H5).
            var lair = s.Run != null && s.Mission.Lair is Pos box && !s.Mission.ObjectiveDone && Usable(s, u, AbilityIds.Search)
                ? Looting.SearchableTiles(s, u).FirstOrDefault((p) => Grid.SameTile(p, box)) : null;
            if (lair != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = lair };
            // El Toro first, at any fair odds and farther than the rest: his group comes on, he doesn't stop (H6).
            var bull = Bull(s);
            var atBull = bull != null && !carrier ? BestShot(s, u, Close * 2, bull.Id) : null;
            if (atBull != null && atBull.Value.hit >= 25) return atBull.Value.cmd;
            // Only what is close is shot at: a shot is heard, and draws more of them (docs/FASE2.md). In a run, on the way
            // out, only what stands beside it: it stood fighting by La Lata till the horde had it (a Saqueo, 16/11).
            var going = s.Run != null && leaving && !Holding(s);
            var shot = BestShot(s, u, going ? 1.5 : Close);
            var nearest = Enemies(s).Select((e) => Grid.Distance(u.Pos, e.Pos)).DefaultIfEmpty(double.MaxValue).Min();
            if (shot != null && !carrier && shot.Value.hit >= 30) return shot.Value.cmd;
            // A run's Saqueo, before going: the room it stands in, then a box, a boot or a body beside it.
            if (RunSaqueo(s) && !leaving)
            {
                if (Usable(s, u, AbilityIds.SearchRoom)) return Ability(u, AbilityIds.SearchRoom);
                var spot = Usable(s, u, AbilityIds.Search) ? Looting.SearchableTiles(s, u).FirstOrDefault() : null;
                if (spot != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = spot };
            }
            // The blade alone: a step up to one of the dead, keeping an action to strike (not on a run's way out, nor out of
            // La Lata in an Asedio).
            if (!armed && !carrier && !going && !Holding(s))
            {
                var close = MoveToStrike(s, u);
                if (close != null) return close;
            }
            var goal = (!armed && !leaving ? GunGoal(s, u) : null) ?? target;
            if (goal != null && !(leaving && Abilities.InEvacZone(s, u.Pos)))
            {
                var pry = Pry(s, u, goal, leaving);
                if (pry != null) return pry;
                var stride = Stride(s, u);
                var move = stride >= Paths.CostOrtho ? MoveToward(s, u, goal, stride, leaving, walks) : null;
                if (move != null) return move;
            }
            if (shot != null && shot.Value.hit >= 15) return shot.Value.cmd;
            if (Usable(s, u, AbilityIds.Overwatch) && nearest <= Close * 2) return Ability(u, AbilityIds.Overwatch);
            if (Usable(s, u, AbilityIds.OverwatchMelee) && nearest <= Close) return Ability(u, AbilityIds.OverwatchMelee);
            if (Usable(s, u, AbilityIds.Hunker)) return Ability(u, AbilityIds.Hunker);
            return null;
        }

        private static IEnumerable<Unit> Enemies(GameState s) => Grid.Combatants(s, Teams.Alien);

        /// <summary>Where `u` goes: La Lata (leaving, or carrying the item), the item, the radio, the one to free, the alarm, or the nearest enemy by the walk.</summary>
        private static Pos? Goal(GameState s, Unit u, bool leaving, System.Func<Unit, Reach> walksOf, System.Func<Unit, Pos?> roomOf)
        {
            var m = s.Mission;
            var bus = m.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).OrderBy((p) => Grid.Distance(u.Pos, p)).FirstOrDefault();
            if (leaving && bus != null) return bus;
            if (m.Kind == MissionKinds.Recovery && m.Item != null && m.Item.Pos != null) return m.Item.Pos;
            if (m.Kind == MissionKinds.Hack && m.Terminal != null && !m.ObjectiveDone) return m.Terminal;
            if ((Vip(s) ?? Alarm(s)) is Unit objective) return objective.Pos;
            if (s.Run != null && m.Lair != null) return m.Lair;
            if (Bull(s) is Unit bull) return bull.Pos;
            if (RunSaqueo(s)) return roomOf(u) ?? bus;
            return NearestEnemy(s, u, walksOf(u)) ?? bus;
        }

        /// <summary>
        /// The nearest of the dead by the walk (`walks`, past them), as the crow flies if none can be walked to. By the walk,
        /// a stretch toward one keeps it the nearest, since the walk to any other drops by no more than the stretch; as the
        /// crow flies, the nearest changed with the stretches, and one went back and forth every turn (5/11).
        /// </summary>
        private static Pos? NearestEnemy(GameState s, Unit u, Reach walks)
        {
            var enemies = Enemies(s).ToList();
            int Walk(Unit e) => walks.Cost.TryGetValue(Grid.NodeOf(s, e.Pos), out var w) ? w : int.MaxValue;
            var walked = enemies.Where((e) => Walk(e) < int.MaxValue).OrderBy(Walk).ThenBy((e) => e.Id, System.StringComparer.Ordinal).FirstOrDefault();
            return (walked ?? enemies.OrderBy((e) => Grid.Distance(u.Pos, e.Pos)).ThenBy((e) => e.Id, System.StringComparer.Ordinal).FirstOrDefault())?.Pos;
        }

        /// <summary>
        /// Whether all the squad not gone is in La Lata, but for any that can't walk there from where it is (walled off, no
        /// crowbar): with no clock to call the bus now (Arturo, 5/11 #1), it would wait for them for ever.
        /// </summary>
        private static bool AllAboard(GameState s)
        {
            var bus = s.Mission.EvacZone.Select((z) => Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 })).ToList();
            foreach (var o in Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated && !Abilities.InEvacZone(s, o.Pos)))
            {
                var reach = Paths.ComputeReach(s, o, double.MaxValue);
                if (bus.Any((n) => reach.Cost.TryGetValue(n, out _))) return false;
            }
            return true;
        }

        /// <summary>
        /// In La Lata, waiting for the rest: a stretch further in, away from the nearest still outside, so as not to stand in
        /// their way (5/11: one in the door of the bus kept the others out, and only the clock used to end it).
        /// </summary>
        private static Command? MakeRoom(GameState s, Unit u)
        {
            var outside = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated && !Abilities.InEvacZone(s, o.Pos)).ToList();
            if (outside.Count == 0 || Paths.MoveBudget(s, u) <= 0) return null;
            double Away(Pos p) => outside.Min((o) => Grid.Distance(p, o.Pos));
            var reach = Paths.ComputeReach(s, u, Stride(s, u));
            Destination? best = null;
            foreach (var o in Paths.Destinations(s, u, reach))
                if (Abilities.InEvacZone(s, o.Pos) && (best == null || Away(o.Pos) > Away(best.Pos) + 1e-9 || (System.Math.Abs(Away(o.Pos) - Away(best.Pos)) < 1e-9 && o.Cost < best.Cost))) best = o;
            if (best == null || Away(best.Pos) < Away(u.Pos) + 1 || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>
        /// The next stretch, within what is left, never a run (Paths.IsRun: all the turn's movement at once, heard; Arturo,
        /// 6/11): with all of it, a tile short; with less, the rest. Seven tiles and then one are the same eight in silence:
        /// on a bench of 90 missions with bots (6/11), 29 won so, against 25 running once the squad was seen, on the way out
        /// or to strike. Hidden, with the squad's extra (Paths.ConcealedBonus, Arturo 16/11).
        /// </summary>
        private static double Stride(GameState s, Unit u)
        {
            var left = Paths.MoveBudget(s, u);
            var full = Paths.FullMoves(s, u);
            return left < full ? left : full - Paths.CostOrtho;
        }

        /// <summary>A run's Saqueo: the normal node (Gameplay, 16/11), no objective but to search and go.</summary>
        private static bool RunSaqueo(GameState s) => s.Run?.NodeKind == NodeKinds.Normal && s.Mission.Kind == MissionKinds.Scavenge && s.Mission.EvacZone.Count > 0;

        /// <summary>El Toro alive, in his fight (H6); null in any other.</summary>
        private static Unit? Bull(GameState s) => !string.IsNullOrEmpty(s.Mission.Boss) && Grid.GetUnit(s, s.Mission.Boss!) is Unit b && b.Alive ? b : null;

        /// <summary>A run's Asedio (H5): the squad holds by La Lata till it comes (MissionState.HoldUntil).</summary>
        private static bool Holding(GameState s) => s.Run != null && s.Mission.HoldUntil != null;

        /// <summary>La Lata takes the squad now: in an Asedio, from its turn on («La Lata llega en el turno 8»).</summary>
        private static bool BusHere(GameState s) => s.Mission.HoldUntil == null || s.Turn >= s.Mission.HoldUntil;

        /// <summary>
        /// The squad broken: one or none of it left, or all at half health or less. A Guarida or El Toro is given up then: lost,
        /// but whoever is left comes home.
        /// </summary>
        private static bool Routed(GameState s)
        {
            var squad = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).ToList();
            return squad.Count <= 1 || squad.All((o) => o.Hp * 2 <= o.MaxHp);
        }

        /// <summary>
        /// Whether `p` lies on a line El Toro could charge along (Toro.ChargePath: straight, one of eight ways, 2 to ChargeRun + 1
        /// tiles off, at his level), walls and the rest aside: a place to keep out of.
        /// </summary>
        private static bool InChargeLine(Unit bull, Pos p)
        {
            int dx = p.X - bull.Pos.X, dy = p.Y - bull.Pos.Y;
            var steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
            return p.Z == bull.Pos.Z && steps >= 2 && steps <= Toro.ChargeRun + 1 && (dx == 0 || dy == 0 || System.Math.Abs(dx) == System.Math.Abs(dy));
        }

        /// <summary>
        /// A run's Saqueo is over for the squad: the turn RunLeaveTurn has come, RoomsWanted rooms are searched, one of it is
        /// at half health or less, ThreatMany of the dead it sees are within ThreatNear of it, or none of it has a room or a
        /// box left to search that it can walk to (`roomOf`). Hunting the dead it fell on every one (Gameplay, 16/11: 4 of 4,
        /// with rifles too): the horde grows with the noise (Rules.HordeClock), and kept coming.
        /// </summary>
        private static bool RunLeaving(GameState s, System.Func<Unit, Pos?> roomOf)
        {
            if (s.Turn >= RunLeaveTurn || (s.RoomsSearched?.Count ?? 0) >= RoomsWanted) return true;
            var squad = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).ToList();
            if (squad.Any((o) => o.Hp * 2 <= o.MaxHp)) return true;
            var near = Enemies(s).Count((e) => Aim.TeamSees(s, Teams.Xcom, e) && squad.Any((o) => Grid.Distance(o.Pos, e.Pos) <= ThreatNear));
            return near >= ThreatMany || squad.All((o) => roomOf(o) == null);
        }

        /// <summary>
        /// The nearest place by the walk (`walks`, within RoomTurns of movement) in a room nobody has searched (Looting.RoomAt,
        /// RoomSearched), or beside a box, a boot or a body still shut (Looting.IsSearchable): whichever way, La Lata waiting
        /// near since 16/11 (Gameplay: «las 3–4 más cercanas a la escuadra», RoomsWanted). Null with none. `known`: each tile's
        /// room as found, for the next soldier (a room is a flood of its floor).
        /// </summary>
        private static Pos? RoomGoal(GameState s, Unit u, Reach walks, Dictionary<int, LootRoom?> known)
        {
            var most = RoomTurns * Paths.FullMoves(u);
            foreach (var kv in walks.Cost.Where((kv) => kv.Value <= most).OrderBy((kv) => kv.Value).ThenBy((kv) => kv.Key))
            {
                if (!known.TryGetValue(kv.Key, out var room))
                {
                    room = Looting.RoomAt(s, Grid.PosOf(s, kv.Key));
                    if (room == null) known[kv.Key] = null;
                    else foreach (var p in room.Tiles) known[Grid.NodeOf(s, p)] = room;
                }
                var at = Grid.PosOf(s, kv.Key);
                if ((room != null && !Looting.RoomSearched(s, room))
                    || Grid.Dirs8.Any((d) => new Pos { X = at.X + d.X, Y = at.Y + d.Y, Z = at.Z } is Pos q && Looting.IsSearchable(s, q) && Looting.ReachesToSearch(s, at, q))) return at;
            }
            return null;
        }

        /// <summary>
        /// Whether `u` has something to fire: a gun, and in a run rounds in it or a magazine to reload (Unit.mags, the pack's:
        /// Gameplay, 16/11); empty with none, it goes to the blade.
        /// </summary>
        private static bool Armed(GameState s, Unit u)
        {
            if (!Content.HasGun(u)) return false;
            return s.Run == null || u.Ammo > 0 || (u.Mags ?? 0) > 0 || (u.PistolAmmo ?? 0) > 0;
        }

        /// <summary>
        /// What `u` does with its pack in a run's fight (RunPack), the first that the rules take (RunPack.Refusal): a medicine
        /// on itself at half health or less; what lies at its feet picked up (free), a gun first if it has none to fire; a
        /// gun in the pack put on if it has none to fire; with nobody near, a vest in the pack put on if it wears none.
        /// </summary>
        private static Command? PackOrder(GameState s, Unit u, bool armed, int slot, IReadOnlyList<int> connected)
        {
            if (u.Pack == null || u.Kit == null) return null;
            Command? Try(ItemCommand cmd) => RunPack.Refusal(s, cmd, slot, connected) == null ? cmd : null;
            var packed = u.Pack.Items.Select((p) => p.Gear).ToList();
            if (u.Hp * 2 <= u.MaxHp && packed.FirstOrDefault((g) => g.Kind == GearKinds.Medicine) is Gear medicine)
            {
                var use = Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = medicine.Id });
                if (use != null) return use;
            }
            var floor = (s.FloorGear ?? new List<DroppedGear>()).Where((d) => Grid.SamePos(d.Pos, u.Pos)).Select((d) => d.Gear)
                .OrderBy((g) => !armed && IsGunGear(g) ? 0 : 1).ThenBy((g) => g.Id, System.StringComparer.Ordinal);
            foreach (var g in floor)
            {
                var take = Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Gear = g.Id });
                if (take != null) return take;
            }
            if (!armed && packed.FirstOrDefault(IsGunGear) is Gear gun)
            {
                var equip = Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Gear = gun.Id });
                if (equip != null) return equip;
            }
            var near = Enemies(s).Any((e) => Grid.Distance(u.Pos, e.Pos) <= Close);
            if (!near && u.Kit.Vest == null && packed.FirstOrDefault((g) => g.Kind == GearKinds.Vest) is Gear vest) return Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Gear = vest.Id });
            return null;
        }

        /// <summary>A run's gear that fires: a weapon for the hands or the side, not a blade.</summary>
        private static bool IsGunGear(Gear g) => Content.GearWeapon(g) is FoundWeaponDef w && w.Slot != FoundWeaponSlots.Melee;

        /// <summary>Whether a found weapon of `kind` fires: in the hand or at the side, not a blade.</summary>
        private static bool IsGun(string? kind) => kind != null && Content.FoundWeapons.TryGetValue(kind, out var def) && def.Slot != FoundWeaponSlots.Melee;

        /// <summary>A gun in `u`'s backpack (Loot.weapon), by its index; null with none.</summary>
        private static int? PackedGun(Unit u)
        {
            var i = u.Backpack?.FindIndex((l) => IsGun(l.Weapon)) ?? -1;
            return i >= 0 ? i : (int?)null;
        }

        /// <summary>
        /// Where a soldier without a gun may find one: the boots of police cars still shut (MissionState.PoliceCar, a gun
        /// one time in three), or a gun on the floor. Only as many go as there are, the nearest to them; null for the rest.
        /// </summary>
        private static Pos? GunGoal(GameState s, Unit u)
        {
            var found = new List<Pos>();
            foreach (var i in s.Mission.PoliceCar ?? new List<int>())
            {
                var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
                if (Looting.SearchSpot(s, p) == Looting.PoliceBoot && Looting.IsSearchable(s, p)) found.Add(p);
            }
            found.AddRange((s.Dropped ?? new List<DroppedLoot>()).Where((d) => IsGun(d.Loot.Weapon)).Select((d) => d.Pos));
            found.AddRange((s.FloorGear ?? new List<DroppedGear>()).Where((d) => IsGunGear(d.Gear)).Select((d) => d.Pos));
            if (found.Count == 0) return null;
            double Near(Unit x) => found.Min((p) => Grid.Distance(x.Pos, p));
            var going = Grid.Combatants(s, Teams.Xcom).Where((x) => !x.Evacuated && !Armed(s, x) && PackedGun(x) == null && x.Pack?.Items.Any((p) => IsGunGear(p.Gear)) != true && s.Mission.Item?.Carrier != x.Id)
                .OrderBy(Near).ThenBy((x) => x.Id, System.StringComparer.Ordinal).Take(found.Count);
            if (!going.Any((x) => x.Id == u.Id)) return null;
            return found.OrderBy((p) => Grid.Distance(u.Pos, p)).First();
        }

        /// <summary>
        /// The blade's move (Arturo, 16/10: striking is striking and moving is moving): with an action to strike, a stretch
        /// to a tile beside one of the dead the squad sees (moving takes none: Arturo, 5/11 #14). The weakest first, then
        /// where fewest others can bite, then the shortest; null when none is that near.
        /// </summary>
        private static Command? MoveToStrike(GameState s, Unit u)
        {
            if (Paths.MoveBudget(s, u) <= 0 || !Usable(s, u, AbilityIds.Slash)) return null;
            var enemies = Enemies(s).ToList();
            var seen = enemies.Where((e) => Aim.TeamSees(s, u.Team, e)).ToList();
            if (seen.Count == 0) return null;
            var reach = Paths.ComputeReach(s, u, Stride(s, u));
            Destination? best = null;
            (int hp, int crowd, double cost) bestKey = default;
            foreach (var o in Paths.Destinations(s, u, reach))
            {
                var beside = seen.Where((e) => Aim.InMeleeReach(s, o.Pos, e.Pos)).ToList();
                if (beside.Count == 0) continue;
                var key = (hp: beside.Min((e) => e.Hp), crowd: enemies.Count((e) => Grid.Distance(o.Pos, e.Pos) <= 2.5), cost: o.Cost);
                if (best == null || key.hp < bestKey.hp || (key.hp == bestKey.hp && (key.crowd < bestKey.crowd || (key.crowd == bestKey.crowd && key.cost < bestKey.cost - 1e-9))))
                {
                    best = o;
                    bestKey = key;
                }
            }
            if (best == null || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>
        /// One move toward `goal`, within `cost`, along the way there (`walks`, its walks past the dead): the place on it
        /// with the least walk left and danger, if better than staying; as the crow flies if the goal can't be walked to.
        /// `bus`: any place of La Lata is as good as the goal (the nearest may be taken by a partner). Null: none nearer.
        /// </summary>
        /// <remarks>
        /// Along the way, the way round a wall or a boarded door counts as walked, and one stretch doesn't undo the one
        /// before: with the movement in stretches (Arturo, 5/11 #14), one went back and forth by a wall every turn, nearer as
        /// the crow flies and then along the way round. The walk is the way there, from the soldier: the walk back from the
        /// goal goes down drops the soldier can't climb, and one stood still under one.
        /// </remarks>
        private static Command? MoveToward(GameState s, Unit u, Pos goal, double cost, bool bus, Reach walks)
        {
            var reach = Paths.ComputeReach(s, u, cost);
            // Where it ends up matters as much as how near: beside the dead it is bitten in their turn.
            var enemies = Enemies(s).ToList();
            // El Toro's charge ready (or by the next turn): his lines are kept out of (H6).
            var bull = Bull(s);
            var charging = bull != null && (!bull.Cooldowns.TryGetValue(AbilityIds.Charge, out var wait) || wait <= 1);
            double Danger(Pos p) => enemies.Sum((e) => Grid.Distance(p, e.Pos) <= 2.5 ? 4 : Grid.Distance(p, e.Pos) <= 5 ? 1.5 : 0) + (charging && InChargeLine(bull!, p) ? 6 : 0);
            var stops = Paths.Destinations(s, u, reach);
            Destination? best = null;
            var end = WayTo(s, walks, goal, bus);
            if (end != null)
            {
                // Along the way: the walk left from each place on it, in tiles; any place of La Lata is there.
                var total = walks.Cost.Get(Grid.NodeOf(s, end), 0);
                var at = stops.ToDictionary((o) => Grid.NodeOf(s, o.Pos));
                var bestScore = (double)total / Paths.CostOrtho + Danger(u.Pos) - 0.5;
                foreach (var n in Way(s, walks, end))
                {
                    if (!at.TryGetValue(n, out var o)) continue;
                    var p = o.Pos;
                    var left = bus && Abilities.InEvacZone(s, p) ? 0 : (double)(total - walks.Cost.Get(n, 0)) / Paths.CostOrtho;
                    var score = left + Danger(p);
                    if (score < bestScore - 1e-9)
                    {
                        best = o;
                        bestScore = score;
                    }
                }
            }
            else
            {
                double Far(Pos p) => bus && Abilities.InEvacZone(s, p) ? 0 : Grid.Distance(p, goal) + 3 * System.Math.Abs(p.Z - goal.Z);
                var bestScore = double.MaxValue;
                foreach (var o in stops)
                {
                    var d = Far(o.Pos) + Danger(o.Pos);
                    if (best == null || d < bestScore - 1e-9 || (System.Math.Abs(d - bestScore) < 1e-9 && o.Cost < best.Cost))
                    {
                        best = o;
                        bestScore = d;
                    }
                }
                // Only a move that brings it nearer, and is no worse than staying.
                if (best != null && (Far(best.Pos) >= Far(u.Pos) - 0.5 || bestScore >= Far(u.Pos) + Danger(u.Pos) - 0.5)) best = null;
            }
            if (best == null || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>The places of the way to `end` in `walks`, from the first step on (Paths.BuildPath, but to a place taken too: one of the dead).</summary>
        private static List<int> Way(GameState s, Reach walks, Pos end)
        {
            var nodes = new List<int>();
            var start = Grid.NodeOf(s, walks.Origin);
            int? i = Grid.NodeOf(s, end);
            while (i != null && i != start)
            {
                nodes.Add(i.Value);
                i = walks.Prev.TryGetValue(i.Value, out var back) ? back : (int?)null;
            }
            nodes.Reverse();
            return nodes;
        }

        /// <summary>
        /// Where the way to `goal` ends in `walks`: the goal, or the place beside it nearest by the walk if it can't be stood
        /// on (a radio, a car's boot); for La Lata, its place nearest by the walk. Null if none can be walked to.
        /// </summary>
        private static Pos? WayTo(GameState s, Reach walks, Pos goal, bool bus)
        {
            var ends = bus ? s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 })
                : walks.Cost.TryGetValue(Grid.NodeOf(s, goal), out _) ? new[] { goal }
                : Grid.Dirs8.Select((d) => new Pos { X = goal.X + d.X, Y = goal.Y + d.Y, Z = goal.Z });
            Pos? best = null;
            var bestWalk = int.MaxValue;
            foreach (var p in ends)
            {
                if (!Grid.InBounds(s, p.X, p.Y) || !walks.Cost.TryGetValue(Grid.NodeOf(s, p), out var w) || w >= bestWalk) continue;
                best = p;
                bestWalk = w;
            }
            return best;
        }

        /// <summary>
        /// With the crowbar, the boards off a door beside it on `goal`'s side (Arturo, 2/11), if the way through saves a
        /// turn's movement on the way round (which MoveToward walks); null otherwise.
        /// </summary>
        private static Command? Pry(GameState s, Unit u, Pos goal, bool bus)
        {
            if (!Usable(s, u, AbilityIds.Pry)) return null;
            var boarded = Abilities.BoardedBeside(s, u.Pos).Where((p) => Grid.Distance(p, goal) < Grid.Distance(u.Pos, goal)).ToList();
            if (boarded.Count == 0) return null;
            // The walk from the goal to each place (the same both ways, but for the drops).
            var back = Paths.ComputeReach(s, u, double.MaxValue, goal);
            int? Walk(Pos p) => bus && Abilities.InEvacZone(s, p) ? 0 : back.Cost.TryGetValue(Grid.NodeOf(s, p), out var w) ? w : (int?)null;
            var here = Walk(u.Pos);
            foreach (var b in boarded)
            {
                // Through it: the walk left from beyond, two steps on.
                var beyond = Grid.Dirs8.Select((d) => Walk(new Pos { X = b.X + d.X, Y = b.Y + d.Y, Z = b.Z })).Where((w) => w != null).Select((w) => w!.Value + 2 * Paths.CostOrtho).DefaultIfEmpty(int.MaxValue).Min();
                if (beyond < int.MaxValue && (here == null || beyond + Paths.FullMoves(s, u) < here)) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Pry, Tile = b };
            }
            return null;
        }

        /// <summary>The likeliest attack `u` has now (rifle, pistol or the blade) at an enemy `within` tiles (only `at`, if given), and how likely.</summary>
        private static (Command cmd, int hit)? BestShot(GameState s, Unit u, double within, string? at = null)
        {
            (Command cmd, int hit)? best = null;
            var abilities = Abilities.AbilitiesOf(u);
            foreach (var a in new[] { AbilityIds.Shoot, AbilityIds.Pistol, AbilityIds.Slash })
            {
                if (!abilities.Contains(a) || !Usable(s, u, a)) continue;
                foreach (var t in Abilities.AbilityTargets(s, u, a))
                {
                    if (Grid.Distance(u.Pos, t.Pos) > within || (at != null && t.Id != at)) continue;
                    var p = Aim.PreviewShot(s, u, t, new ShotOptions { Ability = a });
                    if (p != null && (best == null || p.Hit > best.Value.hit)) best = (Ability(u, a, t.Id), p.Hit);
                }
            }
            return best;
        }

        private static bool Usable(GameState s, Unit u, string ability) => Abilities.AbilitiesOf(u).Contains(ability) && Abilities.AbilityBlocker(s, u, ability) == null;

        private static Command Ability(Unit u, string ability, string? target = null) => new AbilityCommand { Unit = u.Id, Ability = ability, Target = target };
    }
}
