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
    /// job done, La Lata, everyone) or the nearest enemy, in stretches (Arturo, 5/11 #14: moving apart from the actions):
    /// first what it walks without being heard and, with the squad seen, on the way out or to strike, the rest, at a run
    /// (past Content.QuietTiles in the turn, heard: Arturo, 5/11); in La Lata with the job done and everyone aboard who can get there, it calls the bus; the
    /// partner's call it answers yes.
    /// Without a gun (the campaign starts with two pistols for four: Arturo, 2/11) it goes for one first, in a police car's
    /// boot or on the floor, and puts it on; and with the blade it walks up to the dead and strikes. Walled off from where it
    /// goes (a boarded door: Arturo, 2/11), it takes the boards off with the crowbar or goes round. With nothing to do, it
    /// keeps watch or takes cover, and with nothing left for any soldier it ends its turn, movement left or not (the turn
    /// doesn't end by itself while some is left: Arturo, 5/11 #14). Deterministic: no dice of its own.
    /// </summary>
    public static class MissionBot
    {
        /// <summary>Shots at least this likely are taken whatever else there is to do.</summary>
        public const int GoodShot = 50;

        /// <summary>An enemy this close (tiles) is shot at worse odds.</summary>
        public const double Close = 7;

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
            var leaving = Leaving(s, WalksOf);
            var goals = able.ToDictionary((x) => x.Id, (x) => Goal(s, x, leaving, WalksOf));
            // The soldier in play first: it is to be finished before another (Rules InPlay). Then the others, the one
            // nearest its goal first; one with nothing to do lets the next one play, and only with none left is the turn over,
            // movement left or not.
            var order = able.OrderBy((x) => x.Id == s.ActiveUnit ? 0 : 1)
                .ThenBy((x) => goals[x.Id] is Pos g ? Grid.Distance(x.Pos, g) : 0)
                .ThenBy((x) => x.Id, System.StringComparer.Ordinal);
            foreach (var u in order)
            {
                var cmd = Choose(s, u, leaving, goals[u.Id], WalksOf(u));
                if (cmd != null) return cmd;
                // In play with nothing to do (its actions kept, having moved), it holds the others back while the partner
                // plays (Rules InPlay): the partner's go.
                if (u.Id == s.ActiveUnit && u.Ap > 0 && PartnerPlays(s, slot, connected)) return new PassCommand();
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
        private static bool Leaving(GameState s, System.Func<Unit, Reach> walksOf)
        {
            var m = s.Mission;
            if (m.Kind == MissionKinds.Recovery) return !string.IsNullOrEmpty(m.Item?.Carrier);
            if (m.Kind == MissionKinds.Rescue && Vip(s) is Unit vip && !vip.Captive) return true;
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

        private static Command? Choose(GameState s, Unit u, bool leaving, Pos? target, Reach walks)
        {
            // In La Lata with the job done: the bus, once everyone is aboard who can get there. No clock hurries it: only the
            // sabotage's alarm has one now (Arturo, 5/11 #1), and it stops once the alarm is silenced (Events.CompleteObjective).
            if (leaving && Abilities.InEvacZone(s, u.Pos) && u.Infected != true)
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
            var armed = Content.HasGun(u);
            var carrier = s.Mission.Item?.Carrier == u.Id;
            if (!armed)
            {
                var gun = PackedGun(u);
                if (gun != null && u.Ap > 0) return new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Index = gun };
                // One at its feet is picked up (free); a police car's boot beside it, searched.
                var dropped = s.Dropped?.FirstOrDefault((d) => IsGun(d.Loot.Weapon) && Grid.SamePos(d.Pos, u.Pos));
                if (dropped != null && (u.Backpack?.Count ?? 0) < Looting.PackSlots(u)) return new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Id = dropped.Id };
                var boot = Usable(s, u, AbilityIds.Search) ? Looting.SearchableTiles(s, u).FirstOrDefault((p) => Looting.SearchSpot(s, p) == Looting.PoliceBoot) : null;
                if (boot != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = boot };
            }
            if (u.Ammo <= 0 && Usable(s, u, AbilityIds.Reload)) return Ability(u, AbilityIds.Reload);
            // The job, where it is a thing to do: the radio fixed from beside it, the alarm hit at any fair odds.
            if (Usable(s, u, AbilityIds.Hack)) return Ability(u, AbilityIds.Hack);
            var alarm = Alarm(s);
            var silence = alarm != null ? BestShot(s, u, double.MaxValue, alarm.Id) : null;
            if (silence != null && silence.Value.hit >= 15) return silence.Value.cmd;
            // Only what is close is shot at: a shot is heard, and draws more of them (docs/FASE2.md).
            var shot = BestShot(s, u, Close);
            var nearest = Enemies(s).Select((e) => Grid.Distance(u.Pos, e.Pos)).DefaultIfEmpty(double.MaxValue).Min();
            if (shot != null && !carrier && shot.Value.hit >= 30) return shot.Value.cmd;
            // The blade alone: a step up to one of the dead, keeping an action to strike.
            if (!armed && !carrier)
            {
                var close = MoveToStrike(s, u);
                if (close != null) return close;
            }
            var goal = (!armed && !leaving ? GunGoal(s, u) : null) ?? target;
            if (goal != null && !(leaving && Abilities.InEvacZone(s, u.Pos)))
            {
                var pry = Pry(s, u, goal, leaving);
                if (pry != null) return pry;
                var stride = Stride(s, u, leaving);
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
        private static Pos? Goal(GameState s, Unit u, bool leaving, System.Func<Unit, Reach> walksOf)
        {
            var m = s.Mission;
            var bus = m.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).OrderBy((p) => Grid.Distance(u.Pos, p)).FirstOrDefault();
            if (leaving && bus != null) return bus;
            if (m.Kind == MissionKinds.Recovery && m.Item != null && m.Item.Pos != null) return m.Item.Pos;
            if (m.Kind == MissionKinds.Hack && m.Terminal != null && !m.ObjectiveDone) return m.Terminal;
            if ((Vip(s) ?? Alarm(s)) is Unit objective) return objective.Pos;
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
            if (outside.Count == 0 || Paths.MoveBudget(u) <= 0) return null;
            double Away(Pos p) => outside.Min((o) => Grid.Distance(p, o.Pos));
            var reach = Paths.ComputeReach(s, u, Stride(s, u, false));
            Destination? best = null;
            foreach (var o in Paths.Destinations(s, u, reach))
                if (Abilities.InEvacZone(s, o.Pos) && (best == null || Away(o.Pos) > Away(best.Pos) + 1e-9 || (System.Math.Abs(Away(o.Pos) - Away(best.Pos)) < 1e-9 && o.Cost < best.Cost))) best = o;
            if (best == null || Away(best.Pos) < Away(u.Pos) + 1 || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>
        /// The next stretch, within what is left: first as far as it is not heard (Paths.IsRun: past Content.QuietTiles of
        /// the turn's movement, in one stretch or several, it runs; Arturo, 5/11, in the box); with none of that left, the
        /// rest when `loud` or once the squad has been seen, and otherwise nothing. On a bench of 90 missions with bots
        /// (6/11), 26 won against 13 with never a run, and 364's campaign won both only like this or always running.
        /// </summary>
        private static double Stride(GameState s, Unit u, bool loud)
        {
            var left = Paths.MoveBudget(u);
            var quiet = Content.QuietTiles * Paths.CostOrtho - (Paths.FullMoves(u) - left);
            if (quiet >= Paths.CostOrtho) return System.Math.Min(left, quiet);
            return loud || !s.Concealed ? left : 0;
        }

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
            if (found.Count == 0) return null;
            double Near(Unit x) => found.Min((p) => Grid.Distance(x.Pos, p));
            var going = Grid.Combatants(s, Teams.Xcom).Where((x) => !x.Evacuated && !Content.HasGun(x) && PackedGun(x) == null && s.Mission.Item?.Carrier != x.Id)
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
            if (Paths.MoveBudget(u) <= 0 || !Usable(s, u, AbilityIds.Slash)) return null;
            var enemies = Enemies(s).ToList();
            var seen = enemies.Where((e) => Aim.TeamSees(s, u.Team, e)).ToList();
            if (seen.Count == 0) return null;
            var reach = Paths.ComputeReach(s, u, Stride(s, u, true));
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
            double Danger(Pos p) => enemies.Sum((e) => Grid.Distance(p, e.Pos) <= 2.5 ? 4 : Grid.Distance(p, e.Pos) <= 5 ? 1.5 : 0);
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
                if (beyond < int.MaxValue && (here == null || beyond + Paths.FullMoves(u) < here)) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Pry, Tile = b };
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
