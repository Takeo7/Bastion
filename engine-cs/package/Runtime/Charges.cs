#nullable enable
// G23 Las cargas (Arturo, cuadros 105 and 109; .dev-snapshots/gameplay/g23/CARGAS.md): a nest of the dead to blow up. Three
// charges carried in the packs (GearKinds.Charge), set one by one at the nest's three points beside them
// (AbilityIds.PlaceCharge); the last one set, a count of Setup.ChargesTurns enemy turns; at 0 the three go off (Setup.ChargesDamage
// to everyone within Setup.ChargesRadius of one, the squad too) and La Lata goes: whoever is out is left behind. Won with the
// three set and somebody aboard; leaving before, «superada, sin la recompensa». The setup (Setup.PlaceCharges) is in
// SetupRuns.cs; the events' effects in Events.cs.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Setup
    {
        /// <summary>Cuadro 109: the count once the third is set, and the blast (10 to whoever is within 4 of a point, the squad too).</summary>
        public const int ChargesTurns = 4;
        public const int ChargesDamage = 10;
        public const double ChargesRadius = 4;
        /// <summary>The charges carried in (cuadro 109: «tres en la mochila»).</summary>
        public const int ChargesCount = 3;
        /// <summary>The nest: a building of this many ground tiles or more, its nearest walk from La Lata ChargesNear to ChargesFar tiles.</summary>
        public const int ChargesHouse = 9, ChargesNear = 20, ChargesFar = 50;
        /// <summary>The points apart from each other, at least, in tiles.</summary>
        public const int ChargesApart = 3;
        /// <summary>The nest's dead, asleep inside, by difficulty 1–4.</summary>
        public static readonly IReadOnlyList<int> NestDead = new[] { 4, 6, 7, 8 };

        /// <summary>
        /// Las cargas' nest (CARGAS.md, after SeatZone and once the fight's Run is set, for the charges' gear): a building of
        /// ChargesHouse tiles or more at ChargesNear–ChargesFar on foot from La Lata, its three points on the ground outside
        /// it, by its walls, on three sides where it can (ChargesApart apart at least), each walked to from La Lata; its dead
        /// asleep inside it (NestDead), a group of their own; the three charges into the squad's packs, one each in turn
        /// alternating players (two to the first with two seats), to another with room when a pack is full, at the feet
        /// when nobody has. With no such building, nothing of it: a Saqueo of the stage.
        /// </summary>
        public static void PlaceCharges(GameState s, int difficulty, Rng rng)
        {
            var bus = s.Mission.EvacZone.Select((z) => new Pos { X = (int)z.X, Y = (int)z.Y, Z = 0 }).FirstOrDefault((p) => Grid.InBounds(s, p.X, p.Y));
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive && u.CampaignId != null).ToList();
            if (bus == null || squad.Count == 0 || s.Run == null) return;
            var walker = squad[0];
            var home = walker.Pos;
            walker.Pos = bus;
            var area = s.Width * s.Height;
            var fromBus = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true).Cost.Where((kv) => kv.Key < area).ToDictionary((kv) => kv.Key, (kv) => kv.Value);
            walker.Pos = home;
            Pos At(int i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
            bool Walled(int i) => Grid.KindAt(s, At(i)) == TileKinds.Wall;
            var nests = new List<(List<int> Floor, List<int> Points)>();
            foreach (var floor in Buildings.GroundTiles(s).Where((t) => t.Count >= ChargesHouse))
            {
                var inside = floor.ToHashSet();
                // Its walls: those beside its floor; the points, the ground outside beside them, walked to from La Lata.
                var walls = new HashSet<int>();
                foreach (var i in floor)
                    foreach (var d in Grid.Dirs8)
                    {
                        var x = i % s.Width + d.X;
                        var y = i / s.Width + d.Y;
                        if (Grid.InBounds(s, x, y) && Walled(y * s.Width + x)) walls.Add(y * s.Width + x);
                    }
                var outside = new HashSet<int>();
                foreach (var w in walls)
                    foreach (var d in Grid.Dirs4)
                    {
                        var x = w % s.Width + d.X;
                        var y = w / s.Width + d.Y;
                        if (!Grid.InBounds(s, x, y)) continue;
                        var j = y * s.Width + x;
                        if (inside.Contains(j) || (s.Ground != null && j < s.Ground.Count && s.Ground[j] == 2)) continue;
                        if (Grid.KindAt(s, At(j)) != TileKinds.Floor || Grid.UnitAt(s, At(j)) != null || !fromBus.ContainsKey(j)) continue;
                        outside.Add(j);
                    }
                if (outside.Count < ChargesCount) continue;
                var walk = outside.Min((j) => fromBus[j]);
                if (walk < ChargesNear * Paths.CostOrtho || walk > ChargesFar * Paths.CostOrtho) continue;
                // One a side (by where it lies from the middle), on three of them where it can; else spread out.
                var mx = floor.Average((i) => i % s.Width);
                var my = floor.Average((i) => i / s.Width);
                int Side(int j)
                {
                    var dx = j % s.Width - mx;
                    var dy = j / s.Width - my;
                    return Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? 1 : 3) : (dy >= 0 ? 2 : 0);
                }
                var ordered = outside.OrderBy((j) => j).ToList();
                var points = new List<int>();
                foreach (var side in ordered.Select(Side).Distinct().OrderBy((x) => x).ToList())
                {
                    if (points.Count == ChargesCount) break;
                    var some = ordered.Where((j) => Side(j) == side && points.All((p) => Apart(s, p, j) >= ChargesApart)).ToList();
                    if (some.Count > 0) points.Add(some[rng.Int(some.Count)]);
                }
                while (points.Count < ChargesCount)
                {
                    var some = ordered.Where((j) => !points.Contains(j) && points.All((p) => Apart(s, p, j) >= ChargesApart)).ToList();
                    if (some.Count == 0) break;
                    points.Add(some[rng.Int(some.Count)]);
                }
                if (points.Count == ChargesCount) nests.Add((floor, points));
            }
            if (nests.Count == 0) return;
            var nest = nests[rng.Int(nests.Count)];
            s.Mission.Charges = new ChargesState
            {
                Points = nest.Points.Select((j) => new ChargePoint { Tile = j }).ToList(),
                Nest = nest.Floor.OrderBy((i) => i).ToList(),
            };
            // Its dead, asleep inside it.
            var free = nest.Floor.Where((i) => Grid.KindAt(s, At(i)) == TileKinds.Floor && Grid.UnitAt(s, At(i)) == null).OrderBy((i) => i).ToList();
            var pod = new Pod { Id = "pn", Active = false, UnitIds = new List<string>(), Patrol = new List<Pos>(), PatrolIndex = 0 };
            var count = Math.Min(free.Count, NestDead[Math.Max(1, Math.Min(4, difficulty)) - 1]);
            for (var k = 0; k < count; k++)
            {
                var i = free[rng.Int(free.Count)];
                free.Remove(i);
                var template = difficulty >= 3 && k == 0 ? TemplateIds.Xenoid : TemplateIds.Zombie;
                var u = MakeUnit($"n{k + 1}", template, Content.Templates[template].Name, null, At(i), pod.Id);
                u.Facing = rng.Next() * 2 * Math.PI - Math.PI;
                s.Units.Add(u);
                pod.UnitIds.Add(u.Id);
            }
            if (pod.UnitIds.Count > 0) s.Pods.Add(pod);
            // The three charges, into the packs: one each in turn, alternating players.
            var turns = new List<Unit>();
            var byOwner = squad.GroupBy((u) => u.Owner ?? 0).OrderBy((g) => g.Key).Select((g) => g.ToList()).ToList();
            for (var k = 0; turns.Count < squad.Count; k++)
                foreach (var g in byOwner)
                    if (k < g.Count) turns.Add(g[k]);
            var maker = new RunLoot.Maker(s.Run.NextGear);
            for (var k = 0; k < ChargesCount; k++)
            {
                var g = maker.Make(GearKinds.Charge, null)[0];
                var first = turns[k % turns.Count];
                var into = new[] { first }.Concat(turns.Where((u) => u != first)).FirstOrDefault((u) => u.Pack != null && Gears.CanStow(u.Pack, g));
                if (into != null) Gears.Stow(into.Pack!, g);
                else (s.FloorGear ??= new List<DroppedGear>()).Add(new DroppedGear { Gear = g, Pos = Events.CopyPos(first.Pos) });
            }
            s.Run.NextGear = maker.Next;
        }

        private static int Apart(GameState s, int a, int b) => Math.Max(Math.Abs(a % s.Width - b % s.Width), Math.Abs(a / s.Width - b / s.Width));
    }

    public static partial class Rules
    {
        /// <summary>Las cargas: the three set (MissionState.Charges); a win needs it.</summary>
        public static bool ChargesSet(GameState s) => s.Mission.Charges != null && s.Mission.Charges.Points.All((p) => p.Placed);

        /// <summary>Las cargas: a point's place on the ground.</summary>
        public static Pos ChargeAt(GameState s, ChargePoint p) => new Pos { X = p.Tile % s.Width, Y = p.Tile / s.Width, Z = 0 };

        /// <summary>Las cargas: the point still to set that `u` stands on or beside, within reach of its hands (null: none).</summary>
        public static ChargePoint? ChargePointBeside(GameState s, Unit u)
        {
            return s.Mission.Charges?.Points.FirstOrDefault((p) => !p.Placed && (Grid.SamePos(u.Pos, ChargeAt(s, p)) || Looting.ReachesToSearch(s, u.Pos, ChargeAt(s, p))));
        }

        /// <summary>Las cargas: the charge `u` carries (the first in its pack), if any.</summary>
        public static Gear? ChargeOf(Unit u) => u.Pack?.Items.FirstOrDefault((p) => p.Gear.Kind == GearKinds.Charge)?.Gear;
    }

    public static partial class Abilities
    {
        /// <summary>
        /// Las cargas: what a soldier of the squad carrying a charge can do with it, the nest not blown yet: set it
        /// (AbilityIds.PlaceCharge). Not in AbilitiesOf, which knows no state (as CarryAbilities).
        /// </summary>
        public static List<string> ChargeAbilities(GameState s, Unit u)
        {
            if (s.Mission.Charges == null || s.Mission.Charges.Blown == true || u.Team != Teams.Xcom || u.Captive || u.Automaton != null
                || Content.Templates[u.Template].Team != Teams.Xcom || u.Template == TemplateIds.Vip) return new List<string>();
            return Rules.ChargeOf(u) != null ? new List<string> { AbilityIds.PlaceCharge } : new List<string>();
        }

        /// <summary>Las cargas: why `u` can't set a charge now (null: it can). Lore's words, provisional.</summary>
        public static string? PlaceChargeRefusal(GameState s, Unit u)
        {
            if (Rules.ChargeOf(u) == null) return "No lleva ninguna carga.";
            if (Rules.ChargePointBeside(s, u) == null) return "No hay ningún punto de carga al lado.";
            return null;
        }
    }

    public sealed partial class Sim
    {
        /// <summary>Las cargas: `u` sets its charge at the point beside it; the last one set, the count starts (Events).</summary>
        private void PlaceCharge(Unit u)
        {
            var point = Rules.ChargePointBeside(S, u)!;
            var gear = Rules.ChargeOf(u)!;
            var left = S.Mission.Charges!.Points.Count((p) => !p.Placed) - 1;
            Emit(new ChargePlacedEvent { Unit = u.Id, Tile = Rules.ChargeAt(S, point), Gear = gear.Id, Left = left });
        }

        /// <summary>
        /// Las cargas, after each enemy turn once set: the count down; at 0 the three go off (Setup.ChargesDamage to everyone
        /// within Setup.ChargesRadius of one, through armour, the squad too; the cover round them broken as by a grenade) and
        /// La Lata goes (whoever is out, left behind). True when it ended the fight.
        /// </summary>
        private bool ChargesCount()
        {
            var c = S.Mission.Charges;
            if (c?.Countdown == null || c.Blown == true) return false;
            var left = c.Countdown.Value - 1;
            Emit(new ChargesTickEvent { Left = Math.Max(0, left) });
            if (left > 0) return false;
            var points = c.Points.Select((p) => Rules.ChargeAt(S, p)).ToList();
            Emit(new ChargesBlownEvent { Points = points.Select(Engine.Events.CopyPos).ToList() });
            bool Near(Pos q) => points.Any((p) => Js.Hypot(q.X - p.X, q.Y - p.Y) <= Setup.ChargesRadius);
            foreach (var v in S.Units.Where((u) => u.Alive && u.Aboard != true && !u.Evacuated && Near(u.Pos)).ToList())
            {
                if (Over) break;
                if (v.Alive) Damage(v, Setup.ChargesDamage, 0, null, v.Armor);
            }
            var done = new HashSet<int>();
            var r = (int)Math.Ceiling(Setup.ChargesRadius);
            foreach (var p in points)
                for (var dy = -r; dy <= r; dy++)
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var q = new Pos { X = p.X + dx, Y = p.Y + dy, Z = 0 };
                        if (!Grid.InBounds(S, q.X, q.Y) || Js.Hypot(dx, dy) > Setup.ChargesRadius) continue;
                        var kind = Grid.KindAt(S, q);
                        if (kind != TileKinds.Low && kind != TileKinds.High) continue;
                        foreach (var o in Rules.ObjectPlaces(S, q))
                        {
                            var node = Grid.NodeOf(S, o);
                            if (!done.Add(node)) continue;
                            var now = Grid.KindAt(S, o);
                            if (now != null && BlastResult.TryGetValue(now, out var next)) Emit(new TileChangedEvent { Pos = o, Kind = next });
                        }
                    }
            CheckOutcome();
            if (!Over && S.Mission.EvacZone.Count > 0) DepartEvac();
            return true;
        }
    }
}
