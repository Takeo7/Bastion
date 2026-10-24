#nullable enable
// aim.ts: hit chance, the roll and the damage of an attack, and the Armería's weapon cards.
// Hit chance follows XCOM 2's X2AbilityToHitCalc_StandardAim:
// aim + weapon aim + range − defense − cover, reaction shots lose 30 %,
// flanking adds crit, crit and graze are carved out of the hit band.
// Melee ignores cover and range; squadsight shots lose aim beyond sight range.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public sealed class Modifier
    {
        public string Label { get; set; } = "";
        public int Value { get; set; }
    }

    public sealed class ShotPreview
    {
        /// <summary>Tile the attacker strikes from (own tile, step-out tile or melee position).</summary>
        public Pos Origin { get; set; } = new Pos();
        public int Hit { get; set; }
        public int Crit { get; set; }
        public int Graze { get; set; }
        public bool Flanked { get; set; }
        public int Cover { get; set; } // CoverLevel: 0–2.
        public double Distance { get; set; }
        public bool Squadsight { get; set; }
        public List<Modifier> Mods { get; set; } = new List<Modifier>();
        public List<Modifier> CritMods { get; set; } = new List<Modifier>();
    }

    public sealed class ShotOptions
    {
        public string? Ability { get; set; } // AttackAbilities
        public bool Reaction { get; set; }
        /// <summary>Evaluate as if the attacker stood here (AI planning, melee approach); without a level, on the ground.</summary>
        public Pos? From { get; set; }
    }

    /// <summary>resolveShot's `{ result: ShotResult; roll: number }`.</summary>
    public sealed class ShotRoll
    {
        public string Result { get; set; } = ""; // ShotResults
        public int Roll { get; set; }
    }

    public sealed class DamageRange
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    /// <summary>damageTaken's `{ total: number; mitigated: number }`.</summary>
    public sealed class DamageTaken
    {
        public int Total { get; set; }
        public int Mitigated { get; set; }
    }

    /// <summary>
    /// What an attack would take off, for the players to decide with: the health a
    /// hit, a critical and a graze remove (after the target's armour and Ruptura;
    /// with no target, the weapon's own numbers), and the armour.
    /// </summary>
    public sealed class DamagePreview
    {
        public DamageRange Hit { get; set; } = new DamageRange();
        /// <summary>Null for what never crits or grazes (explosives).</summary>
        public DamageRange? Crit { get; set; }
        public DamageRange? Graze { get; set; }
        /// <summary>Armour that soaks each hit, after what the attack pierces (0 without a target).</summary>
        public int Armor { get; set; }
        /// <summary>Armour each hit strips.</summary>
        public int Shred { get; set; }
    }

    /// <summary>shotDamageMods's `{ bonus; mult; shred; pierce; critBonus }`.</summary>
    public sealed class ShotDamageMods
    {
        public int Bonus { get; set; }
        public double Mult { get; set; }
        public int Shred { get; set; }
        public int Pierce { get; set; }
        public int CritBonus { get; set; }
    }

    /// <summary>A distance and the aim it gives (WeaponProfile.range).</summary>
    public sealed class RangeAim
    {
        public int Tiles { get; set; }
        public int Aim { get; set; }
    }

    /// <summary>What a weapon of the Armería does, to choose one (Arturo, 17/10: «para saber qué elegir»).</summary>
    public sealed class WeaponProfile
    {
        /// <summary>The Armería's kind; absent for what every soldier carries (the pistol, the knife: baseWeaponProfile).</summary>
        public string? Kind { get; set; } // FoundWeaponIds
        public string Name { get; set; } = "";
        public bool Melee { get; set; }
        /// <summary>Damage of a hit and of a critical one.</summary>
        public DamageRange Damage { get; set; } = new DamageRange();
        public DamageRange CritDamage { get; set; } = new DamageRange();
        /// <summary>Chance of a critical, of the weapon alone.</summary>
        public int Crit { get; set; }
        /// <summary>Aim the weapon itself adds (a scope, being worn).</summary>
        public int Aim { get; set; }
        /// <summary>Rounds in the magazine; 0, none needed.</summary>
        public int Clip { get; set; }
        /// <summary>Actions a shot takes.</summary>
        public int Actions { get; set; }
        /// <summary>How far, in tiles, a shot is heard.</summary>
        public int Noise { get; set; }
        /// <summary>Armour each hit takes off.</summary>
        public int Shred { get; set; }
        /// <summary>Where it shoots well: short, medium or long ('none' for a blade).</summary>
        public string Reach { get; set; } = ""; // RangeTableIds
        /// <summary>Aim the distance adds or takes, at a few distances (tiles).</summary>
        public List<RangeAim> Range { get; set; } = new List<RangeAim>();
    }

    public static class Aim
    {
        private static int Clamp(int v, int lo, int hi)
        {
            return Math.Max(lo, Math.Min(hi, v));
        }

        /// <summary>Bad luck (XCOM 2's aim assist): aim per miss in a row by a player's soldiers, counting up to two.</summary>
        public const int BadLuckAim = 10;
        public const int BadLuckMisses = 2;

        /// <summary>The bad-luck aim `u`'s next shot gets: only a player's own soldiers, never the aliens.</summary>
        public static int BadLuckAimOf(GameState s, Unit u)
        {
            if (u.Team != Teams.Xcom || u.Owner == null || !string.IsNullOrEmpty(u.ControlledBy)) return 0;
            return BadLuckAim * Math.Min(BadLuckMisses, Js.At(s.MissStreak, u.Owner.Value, 0));
        }

        /// <summary>Melee reach: the eight surrounding tiles.</summary>
        public static bool Adjacent(Vec2 a, Vec2 b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) == 1;
        }

        /// <summary>Adjacent and on the same level: a blade can't reach someone on the roof above, or on the storey above.</summary>
        public static bool InMeleeReach(GameState s, Pos a, Pos b)
        {
            return Adjacent(a, b) && Grid.HeightAt(s, a) == Grid.HeightAt(s, b);
        }

        /// <summary>Whether any living member of `team` can see `target`.</summary>
        public static bool TeamSees(GameState s, string team, Unit target)
        {
            return s.Units.Any((u) => Los.IsViewer(u, team) && Los.CanSee(s, u, target));
        }

        /// <summary>"Llave doble": a living soldier of the other player within 4 tiles of `pos`.</summary>
        public static bool PartnerNear(GameState s, Unit u, Pos pos)
        {
            if (u.Owner == null) return false;
            return s.Units.Any((o) => o.Alive && o.Team == u.Team && o.Owner != null && o.Owner != u.Owner && Grid.Distance(o.Pos, pos) <= 4 && Grid.SameSpace(s, o.Pos, pos));
        }

        /// <summary>Null when the attack is impossible (no line of sight, or not adjacent for melee).</summary>
        public static ShotPreview? PreviewShot(GameState s, Unit shooter, Unit target, ShotOptions? opts = null)
        {
            opts ??= new ShotOptions();
            var ability = opts.Ability ?? AbilityIds.Shoot;
            var from = opts.From != null ? Grid.ToPos(opts.From) : shooter.Pos;
            var st = Content.Templates[shooter.Template];
            var tt = Content.Templates[target.Template];
            var weapon = Content.AttackWeapon(shooter, ability);

            var def = Content.Abilities[ability];
            var primary = (def.Weapon ?? AbilityWeapons.Primary) == AbilityWeapons.Primary;
            Pos? origin;
            var squadsight = false;
            if (weapon.Melee)
            {
                origin = InMeleeReach(s, from, target.Pos) ? from : null;
            }
            else
            {
                origin = Los.SightOrigin(s, from, target.Pos, Los.SightTo(s, shooter, target.Pos));
                if (origin == null && primary && Content.HasSquadsight(shooter) && TeamSees(s, shooter.Team, target))
                {
                    origin = Los.SightOrigin(s, from, target.Pos, double.PositiveInfinity);
                    squadsight = origin != null;
                }
            }
            if (origin == null) return null;

            var dist = Grid.Distance(origin, target.Pos);
            var cover = weapon.Melee ? 0 : Cover.CoverAgainst(s, target.Pos, origin);
            var flanked = !weapon.Melee && cover == 0;

            var sm = shooter.Mods;
            var mods = new List<Modifier> { new Modifier { Label = "Puntería", Value = st.Aim + sm.Aim } };
            if (weapon.Aim != 0) mods.Add(new Modifier { Label = weapon.Melee ? "Arma cuerpo a cuerpo" : "Arma", Value = weapon.Aim });
            if (def.Aim.HasValue && def.Aim.Value != 0) mods.Add(new Modifier { Label = def.Name, Value = def.Aim.Value });
            if (weapon.Melee && sm.MeleeAim != 0) mods.Add(new Modifier { Label = "Cuerpo a cuerpo", Value = sm.MeleeAim });
            if (!weapon.Melee && sm.GunAim != 0) mods.Add(new Modifier { Label = "Tiro", Value = sm.GunAim });
            // The weapon in the hand (docs/FASE9.md §5): worn, a scope, a stock that steadies it on watch.
            if (Content.FiresPrimary(shooter, ability))
            {
                if (Content.ConditionOf(shooter) < Content.WornBelow) mods.Add(new Modifier { Label = "Gastada", Value = -Content.WornAim });
                if (Content.HasMod(shooter, WeaponModIds.Scope)) mods.Add(new Modifier { Label = "Mira", Value = Content.ModAim });
                if (opts.Reaction && Content.HasMod(shooter, WeaponModIds.Stock)) mods.Add(new Modifier { Label = "Culata", Value = Content.ModAim });
            }
            if (shooter.Disoriented != 0) mods.Add(new Modifier { Label = "Desorientado", Value = -Content.DisorientedAim });
            if (!string.IsNullOrEmpty(shooter.SuppressedBy))
            {
                var by = s.Units.Find((u) => u.Id == shooter.SuppressedBy);
                mods.Add(new Modifier { Label = "Suprimido", Value = -(Content.SuppressedAim + (by?.Mods.SuppressAim ?? 0)) });
            }
            if (target.Marked != 0 && shooter.Team != target.Team) mods.Add(new Modifier { Label = "Marcado", Value = target.Marked });
            var luck = BadLuckAimOf(s, shooter);
            if (luck != 0) mods.Add(new Modifier { Label = "Mala racha", Value = luck });
            if (!weapon.Melee)
            {
                var range = Content.RangeModifier(weapon.Range, dist);
                if (range != 0) mods.Add(new Modifier { Label = "Alcance", Value = range });
            }
            if (!weapon.Melee && Grid.HeightAt(s, origin) > Grid.HeightAt(s, target.Pos))
            {
                mods.Add(new Modifier { Label = "Ventaja de altura", Value = Content.HeightAdvantageAim });
                if (sm.HeightAim != 0) mods.Add(new Modifier { Label = "Tejado", Value = sm.HeightAim });
            }
            if (sm.PairAim != 0 && PartnerNear(s, shooter, from)) mods.Add(new Modifier { Label = "Codo con codo", Value = sm.PairAim });
            if (sm.SpotterAim != 0 && PartnerNear(s, shooter, target.Pos)) mods.Add(new Modifier { Label = "Fuego cruzado", Value = sm.SpotterAim });
            if (def.Weapon == AbilityWeapons.Secondary && sm.PistolAim != 0) mods.Add(new Modifier { Label = "Pistolero", Value = sm.PistolAim });
            var close = !weapon.Melee && dist <= 4;
            if (close && sm.CloseRangeAim != 0) mods.Add(new Modifier { Label = "Quemarropa", Value = sm.CloseRangeAim });
            if (squadsight)
            {
                var beyond = (int)Math.Max(0, Math.Floor(dist - Los.SightTo(s, shooter, target.Pos)));
                if (beyond != 0) mods.Add(new Modifier { Label = "Visión de escuadra", Value = Math.Min(0, Content.SquadsightDistanceMod + sm.SquadsightKeep) * beyond });
            }
            var defense = tt.Defense + target.Mods.Defense;
            if (defense != 0) mods.Add(new Modifier { Label = "Defensa", Value = -defense });
            if (target.Overwatch && target.Mods.WatchDefense != 0) mods.Add(new Modifier { Label = "Puesto fijo", Value = -target.Mods.WatchDefense });
            if (target.Mods.PairDefense != 0 && PartnerNear(s, target, target.Pos)) mods.Add(new Modifier { Label = "Parapeto", Value = -target.Mods.PairDefense });
            if (cover == 1) mods.Add(new Modifier { Label = "Cobertura baja", Value = -Content.LowCoverBonus });
            if (cover == 2) mods.Add(new Modifier { Label = "Cobertura alta", Value = -Content.HighCoverBonus });
            if (target.Hunkered) mods.Add(new Modifier { Label = "Cubriéndose", Value = -Content.HunkerDefense });
            if (target.Aided != 0) mods.Add(new Modifier { Label = "Atendido", Value = -target.Aided });
            if (!weapon.Melee && shooter.Mods.SmokeSight == 0 && s.Smoke.Any((c) => Grid.Distance(c.Pos, target.Pos) <= c.Radius && Grid.SameSpace(s, c.Pos, target.Pos))) mods.Add(new Modifier { Label = "Humo", Value = -Content.SmokeDefense });

            var hit = mods.Sum((m) => m.Value);
            if (opts.Reaction)
            {
                var penalty = -(int)Math.Floor(Math.Max(hit, 0) * Math.Max(0, Content.ReactionPenalty - sm.ReactionKeep / 100.0));
                if (penalty != 0) mods.Add(new Modifier { Label = "Fuego de reacción", Value = penalty });
                hit += penalty;
                if (target.Mods.ReactionDefense != 0)
                {
                    mods.Add(new Modifier { Label = "Reflejos", Value = -target.Mods.ReactionDefense });
                    hit -= target.Mods.ReactionDefense;
                }
            }
            hit = Clamp(hit, 0, 100);

            var critMods = new List<Modifier>();
            if (!opts.Reaction || Content.HasPerk(shooter, PerkIds.GuardCrit))
            {
                if (st.Crit != 0) critMods.Add(new Modifier { Label = "Base", Value = st.Crit });
                if (sm.Crit != 0) critMods.Add(new Modifier { Label = "Habilidad", Value = sm.Crit });
                if (weapon.Melee && sm.MeleeCrit != 0) critMods.Add(new Modifier { Label = "Golpe certero", Value = sm.MeleeCrit });
                if (!weapon.Melee && sm.GunCrit != 0) critMods.Add(new Modifier { Label = "Tiro", Value = sm.GunCrit });
                if (def.Weapon == AbilityWeapons.Secondary && sm.PistolCrit != 0) critMods.Add(new Modifier { Label = "Gatillo fino", Value = sm.PistolCrit });
                if (weapon.Crit != 0) critMods.Add(new Modifier { Label = "Arma", Value = weapon.Crit });
                if (close && sm.CloseRangeCrit != 0) critMods.Add(new Modifier { Label = "Quemarropa", Value = sm.CloseRangeCrit });
                if (flanked && st.FlankCrit != 0) critMods.Add(new Modifier { Label = "Flanqueo", Value = st.FlankCrit });
                if (squadsight) critMods.Add(new Modifier { Label = "Visión de escuadra", Value = Content.SquadsightCritMod });
            }
            var crit = Clamp(critMods.Sum((m) => m.Value), 0, 100);

            var dodge = tt.Dodge + target.Mods.Dodge + (target.Hunkered ? Content.HunkerDodge : 0);
            var serene = primary && !weapon.Melee && Content.HasPerk(shooter, PerkIds.Serenity);
            var graze = hit < 100 && !serene ? (int)Js.Round((dodge / 100.0) * hit) : 0;

            return new ShotPreview { Origin = origin, Hit = hit, Crit = crit, Graze = graze, Flanked = flanked, Cover = cover, Distance = dist, Squadsight = squadsight, Mods = mods, CritMods = critMods };
        }

        /// <summary>Single d100 roll; crit and graze bands sit inside the hit band.</summary>
        public static string RollShot(ShotPreview p, Rng rng)
        {
            return ResolveShot(p, rng).Result;
        }

        /// <summary>
        /// The same roll, with the die shown to the players: `roll` runs 1–100 and the
        /// shot hits when it is at most `p.hit` (a crit at most `p.crit`).
        /// </summary>
        public static ShotRoll ResolveShot(ShotPreview p, Rng rng)
        {
            var roll = rng.Int(100);
            var crit = Math.Min(p.Crit, p.Hit);
            var graze = Math.Min(p.Graze, p.Hit - crit);
            var result = roll >= p.Hit ? ShotResults.Miss : roll < crit ? ShotResults.Crit : roll < crit + graze ? ShotResults.Graze : ShotResults.Hit;
            return new ShotRoll { Result = result, Roll = roll + 1 };
        }

        public static int RollDamage(WeaponDef weapon, string result, Rng rng, int bonus = 0, double mult = 1, int critBonus = 0)
        {
            if (result == ShotResults.Miss) return 0;
            var spread = weapon.Spread != 0 ? rng.Range(-weapon.Spread, weapon.Spread) : 0;
            var plusOne = weapon.PlusOne != 0 && rng.Int(100) < weapon.PlusOne ? 1 : 0;
            return DamageOf(weapon, result, spread + plusOne, bonus, mult, critBonus);
        }

        /// <summary>A roll's damage once its dice (the spread and the +1) have come up `dice`.</summary>
        private static int DamageOf(WeaponDef weapon, string result, int dice, int bonus, double mult, int critBonus)
        {
            var dmg = weapon.Damage + bonus + dice;
            if (result == ShotResults.Crit) dmg += weapon.CritDamage + critBonus;
            if (mult != 1) dmg = (int)Math.Ceiling(dmg * mult);
            if (result == ShotResults.Graze) dmg = (int)Js.Round(dmg * Content.GrazeDamageMult);
            return Math.Max(1, dmg);
        }

        /// <summary>The lowest and the highest `rollDamage` can give for this result.</summary>
        public static DamageRange DamageRangeOf(WeaponDef weapon, string result, int bonus = 0, double mult = 1, int critBonus = 0)
        {
            return new DamageRange
            {
                Min = DamageOf(weapon, result, -weapon.Spread, bonus, mult, critBonus),
                Max = DamageOf(weapon, result, weapon.Spread + (weapon.PlusOne != 0 ? 1 : 0), bonus, mult, critBonus),
            };
        }

        /// <summary>
        /// What a hit of `raw` does to `u`: Ruptura adds to it, then armour (less what
        /// the attack pierces) soaks up to its value.
        /// </summary>
        public static DamageTaken DamageTakenBy(Unit u, int raw, int pierce = 0)
        {
            var total = raw + u.Ruptured;
            return new DamageTaken { Total = total, Mitigated = Math.Min(Math.Max(0, u.Armor - pierce), total) };
        }

        /// <summary>`range` as it lands on `target` (unchanged without one).</summary>
        public static DamageRange RangeOn(Unit? target, DamageRange range, int pierce = 0)
        {
            if (target == null) return range;
            int Dealt(int raw)
            {
                var d = DamageTakenBy(target, raw, pierce);
                return d.Total - d.Mitigated;
            }
            return new DamageRange { Min = Dealt(range.Min), Max = Dealt(range.Max) };
        }

        /// <summary>
        /// Damage of an attack (shots, blades, Cara a cara): `shot`
        /// gives the flank and the distance it is fired from; without it, as from the
        /// front and out of point-blank range, which is what the cards show.
        /// </summary>
        public static DamagePreview ShotDamage(Unit shooter, string ability, Unit? target = null, ShotPreview? shot = null)
        {
            var weapon = Content.AttackWeapon(shooter, ability);
            var dm = ShotDamageModsOf(shooter, ability, shot?.Flanked ?? false, shot?.Distance ?? double.PositiveInfinity);
            DamageRange Roll(string result) => RangeOn(target, DamageRangeOf(weapon, result, dm.Bonus, dm.Mult, dm.CritBonus), dm.Pierce);
            return new DamagePreview
            {
                Hit = Roll(ShotResults.Hit),
                Crit = Roll(ShotResults.Crit),
                Graze = Roll(ShotResults.Graze),
                Armor = target != null ? Math.Max(0, target.Armor - dm.Pierce) : 0,
                Shred = dm.Shred,
            };
        }

        /// <summary>Extra damage, multiplier and shred a shot gets from the ability and the shooter's talents.</summary>
        public static ShotDamageMods ShotDamageModsOf(Unit shooter, string ability, bool flanked, double dist = double.PositiveInfinity)
        {
            var def = Content.Abilities[ability];
            var weapon = Content.AttackWeapon(shooter, ability);
            var primary = (def.Weapon ?? AbilityWeapons.Primary) == AbilityWeapons.Primary;
            var m = shooter.Mods;
            var weaponBonus = weapon.Melee ? m.MeleeDamage : primary ? m.Damage : def.Weapon == AbilityWeapons.Secondary ? m.PistolDamage : 0;
            var close = !weapon.Melee && dist <= 4 ? m.CloseDamage : 0;
            var bonus = weaponBonus + (def.DamageBonus ?? 0) + (flanked ? m.FlankDamage : 0) + close;
            var gun = primary && !weapon.Melee;
            return new ShotDamageMods { Bonus = bonus, Mult = def.DamageMult ?? 1, Shred = weapon.Shred + (gun ? m.Shred : 0), Pierce = gun ? m.Pierce : 0, CritBonus = m.CritDamage };
        }

        public static WeaponDef WeaponOf(Unit u)
        {
            return Content.AttackWeapon(u, AbilityIds.Shoot);
        }

        // ------------------------------------------------------------- the Armería

        /// <summary>The distances a weapon's card shows its aim at.</summary>
        private static readonly IReadOnlyList<int> ProfileTiles = new[] { 2, 5, 8, 12, 16 };

        /// <summary>
        /// A weapon's card for the Armería: `condition` and `mods` as the weapon is
        /// (a worn one aims worse, a scope better, a silencer halves the noise, an
        /// extended magazine holds more); without them, as found, with the mod a good
        /// find comes with.
        /// </summary>
        public static WeaponProfile WeaponProfileOf(string kind, double condition = 100, IReadOnlyList<string>? mods = null)
        {
            var def = Content.FoundWeapons[kind];
            mods ??= def.Mod != null ? new[] { def.Mod } : new string[0];
            var profile = ProfileOf(Content.Weapons[def.Weapon], def.Name, condition, mods);
            profile.Kind = kind;
            return profile;
        }

        /// <summary>The card of what every soldier carries without one of the Armería's: the pistol (`pistol`) or the knife (`knife`).</summary>
        public static WeaponProfile BaseWeaponProfile(string weapon, string? name = null)
        {
            return ProfileOf(Content.Weapons[weapon], name ?? Content.Weapons[weapon].Name, 100, new string[0]);
        }

        private static WeaponProfile ProfileOf(WeaponDef w, string name, double condition, IReadOnlyList<string> mods)
        {
            var gun = !w.Melee;
            return new WeaponProfile
            {
                Name = name,
                Melee = w.Melee,
                Damage = DamageRangeOf(w, ShotResults.Hit),
                CritDamage = DamageRangeOf(w, ShotResults.Crit),
                Crit = w.Crit,
                Aim = w.Aim + (gun && mods.Contains(WeaponModIds.Scope) ? Content.ModAim : 0) - (gun && condition < Content.WornBelow ? Content.WornAim : 0),
                Clip = w.Clip + (gun && w.Clip > 0 && mods.Contains(WeaponModIds.ExtMag) ? Content.ExtMag : 0),
                Actions = w.ApCost,
                Noise = gun && mods.Contains(WeaponModIds.Silencer) ? (int)Math.Ceiling(w.Noise / 2.0) : w.Noise,
                Shred = w.Shred,
                Reach = w.Range,
                Range = w.Melee ? new List<RangeAim>() : ProfileTiles.Select((tiles) => new RangeAim { Tiles = tiles, Aim = Content.RangeModifier(w.Range, tiles) }).ToList(),
            };
        }
    }
}
