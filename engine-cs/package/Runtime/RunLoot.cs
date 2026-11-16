#nullable enable
// What a run's fight turns up (H3; new in C#): today's rolls (Looting) as gear, and where it goes, the searcher's pack
// or the floor at its feet. Supplies grow a quarter a row and half as much again with a modifier; nothing else does.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class RunLoot
    {
        /// <summary>A vest on a policeman's or a soldier's body, or in a police car's boot: one time in so many out of 100.</summary>
        public const int VestChance = 15;
        /// <summary>One vest in so many is a heavy one.</summary>
        public const int HeavyIn = 5;

        /// <summary>What a row and a modifier make the supplies: ×(1 + 0.25·(row − 1)), ×1.5 with any modifier (an event's too).</summary>
        public static double SuppliesScale(RunFight run)
        {
            return (1 + 0.25 * (run.Row - 1)) * (run.Modifier != null || run.Added != null ? 1.5 : 1);
        }

        /// <summary>New gear for a fight: its ids go on from the fight's counter (RunFight.NextGear), which `Next` keeps.</summary>
        public sealed class Maker
        {
            public int Next { get; set; }

            public Maker(int next)
            {
                Next = next;
            }

            /// <summary>`count` of it, in piles of Content.PileOf at most.</summary>
            public List<Gear> Make(string kind, string? type, int count = 1, List<string>? mods = null)
            {
                var pile = Content.PileOf(kind, type);
                var out_ = new List<Gear>();
                for (var left = count; left > 0; left -= pile)
                {
                    out_.Add(new Gear { Id = $"g{++Next}", Kind = kind, Type = type, Count = Math.Min(pile, left), Mods = mods != null && mods.Count > 0 ? new List<string>(mods) : null });
                }
                return out_;
            }
        }

        /// <summary>
        /// Today's find (Looting) as gear: supplies as supplies, scaled; only the runs' belt things (Content.BeltItems);
        /// medicines; rations as a few supplies; parts as a magazine half the time; a weapon as found, with its improvement and
        /// no wear; an improvement; magazines.
        /// </summary>
        public static List<Gear> GearOf(Loot? l, RunFight run, Rng rng, Maker maker)
        {
            var out_ = new List<Gear>();
            if (l == null) return out_;
            var scale = SuppliesScale(run);
            int Scaled(int n) => Math.Max(1, (int)Js.Round(n * scale));
            if ((l.Supplies ?? 0) > 0) out_.AddRange(maker.Make(GearKinds.Supplies, null, Scaled(l.Supplies!.Value)));
            if (!string.IsNullOrEmpty(l.Item) && Content.BeltItems.Contains(l.Item!)) out_.AddRange(maker.Make(GearKinds.Item, l.Item, l.Count ?? 1));
            if ((l.Medicines ?? 0) > 0) out_.AddRange(maker.Make(GearKinds.Medicine, null, l.Medicines!.Value));
            if ((l.Food ?? 0) > 0) out_.AddRange(maker.Make(GearKinds.Supplies, null, Scaled(l.Food!.Value)));
            if ((l.Parts ?? 0) > 0 && rng.Int(2) == 0) out_.AddRange(maker.Make(GearKinds.Mags, null));
            if (!string.IsNullOrEmpty(l.Weapon) && Content.IsGear(GearKinds.Weapon, l.Weapon)) out_.AddRange(maker.Make(GearKinds.Weapon, l.Weapon, 1, l.Mods));
            if (!string.IsNullOrEmpty(l.Mod)) out_.AddRange(maker.Make(GearKinds.Mod, l.Mod, l.Count ?? 1));
            if ((l.Mags ?? 0) > 0) out_.AddRange(maker.Make(GearKinds.Mags, null, l.Mags!.Value));
            return out_;
        }

        /// <summary>The good weapons a Guarida's box and a boss give (H5, H6): one of these, with its improvement.</summary>
        public static readonly IReadOnlyList<string> GoodWeapons = new[] { FoundWeaponIds.ScopedRifle, FoundWeaponIds.HuntingRifle };

        /// <summary>One of GoodWeapons at random, mounted as found (FoundWeaponDef.Mod).</summary>
        public static List<Gear> GoodWeapon(Rng rng, Maker maker)
        {
            var kind = rng.Pick(GoodWeapons);
            var mod = Content.FoundWeapons[kind].Mod;
            return maker.Make(GearKinds.Weapon, kind, 1, mod != null ? new List<string> { mod } : null);
        }

        /// <summary>A Guarida's box (H5): 80–120 § and a good weapon.</summary>
        public const int LairMin = 80;
        public const int LairMax = 120;

        public static List<Gear> LairOf(Rng rng, Maker maker)
        {
            var found = maker.Make(GearKinds.Supplies, null, rng.Range(LairMin, LairMax));
            found.AddRange(GoodWeapon(rng, maker));
            return found;
        }

        /// <summary>A vest now and then (VestChance; a heavy one in HeavyIn), where the police or the army left one.</summary>
        public static List<Gear> VestOf(Rng rng, Maker maker)
        {
            if (rng.Int(100) >= VestChance) return new List<Gear>();
            return maker.Make(GearKinds.Vest, rng.Int(HeavyIn) == 0 ? VestIds.HeavyVest : VestIds.Vest);
        }

        /// <summary>
        /// Where what was found goes: onto the equal piles in `pack` as far as they take, the rest where it first fits; what
        /// doesn't, at the searcher's feet. Each entry of `packed` is laid where it says, or poured onto the pile lying there.
        /// </summary>
        public static (List<Placed> Packed, List<Gear> Floor) Stow(GearGrid pack, IReadOnlyList<Gear> found)
        {
            var trial = Gears.CloneGrid(pack);
            var packed = new List<Placed>();
            var floor = new List<Gear>();
            foreach (var g in found)
            {
                var left = g.Count;
                foreach (var p in trial.Items)
                {
                    if (left == 0) break;
                    if (!Gears.SamePile(p.Gear, g)) continue;
                    var n = Math.Min(Gears.Room(p.Gear), left);
                    if (n == 0) continue;
                    p.Gear.Count += n;
                    left -= n;
                    packed.Add(new Placed { Gear = new Gear { Id = g.Id, Kind = g.Kind, Type = g.Type, Count = n }, X = p.X, Y = p.Y, Turned = p.Turned });
                }
                if (left == 0) continue;
                var rest = Gears.CloneGear(g);
                rest.Count = left;
                var at = Gears.FirstFit(trial, rest);
                if (at == null)
                {
                    floor.Add(rest);
                    continue;
                }
                trial.Items.Add(new Placed { Gear = Gears.CloneGear(rest), X = at.X, Y = at.Y, Turned = at.Turned });
                packed.Add(new Placed { Gear = rest, X = at.X, Y = at.Y, Turned = at.Turned });
            }
            return (packed, floor);
        }

        /// <summary>
        /// A search's gear arrives (SearchedEvent, RoomSearchedEvent): into `u`'s pack as `packed` says, the rest on the floor
        /// at its feet, and the counter where the host left it.
        /// </summary>
        public static void Receive(GameState s, Unit u, List<Placed>? packed, List<Gear>? floor, int? next)
        {
            if (u.Pack != null)
            {
                foreach (var p in packed ?? new List<Placed>())
                {
                    var there = Gears.At(u.Pack, p.X, p.Y);
                    if (there != null && there.X == p.X && there.Y == p.Y && Gears.SamePile(there.Gear, p.Gear)) there.Gear.Count += p.Gear.Count;
                    else u.Pack.Items.Add(new Placed { Gear = Gears.CloneGear(p.Gear), X = p.X, Y = p.Y, Turned = p.Turned });
                }
            }
            foreach (var g in floor ?? new List<Gear>()) (s.FloorGear ??= new List<DroppedGear>()).Add(new DroppedGear { Gear = Gears.CloneGear(g), Pos = Events.CopyPos(u.Pos) });
            if (s.Run != null && next != null) s.Run.NextGear = Math.Max(s.Run.NextGear, next.Value);
            RunPack.SyncMags(u);
        }
    }
}
