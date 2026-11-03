using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// A player that plays a mission by itself, with the engine's own functions, for the tests between two apps
    /// (BASTION_CAMPANA, the app's CampaignRun): never in a game of the players. Each seat plays its own soldiers while it
    /// holds the command, one at a time: it reloads, shoots or strikes what it is likely to hit or what is close, and
    /// otherwise goes for the objective (a Botín's item; with it carried, La Lata, everyone) or the nearest enemy; in La
    /// Lata with the job done and everyone aboard (or time short), it calls the bus; the partner's call it answers yes.
    /// Without a gun (the campaign starts with two pistols for four: Arturo, 2/11) it goes for one first, in a police car's
    /// boot or on the floor, and puts it on; and with the blade it walks up to the dead and strikes. Walled off from where it
    /// goes (a boarded door: Arturo, 2/11), it takes the boards off with the crowbar or goes round. With nothing to do, it
    /// keeps watch or takes cover, and with no soldier with actions it ends its turn. Deterministic: no dice of its own.
    /// </summary>
    public static class MissionBot
    {
        /// <summary>Shots at least this likely are taken whatever else there is to do.</summary>
        public const int GoodShot = 50;

        /// <summary>An enemy this close (tiles) is shot at worse odds, and stops the dashing.</summary>
        public const double Close = 7;

        /// <summary>The next order of `slot`, or null: nothing for it now (the partner holds the command, or it is done).</summary>
        public static Command? Next(GameState s, int slot, IReadOnlyList<int> connected)
        {
            if (s.Outcome != null || s.ActiveTeam != Teams.Xcom) return null;
            // The partner calls La Lata: yes. One's own call waits for the partner.
            if (s.EvacCall != null) return s.EvacCall.By != slot ? new EvacAnswerCommand { Accept = true } : null;
            var holds = s.Command == slot || s.Command == null || !connected.Contains(s.Command.Value);
            if (!holds || s.Ready[slot]) return null;
            var able = Grid.Combatants(s, Teams.Xcom).Where((u) => !u.Evacuated && u.Ap > 0 && Content.CommandsFor(slot, u.Owner, connected)).ToList();
            if (able.Count == 0) return new EndTurnCommand { Ready = true };
            // The soldier in play first: it is to be finished before another (Rules InPlay). Then the others, the one
            // nearest its goal first; one with nothing to do lets the next one play, and only with none left is the turn over.
            var leaving = Leaving(s);
            var order = able.OrderBy((x) => x.Id == s.ActiveUnit ? 0 : 1)
                .ThenBy((x) => Goal(s, x, leaving) is Pos g ? Grid.Distance(x.Pos, g) : 0)
                .ThenBy((x) => x.Id, System.StringComparer.Ordinal);
            foreach (var u in order)
            {
                var cmd = Choose(s, u, leaving);
                if (cmd != null) return cmd;
                if (u.Id == s.ActiveUnit) break;
            }
            return new EndTurnCommand { Ready = true };
        }

        /// <summary>The job is done and the squad is to go: the item carried, or a VIP freed.</summary>
        private static bool Leaving(GameState s)
        {
            var m = s.Mission;
            if (m.Kind == MissionKinds.Recovery) return !string.IsNullOrEmpty(m.Item?.Carrier);
            return m.Kind != MissionKinds.Elimination && m.ObjectiveDone;
        }

        private static Command? Choose(GameState s, Unit u, bool leaving)
        {
            // In La Lata with the job done: the bus, once everyone is aboard, or the time is nearly out.
            if (leaving && Abilities.InEvacZone(s, u.Pos) && u.Infected != true)
            {
                var all = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).All((o) => Abilities.InEvacZone(s, o.Pos));
                var hurry = s.Mission.TurnsLeft != null && s.Mission.TurnsLeft <= 2;
                // The one with the item aboard and the dead on it: now, with whoever is in.
                var cornered = s.Mission.Item?.Carrier is string c && Grid.GetUnit(s, c) is Unit holder && Abilities.InEvacZone(s, holder.Pos)
                    && Enemies(s).Any((e) => Grid.Distance(holder.Pos, e.Pos) <= 2);
                if (all || hurry || cornered) return new EvacCommand { Unit = u.Id };
            }
            var armed = Content.HasGun(u);
            var carrier = s.Mission.Item?.Carrier == u.Id;
            if (!armed)
            {
                var gun = PackedGun(u);
                if (gun != null) return new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Index = gun };
                // One at its feet is picked up (free); a police car's boot beside it, searched.
                var dropped = s.Dropped?.FirstOrDefault((d) => IsGun(d.Loot.Weapon) && Grid.SamePos(d.Pos, u.Pos));
                if (dropped != null && (u.Backpack?.Count ?? 0) < Looting.PackSlots(u)) return new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Id = dropped.Id };
                var boot = Usable(s, u, AbilityIds.Search) ? Looting.SearchableTiles(s, u).FirstOrDefault((p) => Looting.SearchSpot(s, p) == Looting.PoliceBoot) : null;
                if (boot != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = boot };
            }
            if (u.Ammo <= 0 && Usable(s, u, AbilityIds.Reload)) return Ability(u, AbilityIds.Reload);
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
            var goal = (!armed && !leaving ? GunGoal(s, u) : null) ?? Goal(s, u, leaving);
            if (goal != null && !(leaving && Abilities.InEvacZone(s, u.Pos)))
            {
                // Dashing with nobody close, and always with the item: one action of moving keeps one to shoot.
                var dash = u.Ap >= 2 && (nearest > Close || carrier);
                var cost = dash ? Paths.MaxMoveCost(u) : Paths.CostPerAp(u);
                var move = MoveToward(s, u, goal, cost, leaving) ?? Unblock(s, u, goal, cost, leaving);
                if (move != null) return move;
            }
            if (shot != null && shot.Value.hit >= 15) return shot.Value.cmd;
            if (Usable(s, u, AbilityIds.Overwatch) && nearest <= Close * 2) return Ability(u, AbilityIds.Overwatch);
            if (Usable(s, u, AbilityIds.OverwatchMelee) && nearest <= Close) return Ability(u, AbilityIds.OverwatchMelee);
            if (Usable(s, u, AbilityIds.Hunker)) return Ability(u, AbilityIds.Hunker);
            return null;
        }

        private static IEnumerable<Unit> Enemies(GameState s) => Grid.Combatants(s, Teams.Alien);

        /// <summary>Where `u` goes: La Lata (leaving, or carrying the item), the item, or the nearest enemy.</summary>
        private static Pos? Goal(GameState s, Unit u, bool leaving)
        {
            var m = s.Mission;
            var bus = m.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).OrderBy((p) => Grid.Distance(u.Pos, p)).FirstOrDefault();
            if (leaving && bus != null) return bus;
            if (m.Kind == MissionKinds.Recovery && m.Item != null && m.Item.Pos != null) return m.Item.Pos;
            return Enemies(s).OrderBy((e) => Grid.Distance(u.Pos, e.Pos)).ThenBy((e) => e.Id, System.StringComparer.Ordinal).Select((e) => e.Pos).FirstOrDefault() ?? bus;
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
        /// The blade's move (Arturo, 16/10: striking is striking and moving is moving): with two actions, one move to a
        /// tile beside one of the dead the squad sees, keeping the second to strike. The weakest first, then where fewest
        /// others can bite, then the shortest; null when none is that near.
        /// </summary>
        private static Command? MoveToStrike(GameState s, Unit u)
        {
            if (u.Ap < 2 || !Usable(s, u, AbilityIds.Slash)) return null;
            var enemies = Enemies(s).ToList();
            var seen = enemies.Where((e) => Aim.TeamSees(s, u.Team, e)).ToList();
            if (seen.Count == 0) return null;
            var reach = Paths.ComputeReach(s, u, Paths.CostPerAp(u));
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
        /// One move toward `goal`, within `cost`: the place it can reach nearest it; null if none is nearer. `bus`: any
        /// free place of La Lata is as good as the goal (the nearest may be taken by a partner).
        /// </summary>
        private static Command? MoveToward(GameState s, Unit u, Pos goal, double cost, bool bus)
        {
            var reach = Paths.ComputeReach(s, u, cost);
            double Far(Pos p) => bus && Abilities.InEvacZone(s, p) ? 0 : Grid.Distance(p, goal) + 3 * System.Math.Abs(p.Z - goal.Z);
            // Where it ends up matters as much as how near: beside the dead it is bitten in their turn.
            var enemies = Enemies(s).ToList();
            double Danger(Pos p) => enemies.Sum((e) => Grid.Distance(p, e.Pos) <= 2.5 ? 4 : Grid.Distance(p, e.Pos) <= 5 ? 1.5 : 0);
            Destination? best = null;
            var bestScore = double.MaxValue;
            foreach (var o in Paths.Destinations(s, u, reach))
            {
                var d = Far(o.Pos) + Danger(o.Pos);
                if (best == null || d < bestScore - 1e-9 || (System.Math.Abs(d - bestScore) < 1e-9 && o.Cost < best.Cost))
                {
                    best = o;
                    bestScore = d;
                }
            }
            // Only a move that brings it nearer, and is no worse than staying.
            if (best == null || Far(best.Pos) >= Far(u.Pos) - 0.5 || bestScore >= Far(u.Pos) + Danger(u.Pos) - 0.5 || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>
        /// When no place within `cost` is any nearer `goal` as the crow flies (a wall in between, or a boarded door: Arturo,
        /// 2/11): with the crowbar, the boards beside it on the goal's side off, if the way through saves a move; else a move
        /// along the way round, to the place with the shortest walk left. Null if it isn't walled off, or there's no way.
        /// </summary>
        private static Command? Unblock(GameState s, Unit u, Pos goal, double cost, bool bus)
        {
            var reach = Paths.ComputeReach(s, u, cost);
            var places = Paths.Destinations(s, u, reach);
            if (places.Any((o) => Grid.Distance(o.Pos, goal) < Grid.Distance(u.Pos, goal) - 0.5)) return null;
            // The walk from the goal to each place (the same both ways, but for the drops).
            var back = Paths.ComputeReach(s, u, double.MaxValue, goal);
            int? Walk(Pos p) => bus && Abilities.InEvacZone(s, p) ? 0 : back.Cost.TryGetValue(Grid.NodeOf(s, p), out var w) ? w : (int?)null;
            var here = Walk(u.Pos);
            if (Usable(s, u, AbilityIds.Pry))
                foreach (var b in Abilities.BoardedBeside(s, u.Pos).Where((p) => Grid.Distance(p, goal) < Grid.Distance(u.Pos, goal)))
                {
                    // Through it: the walk left from beyond, two steps on.
                    var beyond = Grid.Dirs8.Select((d) => Walk(new Pos { X = b.X + d.X, Y = b.Y + d.Y, Z = b.Z })).Where((w) => w != null).Select((w) => w!.Value + 2 * Paths.CostOrtho).DefaultIfEmpty(int.MaxValue).Min();
                    if (beyond < int.MaxValue && (here == null || beyond + Paths.CostPerAp(u) < here)) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Pry, Tile = b };
                }
            if (here == null) return null;
            var enemies = Enemies(s).ToList();
            double Danger(Pos p) => Paths.CostOrtho * enemies.Sum((e) => Grid.Distance(p, e.Pos) <= 2.5 ? 4 : Grid.Distance(p, e.Pos) <= 5 ? 1.5 : 0);
            Destination? best = null;
            var bestScore = double.MaxValue;
            foreach (var o in places)
            {
                if (!(Walk(o.Pos) is int w)) continue;
                var d = w + Danger(o.Pos);
                if (best == null || d < bestScore - 1e-9 || (System.Math.Abs(d - bestScore) < 1e-9 && o.Cost < best.Cost))
                {
                    best = o;
                    bestScore = d;
                }
            }
            if (best == null || Walk(best.Pos) >= here - Paths.CostOrtho || bestScore >= here + Danger(u.Pos) - Paths.CostOrtho || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>The likeliest attack `u` has now (rifle, pistol or the blade) at an enemy `within` tiles, and how likely.</summary>
        private static (Command cmd, int hit)? BestShot(GameState s, Unit u, double within)
        {
            (Command cmd, int hit)? best = null;
            var abilities = Abilities.AbilitiesOf(u);
            foreach (var a in new[] { AbilityIds.Shoot, AbilityIds.Pistol, AbilityIds.Slash })
            {
                if (!abilities.Contains(a) || !Usable(s, u, a)) continue;
                foreach (var t in Abilities.AbilityTargets(s, u, a))
                {
                    if (Grid.Distance(u.Pos, t.Pos) > within) continue;
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
