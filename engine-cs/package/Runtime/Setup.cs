#nullable enable
// setup.ts: making a mission (createMission): its map (mapgen.ts, maps.ts or base.ts), the squad, the dead and their
// plans, the objective, the evacuation and the night's lights.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>A campaign soldier going into a mission, with everything already computed.</summary>
    public sealed class SquadMember
    {
        public string CampaignId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Nickname { get; set; }
        public Appearance? Appearance { get; set; }
        public int? WeaponTier { get; set; }
        public int? ArmorTier { get; set; }
        /// <summary>A found weapon's name, when the soldier carries one (docs/FASE4.md).</summary>
        public string? WeaponName { get; set; }
        public int Owner { get; set; } // Slot
        /// <summary>The class, or 'rookie' for a recruit with no class yet.</summary>
        public string Template { get; set; } = ""; // TemplateIds
        public int Rank { get; set; }
        public List<string> Perks { get; set; } = new List<string>(); // PerkIds
        public UnitMods Mods { get; set; } = new UnitMods();
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public Dictionary<string, int> Charges { get; set; } = new Dictionary<string, int>(); // ChargeIds → uses (Partial<Record>)
        public int Will { get; set; }
        public int Armor { get; set; }
        public List<string> Gear { get; set; } = new List<string>(); // ItemIds
        /// <summary>Backpack slots, when not BACKPACK_SLOTS (the Chapistería's one more, docs/FASE8.md §C).</summary>
        public int? PackSlots { get; set; }
        /// <summary>Its own weapons (docs/FASE8.md §A); absent, its class's.</summary>
        public Arms? Arms { get; set; }
        /// <summary>Spare magazines it sets out with (the Taller's munition); absent, SPARE_MAGS.</summary>
        public int? Mags { get; set; }
        /// <summary>Its experience lines, to fill out there (docs/FASE10.md §2).</summary>
        public MissionLines? Lines { get; set; }
    }

    /// <summary>MapSource's `kind`.</summary>
    public static class MapSourceKinds
    {
        public const string Handmade = "handmade";
        public const string Generated = "generated";
        public const string Base = "base";

        public static readonly IReadOnlyList<string> All = new[] { Handmade, Generated, Base };
    }

    /// <summary>
    /// Hand-made map by id, or a procedural one from a biome and a seed. `site`: a scenario other than the biome's usual one
    /// (playtest 2026-10-14, #25). `base`: the campaign's base, for its siege (docs/FASE12.md §11).
    ///   | { kind: 'handmade'; id: string }
    ///   | { kind: 'generated'; biome: Biome; seed: number; site?: Site; medical?: 'clinic' | 'ward' | 'hospital' }
    ///   | { kind: 'base'; base: BaseState }
    /// </summary>
    public sealed class MapSource
    {
        public string Kind { get; set; } = MapSourceKinds.Generated; // MapSourceKinds
        /// <summary>A hand-made map's (maps.ts MAPS).</summary>
        public string? Id { get; set; }
        public string? Biome { get; set; } // Biomes
        /// <summary>A generated map's; missing on the others (a campaign's siege offer is `{ kind: 'handmade', id: 'bastion' }`).</summary>
        public int? Seed { get; set; }
        public string? Site { get; set; } // Sites
        public string? Medical { get; set; } // MedicalSiteKinds
        public BaseState? Base { get; set; }

        public static MapSource Handmade(string id)
        {
            return new MapSource { Kind = MapSourceKinds.Handmade, Id = id };
        }

        public static MapSource Generated(string biome, int seed, string? site = null, string? medical = null)
        {
            return new MapSource { Kind = MapSourceKinds.Generated, Biome = biome, Seed = seed, Site = site, Medical = medical };
        }

        public static MapSource OfBase(BaseState @base)
        {
            return new MapSource { Kind = MapSourceKinds.Base, Base = @base };
        }
    }

    /// <summary>MissionOptions.leader: `{ template: TemplateId; name: string }`.</summary>
    public sealed class MissionLeader
    {
        public string Template { get; set; } = ""; // TemplateIds
        public string Name { get; set; } = "";
    }

    /// <summary>MissionOptions.siegePlan: `{ count: number; sides: number[] }`.</summary>
    public sealed class SiegePlan
    {
        public int Count { get; set; }
        public List<int> Sides { get; set; } = new List<int>();
    }

    public sealed class MissionOptions
    {
        public MapSource Map { get; set; } = new MapSource();
        /// <summary>Seats taking part; soldiers are split evenly between them.</summary>
        public List<int> Slots { get; set; } = new List<int>();
        public Rng Rng { get; set; } = new Rng(0);
        public string? Kind { get; set; } // MissionKinds
        public int? SquadSize { get; set; }
        /// <summary>Campaign squad; when absent a default squad is generated (skirmish).</summary>
        public List<SquadMember>? Squad { get; set; }
        /// <summary>Alien pods in order; defaults to the map's pods or a plan for the difficulty.</summary>
        public List<List<string>>? Pods { get; set; } // TemplateIds
        /// <summary>1 (easy) to 4 (hard): default pods and reinforcements.</summary>
        public int? Difficulty { get; set; }
        /// <summary>Objective timer override.</summary>
        public int? Turns { get; set; }
        /// <summary>Added to every hack attempt (campaign research).</summary>
        public int? HackBonus { get; set; }
        /// <summary>Skirmish: soldiers start as sergeants to captains with random abilities.</summary>
        public bool? Veterans { get; set; }
        public CampaignMission? CampaignMission { get; set; }
        /// <summary>Rescue: who waits at the objective (story chapters, side quests); a random name otherwise.</summary>
        public string? VipName { get; set; }
        /// <summary>A sabotage's alarm (playtest 2026-10-15, #14), and what the objective is called.</summary>
        public string? Alarm { get; set; } // Alarms
        public string? ObjectiveName { get; set; }
        /// <summary>Names the first enemy of this kind (a side quest's leader).</summary>
        public MissionLeader? Leader { get; set; }
        /// <summary>A night mission (docs/ALTURA-Y-NOCHE.md): one more group of the dead, and the living see less.</summary>
        public bool? Night { get; set; }
        /// <summary>Played in real time (docs/TIEMPO-REAL.md): the host's clock runs it from a pause at the start.</summary>
        public bool? Realtime { get; set; }
        /// <summary>The living hold it (docs/ZOMBIS-PRIMERO.md): their bands and their garrison; without it, the dead's.</summary>
        public bool? Living { get; set; }
        /// <summary>A recovery whose story puts the item outdoors (playtest 2026-10-14, #27).</summary>
        public bool? ItemOutdoors { get; set; }
        /// <summary>Medicines also stop a bite's contagion, for everyone (docs/FASE8.md §C).</summary>
        public bool? MedicineCures { get; set; }
        /// <summary>The campaign's Bastion has a Cuarentena (#13): false, the bitten can't board La Lata.</summary>
        public bool? Quarantine { get; set; }
        /// <summary>Still winter in the campaign (campaign.ts wintry): the map under snow.</summary>
        public bool? Snow { get; set; }
        /// <summary>A campaign's siege (docs/FASE12.md §11.5): how many of the dead come in all, and by which sides; without it, the test siege's waves.</summary>
        public SiegePlan? SiegePlan { get; set; }
        /// <summary>La Lata's vehicle in a campaign (campaign.ts VEHICLE_SEATS: 0 todoterreno … 3 autobús); the bus without it.</summary>
        public int? Vehicle { get; set; }
        /// <summary>Where defenders stand when a siege begins (campaign.ts siegePosts, by campaignId): the rest, at the spawns.</summary>
        public Dictionary<string, Vec2>? Posts { get; set; }
    }

    public static class Setup
    {
        /// <summary>What a skirmish soldier carries in the hand (docs/FASE9.md §6): one of these, at random.</summary>
        private static readonly IReadOnlyList<string> SkirmishArms = new[] { FoundWeaponIds.AssaultRifle, FoundWeaponIds.PumpShotgun, FoundWeaponIds.PrecisionRifle, FoundWeaponIds.MachineGun };
        /// <summary>And on the belt: grenades, and one of these.</summary>
        private static readonly IReadOnlyList<string> SkirmishBelt = new[] { ItemIds.FirstAid, ItemIds.SmokeGrenade, ItemIds.Flashbang };

        /// <summary>A skirmish soldier (docs/FASE10.md §7): a weapon and gear at random; a veteran, bars filled in every line and its board.</summary>
        private static (FighterStats Stats, Arms Arms, List<string> Gear, MissionLines Lines) SkirmishSoldier(Rng rng, bool veteran)
        {
            var primary = rng.Pick(SkirmishArms);
            var arms = new Arms { Primary = primary, Melee = rng.Next() < 0.5 ? FoundWeaponIds.Machete : null };
            var gear = new List<string> { ItemIds.FragGrenade, rng.Pick(SkirmishBelt) };
            var lines = veteran ? Bars.VeteranSkills(rng) : Bars.RecruitSkills(rng);
            var board = veteran ? (IReadOnlyList<string>)lines.Board! : new string[0];
            return (Bars.FighterStatsOf(lines.Skills, lines.Growth, board, gear, arms), arms, gear, Bars.MissionLinesOf(lines.Skills, lines.Growth));
        }

        /// <summary>Whether a unit counts its spare magazines (docs/FASE7.md): the squad's, with a weapon that takes them.</summary>
        private static bool CountsMags(string team, string weapon)
        {
            return team == Teams.Xcom && Content.Weapons[weapon].Clip > 0;
        }

        private static Arms CopyArms(Arms a)
        {
            return new Arms
            {
                Primary = a.Primary,
                PrimaryId = a.PrimaryId,
                Condition = a.Condition,
                Mods = a.Mods != null ? new List<string>(a.Mods) : null,
                Melee = a.Melee,
                MeleeId = a.MeleeId,
            };
        }

        /// <summary>
        /// A soldier's own weapons, loaded, and its spare magazines (docs/FASE8.md §A): TypeScript's `armed(u, m)`, a
        /// Partial&lt;Unit&gt; worked out on `u` as makeUnit gave it, spread over the unit being made.
        /// </summary>
        private static Action<Unit> Armed(Unit u, Arms? marms, int? mags)
        {
            if (marms == null)
            {
                if (mags != null && u.Mags != null) return (unit) => unit.Mags = mags;
                return (unit) => { };
            }
            var arms = CopyArms(marms);
            var withArms = Events.CloneUnit(u);
            withArms.Arms = arms;
            var ammo = Content.ClipOf(withArms, AbilityIds.Shoot);
            var primary = !string.IsNullOrEmpty(arms.Primary);
            return (unit) =>
            {
                unit.Arms = arms;
                unit.Ammo = ammo;
                if (primary) unit.PistolAmmo = Content.PistolClip;
                unit.Mags = mags ?? 0;
            };
        }

        /// <summary>A unit of a kind, on `pos` (on the ground unless it says its level).</summary>
        public static Unit MakeUnit(string id, string template, string name, int? owner, Pos pos, string? podId)
        {
            var t = Content.Templates[template];
            var u = new Unit
            {
                Id = id,
                Team = t.Team,
                Template = template,
                Name = name,
                Owner = owner,
                Pos = Grid.ToPos(pos),
                Hp = t.Hp,
                MaxHp = t.Hp,
                Armor = t.Armor,
                Ap = t.Team == Teams.Xcom ? Content.ApPerTurn : 0,
                Ammo = Content.Weapons[t.Weapon].Clip,
                Charges = new Dictionary<string, int>(t.Charges),
                Overwatch = false,
                Hunkered = false,
                Aided = 0,
                Stunned = false,
                Alive = true,
                Evacuated = false,
                PodId = podId,
                Rank = 0,
                Perks = new List<string>(),
                Mods = Content.ZeroMods,
                Kills = 0,
                CampaignId = null,
                Objective = false,
                Captive = false,
                Will = t.Will,
                Cooldowns = new Dictionary<string, int>(),
                KillZone = false,
                ReactedTo = new List<string>(),
                Suppressing = null,
                SuppressedBy = null,
                Marked = 0,
                Ruptured = 0,
                Disoriented = 0,
                Panicked = false,
                ControlledBy = null,
                ControlTurns = 0,
                Reanimated = false,
                Refunded = false,
                Gear = new List<string>(),
            };
            // The enemies look one way from the start (los.ts inViewCone): one of eight, from where they stand, no dice spent.
            if (t.Team != Teams.Xcom) u.Facing = (((pos.X * 7 + pos.Y * 13 + id.Length) % 8) * Math.PI) / 4;
            if (CountsMags(t.Team, t.Weapon)) u.Mags = Content.SpareMags;
            return u;
        }

        public static readonly IReadOnlyDictionary<string, string> MissionNames = new Dictionary<string, string>
        {
            [MissionKinds.Elimination] = "Limpieza",
            [MissionKinds.Recovery] = "Botín",
            [MissionKinds.Sabotage] = "Sabotaje",
            [MissionKinds.Hack] = "Arreglar la radio",
            [MissionKinds.Rescue] = "Rescate",
            [MissionKinds.Scavenge] = "Saqueo",
        };

        /// <summary>How a night mission is labelled (placeholder for Lore).</summary>
        public const string NightLabel = "De noche";

        /// <summary>The extra group of the dead a night mission starts with (NIGHT in content.ts).</summary>
        public static List<string> NightPod(int difficulty)
        {
            // Twice as many as before the playtest of 2026-10-14 (#26): the night is theirs.
            return difficulty >= 3
                ? new List<string> { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid, TemplateIds.Xenoid }
                : new List<string> { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie };
        }

        public static readonly IReadOnlyDictionary<string, string> BiomeNames = new Dictionary<string, string>
        {
            [Biomes.City] = "Ciudad",
            [Biomes.Wilds] = "Afueras",
            [Biomes.Facility] = "Fábrica",
        };

        /// <summary>XCOM turns to secure each objective, tuned on 36×36 maps (see `missionTurns`).</summary>
        public static readonly IReadOnlyDictionary<string, int> MissionTurnsByKind = new Dictionary<string, int>
        {
            [MissionKinds.Recovery] = Content.RecoveryTurns,
            [MissionKinds.Sabotage] = 10,
            [MissionKinds.Hack] = 10,
            [MissionKinds.Rescue] = 14,
        };

        /// <summary>
        /// Turns to secure the objective on a map `size` tiles across: on bigger maps
        /// the objective lies further away, so every 6 tiles beyond 36 add a turn, half
        /// as much again when the data or the VIP has to be brought back to the evac.
        /// </summary>
        public static int? MissionTurns(string kind, double size)
        {
            if (!MissionTurnsByKind.TryGetValue(kind, out var @base)) return null;
            var extra = Math.Max(0, (size - 36) / 6);
            return @base + (int)Js.Round(kind == MissionKinds.Recovery || kind == MissionKinds.Rescue ? extra * 1.5 : extra);
        }

        /// <summary>The walk a generated map of 48 takes, today (the median of MissionLayout.route over its kinds, 19/10).</summary>
        private const int Route48 = 83;

        /// <summary>
        /// Turns by the walk the map's mission takes (docs/MAPAS.md §6: from the squad to the objective and on to the
        /// evacuation), as missionTurns gives a map whose walk is as long: a 48 map's at ROUTE_48, a 36's at three quarters
        /// of it. A bigger map whose walk is no longer gets no more turns.
        /// </summary>
        public static int? RouteTurns(string kind, int route)
        {
            return MissionTurns(kind, 36 + 48 * ((double)route / Route48 - 0.75));
        }

        /// <summary>How long La Lata is at each stage (campaign.ts VEHICLE_SEATS: the 4×4, the van, the lorry, the bus), in tiles, as Arte draws it.</summary>
        private static readonly IReadOnlyList<int> VehicleLength = new[] { 3, 4, 6, 9 };

        /// <summary>
        /// The place the map keeps for the bus cut to La Lata's length (Arte, 20/10): its nose where it was, what is behind
        /// it ground again. Never longer than the place.
        /// </summary>
        private static void FitVehicle(MissionLayout layout, int vehicle)
        {
            var bus = layout.EvacBus;
            if (bus == null) return;
            var d = Grid.Dirs4[bus.Heading];
            int Along(Vec2 p) => p.X * d.X + p.Y * d.Y;
            var nose = bus.Tiles.Count > 0 ? bus.Tiles.Max(Along) : double.NegativeInfinity;
            var length = VehicleLength[Math.Max(0, Math.Min(VehicleLength.Count - 1, vehicle))];
            var kept = bus.Tiles.Where((p) => Along(p) > nose - length).ToList();
            foreach (var p in bus.Tiles)
            {
                if (kept.Contains(p)) continue;
                layout.Tiles[p.Y * layout.Width + p.X] = TileKinds.Floor;
                if (layout.Decor != null) layout.Decor[p.Y * layout.Width + p.X] = 0;
            }
            bus.Tiles = kept;
        }

        /// <summary>The side of the map a source makes (a generated town is bigger: mapgen.ts generatedSize), for its groups of the dead.</summary>
        public static int SideOf(MapSource map)
        {
            if (map.Kind != MapSourceKinds.Generated) return Mapgen.GenSize;
            var (width, height) = Mapgen.GeneratedSize(map.Biome!, map.Site, map.Medical == MedicalSiteKinds.Hospital);
            return Math.Max(width, height);
        }

        /// <summary>How many more of the dead a map bigger than GEN_SIZE has, besides those its size brings (scalePods).</summary>
        public const double BigMapDead = 1.25;

        /// <summary>As many groups as the map's side is longer than 48 (docs/MAPAS.md §6), and a quarter more on a big one: 13 at 64 and 15 at 72 at difficulty 2, the first ones again.</summary>
        public static List<List<string>> ScalePods(IReadOnlyList<IReadOnlyList<string>> plan, int side)
        {
            // On the big maps a quarter more besides (Ana and Arturo, 20/10: since the maps grew it had got easier).
            var n = (int)Js.Round(((double)(plan.Count * side) / Mapgen.GenSize) * (side > Mapgen.GenSize ? BigMapDead : 1));
            var out_ = new List<List<string>>();
            for (var i = 0; i < Math.Max(plan.Count, n); i++) out_.Add(new List<string>(plan[i % plan.Count]));
            return out_;
        }

        /// <summary>A plan's groups for the map they go to (scalePods): what a campaign's operation brings when it is launched.</summary>
        public static List<List<string>> PodsFor(MapSource map, List<List<string>> plan)
        {
            return map.Kind == MapSourceKinds.Generated && plan.Count > 0 ? ScalePods(plan, SideOf(map)) : plan;
        }

        private static List<List<string>> Groups(params string[][] groups)
        {
            return groups.Select((g) => g.ToList()).ToList();
        }

        /// <summary>
        /// The dead by difficulty for procedural maps (docs/ZOMBIS-PRIMERO.md): every
        /// mission is the dead's, but those where the living hold something
        /// (livingPlan). Three times as many as at first (playtest 2026-10-14, #26:
        /// going into town has to be a risk): about 20 at difficulty 1 and 45 at 4, in
        /// groups of three to five spread over the map, so the streets are full of them.
        /// </summary>
        public static List<List<string>> PodPlan(int difficulty)
        {
            const string z = TemplateIds.Zombie;
            const string xenoid = TemplateIds.Xenoid;
            const string bloater = TemplateIds.Bloater;
            const string sectoid = TemplateIds.Sectoid;
            const string mec = TemplateIds.Mec;
            switch (Math.Max(1, Math.Min(4, difficulty)))
            {
                case 1:
                    return Groups(new[] { z, z, z }, new[] { z, z, z }, new[] { z, z, z, bloater }, new[] { z, z, z }, new[] { z, z, z, z }, new[] { z, z, z });
                case 2:
                    return Groups(new[] { z, z, xenoid }, new[] { sectoid, z, z }, new[] { z, z, z, bloater }, new[] { z, xenoid, z, z }, new[] { z, z, z }, new[] { z, z, z, z }, new[] { z, xenoid, z }, new[] { z, z, z, bloater });
                case 3:
                    return Groups(
                        new[] { z, z, z, xenoid },
                        new[] { mec, z, z, z },
                        new[] { sectoid, z, xenoid, z },
                        new[] { z, z, z, bloater },
                        new[] { z, z, xenoid, z },
                        new[] { z, z, z, z },
                        new[] { mec, z, z },
                        new[] { z, z, z, bloater },
                        new[] { z, xenoid, z, z });
                default:
                    return Groups(
                        new[] { mec, z, z, z, z },
                        new[] { sectoid, xenoid, z, z },
                        new[] { xenoid, xenoid, z, z },
                        new[] { bloater, z, z, z, z },
                        new[] { mec, xenoid, z, z },
                        new[] { z, z, z, z, z },
                        new[] { sectoid, z, z, z },
                        new[] { bloater, z, z, z, xenoid },
                        new[] { mec, z, z, z },
                        new[] { z, z, xenoid, z, z });
            }
        }

        /// <summary>
        /// Bands of the living (the Cuervos, the Congregación) by difficulty: only in
        /// the missions that are theirs (docs/ZOMBIS-PRIMERO.md), two of them with two
        /// groups of the dead about (missionPods).
        /// </summary>
        public static List<List<string>> LivingPlan(int difficulty)
        {
            const string trooper = TemplateIds.Trooper;
            const string officer = TemplateIds.Officer;
            const string lancer = TemplateIds.Lancer;
            switch (Math.Max(1, Math.Min(4, difficulty)))
            {
                case 1:
                    return Groups(new[] { trooper, trooper }, new[] { trooper, officer }, new[] { trooper, trooper, lancer });
                case 2:
                    return Groups(new[] { trooper, lancer }, new[] { trooper, officer }, new[] { trooper, trooper, lancer });
                case 3:
                    return Groups(new[] { officer, lancer }, new[] { officer, trooper, trooper }, new[] { lancer, trooper, trooper });
                default:
                    return Groups(new[] { officer, lancer, trooper }, new[] { officer, trooper, trooper }, new[] { lancer, officer });
            }
        }

        /// <summary>
        /// The enemy groups of a procedural mission (docs/ZOMBIS-PRIMERO.md): a
        /// Saqueo's dead scattered about; a mission of the living, two of their bands
        /// and two groups of the dead; any other, the dead.
        /// </summary>
        public static List<List<string>> MissionPods(int difficulty, string kind, bool living = false)
        {
            if (kind == MissionKinds.Scavenge) return ScavengePlan(difficulty);
            return living ? LivingPlan(difficulty).Take(2).Concat(PodPlan(difficulty).Take(2)).ToList() : PodPlan(difficulty);
        }

        /// <summary>
        /// Saqueo: the dead scattered about the town in small groups, unaware. The
        /// horde comes later, by the edges (HORDE_WAVES).
        /// </summary>
        public static List<List<string>> ScavengePlan(int difficulty)
        {
            // Three times as many as at first (playtest 2026-10-14, #26), in small groups all over the town.
            const string z = TemplateIds.Zombie;
            const string xenoid = TemplateIds.Xenoid;
            const string bloater = TemplateIds.Bloater;
            const string sectoid = TemplateIds.Sectoid;
            const string mec = TemplateIds.Mec;
            switch (Math.Max(1, Math.Min(4, difficulty)))
            {
                case 1:
                    return Groups(new[] { z, z }, new[] { z, z }, new[] { z, z, z }, new[] { xenoid, z }, new[] { z, bloater, z }, new[] { z, z }, new[] { z, z, z }, new[] { z, z }, new[] { z, z, z }, new[] { z, xenoid });
                case 2:
                    return Groups(new[] { z, z, z }, new[] { z, xenoid, z }, new[] { z, z, bloater }, new[] { sectoid, z, z }, new[] { z, z, z }, new[] { z, z }, new[] { z, xenoid, z }, new[] { z, z, z, bloater }, new[] { z, z, z }, new[] { z, z, z });
                case 3:
                    return Groups(
                        new[] { z, z, xenoid },
                        new[] { mec, z, z },
                        new[] { z, z, bloater },
                        new[] { sectoid, z, z, z },
                        new[] { xenoid, z, z },
                        new[] { z, z, z, z },
                        new[] { z, xenoid, z },
                        new[] { z, z, z, bloater },
                        new[] { mec, z, z },
                        new[] { z, z, z, z });
                default:
                    return Groups(
                        new[] { z, z, z, xenoid },
                        new[] { mec, z, z, z },
                        new[] { bloater, z, z, z },
                        new[] { sectoid, z, xenoid, z },
                        new[] { mec, xenoid, z },
                        new[] { z, z, z, bloater },
                        new[] { xenoid, z, z, z },
                        new[] { z, z, z, z, z },
                        new[] { sectoid, z, z },
                        new[] { mec, z, z, z },
                        new[] { z, xenoid, z, z });
            }
        }

        /// <summary>
        /// Saqueo (docs/FASE2.md): who comes in each wave of the horde, by difficulty;
        /// the horde comes later, by the edges (HORDE_WAVES).
        /// </summary>
        private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> HordeWaves = new Dictionary<int, IReadOnlyList<string>>
        {
            [1] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie },
            [2] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid, TemplateIds.Xenoid },
            [3] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid, TemplateIds.Xenoid, TemplateIds.Bloater, TemplateIds.Zombie },
            [4] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid, TemplateIds.Xenoid, TemplateIds.Mec, TemplateIds.Bloater },
        };

        public static List<string> HordeWave(int difficulty)
        {
            return new List<string>(HordeWaves[Math.Max(1, Math.Min(4, difficulty))]);
        }

        /// <summary>
        /// Nobody drops in out of nowhere (playtest 2026-10-13): one more group on the
        /// map from the start, in the missions that had one (elimination from
        /// difficulty 3 on, and every mission with an objective but the Saqueo). The
        /// living's when the mission is theirs, the dead's round the objective
        /// otherwise (docs/ZOMBIS-PRIMERO.md).
        /// </summary>
        private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> GarrisonOf = new Dictionary<int, IReadOnlyList<string>>
        {
            [1] = new[] { TemplateIds.Trooper, TemplateIds.Trooper },
            [2] = new[] { TemplateIds.Trooper, TemplateIds.Lancer },
            [3] = new[] { TemplateIds.Officer, TemplateIds.Trooper, TemplateIds.Lancer },
            [4] = new[] { TemplateIds.Officer, TemplateIds.Lancer, TemplateIds.Trooper },
        };
        private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> DeadGarrison = new Dictionary<int, IReadOnlyList<string>>
        {
            [1] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie },
            [2] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid },
            [3] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid, TemplateIds.Bloater },
            [4] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Mec, TemplateIds.Xenoid },
        };

        public static List<string>? Garrison(string kind, int difficulty, bool living = false)
        {
            if (kind == MissionKinds.Scavenge || (kind == MissionKinds.Elimination && difficulty < 3)) return null;
            return new List<string>((living ? GarrisonOf : DeadGarrison)[Math.Max(1, Math.Min(4, difficulty))]);
        }

        /// <summary>A siege's wave comes in groups of at most this many, so that each finds room by its edge.</summary>
        private const int SiegeGroup = 5;

        /// <summary>
        /// A siege's waves (docs/FASE12.md, hito 1): five, more each time, each with its
        /// own: a Corredor, a Hinchado, an Acorazado, and a Chillón with the last. They
        /// overlap (Ana, 23/10: «que lleguen de todos lados y a la vez… no ha sido un
        /// asedio, ha sido esperar a que vengan»): one at the end of each of the first
        /// enemy turns, with a breath between the third and the fourth, and each one's
        /// groups spread over every side the siege comes by at once.
        /// </summary>
        public static List<Reinforcement> SiegeWaves(int difficulty, Rng rng, SiegePlan? plan = null)
        {
            var d = Math.Max(1, Math.Min(4, difficulty));
            List<string> Zombies(int n) => Enumerable.Repeat(TemplateIds.Zombie, n).ToList();
            // A campaign's siege brings its own count, the same five specials and the zombies for the rest, spread over
            // the five waves (more each time; what the rounding leaves, to the waves it shortchanged most).
            var share = new[] { 0.14, 0.18, 0.2, 0.22, 0.26 };
            var total = plan != null ? Math.Max(share.Length, plan.Count - 5) : 0;
            var counts = share.Select((s) => Math.Max(1, (int)Math.Floor(total * s))).ToArray();
            var order = share.Select((s, k) => k).ToList();
            Js.Sort(order, (a, b) =>
            {
                var by = (total * share[b]) % 1 - (total * share[a]) % 1;
                return by != 0 && !double.IsNaN(by) ? by : b - a;
            });
            foreach (var k in order)
            {
                if (counts.Sum() >= total) break;
                counts[k]++;
            }
            int Many(int k, int test) => plan != null ? counts[k] : test;
            var waves = new List<(int Turn, List<string> Units)>
            {
                (1, Zombies(Many(0, 3 + d))),
                (2, Zombies(Many(1, 4 + d)).Concat(new[] { TemplateIds.Xenoid }).ToList()),
                (3, Zombies(Many(2, 4 + d)).Concat(new[] { TemplateIds.Bloater }).ToList()),
                (5, Zombies(Many(3, 5 + d)).Concat(new[] { TemplateIds.Mec }).ToList()),
                (6, Zombies(Many(4, 5 + d)).Concat(new[] { TemplateIds.Xenoid, TemplateIds.Sectoid }).ToList()),
            };
            // By the sides the plan says (what the Sala de radio hears), or any of the four.
            var sides = plan != null && plan.Sides.Count > 0 ? plan.Sides : new List<int> { 0, 1, 2, 3 };
            // Each side the plan names sees someone come by it: its first groups go one to each, in a random order.
            var first = plan != null ? new List<int>(sides) : new List<int>();
            for (var i = first.Count - 1; i > 0; i--)
            {
                var j = rng.Int(i + 1);
                var t = first[i];
                first[i] = first[j];
                first[j] = t;
            }
            var out_ = new List<Reinforcement>();
            var next = rng.Int(sides.Count);
            foreach (var w in waves)
            {
                // Over every side at once: as many groups as sides (fewer for a wave too small to split), dealt round them.
                var groups = Math.Max(1, Math.Min(Math.Min(sides.Count, (int)Math.Ceiling(w.Units.Count / 2.0)), w.Units.Count));
                var size = Math.Min(SiegeGroup, (int)Math.Ceiling((double)w.Units.Count / groups));
                for (var i = 0; i < w.Units.Count; i += size)
                {
                    var side = out_.Count < first.Count ? first[out_.Count] : sides[next++ % sides.Count];
                    out_.Add(new Reinforcement { Id = $"r{out_.Count + 1}", Turn = w.Turn, Pos = null, Units = w.Units.Skip(i).Take(size).ToList(), Landed = false, Horde = true, Side = side });
                }
            }
            return out_;
        }

        /// <summary>The pressure on the clock of sabotage and hack: zombies drawn in by the edges, at the end of these enemy turns.</summary>
        private static List<int> HordeTurns(string kind)
        {
            return kind == MissionKinds.Sabotage || kind == MissionKinds.Hack ? new List<int> { 4, 8 } : new List<int>();
        }

        /// <summary>Who a rescue frees, when it isn't a story's or a quest's (their sex is in lore.ts NAMED_SEX).</summary>
        /// <remarks>Who an ordinary rescue is for; the campaign picks one when it offers the operation (MissionOffer.vip).</remarks>
        public static readonly IReadOnlyList<string> VipNames = new[] { "Dra. Irina Sokolova", "Ing. Kenji Mori", "Alcaldesa Mara Velasco", "Dr. Femi Adebayo", "Agente Claire Dubois" };

        /// <summary>
        /// The battlefield a kind asks the generator for, when it isn't its own: a
        /// Saqueo is laid out like a recovery (houses, shops and offices toward the
        /// evac), and the recovery's item is never placed.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> LayoutKind = new Dictionary<string, string> { [MissionKinds.Scavenge] = MissionKinds.Recovery };

        public static MissionLayout MissionLayout(MapSource map, string kind, List<int> podSizes, bool itemOutdoors = false, bool clinic = false, bool hospital = false, string? alarm = null)
        {
            if (map.Kind == MapSourceKinds.Base) return Base.BaseLayout(map.Base!);
            if (map.Kind == MapSourceKinds.Handmade)
            {
                if (map.Id == null || !Maps.All.TryGetValue(map.Id, out var def)) throw new InvalidOperationException($"Unknown map {map.Id}");
                return Mapgen.HandmadeLayout(def, kind);
            }
            return Mapgen.GenerateLayout(new GenOptions
            {
                Biome = map.Biome!,
                Kind = LayoutKind.TryGetValue(kind, out var layoutKind) ? layoutKind : kind,
                PodSizes = podSizes,
                Seed = map.Seed ?? 0,
                Site = !string.IsNullOrEmpty(map.Site) ? map.Site : null,
                Medical = !string.IsNullOrEmpty(map.Medical) ? map.Medical : null,
                ItemOutdoors = itemOutdoors ? true : (bool?)null,
                Clinic = clinic ? true : (bool?)null,
                Hospital = hospital ? true : (bool?)null,
                Alarm = !string.IsNullOrEmpty(alarm) ? alarm : null,
            });
        }

        /// <summary>
        /// The lone dead of a mission, by its difficulty (1 to 4), over the patrols (Ana, 22/10: «que cuando entras en una casa
        /// pueda haber un zombi o dos, que cuando giras una esquina le tengas justo ahí»; Arturo: on top of them, a little
        /// harder). Each stands still where it is, facing anywhere, till it notices someone or hears something.
        /// </summary>
        public static readonly IReadOnlyList<int> Lurkers = new[] { 0, 3, 4, 5, 6 };
        /// <summary>None of them this near where the squad starts, nor this near the bus.</summary>
        private const int LurkerClear = 14;
        private const int LurkerBus = 5;
        /// <summary>Nor this near another of them, or the patrols.</summary>
        private const int LurkerGap = 7;
        private const int LurkerPodGap = 4;

        /// <summary>
        /// Where the lone dead wait (LURKERS): half inside the buildings, on a room's floor; half outside, just round a
        /// building's outer corner (one of its tiles diagonal to theirs, none beside), so one may be right there as the squad
        /// turns it. Never near where the squad starts, the bus or each other. On maps with buildings only.
        /// </summary>
        private static List<(Pos Pos, double Facing)> LurkerSpots(MissionLayout layout, IReadOnlyList<Unit> units, int count, Rng rng)
        {
            var w = layout.Width;
            var h = layout.Height;
            var tiles = layout.Tiles;
            var storeys = layout.Storeys;
            if (storeys == null || count <= 0) return new List<(Pos, double)>();
            int At(int x, int y) => y * w + x;
            bool Built(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && Js.At(storeys, At(x, y), 0) > 0;
            bool Near(int x, int y, IEnumerable<Vec2> ps, double d) => ps.Any((p) => Js.Hypot(p.X - x, p.Y - y) < d);
            var dead = units.Where((u) => u.Team == Teams.Alien).Select((u) => (Vec2)u.Pos).ToList();
            var taken = new HashSet<int>(units.Select((u) => At(u.Pos.X, u.Pos.Y)));
            var inside = new List<Vec2>();
            var corners = new List<Vec2>();
            var diagonals = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };
            for (var y = 1; y < h - 1; y++)
            {
                for (var x = 1; x < w - 1; x++)
                {
                    var i = At(x, y);
                    if (tiles[i] != TileKinds.Floor || Js.At(layout.Elev, i, 0) != 0 || (layout.Ground != null && i < layout.Ground.Count && layout.Ground[i] == 4) || taken.Contains(i)) continue;
                    if (Near(x, y, layout.Spawns, LurkerClear) || Near(x, y, layout.EvacZone, LurkerBus) || Near(x, y, dead, LurkerPodGap)) continue;
                    if (Built(x, y)) inside.Add(new Vec2 { X = x, Y = y });
                    else if (!Grid.Dirs4.Any((d) => Built(x + d.X, y + d.Y)) && diagonals.Count((dd) => Built(x + dd.Item1, y + dd.Item2)) == 1) corners.Add(new Vec2 { X = x, Y = y });
                }
            }
            var out_ = new List<(Pos Pos, double Facing)>();
            void Draw(List<Vec2> from, int n)
            {
                for (var tries = 0; tries < 60 && n > 0 && from.Count > 0; tries++)
                {
                    var p = from[rng.Int(from.Count)];
                    if (Near(p.X, p.Y, out_.Select((o) => (Vec2)o.Pos), LurkerGap)) continue;
                    // Facing anywhere: a way round it, at random.
                    var d = Grid.Dirs4[rng.Int(4)];
                    out_.Add((new Pos { X = p.X, Y = p.Y, Z = 0 }, Los.FacingTo(p, new Vec2 { X = p.X + d.X, Y = p.Y + d.Y })!.Value));
                    n--;
                }
            }
            Draw(inside, (int)Math.Ceiling(count / 2.0));
            Draw(corners, count - out_.Count);
            // Not enough corners: the rest inside, and the other way round.
            Draw(inside, count - out_.Count);
            return out_;
        }

        /// <summary>los.ts facingTo, where the point looked at may lie between tiles (the map's middle).</summary>
        private static double? FacingToward(Vec2 a, double bx, double by)
        {
            return a.X == bx && a.Y == by ? (double?)null : Js.Atan2(by - a.Y, bx - a.X);
        }

        public static GameState CreateMission(MissionOptions opts)
        {
            if (opts.Slots.Count == 0) throw new InvalidOperationException("A mission needs at least one player");
            var rng = opts.Rng;
            var difficulty = opts.Difficulty ?? 2;
            var handmade = opts.Map.Kind == MapSourceKinds.Handmade && opts.Map.Id != null && Maps.All.TryGetValue(opts.Map.Id, out var def) ? def : null;
            // On a base, it is always a siege (docs/FASE12.md).
            string kind = handmade?.Siege == true || opts.Map.Kind == MapSourceKinds.Base ? MissionKinds.Elimination : (opts.Kind ?? MissionKinds.Elimination);
            List<List<string>> basePlan;
            if (opts.Pods != null) basePlan = opts.Pods;
            else if (handmade != null)
            {
                // Object.keys(…).sort(): by UTF-16 code units, ordinal.
                var keys = handmade.Pods.Keys.ToList();
                Js.Sort(keys, (a, b) => string.CompareOrdinal(a, b));
                basePlan = keys.Select((k) => handmade.Pods[k].ToList()).ToList();
            }
            else basePlan = PodsFor(opts.Map, MissionPods(difficulty, kind, opts.Living == true));
            // The garrison from the start (the living's, if it is theirs); at night, the dead out in force too (a hand-made map has no room for either).
            var extra = Garrison(kind, difficulty, opts.Living == true);
            // A siege by night brings no more than its waves: nobody waits inside the base.
            var plan = new List<List<string>>(basePlan);
            if (extra != null) plan.Add(extra);
            if (opts.Night == true && opts.Map.Kind != MapSourceKinds.Base) plan.Add(NightPod(difficulty));
            var layout = MissionLayout(opts.Map, kind, plan.Select((p) => p.Count).ToList(), opts.ItemOutdoors == true, opts.CampaignMission?.Story == 1, opts.CampaignMission?.Story == 2, opts.Alarm);
            if (opts.Vehicle != null) FitVehicle(layout, opts.Vehicle.Value);
            // A map that can't host the objective falls back to elimination.
            var objectiveMissing =
                (kind == MissionKinds.Recovery && layout.Item == null) ||
                (kind == MissionKinds.Hack && layout.Terminal == null) ||
                ((kind == MissionKinds.Sabotage || kind == MissionKinds.Rescue) && layout.ObjectiveSpot == null) ||
                (kind != MissionKinds.Elimination && layout.EvacZone.Count == 0);
            if (objectiveMissing) kind = MissionKinds.Elimination;
            var squadSize = Math.Min(opts.SquadSize ?? Content.SquadSize, layout.Spawns.Count);

            var names = Content.SoldierNames.ToList();
            for (var i = names.Count - 1; i > 0; i--)
            {
                var j = rng.Int(i + 1);
                var t = names[i];
                names[i] = names[j];
                names[j] = t;
            }

            var units = new List<Unit>();
            if (opts.Squad != null)
            {
                // Posted defenders where they were put (#58: on free ground); the rest, at the spawns left over.
                int At(Vec2 p) => p.Y * layout.Width + p.X;
                // A post a turret, a trap or the wire was built on since goes back to the spawns.
                var held = opts.Map.Kind == MapSourceKinds.Base ? Base.ArmedTiles(opts.Map.Base!) : new JsSet<int>();
                var posted = opts.Squad.Select((m) =>
                {
                    Vec2? p = null;
                    if (!string.IsNullOrEmpty(m.CampaignId) && opts.Posts != null && opts.Posts.TryGetValue(m.CampaignId, out var post)) p = post;
                    return p != null && Js.At(layout.Tiles, At(p), "") == TileKinds.Floor && !held.Has(At(p)) ? p : null;
                }).ToList();
                var taken = new HashSet<int>(posted.Where((p) => p != null).Select((p) => At(p!)));
                var spare = layout.Spawns.Where((p) => !taken.Contains(At(p))).ToList();
                var count = Math.Min(opts.Squad.Count, layout.Spawns.Count);
                for (var i = 0; i < count; i++)
                {
                    var m = opts.Squad[i];
                    Vec2? at = posted[i];
                    if (at == null && spare.Count > 0)
                    {
                        at = spare[0];
                        spare.RemoveAt(0);
                    }
                    at ??= layout.Spawns[i];
                    var u = MakeUnit($"s{i + 1}", m.Template, m.Name, m.Owner, Grid.Ground(at), null);
                    var arm = Armed(u, m.Arms, m.Mags);
                    u.Rank = m.Rank;
                    u.Ap = Content.ApPerTurnOf(u);
                    u.Perks = new List<string>(m.Perks);
                    u.Mods = Content.AddMods(m.Mods, new UnitMods());
                    u.MaxHp = m.MaxHp;
                    u.Hp = m.Hp;
                    u.Charges = new Dictionary<string, int>(m.Charges);
                    u.CampaignId = m.CampaignId;
                    u.Nickname = m.Nickname;
                    u.Appearance = m.Appearance != null ? CopyAppearance(m.Appearance) : null;
                    u.WeaponTier = m.WeaponTier;
                    u.ArmorTier = m.ArmorTier;
                    if (!string.IsNullOrEmpty(m.WeaponName)) u.WeaponName = m.WeaponName;
                    u.Will = m.Will;
                    u.Armor = m.Armor;
                    if (m.PackSlots != null && m.PackSlots != 0) u.PackSlots = m.PackSlots;
                    if (m.Lines != null) u.Lines = Bars.MissionLinesOf(m.Lines.Start, m.Lines.Growth, m.Lines.Bonus);
                    arm(u);
                    u.Gear = new List<string>(m.Gear);
                    units.Add(u);
                }
            }
            else
            {
                var perPlayer = (int)Math.Ceiling((double)squadSize / opts.Slots.Count);
                for (var i = 0; i < squadSize; i++)
                {
                    var owner = opts.Slots[Math.Min((int)Math.Floor((double)i / perPlayer), opts.Slots.Count - 1)];
                    var name = names[i].Name;
                    var sex = names[i].Sex;
                    var u = MakeUnit($"s{i + 1}", TemplateIds.Rookie, name, owner, Grid.Ground(layout.Spawns[i]), null);
                    var (stats, arms, gear, lines) = SkirmishSoldier(rng, opts.Veterans == true);
                    u.Rank = stats.Rank;
                    u.Ap = Content.ApPerTurnOf(u);
                    u.Perks = stats.Perks;
                    u.Mods = stats.Mods;
                    u.MaxHp = stats.MaxHp;
                    u.Hp = stats.MaxHp;
                    u.Charges = stats.Charges;
                    u.Will = stats.Will;
                    u.Armor = stats.Armor;
                    u.Gear = gear;
                    u.Appearance = Appearances.DefaultAppearance(owner, null, sex, name);
                    u.Lines = lines;
                    Armed(u, arms, Content.SpareMags)(u);
                    units.Add(u);
                }
            }

            // The base's own defences (docs/FASE12.md §11.4): its turrets, standing and with whatever they have left in them.
            var defences = opts.Map.Kind == MapSourceKinds.Base ? BaseDefences(opts.Map.Base!, layout.Width) : ((List<Unit> Turrets, List<Trap> Traps, List<int> Slow)?)null;
            if (defences != null) units.AddRange(defences.Value.Turrets);

            string? objectiveUnit = null;
            if (kind == MissionKinds.Sabotage && layout.ObjectiveSpot != null)
            {
                var relay = MakeUnit("relay", TemplateIds.Relay, opts.ObjectiveName ?? Content.Templates[TemplateIds.Relay].Name, null, Grid.Ground(layout.ObjectiveSpot), null);
                relay.Objective = true;
                units.Add(relay);
                objectiveUnit = "relay";
            }
            if (kind == MissionKinds.Rescue && layout.ObjectiveSpot != null)
            {
                var vip = MakeUnit("vip", TemplateIds.Vip, opts.VipName ?? rng.Pick(VipNames), null, Grid.Ground(layout.ObjectiveSpot), null);
                vip.Captive = true;
                vip.Ap = 0;
                units.Add(vip);
                objectiveUnit = "vip";
            }

            var pods = new List<Pod>();
            var alienIndex = 0;
            // From difficulty 2, one group of the living posted upstairs, where the map has room for it (decision 7 in docs/FASE6-PLAN.md).
            var perch = layout.Perch ?? new List<Pos>();
            var perched = difficulty >= 2 ? plan.FindIndex((members) => members.Count > 0 && members.Count <= perch.Count && members.All((t) => !Content.Templates[t].Undead)) : -1;
            for (var i = 0; i < plan.Count; i++)
            {
                var members = plan[i];
                var spot = i == perched ? new PodSpot { Tiles = perch, Patrol = new List<Pos>() } : Js.At(layout.Pods, i, null!);
                if (spot == null) continue;
                var pod = new Pod { Id = $"p{i + 1}", Active = false, UnitIds = new List<string>(), Patrol = spot.Patrol.Select(Grid.ToPos).ToList(), PatrolIndex = 0 };
                var k = 0;
                foreach (var template in members.Take(spot.Tiles.Count))
                {
                    var id = $"a{++alienIndex}";
                    units.Add(MakeUnit(id, template, Content.Templates[template].Name, null, spot.Tiles[k], pod.Id));
                    pod.UnitIds.Add(id);
                    k++;
                }
                if (pod.UnitIds.Count > 0) pods.Add(pod);
            }
            // Over the patrols, the lone dead that wait where they stand, in the houses and round the corners (Ana and Arturo, 22/10).
            if (layout.Siege != true && opts.Map.Kind == MapSourceKinds.Generated && plan.Any((members) => members.Any((t) => Content.Templates[t].Undead)))
            {
                // Drawn from the map's own seed, so the rest of the mission is as it was.
                var spots = LurkerSpots(layout, units, Lurkers[Math.Min(difficulty, Lurkers.Count - 1)], new Rng(unchecked((uint)((opts.Map.Seed ?? 0) ^ 0x2f6b))));
                for (var k = 0; k < spots.Count; k++)
                {
                    var id = $"a{++alienIndex}";
                    var pod = new Pod { Id = $"l{k + 1}", Active = false, UnitIds = new List<string> { id }, Patrol = new List<Pos>(), PatrolIndex = 0 };
                    var zombie = MakeUnit(id, TemplateIds.Zombie, Content.Templates[TemplateIds.Zombie].Name, null, spots[k].Pos, pod.Id);
                    zombie.Facing = spots[k].Facing;
                    units.Add(zombie);
                    pods.Add(pod);
                }
            }
            var leader = opts.Leader != null ? units.FirstOrDefault((u) => u.Team == Teams.Alien && u.Template == opts.Leader.Template) : null;
            if (leader != null) leader.Name = opts.Leader!.Name;

            // A siege of the base (docs/FASE12.md): its waves; every door and barricade holds so much; the squad has its magazines at hand.
            var siege = layout.Siege == true ? new Siege { Hold = Content.Siege.Hold } : null;
            var reinforcements = siege != null
                ? SiegeWaves(difficulty, rng, opts.SiegePlan)
                : HordeTurns(kind).Select((turn, i) => new Reinforcement { Id = $"r{i + 1}", Turn = turn, Pos = null, Units = HordeWave(difficulty), Landed = false, Horde = true }).ToList();
            var breakables = layout.Breakables != null ? new Dictionary<int, int>(layout.Breakables) : new Dictionary<int, int>();
            if (siege != null && layout.Breakables == null)
            {
                for (var i = 0; i < layout.Tiles.Count; i++) if (layout.Tiles[i] == TileKinds.Door) breakables[i] = Content.Siege.Door;
                foreach (var b in layout.Barricades ?? new List<Vec2>()) breakables[b.Y * layout.Width + b.X] = Content.Siege.Barricade;
            }
            if (siege != null)
            {
                foreach (var u in units) if (u.Team == Teams.Xcom && u.Mags != null) u.Mags = Math.Max(u.Mags.Value, Content.Siege.Mags);
            }

            var mission = new MissionState
            {
                Kind = kind,
                TurnsLeft = kind == MissionKinds.Elimination ? null : opts.Turns ?? (layout.Route != null && layout.Route != 0 ? RouteTurns(kind, layout.Route.Value) : MissionTurns(kind, Math.Max(layout.Width, layout.Height))),
                Item = kind == MissionKinds.Recovery && layout.Item != null ? new MissionItem { Pos = Grid.Ground(layout.Item), Carrier = null, Evacuated = false } : null,
                EvacZone = kind == MissionKinds.Elimination ? new List<Vec2>() : layout.EvacZone,
                EvacBus = kind != MissionKinds.Elimination && layout.EvacBus != null ? layout.EvacBus : null,
                Terminal = kind == MissionKinds.Hack && layout.Terminal != null ? Grid.Ground(layout.Terminal) : null,
                ObjectiveUnit = objectiveUnit,
                ObjectiveDone = false,
                HackBonus = opts.HackBonus ?? 0,
                MedicineCures = opts.MedicineCures == true ? true : (bool?)null,
                Alarm = !string.IsNullOrEmpty(opts.Alarm) ? opts.Alarm : null,
                Quarantine = opts.Quarantine,
                Reinforcements = reinforcements,
                Difficulty = difficulty,
                // The dead keep coming by the edges on every mission you leave by bus (playtest 2026-10-15, #11); in a Limpieza,
                // only as the noise calls them (Arturo, 29/10). A siege has its own waves.
                Horde = siege == null ? HordeWave(difficulty) : null,
                Night = opts.Night == true,
                Siege = siege,
            };

            // The squad starts looking into the map (Arturo, 23/10: «los soldados aparecen mirando al revés»): towards what the
            // mission is about, or the map's middle. Only how they stand: the squad sees all round (los.ts inViewCone).
            var aheadSpot = layout.ObjectiveSpot ?? layout.Item ?? layout.Terminal;
            var aheadX = aheadSpot != null ? aheadSpot.X : (layout.Width - 1) / 2.0;
            var aheadY = aheadSpot != null ? aheadSpot.Y : (layout.Height - 1) / 2.0;
            foreach (var u in units)
            {
                if (u.Team != Teams.Xcom || u.Facing != null || u.Automaton != null) continue;
                var f = FacingToward(u.Pos, aheadX, aheadY);
                if (f != null) u.Facing = f;
            }

            var tiles = new List<string>(layout.Tiles);
            // The terminal only exists on hack missions (hand-made maps add it on demand).
            if (kind != MissionKinds.Hack && layout.Terminal != null) tiles[layout.Terminal.Y * layout.Width + layout.Terminal.X] = TileKinds.Floor;

            var state = new GameState
            {
                Seq = 0,
                MapId = layout.Id,
                // In the campaign the map is called as the operation (playtest 2026-10-15, #20): one name for the place everywhere.
                MapName = opts.CampaignMission?.Name ?? layout.Name,
                Biome = layout.Biome,
                Site = !string.IsNullOrEmpty(layout.Site) ? layout.Site : null,
                Width = layout.Width,
                Height = layout.Height,
                Tiles = tiles,
                Elev = new List<int>(layout.Elev),
                Ground = layout.Ground != null ? new List<int>(layout.Ground) : null,
                Medical = layout.Medical?.Select(CopyMedical).ToList(),
                Homes = layout.Homes?.Select(CopyHome).ToList(),
                Markets = layout.Markets?.Select((m) => new MarketSite { X = m.X, Y = m.Y, W = m.W, H = m.H, Front = m.Front, Stock = m.Stock }).ToList(),
                Decor = layout.Decor != null ? new List<int>(layout.Decor) : null,
                Storeys = layout.Storeys != null ? new List<int>(layout.Storeys) : null,
                Frame = layout.Frame?.Select((f) => new FramePiece { Kind = f.Kind, X = f.X, Y = f.Y, W = f.W, H = f.H, Side = f.Side, Storeys = f.Storeys }).ToList(),
                Levels = layout.Levels?.Select((l) => new Level { Tiles = new List<string?>(l.Tiles), Decor = l.Decor != null ? new List<int>(l.Decor) : null }).ToList(),
                Lights = new List<Light>(),
                Units = units,
                Pods = pods,
                Mission = mission,
                Turn = 1,
                ActiveTeam = Teams.Xcom,
                // The dead know where the base is.
                Concealed = siege == null,
                Ready = new[] { false, false },
                Command = opts.Slots.Count > 0 ? opts.Slots[0] : 0,
                Outcome = null,
                OutcomeReason = null,
                CampaignMission = opts.CampaignMission,
                Snow = opts.Snow == true ? true : (bool?)null,
                Breakables = siege != null ? breakables : null,
                // The campaign's base, as it stands (docs/FASE12.md §11): what the client draws, and what the siege's end gives back.
                Base = opts.Map.Kind == MapSourceKinds.Base
                    ? new SiegeBase
                    {
                        Pieces = opts.Map.Base!.Pieces.Select(CopyPiece).ToList(),
                        Felled = opts.Map.Base.Felled != null && opts.Map.Base.Felled.Count > 0 ? new List<int>(opts.Map.Base.Felled) : null,
                        Level = opts.Map.Base.Level != null && opts.Map.Base.Level != 0 ? opts.Map.Base.Level : null,
                    }
                    : null,
                Traps = defences != null && defences.Value.Traps.Count > 0 ? defences.Value.Traps : null,
                Slow = defences != null && defences.Value.Slow.Count > 0 ? defences.Value.Slow : null,
                Smoke = new List<SmokeCloud>(),
                Vehicle = opts.Vehicle,
            };
            // The map's street lights (they never go out): each lights what its lamp sees round it, worked out once.
            var lights = layout.Lights ?? new List<LayoutLight>();
            state.Lights = lights.Select((tile, i) => new Light
            {
                Id = $"l{i + 1}",
                Tile = Grid.Ground(tile),
                Radius = tile.Radius ?? Content.Night.StreetLight,
                Tiles = Los.LightTiles(state, Grid.Ground(tile), tile.Radius ?? Content.Night.StreetLight, Content.Night.LampHeight),
            }).ToList();
            // Paused at the start, while the briefing is read: the space bar (or «Empezar») sets the clock going.
            if (opts.Realtime == true) state.Realtime = new Realtime { Clock = 0, Paused = true, Units = new Dictionary<string, RtUnit>() };
            return state;
        }

        private static Appearance CopyAppearance(Appearance a)
        {
            return new Appearance { Primary = a.Primary, Secondary = a.Secondary, Visor = a.Visor, Head = a.Head, Build = a.Build, Sex = a.Sex, Helmet = a.Helmet, Torso = a.Torso, Arms = a.Arms, Legs = a.Legs, Character = a.Character };
        }

        private static MedicalDetail CopyDetail(MedicalDetail d)
        {
            return new MedicalDetail { Kind = d.Kind, X = d.X, Y = d.Y, Z = d.Z, Facing = d.Facing, Height = d.Height, Variant = d.Variant };
        }

        private static MedicalSite CopyMedical(MedicalSite b)
        {
            return new MedicalSite
            {
                X = b.X,
                Y = b.Y,
                W = b.W,
                H = b.H,
                Wear = b.Wear,
                Kind = b.Kind,
                Rooms = b.Rooms?.Select((r) => new MedicalRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = r.Z, Use = r.Use }).ToList(),
                Dressing = b.Dressing?.Select(CopyDetail).ToList(),
            };
        }

        private static HomeSite CopyHome(HomeSite h)
        {
            return new HomeSite
            {
                X = h.X,
                Y = h.Y,
                W = h.W,
                H = h.H,
                Wear = h.Wear,
                Rooms = h.Rooms.Select((r) => new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = r.Z, Use = r.Use }).ToList(),
                Dressing = h.Dressing?.Select(CopyDetail).ToList(),
            };
        }

        private static BasePiece CopyPiece(BasePiece p)
        {
            return new BasePiece { Id = p.Id, Piece = p.Piece, X = p.X, Y = p.Y, Rot = p.Rot, Size = p.Size != null ? (int[])p.Size.Clone() : null, Hp = p.Hp, Level = p.Level, Charges = p.Charges };
        }

        /// <summary>
        /// The base's defences that act in a siege (docs/FASE12.md §11.4), from what
        /// stands on it: its turrets as units with nobody's orders (their health and
        /// the rounds left in them), its traps still set, and its barbed wire.
        /// </summary>
        private static (List<Unit> Turrets, List<Trap> Traps, List<int> Slow) BaseDefences(BaseState @base, int width)
        {
            var turrets = new List<Unit>();
            var traps = new List<Trap>();
            var slow = new List<int>();
            foreach (var p in @base.Pieces)
            {
                if (p.Hp <= 0) continue;
                var node = p.Y * width + p.X;
                if (p.Piece == PieceIds.Turret)
                {
                    var u = MakeUnit($"tur-{p.Id}", TemplateIds.Turret, Content.Templates[TemplateIds.Turret].Name, null, new Pos { X = p.X, Y = p.Y }, null);
                    // Made to fire at what comes in: none of a soldier's loss of aim on a reaction shot (18/10 test: 27 against a Corredor).
                    var mods = Content.AddMods(u.Mods, new UnitMods());
                    mods.ReactionKeep = (int)Js.Round(Content.ReactionPenalty * 100);
                    u.Mods = mods;
                    u.Ap = 0;
                    u.Hp = p.Hp;
                    u.MaxHp = Base.Pieces[PieceIds.Turret].Hp;
                    u.Ammo = p.Charges ?? 0;
                    u.Automaton = new Automaton { Piece = p.Id };
                    turrets.Add(u);
                }
                else if (p.Piece == PieceIds.Mine || p.Piece == PieceIds.Spikes || p.Piece == PieceIds.Snare)
                {
                    if ((p.Charges ?? 0) > 0) traps.Add(new Trap { Piece = p.Id, Kind = p.Piece, Node = node, Charges = p.Charges!.Value });
                }
                else if (p.Piece == PieceIds.Wire) slow.Add(node);
            }
            return (turrets, traps, slow);
        }

        /// <summary>
        /// The first chapter's title and briefing (lore.ts STORY[0]), for normalizeMission.
        /// TODO(port): lore.STORY (the chapters' mission setup, which the campaign reads) is not in Lore yet; these two
        /// texts are copied from it as they are, and go when it comes.
        /// </summary>
        private static class FirstChapter
        {
            public const string Title = "El consultorio de Metsovo";
            public const string Briefing = "El consultorio de Metsovo, el pueblo de abajo, sigue cerrado. En su nevera quedaba **insulina**, y con este frío puede que siga buena. En el campamento hay quien no pasará el invierno sin ella.\n\nLa nieve ha sepultado a muchos caminantes, pero no sabemos cómo estará el pueblo. ==Buscad una entrada y traed la insulina.==";
        }

        /// <summary>
        /// A mission saved before buildings had storeys (docs/FASE6-PLAN.md): every
        /// place in it gets its level, the ground. Harmless on a mission that has them.
        /// </summary>
        /// <remarks>
        /// In C# a place always has its `z` (0 when the JSON had none: Pos), so making it the ground is done when the save is
        /// read; what is left of TypeScript's is the rest.
        /// </remarks>
        public static GameState NormalizeMission(GameState s)
        {
            // An opening mission already in progress keeps its state, with the approved presentation.
            if (s.CampaignMission?.Story == 1)
            {
                s.CampaignMission.Name = FirstChapter.Title;
                s.CampaignMission.Briefing = FirstChapter.Briefing;
                s.CampaignMission.Kicker = null;
            }
            foreach (var u in s.Units)
            {
                // A mission saved before magazines were counted (docs/FASE7.md): the squad starts with its spares.
                if (u.Mags == null && CountsMags(u.Team, Content.Templates[u.Template].Weapon)) u.Mags = Content.SpareMags;
            }
            StairAccess.RepairStairAccess(s);
            return s;
        }
    }
}
