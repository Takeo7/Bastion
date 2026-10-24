#nullable enable
// ai.ts: the enemy's turn. The living take cover and shoot; the dead walk, bite and break their way in; pods still
// unaware walk their rounds or go to look at what they heard.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>breachPlan's `{ tile: Pos; stand: Pos }`.</summary>
    public sealed class BreachPlan
    {
        public Pos Tile { get; set; } = new Pos();
        public Pos Stand { get; set; } = new Pos();
    }

    public static class Ai
    {
        /// <summary>
        /// How far one place is from another for a walker (docs/FASE6-PLAN.md): the
        /// tiles between them, and for each storey between them the way round to the
        /// stairs. Without storeys, the distance. With flights of several tiles
        /// (docs/MAPAS.md, phase 2) the way round is a tile longer; a map without them
        /// (saved before, hand-made) as it was.
        /// </summary>
        private const int StoreyWalk = 4;
        private const int OldStoreyWalk = 3;

        private static double Farness(GameState s, Pos a, Pos b)
        {
            return Grid.Distance(a, b) + (Flights.HasLongFlights(s) ? StoreyWalk : OldStoreyWalk) * Grid.StoreysApart(s, a, b);
        }

        /// <summary>Beside `b` (or on it), on its floor: a walker that got there has arrived.</summary>
        private static bool Arrived(GameState s, Pos a, Pos b, double gap)
        {
            return Grid.Distance(a, b) <= gap && Grid.StoreysApart(s, a, b) == 0;
        }

        /// <summary>Points a living enemy gives a place for each storey it stands over the nearest soldier: they like to be posted up high.</summary>
        private const int HighGround = 6;

        private sealed class Candidate
        {
            public Pos Pos = new Pos();
            public int Cost;
            public double Score;
        }

        /// <summary>`Math.min(...xs)`: Infinity for none.</summary>
        private static double MinOf(IEnumerable<double> xs)
        {
            var min = double.PositiveInfinity;
            foreach (var x in xs) if (x < min || double.IsNaN(x)) min = x;
            return min;
        }

        /// <summary>The sign of a comparator's number, as Array.prototype.sort reads it (NaN as 0).</summary>
        private static int Order(double d)
        {
            return d > 0 ? 1 : d < 0 ? -1 : 0;
        }

        /// <summary>
        /// `a.localeCompare(b)` on unit ids: lowercase letters, digits and hyphens ('s1', 'a12', 'r3u2', 'tur-b4'), which
        /// collate as their code units do.
        /// </summary>
        private static int IdOrder(string a, string b)
        {
            return Math.Sign(string.CompareOrdinal(a, b));
        }

        /// <summary>
        /// Desirability of standing on `pos`: good cover against every soldier that can
        /// see it, being flanked is very bad, and a good shot from there is a bonus.
        /// </summary>
        private static double TileScore(GameState s, Unit u, Pos pos, List<Unit> enemies, double shotWeight)
        {
            double score = 0;
            var bestShot = 0;
            foreach (var e in enemies)
            {
                if (Los.SightOrigin(s, e.Pos, pos, Los.SightTo(s, e, pos)) != null)
                {
                    var cover = Cover.CoverAgainst(s, pos, e.Pos);
                    score += cover == 2 ? 25 : cover == 1 ? 10 : -30;
                }
                var shot = Aim.PreviewShot(s, u, e, new ShotOptions { From = pos });
                if (shot != null) bestShot = Math.Max(bestShot, shot.Hit + (shot.Flanked ? 15 : 0));
            }
            score += bestShot * shotWeight;
            // The high ground (storeys over the nearest soldier, on a map with them).
            if (s.Levels != null)
            {
                var sorted = new List<Unit>(enemies);
                Js.Sort(sorted, (a, b) => Order(Grid.Distance(pos, a.Pos) - Grid.Distance(pos, b.Pos)));
                var below = sorted.Count > 0 ? sorted[0] : null;
                if (below != null) score += HighGround * Math.Max(0, Js.Round(Grid.HeightAt(s, pos) - Grid.HeightAt(s, below.Pos)));
            }
            var nearest = MinOf(enemies.Select((e) => Grid.Distance(pos, e.Pos)));
            if (nearest > 14) score -= (nearest - 14) * 3;
            // Ranged aliens keep their distance: hugging a soldier gets them flanked and shot point-blank.
            if (nearest < 2.5) score -= 70;
            else if (nearest < 4.5) score -= (4.5 - nearest) * 16;
            return score;
        }

        private static Candidate? BestCandidate(GameState s, Unit u, Reach reach, Func<Pos, double> score)
        {
            var options = new List<Destination> { new Destination { Pos = u.Pos, Cost = 0 } };
            options.AddRange(Paths.Destinations(s, u, reach));
            Candidate? best = null;
            foreach (var o in options)
            {
                var c = new Candidate { Pos = o.Pos, Cost = o.Cost, Score = score(o.Pos) - o.Cost * 0.3 };
                if (
                    best == null ||
                    c.Score > best.Score ||
                    (c.Score == best.Score && (c.Cost < best.Cost || (c.Cost == best.Cost && (c.Pos.Y < best.Pos.Y || (c.Pos.Y == best.Pos.Y && c.Pos.X < best.Pos.X)))))
                )
                {
                    best = c;
                }
            }
            return best;
        }

        /// <summary>Where a freshly activated alien runs to: cover first, shots barely matter.</summary>
        public static Pos? ChooseScamperTile(GameState s, Unit u)
        {
            var enemies = Grid.Combatants(s, Teams.Xcom);
            if (enemies.Count == 0) return null;
            var reach = Paths.ComputeReach(s, u, Paths.CostPerAp(u));
            var best = BestCandidate(s, u, reach, (p) => TileScore(s, u, p, enemies, 0.1));
            return best != null && !Grid.SamePos(best.Pos, u.Pos) ? best.Pos : null;
        }

        /// <summary>
        /// Best impact point for an explosive: as many soldiers as possible, never an
        /// ally. Soldiers well protected from bullets count extra.
        /// </summary>
        private static (Pos tile, double score)? BestBlast(GameState s, Unit u, string ability)
        {
            var soldiers = Grid.Combatants(s, Teams.Xcom);
            var candidates = new List<Pos>();
            foreach (var soldier in soldiers)
            {
                candidates.Add(soldier.Pos);
                foreach (var d in Grid.Dirs8) candidates.Add(new Pos { X = soldier.Pos.X + d.X, Y = soldier.Pos.Y + d.Y, Z = soldier.Pos.Z });
            }
            (Pos tile, double score)? best = null;
            foreach (var tile in candidates)
            {
                if (!Abilities.CanTargetTile(s, u, ability, tile)) continue;
                double score = 0;
                var friendly = false;
                foreach (var p in Abilities.BlastTiles(s, ability, tile, u))
                {
                    var hit = Grid.UnitAt(s, p);
                    if (hit == null) continue;
                    if (hit.Team == u.Team)
                    {
                        friendly = true;
                        break;
                    }
                    var shot = Aim.PreviewShot(s, u, hit);
                    score += 1 + (shot == null || shot.Hit < 35 ? 0.5 : 0);
                }
                if (friendly || score == 0) continue;
                if (best == null || score > best.Value.score) best = (tile, score);
            }
            return best;
        }

        /// <summary>One action toward the nearest of `goals`, ending in cover if it can.</summary>
        private static bool MoveToward(Sim sim, Unit u, List<Pos> goals)
        {
            var s = sim.S;
            double Nearest(Pos p) => MinOf(goals.Select((g) => Farness(s, p, g)));
            // A goal on another storey: the way there goes round by the stairs, further first (a walk planned ahead).
            var sorted = new List<Pos>(goals);
            Js.Sort(sorted, (a, b) => Order(Farness(s, u.Pos, a) - Farness(s, u.Pos, b)));
            var target = sorted.Count > 0 ? sorted[0] : null;
            if (target != null && Grid.StoreysApart(s, u.Pos, target) > 0) return Walk(sim, u, target, 1);
            var reach = Paths.ComputeReach(s, u, Paths.CostPerAp(u));
            var cand = BestCandidate(s, u, reach, (p) => -Nearest(p) * 4 + (Cover.HasAnyCover(s, p) ? 6 : 0));
            if (cand == null || Grid.SamePos(cand.Pos, u.Pos)) return false;
            var path = Paths.BuildPath(s, u, reach, cand.Pos);
            if (path == null) return false;
            sim.MoveAlong(u, path, 1);
            return true;
        }

        /// <summary>Reachable tile closest to `goal` (ties: cheaper, then reading order).</summary>
        private static Pos? ClosestTo(GameState s, Unit u, Reach reach, Pos goal, double minGap = 0)
        {
            // Without storeys, the distance squared: the same order, without a root for every tile (a horde asks it a lot).
            Func<Pos, double> far = s.Levels != null ? (p) => Farness(s, p, goal) : (Func<Pos, double>)((p) => (double)(p.X - goal.X) * (p.X - goal.X) + (double)(p.Y - goal.Y) * (p.Y - goal.Y));
            var gap = s.Levels != null ? minGap : minGap * minGap;
            Pos? bestPos = null;
            double bestD = 0;
            var bestCost = 0;
            foreach (var o in Paths.Destinations(s, u, reach))
            {
                var d = far(o.Pos);
                if (d < gap) continue;
                if (bestPos == null || d < bestD - 1e-9 || (Math.Abs(d - bestD) < 1e-9 && (o.Cost < bestCost || (o.Cost == bestCost && (o.Pos.Y < bestPos.Y || (o.Pos.Y == bestPos.Y && o.Pos.X < bestPos.X))))))
                {
                    bestPos = o.Pos;
                    bestD = d;
                    bestCost = o.Cost;
                }
            }
            return bestPos != null && bestD < far(u.Pos) ? bestPos : null;
        }

        /// <summary>How far ahead a walk is planned, in actions: enough to go round a building to its door.</summary>
        private const int Lookahead = 4;
        /// <summary>To a goal on another storey, the way round by the stairs may first lead away: planned as far as it goes.</summary>
        private const int LookaheadStoreys = 40;

        /// <summary>How much longer a way round its own may be than the way through them, in half-tiles (COST_ORTHO a tile): a few tiles.</summary>
        private const int Detour = 4 * Paths.CostOrtho;

        /// <summary>
        /// A planned walk, and whether it waits behind its own (none of it free to take), or stops short of all of its way
        /// because it can't clear a flight of stairs or a window in the actions it was planned for (nobody stops on one).
        /// </summary>
        private sealed class WalkPlan
        {
            public List<Pos>? Path;
            public bool Jammed;
            public bool Short;
        }

        /// <summary>
        /// A unit's walk this turn toward `goal`: the shortest way to the free tile
        /// nearest it (a pod on patrol goes round by the doors and keeps out of
        /// windows), as far along as `actions` go. The way is planned through its own
        /// side, who will move on, as the walls won't; but nobody walks through anyone
        /// (Arturo, 19/10). With its own in the way, it goes round them if that is
        /// hardly longer (DETOUR), and so a crowd spreads over open ground; else it
        /// queues behind them, as at a siege's gate, rather than press on a wall
        /// beside it; its way taken from the start, it closes in on where it jams as
        /// far as the free ground lets it, or waits (`jammed`). Ending nowhere one can
        /// stand within reach (a window), the free tile nearest the goal that
        /// `actions` reach. No path when it gets no closer.
        /// </summary>
        private static WalkPlan PlanWalk(GameState s, Unit u, Pos goal, double minGap = 0, int actions = 1)
        {
            var ahead = Grid.StoreysApart(s, u.Pos, goal) > 0 ? LookaheadStoreys : Lookahead;
            Reach ReachOf(bool round) => Paths.ComputeReach(s, u, Paths.CostPerAp(u) * Math.Max(ahead, actions), u.Pos, round ? null : (Func<Unit, bool>)((o) => !Content.HostileTo(o, u)));
            var through = ReachOf(false);
            var dest = ClosestTo(s, u, through, goal, minGap);
            var path = dest != null ? Paths.BuildPath(s, u, through, dest) : null;
            if (path == null) return new WalkPlan { Path = null, Jammed = false };
            var budget = Paths.CostPerAp(u) * actions;
            // How far along `way` the unit can go now (the last place it can stop at), who stops it first, and whether it runs out of actions somewhere it can't stop.
            (int end, Pos? jam, bool isShort) Stretch(List<Pos> way, Reach reach)
            {
                var end = -1;
                Pos? jam = null;
                var isShort = false;
                for (var i = 0; i < way.Count; i++)
                {
                    var p = way[i];
                    if (reach.Cost.Get(Grid.NodeOf(s, p), 0) > budget)
                    {
                        // Nowhere to stop before it runs out: at the foot of a flight it can't climb in these actions.
                        isShort = end < 0 && i > 0;
                        break;
                    }
                    if (InTheWay(s, i != 0 ? way[i - 1] : u.Pos, p))
                    {
                        jam = p;
                        break;
                    }
                    if (Flights.CanStopAt(s, p)) end = i;
                }
                return (end, jam, isShort);
            }
            var (endAt, jamAt, shortOf) = Stretch(path, through);
            // Held up behind its own before half its walk: the way round them, if there is one.
            var round = jamAt != null && (endAt < 0 || through.Cost.Get(Grid.NodeOf(s, path[endAt]), 0) * 2 < budget) ? ReachOf(true) : null;
            if (round != null)
            {
                if (round.Cost.TryGetValue(Grid.NodeOf(s, dest!), out var cost) && cost <= through.Cost.Get(Grid.NodeOf(s, dest!), 0) + Detour)
                {
                    var other = Paths.BuildPath(s, u, round, dest!)!;
                    var go = Stretch(other, round).end;
                    if (go >= 0) return new WalkPlan { Path = other.GetRange(0, go + 1), Jammed = false };
                }
            }
            if (endAt >= 0) return new WalkPlan { Path = path.GetRange(0, endAt + 1), Jammed = false };
            // Within reach now, round everyone (the way round, cut to this walk's actions: a Dijkstra's way there costs no more).
            var near = round != null ? Within(round, budget) : Paths.ComputeReach(s, u, budget);
            var step = ClosestTo(s, u, near, jamAt ?? goal, jamAt != null ? 0 : minGap);
            var closer = step != null ? Paths.BuildPath(s, u, near, step) : null;
            return new WalkPlan { Path = closer, Jammed = closer == null && jamAt != null, Short = jamAt == null && shortOf };
        }

        /// <summary>`reach` cut to what costs `budget` at most.</summary>
        private static Reach Within(Reach reach, double budget)
        {
            return new Reach { Origin = reach.Origin, Cost = new JsMap<int, int>(reach.Cost.Where((e) => e.Value <= budget)), Prev = reach.Prev };
        }

        /// <summary>planWalk's path.</summary>
        private static List<Pos>? WalkPath(GameState s, Unit u, Pos goal, double minGap = 0, int actions = 1)
        {
            return PlanWalk(s, u, goal, minGap, actions).Path;
        }

        /// <summary>Whether somebody stops the step from `a` onto `b`: standing on it, or, on a diagonal, on a corner it would cut (path.ts computeReach).</summary>
        private static bool InTheWay(GameState s, Pos a, Pos b)
        {
            if (Grid.UnitAt(s, b) != null) return true;
            if (a.X == b.X || a.Y == b.Y) return false;
            var h = Grid.HeightAt(s, a);
            return new[] { Grid.SurfaceAt(s, b.X, a.Y, h), Grid.SurfaceAt(s, a.X, b.Y, h) }.Any((p) => p != null && Grid.UnitAt(s, p) != null);
        }

        /// <summary>Actions a pod still unaware spends going to look where it heard something (its round takes one).</summary>
        private const int InvestigateActions = 2;

        /// <summary>
        /// Inactive pods walk their patrol route: the leader heads for the next
        /// waypoint, the others follow. A pod that heard something goes to look there
        /// first (docs/FASE2.md), faster, and back to its round once it finds nobody.
        /// Walking into sight of the squad activates the pod (handled by Sim.moveAlong):
        /// the noise alone never does.
        /// </summary>
        public static void PatrolPod(Sim sim, Pod pod)
        {
            var s = sim.S;
            var members = pod.UnitIds.Select((id) => Grid.GetUnit(s, id)).Where((u) => u != null && u.Alive).Select((u) => u!).ToList();
            var leader = members.Count > 0 ? members[0] : null;
            if (leader == null) return;

            var heard = members.Find((m) => m.Heard != null)?.Heard;
            if (heard != null)
            {
                var path = !Arrived(s, leader.Pos, heard, 1.5) ? WalkPath(s, leader, heard, 0, InvestigateActions) : null;
                if (path != null)
                {
                    sim.MoveAlong(leader, path, 0);
                    Follow(sim, pod, members, InvestigateActions);
                    return;
                }
                // Nobody there, or no way to it.
                foreach (var m in members) if (m.Heard != null) sim.Emit(new ForgotEvent { Unit = m.Id });
            }
            if (pod.Patrol.Count == 0)
            {
                LookRound(sim, pod, members);
                return;
            }

            // Whoever of them can walk there goes first: the leader may be boxed in by the rest, and then the pod stood
            // where it was all game, looking the same way (Ana, 23/10 #25).
            (Unit m, List<Pos> path)? GoFor(Pos goal)
            {
                foreach (var m in members)
                {
                    var path = WalkPath(s, m, goal);
                    if (path != null) return (m, path);
                }
                return null;
            }
            var index = pod.PatrolIndex % pod.Patrol.Count;
            var walk = Arrived(s, leader.Pos, pod.Patrol[index], 2) ? null : GoFor(pod.Patrol[index]);
            if (walk == null)
            {
                index = (index + 1) % pod.Patrol.Count;
                sim.Emit(new PatrolEvent { Pod = pod.Id, Index = index });
                walk = GoFor(pod.Patrol[index]);
            }
            if (walk == null)
            {
                LookRound(sim, pod, members);
                return;
            }
            var walker = walk.Value.m;
            sim.MoveAlong(walker, walk.Value.path, 0);
            Follow(sim, pod, new[] { walker }.Concat(members.Where((m) => m != walker)).ToList(), 1);
        }

        /// <summary>How likely one of an unaware pod standing about turns its head, each enemy turn.</summary>
        private const double LookRoundChance = 0.5;

        /// <summary>
        /// An unaware pod with no round to walk looks about: each of them may turn to
        /// another of the eight ways, and seeing a soldier there, the pod wakes (Ana,
        /// 23/10 #25: a group facing away never saw the fight behind it).
        /// </summary>
        private static void LookRound(Sim sim, Pod pod, List<Unit> members)
        {
            foreach (var m in members)
            {
                if (sim.Over || (sim.S.Pods.Find((p) => p.Id == pod.Id)?.Active ?? false)) return;
                if (sim.Rng.Next() >= LookRoundChance) continue;
                var d = Grid.Dirs8[sim.Rng.Int(Grid.Dirs8.Count)];
                var facing = Js.Atan2(d.Y, d.X);
                if (m.Facing != null && facing == m.Facing.Value) continue;
                sim.Emit(new TurnedEvent { Unit = m.Id, Facing = facing });
                var spotted = sim.SpottedFrom(m, m.Pos);
                if (spotted != null) sim.ActivatePods(new List<string> { pod.Id }, new Activation { Cause = PodCauses.Sighted, Unit = spotted.Id, By = m.Id });
            }
        }

        /// <summary>The rest of an unaware pod keep up with its leader (the first of `members`).</summary>
        private static void Follow(Sim sim, Pod pod, List<Unit> members, int actions)
        {
            var s = sim.S;
            var leader = members[0];
            foreach (var f in members.Skip(1))
            {
                if (sim.Over || (s.Pods.Find((p) => p.Id == pod.Id)?.Active ?? true) || !leader.Alive) return;
                if (!f.Alive || Arrived(s, f.Pos, leader.Pos, 1.5)) continue;
                var walk = WalkPath(s, f, leader.Pos, 1, actions);
                if (walk != null) sim.MoveAlong(f, walk, 0);
            }
        }

        /// <summary>
        /// Where a living enemy heads when it has nothing to shoot: the soldiers, or
        /// the last noise it heard while it sees none of them (reached, it forgets it).
        /// </summary>
        private static List<Pos> HeadFor(Sim sim, Unit u, List<Unit> enemies)
        {
            var s = sim.S;
            if (u.Heard != null && !enemies.Any((e) => Los.CanSee(s, u, e)))
            {
                if (!Arrived(s, u.Pos, u.Heard, 1.5)) return new List<Pos> { u.Heard };
                sim.Emit(new ForgotEvent { Unit = u.Id });
            }
            return enemies.Select((e) => e.Pos).ToList();
        }

        /// <summary>Whom `u` fights (content.ts hostileTo), able to be fought.</summary>
        private static List<Unit> Foes(GameState s, Unit u)
        {
            return s.Units.Where((t) => t.Alive && Content.HostileTo(u, t) && !t.Objective && !t.Captive).ToList();
        }

        /// <summary>How near one of the dead must be for the living to turn on it.</summary>
        private const int ZombieThreat = 8;

        /// <summary>
        /// One enemy's full turn (also drives mind-controlled soldiers). The living
        /// take cover and shoot; the dead have their own (takeZombieTurn).
        /// </summary>
        public static void TakeAlienTurn(Sim sim, Unit u)
        {
            if (Content.Templates[u.Template].Undead)
            {
                TakeZombieTurn(sim, u);
                return;
            }
            var s = sim.S;
            var abilities = Abilities.AbilitiesOf(u);
            bool Usable(string a) => abilities.Contains(a) && Abilities.AbilityBlocker(s, u, a) == null;
            for (var guard = 0; guard < 6 && u.Alive && u.Ap > 0 && !sim.Over; guard++)
            {
                // The squad, and the dead it sees close by (18/10 #47): a zombie at hand is a threat too.
                var enemies = Grid.Combatants(s, Teams.Xcom).Concat(Foes(s, u).Where((e) => e.Team != Teams.Xcom && Farness(s, u.Pos, e.Pos) <= ZombieThreat && Los.CanSee(s, u, e))).ToList();
                if (enemies.Count == 0) return;

                if (Aim.WeaponOf(u).Clip > 0 && u.Ammo <= 0 && Usable(AbilityIds.Reload))
                {
                    sim.Perform(u, AbilityIds.Reload);
                    continue;
                }

                // Explosives against clustered or well-covered soldiers.
                foreach (var ability in new[] { AbilityIds.Missiles, AbilityIds.Grenade, AbilityIds.Launch })
                {
                    if (!Usable(ability) || sim.AlienBudget.Explosives <= 0) continue;
                    var blast = BestBlast(s, u, ability);
                    if (blast != null && blast.Value.score >= 2)
                    {
                        sim.AlienBudget.Explosives--;
                        sim.Perform(u, ability, tile: blast.Value.tile);
                        return;
                    }
                }

                // Melee units charge whoever they can reach.
                var melee = abilities.Find((a) => Content.Abilities[a].Target == AbilityTargets.Melee);
                var shoots = abilities.Contains(AbilityIds.Shoot) && !Aim.WeaponOf(u).Melee;
                if (melee != null && Content.IsAttack(melee) && Usable(melee))
                {
                    var options = Abilities.MeleeTargets(s, u, melee);
                    if (options.Count > 0)
                    {
                        Js.Sort(options, (a, b) => a.Target.Hp - b.Target.Hp != 0 ? a.Target.Hp - b.Target.Hp : a.Path.Count - b.Path.Count);
                        sim.Perform(u, melee, melee: options[0]);
                        return;
                    }
                    if ((u.Ap >= 2 || !shoots) && MoveToward(sim, u, HeadFor(sim, u, enemies))) continue;
                }
                if (!shoots) return;

                var dmg = Aim.WeaponOf(u).Damage;
                Unit? bestTarget = null;
                var bestHit = 0;
                var bestValue = 0;
                foreach (var e in enemies)
                {
                    var p = Aim.PreviewShot(s, u, e);
                    if (p == null) continue;
                    // The squad first; a zombie right on them before anyone.
                    var value = p.Hit + (p.Flanked ? 20 : 0) + (e.Hp <= dmg ? 15 : 0) + (e.Team == Teams.Xcom ? 10 : Farness(s, u.Pos, e.Pos) <= 2 ? 30 : 0);
                    if (bestTarget == null || value > bestValue)
                    {
                        bestTarget = e;
                        bestHit = p.Hit;
                        bestValue = value;
                    }
                }

                if (u.Ap >= 2)
                {
                    var reach = Paths.ComputeReach(s, u, Paths.CostPerAp(u));
                    if (bestTarget != null)
                    {
                        var here = TileScore(s, u, u.Pos, enemies, 0.5);
                        var cand = BestCandidate(s, u, reach, (p) => TileScore(s, u, p, enemies, 0.5));
                        if (cand != null && !Grid.SamePos(cand.Pos, u.Pos) && cand.Score > here + 8)
                        {
                            var path = Paths.BuildPath(s, u, reach, cand.Pos);
                            if (path != null)
                            {
                                sim.MoveAlong(u, path, 1);
                                continue;
                            }
                        }
                        if (bestHit >= 35 && Usable(AbilityIds.Shoot))
                        {
                            sim.Perform(u, AbilityIds.Shoot, target: bestTarget);
                            return;
                        }
                    }
                    else
                    {
                        // No shot from here: a place in reach with one first (the window beside it, upstairs: Ana, 23/10 #21), else on toward them.
                        var cand = BestCandidate(s, u, reach, (p) => enemies.Any((e) => Aim.PreviewShot(s, u, e, new ShotOptions { From = p }) != null) ? TileScore(s, u, p, enemies, 0.5) : double.NegativeInfinity);
                        var path = cand != null && cand.Score > double.NegativeInfinity && !Grid.SamePos(cand.Pos, u.Pos) ? Paths.BuildPath(s, u, reach, cand.Pos) : null;
                        if (path != null)
                        {
                            sim.MoveAlong(u, path, 1);
                            continue;
                        }
                        if (MoveToward(sim, u, HeadFor(sim, u, enemies))) continue;
                    }
                }
                else if (bestTarget != null && bestHit >= 20 && Usable(AbilityIds.Shoot))
                {
                    sim.Perform(u, AbilityIds.Shoot, target: bestTarget);
                    return;
                }

                if (Usable(AbilityIds.Overwatch)) sim.Perform(u, AbilityIds.Overwatch);
                else if (Usable(AbilityIds.Reload)) sim.Perform(u, AbilityIds.Reload);
                return;
            }
        }

        // ---------------------------------------------------------------- the dead

        /// <summary>The Chillón keeps this far from the soldiers it sees: it screams, it doesn't bite.</summary>
        private const int ScreamerGap = 5;

        /// <summary>Nearest first (storeys counting as the way round to the stairs); ties by id, so the choice is the same every time.</summary>
        private static Comparison<Unit> ByDistance(GameState s, Pos from)
        {
            return (a, b) =>
            {
                var d = Order(Farness(s, from, a.Pos) - Farness(s, from, b.Pos));
                return d != 0 ? d : IdOrder(a.Id, b.Id);
            };
        }

        /// <summary>Tiles `u` covers in one action: no soldier further than this (plus a step) can be reached this action.</summary>
        private static double Stride(Unit u)
        {
            return Content.Templates[u.Template].Mobility + u.Mods.Mobility + 1.5;
        }

        /// <summary>
        /// Whether one of the dead can only walk this turn, and doesn't scream. It
        /// bites (or bursts) only beside a soldier: one already there, or one it sees
        /// and can reach with a step (two actions: a step and a bite, playtest 16/10).
        /// The rest only walk, and the enemy turn moves them after those who go for
        /// the squad, all at once (Sim.runAlienTurn; Arturo, 17/10).
        /// </summary>
        public static bool OnlyWalks(GameState s, Unit u)
        {
            if (!Content.Templates[u.Template].Undead || Abilities.AbilitiesOf(u).Contains(AbilityIds.Mindspin)) return false;
            return !Foes(s, u).Any((e) => Aim.InMeleeReach(s, u.Pos, e.Pos) || (Farness(s, u.Pos, e.Pos) <= Stride(u) + 1 && Los.CanSee(s, u, e)));
        }

        /// <summary>
        /// The dead that only walk, in the order they go: the nearest to the living
        /// first (ties by id), so a crowd moves from its front and those behind find
        /// the way ahead free, not taken by the ones still to move (19/10).
        /// </summary>
        public static List<Unit> FrontFirst(GameState s, List<Unit> walkers)
        {
            double Gap(Unit u) => MinOf(Foes(s, u).Select((e) => Farness(s, u.Pos, e.Pos)));
            var ranked = walkers.Select((u) => (u, d: Gap(u))).ToList();
            Js.Sort(ranked, (a, b) =>
            {
                var d = Order(a.d - b.d);
                return d != 0 ? d : IdOrder(a.u.Id, b.u.Id);
            });
            return ranked.Select((o) => o.u).ToList();
        }

        /// <summary>
        /// One action's walk toward `goal`, round whatever is in the way; false when it gets no closer. A flight of stairs it
        /// can't climb in one (the Hinchado's stride and a flight of 3: nobody stops on one) takes both its actions; with one
        /// left, it waits at the foot for the next turn's rather than wander off.
        /// </summary>
        private static bool Walk(Sim sim, Unit u, Pos goal, double minGap)
        {
            var once = PlanWalk(sim.S, u, goal, minGap);
            if (once.Short && u.Ap < 2) return false;
            var twice = once.Short ? PlanWalk(sim.S, u, goal, minGap, 2) : null;
            var plan = twice?.Path != null ? twice : once;
            // Waiting behind its own: it goes again once the rest have moved (Sim.runAlienTurn).
            if (plan.Jammed) sim.Jammed.Add(u.Id);
            if (plan.Path == null) return false;
            sim.MoveAlong(u, plan.Path, twice?.Path != null ? 2 : 1);
            return true;
        }

        /// <summary>In a siege, tiles of walk a point of what a door or barricade holds is worth: the dead go for the weakest.</summary>
        private const double BreachHpWeight = 0.5;

        /// <summary>
        /// In a siege (docs/FASE12.md §4), where one of the dead breaks its way to
        /// `goal` when it has no way there: the door or barricade beside a free tile
        /// it can get to, counting the walk to it, the way left from it to the goal
        /// and what it still holds. Only one that opens the way somewhere it can't get
        /// to yet (not the other leaf of a gate already broken). `stand`, where it
        /// beats on it from. Null when it has a way there, or nothing to break.
        /// </summary>
        public static BreachPlan? BreachPlanOf(GameState s, Unit u, Pos goal, Dictionary<int, Reach>? ways = null)
        {
            var holds = s.Breakables;
            if (s.Mission.Siege == null || holds == null) return null;
            // Whether the ground leaves a way: its own crowd in a gap is about to move on, it doesn't close it. The same for
            // all the dead from one place, while nothing changes (`ways`, Sim.ways: one that waited behind its own asks again).
            var from = Grid.NodeOf(s, u.Pos);
            Reach? known = null;
            var all = ways != null && ways.TryGetValue(from, out known) ? known : Paths.ComputeReach(s, u, double.PositiveInfinity, u.Pos, (o) => !Content.HostileTo(o, u));
            if (ways != null) ways[from] = all;
            // Somewhere to stand by the goal: a window's frame beside it is no way in, nobody stops there.
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!Grid.InBounds(s, goal.X + dx, goal.Y + dy)) continue;
                    foreach (var p in Grid.PlacesOn(s, goal.X + dx, goal.Y + dy)) if (all.Cost.Has(Grid.NodeOf(s, p)) && Arrived(s, p, goal, 1.5) && Flights.CanStopAt(s, p)) return null;
                }
            }
            Pos? bestTile = null;
            Pos? bestStand = null;
            double bestScore = 0;
            // Object.keys(holds): integer keys, in ascending order.
            foreach (var key in holds.Keys.OrderBy((k) => k).ToList())
            {
                var tile = Grid.PosOf(s, key);
                if (!Abilities.Breakable(s, tile)) continue;
                var opens = Grid.Dirs8.Any((d) =>
                {
                    var beyond = new Pos { X = tile.X + d.X, Y = tile.Y + d.Y, Z = tile.Z };
                    var kind = Grid.KindAt(s, beyond);
                    return kind != TileKinds.Door && Flights.CanStopAt(s, beyond) && !all.Cost.Has(Grid.NodeOf(s, beyond));
                });
                if (!opens) continue;
                foreach (var d in Grid.Dirs8)
                {
                    var stand = new Pos { X = tile.X + d.X, Y = tile.Y + d.Y, Z = tile.Z };
                    if (!Grid.InBounds(s, stand.X, stand.Y) || !Looting.ReachesToSearch(s, stand, tile)) continue;
                    var here = Grid.SamePos(stand, u.Pos);
                    int cost;
                    if (here) cost = 0;
                    else if (!all.Cost.TryGetValue(Grid.NodeOf(s, stand), out cost)) continue;
                    if (!here && (Grid.UnitAt(s, stand) != null || !Flights.CanStopAt(s, stand))) continue;
                    var score = (double)cost / Paths.CostOrtho + Farness(s, tile, goal) + holds[key] * BreachHpWeight;
                    if (bestTile == null || score < bestScore - 1e-9)
                    {
                        bestTile = tile;
                        bestStand = stand;
                        bestScore = score;
                    }
                }
            }
            return bestTile != null ? new BreachPlan { Tile = bestTile, Stand = bestStand! } : null;
        }

        /// <summary>
        /// One action toward `goal`: a walk, or in a siege, with no way there, the
        /// way broken open (breachPlan): a blow at the door or barricade beside it,
        /// or the walk up to it. False when it does neither.
        /// </summary>
        private static bool Advance(Sim sim, Unit u, Pos goal, double minGap)
        {
            var plan = Abilities.AbilitiesOf(u).Contains(AbilityIds.Smash) ? BreachPlanOf(sim.S, u, goal, sim.Ways) : null;
            if (plan == null) return Walk(sim, u, goal, minGap);
            if (!Grid.SamePos(plan.Stand, u.Pos)) return Walk(sim, u, plan.Stand, 0);
            if (Abilities.AbilityBlocker(sim.S, u, AbilityIds.Smash) != null) return false;
            sim.Perform(u, AbilityIds.Smash, tile: plan.Tile);
            return true;
        }

        /// <summary>The way, within one action, to a tile beside one of `soldiers` (the cheapest): for the Hinchado.</summary>
        private static List<Pos>? BesideSoldier(GameState s, Unit u, List<Unit> soldiers)
        {
            var near = soldiers.Where((t) => Farness(s, u.Pos, t.Pos) <= Stride(u)).ToList();
            if (near.Count == 0) return null;
            var reach = Paths.ComputeReach(s, u, Paths.CostPerAp(u));
            List<Pos>? bestPath = null;
            var bestCost = 0;
            foreach (var t in near)
            {
                foreach (var d in Grid.Dirs8)
                {
                    var tile = new Pos { X = t.Pos.X + d.X, Y = t.Pos.Y + d.Y, Z = t.Pos.Z };
                    if (!Grid.InBounds(s, tile.X, tile.Y) || !Aim.InMeleeReach(s, tile, t.Pos)) continue;
                    if (!reach.Cost.TryGetValue(Grid.NodeOf(s, tile), out var cost) || (bestPath != null && cost >= bestCost)) continue;
                    var path = Paths.BuildPath(s, u, reach, tile);
                    if (path != null)
                    {
                        bestPath = path;
                        bestCost = cost;
                    }
                }
            }
            return bestPath;
        }

        /// <summary>The Chillón steps back (one action) from the soldiers it sees.</summary>
        private static bool BackAway(Sim sim, Unit u, List<Unit> seen)
        {
            var s = sim.S;
            var reach = Paths.ComputeReach(s, u, Paths.CostPerAp(u));
            var cand = BestCandidate(s, u, reach, (p) => Math.Min(ScreamerGap + 2, MinOf(seen.Select((e) => Grid.Distance(p, e.Pos)))) * 4);
            if (cand == null || Grid.SamePos(cand.Pos, u.Pos)) return false;
            var path = Paths.BuildPath(s, u, reach, cand.Pos);
            if (path == null) return false;
            sim.MoveAlong(u, path, 1);
            return true;
        }

        /// <summary>Nothing seen and nothing heard: a few aimless steps (one action).</summary>
        private static void Wander(Sim sim, Unit u)
        {
            var s = sim.S;
            var reach = Paths.ComputeReach(s, u, Paths.CostPerAp(u));
            var options = Paths.Destinations(s, u, reach).Where((o) => Grid.Distance(o.Pos, u.Pos) >= 2).ToList();
            if (options.Count == 0) return;
            var path = Paths.BuildPath(s, u, reach, options[sim.Rng.Int(options.Count)].Pos);
            if (path != null) sim.MoveAlong(u, path, 1);
        }

        /// <summary>How many of its turns one of the dead follows a soldier it no longer sees before it forgets them (Arturo, 22/10).</summary>
        public const int HuntTurns = 3;

        /// <summary>
        /// One zombie's turn (docs/FASE2.md). No cover, no guns: it goes for the
        /// nearest soldier it sees and bites; seeing nobody, it follows the soldier it
        /// saw last wherever they go, for HUNT_TURNS of its turns (Unit.hunting,
        /// Arturo 22/10); with nobody to follow, it goes to the last noise it heard,
        /// and having heard nothing, it wanders. The Chillón screams (and every zombie
        /// within 16 tiles hears it) and keeps its distance; the Hinchado walks up to a
        /// soldier and bursts. Cheap enough for a horde of twenty: one reach per action
        /// and a sight check per soldier. In a siege they know where the living are:
        /// seeing nobody, they go for the nearest soldier, and with no way to them,
        /// they break one (advance; Arturo, 18/10).
        /// </summary>
        public static void TakeZombieTurn(Sim sim, Unit u)
        {
            var sighting = new Sighting();
            ZombieActions(sim, u, sighting);
            if (!u.Alive || sim.Over) return;
            // Whom it goes after next turn: the nearest it saw last this turn; or, seeing none of the living, the one it
            // follows still, a turn more, till it forgets them (or they are gone: dead, out of the map).
            var was = u.Hunting;
            if (sighting.Prey != null)
            {
                if (was?.Id != sighting.Prey || was.Missed != 0) sim.Emit(new HuntingEvent { Unit = u.Id, Target = sighting.Prey, Missed = 0 });
            }
            else if (was != null)
            {
                var missed = was.Missed + 1;
                var there = Foes(sim.S, u).Any((e) => e.Id == was.Id);
                sim.Emit(new HuntingEvent { Unit = u.Id, Target = there && missed < HuntTurns ? was.Id : null, Missed = missed });
            }
        }

        /// <summary>Whom one of the dead saw this turn: the nearest of the living, the last time it saw any (takeZombieTurn).</summary>
        private sealed class Sighting
        {
            public string? Prey;
        }

        /// <summary>One zombie's actions (takeZombieTurn), noting in `sighting` whom it saw.</summary>
        private static void ZombieActions(Sim sim, Unit u, Sighting sighting)
        {
            var s = sim.S;
            var abilities = Abilities.AbilitiesOf(u);
            bool Usable(string a) => abilities.Contains(a) && Abilities.AbilityBlocker(s, u, a) == null;
            var bite = abilities.Find((a) => Content.Abilities[a].Target == AbilityTargets.Melee && Content.IsAttack(a));
            for (var guard = 0; guard < 6 && u.Alive && u.Ap > 0 && !sim.Over; guard++)
            {
                // Any of the living: the squad, and the Saqueadores too (18/10 #47).
                var soldiers = Foes(s, u);
                if (soldiers.Count == 0) return;
                var seen = soldiers.Where((e) => Los.CanSee(s, u, e)).ToList();
                Js.Sort(seen, ByDistance(s, u.Pos));
                if (seen.Count > 0) sighting.Prey = seen[0].Id;

                if (abilities.Contains(AbilityIds.Mindspin))
                {
                    // The Chillón: a scream at whoever it sees (one a turn for the whole horde), or a corpse raised.
                    if (Usable(AbilityIds.Mindspin) && sim.AlienBudget.Psi > 0)
                    {
                        // Only at whoever it sees itself: the dead don't tell each other (Arturo, 16/10).
                        var victims = Abilities.AbilityTargets(s, u, AbilityIds.Mindspin).Where((v) => seen.Contains(v) && v.Team == Teams.Xcom).ToList();
                        Js.Sort(victims, (a, b) =>
                        {
                            if (b.Rank - a.Rank != 0) return b.Rank - a.Rank;
                            var psi = Abilities.PsiChance(b, s) - Abilities.PsiChance(a, s);
                            return psi != 0 ? psi : IdOrder(a.Id, b.Id);
                        });
                        if (victims.Count > 0)
                        {
                            sim.AlienBudget.Psi--;
                            sim.Perform(u, AbilityIds.Mindspin, target: victims[0]);
                            return;
                        }
                    }
                    if (Usable(AbilityIds.Reanimate))
                    {
                        var corpse = Abilities.AbilityTargets(s, u, AbilityIds.Reanimate).FirstOrDefault();
                        if (corpse != null)
                        {
                            sim.Perform(u, AbilityIds.Reanimate, target: corpse);
                            return;
                        }
                    }
                    if (seen.Count > 0)
                    {
                        if (Grid.Distance(u.Pos, seen[0].Pos) < ScreamerGap && BackAway(sim, u, seen)) continue;
                        return;
                    }
                }
                else if (abilities.Contains(AbilityIds.Burst))
                {
                    // The Hinchado: beside a soldier, it bursts; otherwise it lumbers up to the nearest it sees.
                    if (Usable(AbilityIds.Burst) && soldiers.Any((e) => Aim.InMeleeReach(s, u.Pos, e.Pos)))
                    {
                        sim.Perform(u, AbilityIds.Burst);
                        return;
                    }
                    if (seen.Count > 0)
                    {
                        var way = BesideSoldier(s, u, seen);
                        if (way != null)
                        {
                            sim.MoveAlong(u, way, 1);
                            continue;
                        }
                        if (Walk(sim, u, seen[0].Pos, 1)) continue;
                        return;
                    }
                }
                else
                {
                    // A bite costs an action and leaves the other (playtest 16/10 #21): only whoever it has beside it, the weakest first.
                    // A step is another action: to the nearest it sees itself, not whoever another of the dead sees (#12).
                    if (bite != null && Usable(bite) && soldiers.Any((e) => Aim.InMeleeReach(s, u.Pos, e.Pos)))
                    {
                        var beside = Abilities.MeleeTargets(s, u, bite).Where((o) => o.Path.Count == 0).ToList();
                        if (beside.Count > 0)
                        {
                            Js.Sort(beside, (a, b) => a.Target.Hp - b.Target.Hp != 0 ? a.Target.Hp - b.Target.Hp : IdOrder(a.Target.Id, b.Target.Id));
                            sim.Perform(u, bite, melee: beside[0]);
                            continue;
                        }
                    }
                    if (seen.Count > 0)
                    {
                        if (Advance(sim, u, seen[0].Pos, 1)) continue;
                        return;
                    }
                }

                // Nobody in sight: in a siege, for the nearest soldier, wherever they are.
                if (s.Mission.Siege != null)
                {
                    var sorted = new List<Unit>(soldiers);
                    Js.Sort(sorted, ByDistance(s, u.Pos));
                    var prey = sorted[0];
                    if (Advance(sim, u, prey.Pos, 1)) continue;
                    return;
                }
                // Out of sight, the one it saw last: after them, wherever they are now, till it forgets them.
                var hunted = u.Hunting != null ? soldiers.Find((e) => e.Id == u.Hunting.Id) : null;
                if (hunted != null)
                {
                    if (Advance(sim, u, hunted.Pos, 1)) continue;
                    return;
                }
                // Elsewhere, to the last noise heard, and having found nobody there, wandering.
                if (u.Heard != null)
                {
                    if (!Arrived(s, u.Pos, u.Heard, 1.5) && Advance(sim, u, u.Heard, 0)) continue;
                    sim.Emit(new ForgotEvent { Unit = u.Id });
                }
                Wander(sim, u);
                return;
            }
        }
    }
}
