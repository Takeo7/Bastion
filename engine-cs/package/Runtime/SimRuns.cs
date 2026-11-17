#nullable enable
// The runs' stages in a fight (H5 and H6; new in C#): the Sigilo seen, El Toro's roar and charge, and getting up. The
// charge's line is Toro's, pure, for the AI and for UI's line while aiming.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>El Toro's numbers and his charge's line (H6, .dev-snapshots/gameplay/runs/H6-TORO.md).</summary>
    public static class Toro
    {
        /// <summary>What the charge does to whoever it crashes into.</summary>
        public const int ChargeDamage = 4;
        /// <summary>Tiles the charge runs at most.</summary>
        public const int ChargeRun = 6;
        /// <summary>Defence a soldier on the ground loses (Unit.Down).</summary>
        public const int DownDefense = 20;

        private static readonly HashSet<string> Solid = new HashSet<string> { TileKinds.Wall, TileKinds.Door, TileKinds.Window, TileKinds.WindowBroken, TileKinds.Bars };

        /// <summary>
        /// The tiles of a charge from `u` at `t`: a straight line one of eight ways, t on it no more than ChargeRun + 1 tiles
        /// off and seen, every tile before it free to stop on at the same height, no wall's corner cut; the last beside t.
        /// Null if there is no such line (t alongside: a Mazazo, not a charge).
        /// </summary>
        public static List<Pos>? ChargePath(GameState s, Unit u, Unit t)
        {
            var dx = t.Pos.X - u.Pos.X;
            var dy = t.Pos.Y - u.Pos.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (t.Pos.Z != u.Pos.Z || steps < 2 || steps > ChargeRun + 1 || (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))) return null;
            if (!Los.CanSee(s, u, t)) return null;
            int sx = Math.Sign(dx), sy = Math.Sign(dy);
            bool IsSolid(int x, int y)
            {
                var kind = Grid.KindAt(s, new Pos { X = x, Y = y, Z = u.Pos.Z });
                return kind != null && Solid.Contains(kind);
            }
            var path = new List<Pos>();
            var from = u.Pos;
            for (var k = 1; k <= steps; k++)
            {
                var p = new Pos { X = u.Pos.X + sx * k, Y = u.Pos.Y + sy * k, Z = u.Pos.Z };
                // Round a corner a diagonal step doesn't go.
                if (sx != 0 && sy != 0 && (IsSolid(from.X + sx, from.Y) || IsSolid(from.X, from.Y + sy))) return null;
                if (k == steps) break;
                if (!Grid.InBounds(s, p.X, p.Y) || !Flights.CanStopAt(s, p) || Grid.UnitAt(s, p) != null || Grid.HeightAt(s, p) != Grid.HeightAt(s, u.Pos)) return null;
                path.Add(p);
                from = p;
            }
            return path;
        }

        /// <summary>The soldiers El Toro can charge now, the nearest first.</summary>
        public static List<(Unit Target, List<Pos> Path)> ChargeTargets(GameState s, Unit u)
        {
            var out_ = new List<(Unit Target, List<Pos> Path)>();
            foreach (var t in Grid.Combatants(s, Teams.Xcom))
            {
                var path = ChargePath(s, u, t);
                if (path != null) out_.Add((t, path));
            }
            return out_.OrderBy((x) => x.Path.Count).ThenBy((x) => x.Target.Id, StringComparer.Ordinal).ToList();
        }
    }

    public sealed partial class Sim
    {
        /// <summary>
        /// A run's Sigilo seen (H5): a big wave at once by the edges (twice a horde's wave, in groups of five), landed now.
        /// </summary>
        public void StealthSpotted()
        {
            var m = S.Mission;
            var units = Setup.HordeWave(m.Difficulty ?? 2).Concat(Setup.HordeWave(m.Difficulty ?? 2)).ToList();
            var ids = new HashSet<string>();
            for (var i = 0; i < units.Count; i += 5)
            {
                var r = new Reinforcement { Id = $"r{m.Reinforcements.Count + 1}", Turn = S.Turn, Pos = null, Units = units.Skip(i).Take(5).ToList(), Landed = false, Horde = true, Side = m.HordeSide };
                Emit(new ReinforcementsIncomingEvent { R = r });
                ids.Add(r.Id);
            }
            LandReinforcements(S.Turn, ids);
        }

        /// <summary>El Toro roars (H6): every group of the map awake, and all of them come to him (a noise the whole map hears).</summary>
        public void Roar(Unit u)
        {
            Emit(new RoaredEvent { Unit = u.Id });
            var asleep = S.Pods.Where((p) => !p.Active && p.UnitIds.Any((id) => Grid.GetUnit(S, id)?.Alive == true)).Select((p) => p.Id).ToList();
            ActivatePods(asleep, new Activation { Cause = PodCauses.Attacked, By = u.Id });
            MakeNoise(u.Pos, S.Width + S.Height, u);
        }

        /// <summary>
        /// El Toro's charge at `t` (H6), its actions already paid: along the line (Toro.ChargePath), the soldier knocked down
        /// where it stands and hurt. A line cut by now: as far as it goes, and nobody hit.
        /// </summary>
        public void Charge(Unit u, Unit t)
        {
            var path = Toro.ChargePath(S, u, t);
            if (path == null)
            {
                Emit(new ChargedEvent { Unit = u.Id, Path = new List<Pos>(), Target = null });
                return;
            }
            Emit(new ChargedEvent { Unit = u.Id, Path = path.Select(Engine.Events.CopyPos).ToList(), Target = t.Id });
            Emit(new KnockedDownEvent { Unit = t.Id });
            Damage(t, Toro.ChargeDamage, 0, u);
        }

        /// <summary>Whoever is on the ground gets up as the squad's turn begins, before anything (one of its actions).</summary>
        public void GetUp()
        {
            foreach (var d in S.Units.Where((x) => x.Alive && x.Down == true).ToList()) Emit(new GotUpEvent { Unit = d.Id });
        }
    }

    public static partial class Rules
    {
        /// <summary>The cell a caged one is in (H9): its room's tiles; none if it is in no cell.</summary>
        private static List<Pos> CellOf(GameState s, Unit u)
        {
            foreach (var site in s.Interiors ?? new List<InteriorSite>())
            {
                foreach (var r in site.Rooms.Where((r) => r.Use == PoliceUses.Cells && r.Z == u.Pos.Z))
                {
                    if (u.Pos.X < r.X || u.Pos.Y < r.Y || u.Pos.X >= r.X + r.W || u.Pos.Y >= r.Y + r.H) continue;
                    return Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => new Pos { X = x, Y = y, Z = r.Z })).ToList();
                }
            }
            return new List<Pos>();
        }

        /// <summary>The tiles of a caged one's cell just inside its bars, where it stands to bite (H9; Arturo, 17/11).</summary>
        public static List<Pos> CagedFront(GameState s, Unit u)
        {
            return CellOf(s, u).Where((p) => Grid.Dirs4.Any((d) => Grid.InBounds(s, p.X + d.X, p.Y + d.Y) && Grid.KindAt(s, new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z }) == TileKinds.Bars)).ToList();
        }

        /// <summary>
        /// The tiles a caged one can reach from its bars (H9; for the bots, Networking): those across them, straight, from any
        /// tile of its cell just inside them (Aim.AcrossBars). Only there is it dangerous. None if `u` is not caged.
        /// </summary>
        public static List<Pos> CagedReach(GameState s, Unit u)
        {
            var out_ = new List<Pos>();
            if (u.Caged != true) return out_;
            foreach (var p in CagedFront(s, u))
            {
                foreach (var d in Grid.Dirs4)
                {
                    var bar = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z };
                    var q = new Pos { X = p.X + 2 * d.X, Y = p.Y + 2 * d.Y, Z = p.Z };
                    if (!Grid.InBounds(s, q.X, q.Y) || Grid.KindAt(s, bar) != TileKinds.Bars || !Grid.IsWalkable(Grid.KindAt(s, q))) continue;
                    if (!out_.Any((o) => Grid.SamePos(o, q))) out_.Add(q);
                }
            }
            return out_;
        }

        /// <summary>
        /// Whether getting out on La Lata is all a mission asks (a Saqueo); not a Guarida's (its box to open) nor a boss's
        /// (him to kill), which are Saqueos with an objective (H5, H6).
        /// </summary>
        public static bool FreeExit(MissionState m)
        {
            return m.Kind == MissionKinds.Scavenge && m.Lair == null && string.IsNullOrEmpty(m.Boss);
        }
    }
}
