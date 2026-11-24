#nullable enable
// A run's node fought (H3, .dev-snapshots/gameplay/runs/H0-DISENO.md §6; new in C#): the mission of the node the squad
// is at, made the way the operations of before were (Setup.CreateMission, their groups by difficulty), each soldier out
// with its kit, its pack and the health it has left; and the fight folded back into the run: kits and health home, the
// fallen gone for good, the bars learnt, a special's reward, and the run going on, ended, or the chapter done.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Runs
    {
        /// <summary>The cell of the board that gives a run's pack a row more («Todo cabe», «care»).</summary>
        public const string CareCell = "care";
        /// <summary>A special won with its objective: its reward, in § (Élite, later, 80–120).</summary>
        public const int RewardMin = 40;
        public const int RewardMax = 80;

        /// <summary>A soldier's pack rows: the upgrades', and one more with «Todo cabe».</summary>
        public static int PackRowsOf(CampaignState c, CampaignSoldier s)
        {
            return PackRows(c) + (s.Board.Contains(CareCell) ? 1 : 0);
        }

        /// <summary>Whose a soldier is in the fight: the one it was handed to in the run (RunState.hands), or its owner.</summary>
        public static int OwnerOf(CampaignState c, CampaignSoldier s)
        {
            return c.Run?.Hands != null && c.Run.Hands.TryGetValue(s.Id, out var hand) ? hand : s.Owner;
        }

        /// <summary>
        /// The mission a node is: a normal one, a Saqueo (no objective, out on La Lata); the five of before, theirs; an
        /// Asedio, a Travesía and a Guarida, Saqueos with their own rules (MissionOptions.HoldUntil, Crossing, Guard); a
        /// Sigilo, a Recuperación unseen (Stealth); the boss, a Saqueo with him to kill (Boss).
        /// </summary>
        public static string MissionKindOf(string node)
        {
            switch (node)
            {
                case NodeKinds.Elimination:
                case NodeKinds.Recovery:
                case NodeKinds.Sabotage:
                case NodeKinds.Rescue:
                    return node;
                // G23.2: a Rescate whose survivor is bitten.
                case NodeKinds.Bitten:
                    return MissionKinds.Rescue;
                // G23.3: a Rescate whose survivor can't walk.
                case NodeKinds.Wounded:
                    return MissionKinds.Rescue;
                case NodeKinds.Stealth:
                    return MissionKinds.Recovery;
                default:
                    return MissionKinds.Scavenge;
            }
        }

        /// <summary>The boss's group (H6, H6-TORO.md): El Toro, four of the dead and a Corredor; one of the dead more a difficulty past the first.</summary>
        public static List<string> BossPod(int difficulty)
        {
            var pod = new List<string> { TemplateIds.Brute, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid };
            for (var d = 1; d < difficulty; d++) pod.Add(TemplateIds.Zombie);
            return pod;
        }

        /// <summary>A boss beaten (H6): 150 § and a good weapon.</summary>
        public const int BossReward = 150;

        /// <summary>
        /// A sabotage's alarm by its place (Lore, 17/11): on the shopping plaza a patrol car's siren, on the camping an
        /// ambulance's (in its quarantine), in a factory its own.
        /// </summary>
        public static string AlarmOf(RunNode n) => n.Site == Places.Plaza ? Alarms.Police : n.Site == Places.Camping ? Alarms.Ambulance : Alarms.Factory;

        /// <summary>
        /// What a stage is about, in a line, for its sheet on the map (Lore.StageLine with this engine's figures): the
        /// sabotage's alarm, the turn La Lata comes on an Asedio, a Guarida's guard.
        /// </summary>
        public static string StageLine(RunNode n)
        {
            return Lore.StageLine(n.Kind, alarm: n.Kind == NodeKinds.Sabotage ? AlarmOf(n) : null, holdTurns: n.Kind == NodeKinds.Hold ? Setup.HoldTurns : (int?)null, guard: n.Kind == NodeKinds.Elite ? Setup.GuardOf(n.Seed ?? 0) : null, crows: n.Guarded == true);
        }

        /// <summary>
        /// La Lata as big as the seats the squad has: 2 the motorbike (Moto), 4 the 4×4, 6 the van, 8 the bus
        /// (Campaign.VehicleSeats; the lorry's 7 never). The fight's and the base's (UI's BaseController, N6 on 17/11:
        /// Escuadra showed the bus).
        /// </summary>
        public static int VehicleOf(CampaignState c)
        {
            var seats = SeatsOf(c);
            if (seats <= MotoSeats) return Moto;
            var tier = Campaign.VehicleSeats.ToList().FindIndex((n) => n >= seats);
            return tier >= 0 ? tier : Campaign.VehicleSeats.Count - 1;
        }

        /// <summary>
        /// The motorbike's vehicle (N4, N5): after the four of the campaign of before (Campaign.VehicleSeats), so theirs keep
        /// their numbers. Two seats: who rides and who sits behind.
        /// </summary>
        public const int Moto = 4;
        public const int MotoSeats = 2;

        /// <summary>
        /// A run's soldier going into a fight (Setup.SquadMember): its weapons from its kit (with their mods, no wear), the
        /// belt's things as uses, the vest's health and armour, its pack and its magazines, the health it has left.
        /// </summary>
        public static SquadMember SquadMemberOf(CampaignState c, CampaignSoldier s)
        {
            var kit = s.Kit ?? new Kit();
            var arms = new Arms
            {
                Primary = kit.Primary?.Type,
                PrimaryId = kit.Primary?.Id,
                Mods = kit.Primary?.Mods != null && kit.Primary.Mods.Count > 0 ? new List<string>(kit.Primary.Mods) : null,
                // The knife is the blade everyone fights with when there is no other.
                Melee = kit.Melee == null || kit.Melee.Type == FoundWeaponIds.Knife ? null : kit.Melee.Type,
                MeleeId = kit.Melee?.Id,
                Pistol = kit.Pistol != null,
                PistolId = kit.Pistol?.Id,
            };
            var belt = kit.Belt.Where((g) => g?.Type != null).SelectMany((g) => Enumerable.Repeat(g!.Type!, g.Count)).ToList();
            var stats = Bars.FighterStatsOf(s.Skills, s.Growth, s.Board, belt, arms);
            var traits = s.Traits.Select((t) => Quests.Traits[t]).ToList();
            var vest = kit.Vest?.Type != null && Content.Vests.TryGetValue(kit.Vest.Type, out var def) ? def : null;
            var max = stats.MaxHp + (vest?.Hp ?? 0);
            // N9, Supervivencia: the wounds it came home with (CampaignSoldier.Hurt).
            var hp = c.Run != null && c.Run.Hp.TryGetValue(s.Id, out var left) ? Math.Max(1, Math.Min(max, left)) : Math.Max(1, max - (Levels.Of(c).Wounds ? s.Hurt ?? 0 : 0));
            return new SquadMember
            {
                CampaignId = s.Id,
                Name = s.Name,
                Nickname = s.Nickname,
                Appearance = s.Appearance,
                WeaponTier = Progression.WeaponTier(),
                ArmorTier = 1,
                WeaponName = RunPack.WeaponName(kit),
                Owner = OwnerOf(c, s),
                Template = stats.Template,
                Rank = stats.Rank,
                Perks = stats.Perks,
                Mods = Content.AddMods(stats.Mods, new UnitMods { Aim = traits.Sum((t) => t.Aim) }),
                MaxHp = max,
                Hp = hp,
                Charges = stats.Charges,
                Will = stats.Will + traits.Sum((t) => t.Will),
                Armor = stats.Armor + (vest?.Armor ?? 0),
                Gear = belt.Distinct().ToList(),
                Arms = arms,
                Mags = kit.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Mags).Sum((p) => p.Gear.Count),
                Lines = Bars.MissionLinesOf(s.Skills, s.Growth),
                Pack = kit.Pack,
                Kit = Gears.ClonePlaces(kit),
            };
        }

        // ---------------------------------------------------------------- launch

        /// <summary>
        /// The mission of the node the squad is at, fighting (the room's LaunchMission for a run campaign): its map from its
        /// site and seed, its kind, its groups by its difficulty as an operation of before had them, its modifier (night,
        /// storm, the horde awake), and the squad with its kits. Null when the run isn't at a fight.
        /// </summary>
        public static (GameState State, string Name)? Launch(CampaignState c, Rng rng)
        {
            var run = c.Run;
            if (run == null || run.Phase != RunPhases.Fight) return null;
            var node = RunMap.Node(run, run.At);
            var map = node != null ? RunMap.MapOf(node) : null;
            if (node == null || map == null) return null;
            var kind = MissionKindOf(node.Kind);
            var difficulty = Math.Max(1, Math.Min(4, node.Difficulty));
            var boss = node.Kind == NodeKinds.Boss;
            // A Rescate's survivor hides among the dead, as on a Saqueo; now and then the Cuervos' prisoner, two of their bands
            // and two groups of the dead round them, as a Rescate was before (N2, Arturo 17/11: «a veces»; RunNode.Guarded).
            var rescue = kind == MissionKinds.Rescue;
            var guarded = rescue && node.Guarded == true;
            // The boss's group alone (it isn't scaled: there is one Toro).
            var pods = boss ? new List<List<string>> { BossPod(difficulty) } : Setup.PodsFor(map, Setup.MissionPods(difficulty, rescue && !guarded ? MissionKinds.Scavenge : kind, guarded));
            // N9: the game's level's dead (not the boss's group).
            var level = Levels.Of(c);
            if (!boss) pods = Levels.Pods(pods, level.Dead);
            // One Cazador fewer in a guarded Rescate since they carry crossbows (Arturo, cuadro 106; ARCO-Y-BALLESTA.md): the
            // first of its groups', gone with nobody in its place (one swapped for a Saqueador left it harder than before).
            if (guarded) OneCazadorFewer(pods);
            var squad = run.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)).Where((s) => s != null && s.Alive && s.Kit != null).Select((s) => SquadMemberOf(c, s!)).ToList();
            var seed = Math.Abs(node.Seed ?? 0);
            var alarm = kind == MissionKinds.Sabotage ? AlarmOf(node) : null;
            var name = RunTexts.FightName(node, run.Chapter);
            // Cuadro 122 (Producto's review of 23/11): never a fight with no way out. A map whose La Lata the squad can't walk
            // to from where it comes in (or with none: the mission would fall back to a Limpieza without it) is made again
            // from the next seed, MapRetries at most. Looked at once La Lata's zone is by its doors (SeatZone): a door's tile
            // may be walled in where the zone drawn first wasn't (Red, R2 of 23/11: a camping's van among the trees).
            GameState state = null!;
            for (var attempt = 0; ; attempt++)
            {
                state = Setup.CreateMission(new MissionOptions
                {
                    Map = map,
                    Slots = c.Players.ToList(),
                    Rng = rng,
                    Kind = kind,
                    Squad = squad,
                    Pods = pods,
                    Difficulty = difficulty,
                    CampaignMission = new CampaignMission { Id = node.Id, Name = name },
                    Living = guarded ? true : (bool?)null,
                    Guarded = guarded ? true : (bool?)null,
                    Survivor = rescue ? SurvivorOf(c, node) : null,
                    // Never fewer than the squad that went out (a run of the 0.13.0's, with 4, under way when its seats went to 2).
                    Seats = Math.Max(SeatsOf(c), squad.Count),
                    Alarm = alarm,
                    ObjectiveName = alarm != null ? Lore.AlarmNames[alarm] : null,
                    // The node's own modifier, and the one an event left for it (H4).
                    Night = node.Modifier == Modifiers.Night || node.Added == Modifiers.Night ? true : (bool?)null,
                    Weather = node.Modifier == Modifiers.Storm || node.Added == Modifiers.Storm ? Weathers.Storm : null,
                    Awake = node.Modifier == Modifiers.Awake || node.Added == Modifiers.Awake ? true : (bool?)null,
                    // Pistols are counted: searches turn them up too.
                    Pistols = true,
                    Vehicle = VehicleOf(c),
                    // The runs' stages (H5, H6).
                    HoldUntil = node.Kind == NodeKinds.Hold ? Setup.HoldTurns : (int?)null,
                    Crossing = node.Kind == NodeKinds.Crossing ? true : (bool?)null,
                    Stealth = node.Kind == NodeKinds.Stealth ? true : (bool?)null,
                    Guard = node.Kind == NodeKinds.Elite ? Setup.GuardOf(seed) : null,
                    Boss = boss ? TemplateIds.Brute : null,
                    // A normal node's La Lata near the squad (Gameplay, 16/11): far was for the Travesía. Separados' too: its two
                    // starts are measured from there (SEPARADOS.md). Saqueadores' too (G23.5): the camp a way off it, not on the way
                    // (R1 of 20/11, rifles, 4 seats: out 0–33 % with La Lata far, 73–87 % near; the Saqueo, 93–100 %).
                    // Evacuación's too (G23.6): the squad goes out for the civilians, and they walk back to it.
                    EvacNear = node.Kind == NodeKinds.Normal || node.Kind == NodeKinds.Split || node.Kind == NodeKinds.Raiders || node.Kind == NodeKinds.Evacuation || node.Kind == NodeKinds.Charges ? true : (bool?)null,
                    // Saqueadores (G23.5): the camp of the living on the map.
                    Raiders = node.Kind == NodeKinds.Raiders ? true : (bool?)null,
                    // N9: the game's level's lone dead.
                    DeadScale = level.Dead != 1 ? level.Dead : (double?)null,
                    // Cuadro 122: La Lata in every fight, a Limpieza's too (out on it, a retreat): no abandoning, so a way to end each.
                    EvacAlways = true,
                });
                // As many tiles to get out by as La Lata has seats, by its doors (T21, Arturo 18/11).
                SeatZone(state);
                if ((state.Mission.EvacZone.Count > 0 && Paths.Cut(state).Count == 0) || attempt >= MapRetries) break;
                map = RunMap.MapOf(new RunNode { Kind = node.Kind, Site = node.Site, Seed = unchecked((node.Seed ?? 0) + (attempt + 1) * 7919) })!;
            }
            // No hollow to drop into and never get out of to La Lata (cuadro 122): pits nobody walks into, against La Lata's
            // zone as it stays (Producto's review of 23/11).
            Setup.NoPits(state);
            // Nothing to search out of everyone's reach (Red, R3 17/11: a police car walled in).
            Setup.OnlyWithinReach(state);
            // Separados (G23.4): the squad in two groups on either side of La Lata, measured from its zone as it is now.
            // Which group takes which side, by the map's seed (one is clearer of the dead: R1, 20/11).
            if (node.Kind == NodeKinds.Split) Setup.Split(state, unchecked((uint)Math.Abs(node.Seed ?? 0) * 2654435761u) >> 31 == 1, node.Seed ?? 0);
            // N7 (Arturo, 22/11): a Rescate's survivor (Mordido's, El herido's too) never on a door nor in a pass, the dead off them
            // and off the way to them and back.
            if (rescue && state.Mission.Survivor != null) Setup.SurvivorClear(state, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0x5c1ea))));
            // Evacuación (G23.6): its civilians hidden in buildings, each in its circle.
            if (node.Kind == NodeKinds.Evacuation) Setup.PlaceCivilians(state, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0xc1b1))), node.Seed ?? 0);
            // Saqueadores (G23.5): the camp's guards at their posts and its stash, the dead kept off it.
            if (node.Kind == NodeKinds.Raiders) Setup.PlaceCamp(state, difficulty, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0xca4e))));
            // One in RunMap.LooseOdds of the other fights, a survivor loose on the map, unmarked (N2, Arturo 17/11).
            if (RunMap.LooseSurvivor(node)) PlaceLoose(state, SurvivorOf(c, node), new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0x10057))));
            // The survivor's knife numbered from the campaign's, as a recruit's, before the fight's own numbering starts: not
            // one of its finds (Red, 23/11: one with no id fell on the floor and couldn't be taken up through the room).
            if (!string.IsNullOrEmpty(state.Mission.Survivor) && Grid.GetUnit(state, state.Mission.Survivor!)?.Kit?.Melee is Gear knife && string.IsNullOrEmpty(knife.Id)) knife.Id = $"g{++c.NextId}";
            state.Run = new RunFight { Node = node.Id, NodeKind = node.Kind, Row = node.Row, Modifier = node.Modifier, Added = node.Added, FirstGear = c.NextId, NextGear = c.NextId, Loot = level.Loot != 1 ? level.Loot : (double?)null };
            // N9: the game's level's horde.
            if (level.Horde != 1) state.Mission.HordeScale = level.Horde;
            // Its day sky (cuadro 83): look only.
            state.Mission.Sky = RunMap.SkyOf(node);
            // La llave (G23.1): its armoury locked and full, its key (a gear of the fight's) on one of the dead in uniform the
            // squad can get to.
            if (node.Kind == NodeKinds.Key) Setup.KeyArmory(state, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0x6e7))));
            // Mordido (G23.2): the survivor bitten, the antiviral in one medicine cabinet or shelf.
            if (node.Kind == NodeKinds.Bitten) Setup.Bitten(state, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0xb17e))));
            // El herido (G23.3): the survivor can't walk.
            if (node.Kind == NodeKinds.Wounded) Setup.Wounded(state);
            // Las cargas (cuadro 109): the nest, its three points and its dead; the three charges into the packs.
            if (node.Kind == NodeKinds.Charges) Setup.PlaceCharges(state, difficulty, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0xc4a6))));
            // Nobody left in a pit, wherever they were put (Producto's review of 23/11: one of the dead there could never be
            // reached with a blade, and a Limpieza never won).
            Setup.OutOfPits(state);
            return (state, name);
        }

        /// <summary>Maps made again from the next seed when the squad can't walk to La Lata (Launch), at most.</summary>
        public const int MapRetries = 8;

        /// <summary>Cuadro 106: the first Cazador of `pods` out of its group (none in them, as they are).</summary>
        public static void OneCazadorFewer(List<List<string>> pods)
        {
            var band = pods.FirstOrDefault((p) => p.Contains(TemplateIds.Lancer));
            band?.RemoveAt(band.IndexOf(TemplateIds.Lancer));
        }

        // ------------------------------------------------------------ the result

        /// <summary>
        /// A Rescate's survivor (N2) as they wait on the map: a recruit with a knife and nothing else, a soldier's name
        /// (Lore, N8: not a VIP's), drawn from the node's seed so the fight and its end agree; nobody's yet, and not among
        /// the campaign's soldiers (nothing of `c` changes). One of the squad from when they join (Arturo, cuadro 140, M7):
        /// a recruit's pack, empty, at the size the upgrades give (NewKit), and the knife; captive till found, unused.
        /// </summary>
        public static SquadMember SurvivorOf(CampaignState c, RunNode node)
        {
            var s = Campaign.RunsSurvivor(c, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0x5a17))), SurvivorLines(node));
            var member = SquadMemberOf(c, s);
            member.CampaignId = "";
            member.Pack = new GearGrid { W = PackW, H = PackRows(c) };
            member.Mags = 0;
            return member;
        }

        /// <summary>
        /// Where each vehicle is boarded (T21; Arte's MotoLab.Puertas, 18/11), in tiles from its middle: along it (+ to the
        /// nose) and out to a side (+ to `side`, the side its zone was on: the bus's doors are all on it). The tiles beside
        /// the vehicle's place (two wide) are 1.5 out. The motorbike: one each side of its back half; the 4×4: its four doors;
        /// the van: six behind its back doors, two wide and three deep; the bus: four by each of its two doors, two along and
        /// two out. As many as its seats, in this order.
        /// </summary>
        private static IReadOnlyList<(double Along, double Out)> DoorsOf(int vehicle)
        {
            switch (vehicle)
            {
                case Moto:
                    // Within its own place, two wide, beside its back half (Arte, 18/11: from outside it, one was 3,2 m off).
                    return new[] { (-0.5, -0.5), (-0.5, 0.5) };
                case 0:
                    return new[] { (0.11, -1.5), (0.11, 1.5), (-0.51, -1.5), (-0.51, 1.5) };
                case 1:
                    return new[] { (-2.5, -0.5), (-2.5, 0.5), (-3.5, -0.5), (-3.5, 0.5), (-4.5, -0.5), (-4.5, 0.5) };
                case 3:
                    return new[] { (2.96, 1.5), (2.96, 2.5), (1.96, 1.5), (1.96, 2.5), (-0.38, 1.5), (-0.38, 2.5), (-1.38, 1.5), (-1.38, 2.5) };
                default:
                    return new (double, double)[0];
            }
        }

        /// <summary>
        /// Where each vehicle's doors are (Arte's MotoLab.Puertas, 18/11), as DoorsOf: along it from its middle, and out from
        /// its middle line (+ to `side`). The motorbike's seats; the 4×4's four doors; the van's back doors; the bus's two.
        /// </summary>
        private static IReadOnlyList<(double Along, double Out)> DoorPointsOf(int vehicle)
        {
            switch (vehicle)
            {
                case Moto:
                    return new[] { (-0.45, -0.62), (-0.45, 0.62) };
                case 0:
                    return new[] { (0.11, -0.79), (0.11, 0.79), (-0.51, -0.79), (-0.51, 0.79) };
                case 1:
                    // Its back doors open out: where one stands to get in, the two tiles right behind it.
                    return new[] { (-2.5, -0.5), (-2.5, 0.5) };
                case 3:
                    return new[] { (2.96, 0.86), (-0.38, 0.86) };
                default:
                    return new (double, double)[0];
            }
        }

        /// <summary>
        /// What lies about that a door's tile is cleared of (Arte, 18/11: the van's block cut by a portaloo): one-tile things
        /// that grow or lie about, as the map's own dead ends are opened (Mapgen's Soft), and a building site's portaloo.
        /// </summary>
        private static readonly HashSet<string> ClearedForLaLata = new HashSet<string>
        {
            DecorKinds.Tree, DecorKinds.Bush, DecorKinds.Rock, DecorKinds.StreetTree, DecorKinds.Bollard, DecorKinds.Planter, DecorKinds.Bin, DecorKinds.Fence,
            DecorKinds.Wire, DecorKinds.Crates, DecorKinds.Barricade, DecorKinds.Pallets, DecorKinds.Drums, DecorKinds.Rubble, DecorKinds.Pylon,
        };

        /// <summary>
        /// La Lata's zone cut to its seats (T21, Arturo 18/11: «solo la cantidad de huecos que hay y que estén donde
        /// corresponda con el vehículo, de sus puertas»): MissionState.Seats tiles, by its doors as drawn (DoorsOf), the
        /// motorbike's within its own place, made ground, one each side, and what lies about on the others cleared
        /// (ClearedForLaLata). If one of them can't be stood on, or would stand apart, the zone is grown instead from the
        /// vehicle, a free tile touching it or the zone at a time, the nearest to its doors (DoorPointsOf): one piece against
        /// the vehicle, by its doors, never round what is in the way (Producto and Arte, 18/11). Who doesn't fit finds no tile
        /// (Rules.NoSeat as well). Without seats or the vehicle's place, as it was.
        /// </summary>
        public static void SeatZone(GameState s)
        {
            var m = s.Mission;
            var bus = m.EvacBus;
            var seats = m.Seats;
            if (seats == null || m.EvacZone.Count == 0) return;
            // No place of the vehicle on the map (Red, 18/11: a service area's, seed 5111): its zone cut to the seats, the
            // tiles of it nearest its middle, one piece.
            if (bus == null || bus.Tiles.Count == 0)
            {
                if (m.EvacZone.Count <= seats.Value) return;
                double zx = m.EvacZone.Average((t) => t.X), zy = m.EvacZone.Average((t) => t.Y);
                var kept = new List<Vec2>();
                var rest = m.EvacZone.OrderBy((t) => Js.Hypot(t.X - zx, t.Y - zy)).ThenBy((t) => t.Y).ThenBy((t) => t.X).ToList();
                kept.Add(rest[0]);
                rest.RemoveAt(0);
                while (kept.Count < seats.Value && rest.Count > 0)
                {
                    var next = rest.FirstOrDefault((t) => kept.Any((k) => Math.Max(Math.Abs(k.X - t.X), Math.Abs(k.Y - t.Y)) == 1)) ?? rest[0];
                    kept.Add(next);
                    rest.Remove(next);
                }
                m.EvacZone = kept;
                return;
            }
            var d = Grid.Dirs4[bus.Heading];
            var o = new Vec2 { X = -d.Y, Y = d.X };
            double cx = bus.Tiles.Average((t) => t.X), cy = bus.Tiles.Average((t) => t.Y);
            // The side its zone was on: the doors that are on one side face it.
            var lean = m.EvacZone.Average((t) => (t.X - cx) * o.X + (t.Y - cy) * o.Y);
            var side = lean < 0 ? -1 : 1;
            var chosen = new List<Vec2>();
            bool Free(Vec2 p) => Grid.InBounds(s, p.X, p.Y) && !bus.Tiles.Any((t) => t.X == p.X && t.Y == p.Y) && !chosen.Any((t) => t.X == p.X && t.Y == p.Y)
                && Flights.CanStopAt(s, new Pos { X = p.X, Y = p.Y, Z = 0 });
            // Against the vehicle or a tile of the zone already chosen (any of the eight ways).
            bool Next(Vec2 a, Vec2 b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) == 1;
            bool Touches(Vec2 p) => bus.Tiles.Any((t) => Next(t, p)) || chosen.Any((t) => Next(t, p));
            int ToVehicle(Vec2 p) => bus.Tiles.Min((t) => Math.Max(Math.Abs(t.X - p.X), Math.Abs(t.Y - p.Y)));
            IEnumerable<Vec2> Touching()
            {
                var seen = new HashSet<(int, int)>();
                foreach (var t in bus.Tiles.Concat(chosen).ToList())
                {
                    foreach (var d in Grid.Dirs8)
                    {
                        var p = new Vec2 { X = t.X + d.X, Y = t.Y + d.Y };
                        if (seen.Add((p.X, p.Y)) && Free(p)) yield return p;
                    }
                }
            }
            Vec2 At(double along, double out_) => new Vec2 { X = (int)Math.Floor(cx + d.X * along + o.X * out_ * side + 0.5), Y = (int)Math.Floor(cy + d.Y * along + o.Y * out_ * side + 0.5) };
            void Ground(Vec2 p)
            {
                var i = Grid.TileIndex(s, p.X, p.Y);
                s.Tiles[i] = TileKinds.Floor;
                if (s.Decor != null && i < s.Decor.Count) s.Decor[i] = 0;
            }
            // The doors' tiles as drawn (DoorsOf): the motorbike's made ground; what lies about on the others, cleared.
            var spec = new List<Vec2>();
            foreach (var (along, out_) in DoorsOf(s.Vehicle ?? 3))
            {
                var door = At(along, out_);
                if (!Grid.InBounds(s, door.X, door.Y)) continue;
                var at = Grid.TileIndex(s, door.X, door.Y);
                var inPlace = bus.Tiles.Any((t) => t.X == door.X && t.Y == door.Y);
                if (s.Vehicle == Moto && inPlace && (s.Elev.Count <= at || s.Elev[at] == 0)) Ground(door);
                else if (!inPlace && s.Decor != null && (s.Tiles[at] == TileKinds.Low || s.Tiles[at] == TileKinds.High) && Decor.ObjectAt(s.Decor, s.Width, at) is DecorObject thing && ClearedForLaLata.Contains(thing.Kind))
                {
                    foreach (var j in thing.Tiles) Ground(new Vec2 { X = j % s.Width, Y = j / s.Width });
                }
                spec.Add(door);
            }
            // All of them free, each against the vehicle or one before it: as drawn.
            bool Usable(Vec2 p) => Free(p) || (s.Vehicle == Moto && bus.Tiles.Any((t) => t.X == p.X && t.Y == p.Y) && Flights.CanStopAt(s, new Pos { X = p.X, Y = p.Y, Z = 0 }));
            foreach (var door in spec.Take(seats.Value))
            {
                if (!Usable(door) || !Touches(door))
                {
                    chosen.Clear();
                    break;
                }
                chosen.Add(door);
            }
            // Otherwise (a door's tile walled or stood on; Arte, 18/11: a camping's portaloo in the van's block), and for the
            // seats past them (a run of before with more of the squad than seats): grown from the vehicle, a free tile
            // touching it or the zone at a time, the nearest to one of its doors (DoorPointsOf), so the zone stays one piece by
            // the doors and never goes round what is in the way.
            var points = DoorPointsOf(s.Vehicle ?? 3).Select((p) => (X: cx + d.X * p.Along + o.X * p.Out * side, Y: cy + d.Y * p.Along + o.Y * p.Out * side)).ToList();
            if (points.Count == 0) points.Add((cx, cy));
            double ToDoor(Vec2 p) => points.Min((q) => Js.Hypot(p.X - q.X, p.Y - q.Y));
            // Ties: the one further out from the vehicle, by the doors' side (behind the van, not along it).
            var mx = points.Average((q) => q.X);
            var my = points.Average((q) => q.Y);
            var reach = Js.Hypot(mx - cx, my - cy);
            var (ox, oy) = reach > 0 ? (mx + (mx - cx) / reach, my + (my - cy) / reach) : (mx, my);
            while (chosen.Count < seats.Value)
            {
                var next = Touching().OrderBy((p) => Math.Round(ToDoor(p), 6)).ThenBy((p) => Js.Hypot(p.X - ox, p.Y - oy)).ThenBy((p) => p.Y).ThenBy((p) => p.X).FirstOrDefault();
                if (next == null) break;
                chosen.Add(next);
            }
            if (chosen.Count > 0) m.EvacZone = chosen;
        }

        /// <summary>How far by the walk from the squad's start a loose survivor waits, at least (N2): not at the door.</summary>
        public const int LooseFrom = 12 * Paths.CostOrtho;

        /// <summary>
        /// A survivor loose on the map (N2): on a ground-floor tile inside a building (a map that is one building: any of
        /// its rooms), free, walked to from the squad's start and past LooseFrom of it, out of La Lata's zone; drawn from
        /// `rng`. Nobody's and captive (Setup.SurvivorUnit), MissionState.Survivor. None placed if there is no such tile.
        /// </summary>
        public static Unit? PlaceLoose(GameState s, SquadMember member, Rng rng)
        {
            var from = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.CampaignId != null);
            if (from == null || s.Storeys == null) return null;
            var reach = Paths.ComputeReach(s, from, 1_000_000, ignore: (_) => true);
            var spots = new List<Pos>();
            for (var i = 0; i < s.Width * s.Height; i++)
            {
                var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
                if (s.Storeys[i] <= 0 || Abilities.InEvacZone(s, p) || Grid.UnitAt(s, p) != null || !Flights.CanStopAt(s, p)) continue;
                if (!reach.Cost.TryGetValue(Grid.NodeOf(s, p), out var walk) || walk < LooseFrom) continue;
                if (s.Mission.OutOfReach?.Contains(i) == true) continue;
                spots.Add(p);
            }
            if (spots.Count == 0) return null;
            var u = Setup.SurvivorUnit("survivor", member, rng.Pick(spots));
            s.Units.Add(u);
            s.Mission.Survivor = u.Id;
            return u;
        }

        /// <summary>The survivor's bars (Bars.RecruitSkills), from the node's seed: the same in the fight and once joined.</summary>
        private static SkillLines SurvivorLines(RunNode node) => Bars.RecruitSkills(new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0x11e5))));

        /// <summary>
        /// The one a Rescate brought out, a soldier now (Runs.NewSoldier): by their name and look and bars, of whoever talked
        /// to them (N2), or the partner if they have no bed; into the squad with the health they had (they got a seat on La
        /// Lata, Rules.NoSeat). Null with no bed for them.
        /// </summary>
        private static CampaignSoldier? Rescued(CampaignState c, RunNode node, Unit vip, Rng rng, RunReport report)
        {
            bool Bed(int p) => HasBed(c, p, null);
            var talker = vip.Owner ?? Campaign.RunsNeediest(c);
            int? owner = Bed(talker) ? talker : c.Players.Where((p) => p != talker && Bed(p)).Select((p) => (int?)p).FirstOrDefault();
            // Every player at the cap: they don't stay, and the log says so (cuadro 140, M7; Lore's line).
            if (owner == null)
            {
                Campaign.RunsLog(c, RunTexts.SurvivorNoRoom(vip.Name), LogTones.Bad);
                return null;
            }
            var s = NewSoldier(c, owner.Value, rng);
            s.Name = vip.Name;
            var lines = SurvivorLines(node);
            s.Skills = lines.Skills;
            s.Growth = lines.Growth;
            // As they looked on the map, in their player's colours.
            s.Appearance = Appearances.DefaultAppearance(owner.Value, null, vip.Appearance?.Sex ?? Campaign.SexOfPerson(vip.Name) ?? s.Appearance.Sex, Campaign.RunsSurvivorLook(vip.Name), s.Appearance.Wardrobe);
            var run = c.Run!;
            var inSquad = run.Squad.Count < SeatsOf(c);
            if (inSquad)
            {
                run.Squad.Add(s.Id);
                run.Hp[s.Id] = Math.Max(1, Math.Min(MaxHp(s), vip.Hp));
            }
            report.InSquad = inSquad;
            Campaign.RunsLog(c, RunTexts.SurvivorJoins(s.Name, inSquad), LogTones.Good);
            return s;
        }

        /// <summary>Whether `player` has a bed for one more soldier: under the soldiers' cap, without those in `gone`.</summary>
        private static bool HasBed(CampaignState c, int player, ICollection<string>? gone)
        {
            return c.Soldiers.Count((s) => s.Owner == player && s.Alive && (gone == null || !gone.Contains(s.Id))) < Campaign.SoldierCap(c);
        }

        /// <summary>
        /// Whether the survivor of a run's fight, brought out on La Lata, stays (Rescued's rule, cuadro 140): a bed with
        /// whoever talked to them, else with the partner. The beds as ApplyFight will count them, without the squad's soldiers
        /// fallen or left behind in `mission`. For the card at the fight's end, before the room applies it (UI, 24/11).
        /// </summary>
        public static bool SurvivorStays(CampaignState c, GameState mission)
        {
            var gone = new HashSet<string>(mission.Units.Where((u) => !string.IsNullOrEmpty(u.CampaignId) && FellInFight(u)).Select((u) => u.CampaignId!));
            var vip = string.IsNullOrEmpty(mission.Mission.Survivor) ? null : mission.Units.FirstOrDefault((u) => u.Id == mission.Mission.Survivor);
            var talker = vip?.Owner ?? Campaign.RunsNeediest(c);
            return HasBed(c, talker, gone) || c.Players.Any((p) => HasBed(c, p, gone));
        }

        /// <summary>
        /// What a survivor with no bed for them (SurvivorStays false) will leave the squad's packs and what will find no room
        /// there, as ApplyFight's HandOver will: on copies, nothing of `c` nor `mission` changed. For the card at the fight's
        /// end, before the room applies it (UI, review of 0.14.4). Null with no survivor brought out, or one who stays.
        /// </summary>
        public static RunNoBed? HandOverPreview(CampaignState c, GameState mission)
        {
            var run = c.Run;
            var fight = mission.Run;
            if (run == null || fight == null || string.IsNullOrEmpty(mission.Mission.Survivor)) return null;
            var vip = mission.Units.FirstOrDefault((u) => u.Id == mission.Mission.Survivor);
            if (vip == null || !vip.Evacuated || SurvivorStays(c, mission)) return null;
            var report = new RunReport();
            HandOver(vip, HomeBefore(c, run, mission), report, fight);
            return report.NoBed;
        }

        /// <summary>
        /// The § the squad's packs would still take of a stage's reward, as ApplyFight leaves them just before it: after a
        /// survivor with no bed has left them what they carried (HandOver). The room on the supplies piles, and 100 a free
        /// cell (Content.PileOf); 0, none of it fits. On copies, nothing changed. For the card at the fight's end (UI, cuadro
        /// 148: «y la recompensa» with the packs full). A boss's weapon goes in before its § (ApplyFight), and is only drawn
        /// then: not counted.
        /// </summary>
        public static int RewardRoom(CampaignState c, GameState mission)
        {
            var run = c.Run;
            var fight = mission.Run;
            if (run == null || fight == null) return 0;
            var home = HomeBefore(c, run, mission);
            var vip = string.IsNullOrEmpty(mission.Mission.Survivor) ? null : mission.Units.FirstOrDefault((u) => u.Id == mission.Mission.Survivor);
            if (vip != null && vip.Evacuated && !SurvivorStays(c, mission)) HandOver(vip, home, new RunReport(), fight);
            return home.Sum((s) => SuppliesRoom(s.Kit!.Pack));
        }

        /// <summary>
        /// The squad's packs as ApplyFight leaves them before the survivor's turn, as copies: those of the fight as they came
        /// out (KitBack), the fallen and the left behind out, the rest of the squad's as they were; in the squad's order.
        /// </summary>
        private static List<CampaignSoldier> HomeBefore(CampaignState c, RunState run, GameState mission)
        {
            var home = new List<CampaignSoldier>();
            foreach (var id in run.Squad)
            {
                var s = c.Soldiers.FirstOrDefault((x) => x.Id == id && x.Alive);
                if (s == null) continue;
                var u = mission.Units.FirstOrDefault((x) => x.CampaignId == id);
                if (u != null && FellInFight(u)) continue;
                var pack = u != null ? KitBack(u, s).Pack : s.Kit != null ? Gears.CloneGrid(s.Kit.Pack) : null;
                if (pack != null) home.Add(new CampaignSoldier { Id = s.Id, Kit = new Kit { Pack = pack } });
            }
            return home;
        }

        /// <summary>The § a grid takes: the room on its supplies piles, and a full pile in each cell a pile fits in.</summary>
        private static int SuppliesRoom(GearGrid grid)
        {
            var copy = Gears.CloneGrid(grid);
            var most = Content.PileOf(GearKinds.Supplies, null);
            var room = 0;
            foreach (var p in copy.Items.Where((x) => x.Gear.Kind == GearKinds.Supplies))
            {
                room += Gears.Room(p.Gear);
                p.Gear.Count = Math.Max(p.Gear.Count, most);
            }
            for (var at = Gears.FirstFit(copy, new Gear { Kind = GearKinds.Supplies, Count = most }); at != null; at = Gears.FirstFit(copy, new Gear { Kind = GearKinds.Supplies, Count = most }))
            {
                copy.Items.Add(at);
                room += most;
            }
            return room;
        }

        /// <summary>One of a run's fight who fell in it, or was left behind when La Lata went (LeftBehindEvent).</summary>
        private static bool FellInFight(Unit u) => (!u.Alive && !u.Evacuated && u.Hp <= 0) || u.Lost == true;

        /// <summary>
        /// What a unit of a run's fight lost there (ApplyFight): left behind, all it had on it; fallen, what it dropped
        /// (RunPack.Fallen) that nobody took up.
        /// </summary>
        private static List<Gear> LostWith(Unit u, GameState mission)
        {
            var lost = u.Lost == true ? (u.Kit != null ? new[] { u.Kit.Primary, u.Kit.Melee, u.Kit.Pistol, u.Kit.Vest }.Concat(u.Kit.Belt).Where((g) => g != null).Select((g) => g!).Concat(u.Pack?.Items.Select((p) => p.Gear) ?? new Gear[0]).ToList() : new List<Gear>())
                : (mission.FloorGear ?? new List<DroppedGear>()).Where((d) => d.From == u.Id).Select((d) => d.Gear).ToList();
            return lost.Select(Gears.CloneGear).ToList();
        }

        /// <summary>
        /// A survivor brought out with no bed for them (Rescued null; Producto's review of 24/11): before they go, what they
        /// carry (all but their own knife) into the squad's packs, the first with room, piles onto piles; the fight's finds of
        /// it in the stage's (Found), and what they left and what found no room, lost, in the debrief (RunReport.NoBed).
        /// </summary>
        private static void HandOver(Unit vip, List<CampaignSoldier> home, RunReport report, RunFight fight)
        {
            var kit = KitBack(vip, null);
            if (kit.Melee?.Type == FoundWeaponIds.Knife) kit.Melee = null;
            var noBed = new RunNoBed { Name = vip.Name };
            foreach (var g in AllOf(kit).ToList())
            {
                var whole = g.Count;
                var into = home.FirstOrDefault((s) => Gears.CanStow(s.Kit!.Pack, g));
                if (into != null) Gears.Stow(into.Kit!.Pack, g);
                else foreach (var p in home.SelectMany((s) => s.Kit!.Pack.Items).ToList()) if (Gears.SamePile(p.Gear, g)) Gears.Merge(p.Gear, g);
                var came = into != null ? whole : whole - g.Count;
                if (came > 0)
                {
                    var gave = Gears.CloneGear(g);
                    gave.Count = came;
                    noBed.Gave.Add(gave);
                    if (GearNumber(g.Id) > fight.FirstGear) report.Found.Add(Gears.CloneGear(gave));
                }
                if (into == null && g.Count > 0) noBed.Lost.Add(Gears.CloneGear(g));
            }
            report.NoBed = noBed;
        }

        /// <summary>
        /// A survivor who joined (cuadro 140, M7), in the debrief as one of the squad: their row, the kills and the bars of what
        /// they did since they joined (Unit.deeds), the stage counted; home with what they carry (KitBack: the pack they had
        /// from when they joined), the knife numbered as a recruit's (NewKit), what they found in the stage's finds.
        /// </summary>
        private static void JoinedReport(CampaignState c, Unit vip, CampaignSoldier s, MissionReport report, RunFight fight, Rng rng)
        {
            var kit = KitBack(vip, s);
            // (A knife is never one of the finds: theirs, numbered before the fight or as one of a save of before.)
            report.Run!.Found.AddRange(AllOf(kit).Where((g) => GearNumber(g.Id) > fight.FirstGear && g.Type != FoundWeaponIds.Knife).Select(Gears.CloneGear));
            if (kit.Melee == null || string.IsNullOrEmpty(kit.Melee.Id)) kit.Melee = s.Kit?.Melee;
            s.Kit = kit;
            // The health it came out with, against its kit as it is now (a vest put on in the stage).
            if (c.Run != null && c.Run.Hp.ContainsKey(s.Id)) c.Run.Hp[s.Id] = Math.Max(1, Math.Min(MaxHp(s), vip.Hp));
            s.Missions++;
            s.Kills += vip.Kills;
            var entry = new ReportSoldier { Id = s.Id, Name = s.Name, Owner = s.Owner, Status = vip.Hp < MaxHp(s) ? ReportStatuses.Wounded : ReportStatuses.Ok, WoundedDays = 0, Kills = vip.Kills };
            var taught = Bars.Learn(s.Skills, s.Growth, vip.Deeds, rng, 0, vip.Lines);
            foreach (var bar in taught.Bars) Campaign.RunsLog(c, Lore.BarLine(s.Name, bar.Skill, bar.Growth, bar.From ?? 0), LogTones.Good);
            entry.Learned = taught.Gained;
            var deeds = new Deeds();
            foreach (var d in vip.Deeds ?? new Deeds()) deeds[d.Key] = d.Value;
            entry.Deeds = deeds;
            entry.Bars = taught.Bars;
            s.Skills = taught.Skills;
            s.Growth = taught.Growth;
            report.Squad.Add(entry);
        }

        /// <summary>
        /// The recruits a run's end gives (Runs.NobodyLeft: since N8 none, but on a bench, Tuning.Wipe), by id; null with
        /// none. RunReport.recruits (Red, N9: the line for whoever comes into the room after it).
        /// </summary>
        private static List<string>? Recruited(CampaignState c, Action end)
        {
            var had = new HashSet<string>(c.Soldiers.Select((s) => s.Id));
            end();
            var came = c.Soldiers.Where((s) => !had.Contains(s.Id)).Select((s) => s.Id).ToList();
            return came.Count > 0 ? came : null;
        }

        /// <summary>The number of a gear's id ("g17": 17), or 0.</summary>
        private static int GearNumber(string id)
        {
            return id.Length > 1 && int.TryParse(id.Substring(1), out var n) ? n : 0;
        }

        /// <summary>Every gear a kit and its pack hold.</summary>
        private static IEnumerable<Gear> AllOf(Kit k)
        {
            foreach (var g in new[] { k.Primary, k.Melee, k.Pistol, k.Vest }.Concat(k.Belt)) if (g != null) yield return g;
            foreach (var p in k.Pack.Items) yield return p.Gear;
        }

        /// <summary>What a soldier brings home from the unit it was: its kit's places, the belt with what is left of it, its pack.</summary>
        private static Kit KitBack(Unit u, CampaignSoldier? s)
        {
            // Copies: the mission's state stays the room's.
            var places = Gears.ClonePlaces(u.Kit ?? new KitPlaces());
            var belt = RunPack.BeltLeft(u);
            return new Kit
            {
                Primary = places.Primary,
                Melee = places.Melee,
                Pistol = places.Pistol,
                Vest = places.Vest,
                Belt = belt,
                Pack = WithoutKey(Gears.CloneGrid(u.Pack ?? s?.Kit?.Pack ?? new GearGrid { W = PackW, H = PackH })),
            };
        }

        /// <summary>A pack home without the fight's own: the armoury's key (G23.1) and Mordido's antiviral (G23.2).</summary>
        private static GearGrid WithoutKey(GearGrid pack)
        {
            pack.Items.RemoveAll((p) => p.Gear.Kind == GearKinds.Key || p.Gear.Kind == GearKinds.Antiviral || p.Gear.Kind == GearKinds.Charge);
            return pack;
        }

        /// <summary>
        /// A bench's (Tuning): when the whole squad fell, what of theirs comes back to the stash: a share of the § they
        /// carried (dropped where they fell, or still on them) and their firearms, while the stash has room.
        /// </summary>
        private static void Salvage(CampaignState c, GameState mission)
        {
            var t = Tuning.Current;
            if (c.Stash == null || (t.SalvageSupplies <= 0 && !t.SalvageFirearms)) return;
            var squad = new HashSet<string>(mission.Units.Where((u) => u.CampaignId != null && u.Team == Teams.Xcom).Select((u) => u.Id));
            var carried = (mission.FloorGear ?? new List<DroppedGear>()).Where((d) => d.From != null && squad.Contains(d.From)).Select((d) => d.Gear)
                .Concat(mission.Units.Where((u) => squad.Contains(u.Id)).SelectMany((u) => (u.Pack?.Items.Select((p) => p.Gear) ?? Enumerable.Empty<Gear>()).Concat(u.Kit != null ? KitSlots.All.Select((slot) => Gears.InSlot(u.Kit, slot)).Where((g) => g != null).Select((g) => g!) : Enumerable.Empty<Gear>())))
                .ToList();
            var supplies = (int)Js.Round(carried.Where((g) => g.Kind == GearKinds.Supplies).Sum((g) => g.Count) * t.SalvageSupplies);
            while (supplies > 0)
            {
                var pile = Math.Min(supplies, Content.PileOf(GearKinds.Supplies, null));
                if (Gears.Stow(c.Stash, Gears.NewGear(c, GearKinds.Supplies, null, pile)) > 0) break;
                supplies -= pile;
            }
            if (!t.SalvageFirearms) return;
            foreach (var g in carried.Where((g) => g.Kind == GearKinds.Weapon && Content.GearWeapon(g) is FoundWeaponDef w && w.Slot != FoundWeaponSlots.Melee).ToList()) Gears.Stow(c.Stash, Gears.CloneGear(g));
        }

        /// <summary>
        /// A run's fight folded back into the run (the room's ApplyMissionResult for a run campaign), once: what each
        /// soldier comes back with and its health; the fallen dead for good, their gear lost unless another took it up; the
        /// bars, as missions of before taught them; a special won, its reward into the packs; and then the run: back to the
        /// map, the chapter done with the boss dead, or over with the squad fallen. Its report is LastReport.
        /// </summary>
        public static MissionReport ApplyFight(CampaignState c, GameState mission, Rng rng)
        {
            var run = c.Run;
            var fight = mission.Run;
            var victory = mission.Outcome == Outcomes.Victory;
            var node = run != null ? RunMap.Node(run, run.At) : null;
            var report = new MissionReport
            {
                Day = c.Day,
                MissionName = mission.CampaignMission?.Name ?? "",
                Victory = victory,
                Reason = mission.OutcomeReason ?? EndReasons.Wiped,
                Squad = new List<ReportSoldier>(),
                Night = mission.Mission.Night == true ? true : (bool?)null,
                Run = new RunReport { Node = node?.Id ?? fight?.Node ?? "", NodeKind = node?.Kind ?? fight?.NodeKind ?? "", Stage = c.NodesPlayed ?? 0 },
            };
            c.LastReport = report;
            if (run == null || node == null || fight == null || run.Phase != RunPhases.Fight) return report;
            // The gear counter goes on from where the fight left it.
            c.NextId = Math.Max(c.NextId, fight.NextGear);
            var fought = 0;
            var standing = 0;
            foreach (var u in mission.Units)
            {
                var s = !string.IsNullOrEmpty(u.CampaignId) ? c.Soldiers.FirstOrDefault((x) => x.Id == u.CampaignId) : null;
                if (s == null) continue;
                fought++;
                s.Missions++;
                s.Kills += u.Kills;
                var entry = new ReportSoldier { Id = s.Id, Name = s.Name, Owner = s.Owner, Status = ReportStatuses.Ok, WoundedDays = 0, Kills = u.Kills };
                report.Squad.Add(entry);
                if (FellInFight(u))
                {
                    s.Alive = false;
                    s.FellDay = c.Day;
                    entry.Status = u.Lost == true ? ReportStatuses.Lost : ReportStatuses.Dead;
                    if (u.Lost == true) s.FellCause = FellCauses.Lost;
                    // What it carried: left on the floor where it fell (nobody took it up), or with it, left behind.
                    report.Run.Fallen.Add(new RunFallen { Id = s.Id, Name = s.Name, Lost = LostWith(u, mission) });
                    s.Kit = null;
                    run.Hp.Remove(s.Id);
                    run.Hands?.Remove(s.Id);
                    Campaign.RunsLog(c, u.Lost == true ? $"{s.Name} se quedó fuera cuando salió {Lore.Bus.Name}." : $"{s.Name} ha caído en combate.", LogTones.Bad);
                    continue;
                }
                standing++;
                s.Kit = KitBack(u, s);
                var hp = Math.Max(1, Math.Min(MaxHp(s), u.Hp));
                run.Hp[s.Id] = hp;
                if (hp < MaxHp(s)) entry.Status = ReportStatuses.Wounded;
                // What it did teaches it (docs/FASE10.md §3), as on the missions of before.
                var taught = Bars.Learn(s.Skills, s.Growth, u.Deeds, rng, 0, u.Lines);
                foreach (var bar in taught.Bars) Campaign.RunsLog(c, Lore.BarLine(s.Name, bar.Skill, bar.Growth, bar.From ?? 0), LogTones.Good);
                entry.Learned = taught.Gained;
                var deeds = new Deeds();
                foreach (var d in u.Deeds ?? new Deeds()) deeds[d.Key] = d.Value;
                entry.Deeds = deeds;
                entry.Bars = taught.Bars;
                s.Skills = taught.Skills;
                s.Growth = taught.Growth;
            }
            run.Squad = run.Squad.Where((id) => c.Soldiers.Any((s) => s.Id == id && s.Alive)).ToList();
            c.Squad = c.Squad.Where((id) => c.Soldiers.Any((s) => s.Id == id && s.Alive)).ToList();
            var home = run.Squad.Select((id) => c.Soldiers.First((s) => s.Id == id)).Where((s) => s.Kit != null).ToList();
            report.Run.Found = home.SelectMany((s) => AllOf(s.Kit!)).Where((g) => GearNumber(g.Id) > fight.FirstGear).Select(Gears.CloneGear).ToList();

            // A survivor brought out on La Lata (H4, N2: a Rescate's or one loose on the map): they join, with a knife and the
            // health they had, the squad fallen round them or not; into the squad, they stand for it (Red, 18/11: a Rescate won
            // by the survivor alone ended the run as wiped).
            if (!string.IsNullOrEmpty(mission.Mission.Survivor))
            {
                var vip = mission.Units.FirstOrDefault((u) => u.Id == mission.Mission.Survivor);
                if (vip != null && vip.Evacuated)
                {
                    var joined = Rescued(c, node, vip, rng, report.Run);
                    report.Run.Joined = joined?.Id;
                    // In the debrief as one of the squad (cuadro 140, M7): what they did since they joined, and what they found.
                    if (joined != null) JoinedReport(c, vip, joined, report, fight, rng);
                    // No bed for them: what they carry to the squad's packs, and what finds no room said (Producto's review, 24/11).
                    else HandOver(vip, home, report.Run, fight);
                }
                // Found and then fallen, or left behind when La Lata went (cuadro 140; Producto's review, 24/11): in the debrief
                // as a soldier would be, with what was lost with them; no soldier made.
                else if (vip != null && !vip.Captive && vip.Owner != null && FellInFight(vip))
                {
                    var left = vip.Lost == true;
                    report.Squad.Add(new ReportSoldier { Id = "", Name = vip.Name, Owner = vip.Owner.Value, Status = left ? ReportStatuses.Lost : ReportStatuses.Dead, WoundedDays = 0, Kills = vip.Kills });
                    report.Run.Fallen.Add(new RunFallen { Id = "", Name = vip.Name, Lost = LostWith(vip, mission) });
                    Campaign.RunsLog(c, left ? $"{vip.Name} se quedó fuera cuando salió {Lore.Bus.Name}." : $"{vip.Name} ha caído en combate.", LogTones.Bad);
                }
                if (report.Run.Joined != null && report.Run.InSquad == true) standing++;
            }
            // The squad fell: the run is over, and what they carried with it.
            if (fought > 0 && standing == 0)
            {
                report.Run.Ended = RunEnds.Wiped;
                if (Tuning.IsSet) Salvage(c, mission);
                report.Run.Recruits = Recruited(c, () => EndRun(c, rng, RunTexts.Wiped));
                return report;
            }
            // What a stage won gives, into the packs while there is room: a special's reward (a Sigilo's doubled unseen,
            // halved seen; a Guarida's was in its box; La llave's is its armoury, won as a Saqueo: G23.1; Saqueadores' its
            // stash, cuadro 99), the boss's 150 §
            // and a good weapon (H5, H6).
            int? won = null;
            if (victory && node.Kind == NodeKinds.Boss) won = BossReward;
            else if (victory && node.Kind != NodeKinds.Elite && node.Kind != NodeKinds.Key && node.Kind != NodeKinds.Raiders && RunMap.Rewarded(node.Kind))
            {
                won = rng.Range(RewardMin, RewardMax);
                // N9: the game's level's share.
                var loot = Levels.Of(c).Loot;
                if (loot != 1) won = (int)Js.Round(won.Value * loot);
                // Evacuación (G23.6, cuadro 100): 20 § more for each civilian aboard past two.
                if (mission.Mission.Civilians != null) won += Rules.CivilianBonus * Math.Max(0, Rules.CiviliansAboard(mission) - Rules.CiviliansNeeded);
                if (node.Kind == NodeKinds.Stealth)
                {
                    report.Run.Unseen = mission.Concealed;
                    won = mission.Concealed ? won * 2 : (won + 1) / 2;
                }
            }
            if (won != null && node.Kind == NodeKinds.Boss)
            {
                var maker = new RunLoot.Maker(c.NextId);
                var prize = RunLoot.GoodWeapon(rng, maker);
                c.NextId = maker.Next;
                foreach (var g in prize)
                {
                    var into = home.FirstOrDefault((s) => Gears.CanStow(s.Kit!.Pack, g));
                    if (into != null) Gears.Stow(into.Kit!.Pack, g);
                    else report.Run.PrizeLost = true;
                }
                report.Run.Prize = prize.Select(Gears.CloneGear).ToList();
            }
            if (won != null)
            {
                var reward = won.Value;
                var left = 0;
                foreach (var pile in Enumerable.Range(0, (reward + 99) / 100).Select((i) => Gears.NewGear(c, GearKinds.Supplies, null, Math.Min(100, reward - i * 100))).ToList())
                {
                    var into = home.FirstOrDefault((s) => Gears.CanStow(s.Kit!.Pack, pile));
                    if (into != null)
                    {
                        Gears.Stow(into.Kit!.Pack, pile);
                        continue;
                    }
                    // What no pack takes whole: as much as the piles there take; the rest is lost.
                    foreach (var p in home.SelectMany((s) => s.Kit!.Pack.Items)) if (Gears.SamePile(p.Gear, pile)) Gears.Merge(p.Gear, pile);
                    left += pile.Count;
                }
                report.Run.Reward = reward;
                report.Run.RewardLost = left > 0 ? left : (int?)null;
                Campaign.RunsLog(c, RunTexts.Reward(reward, left), LogTones.Good);
            }
            if (node.Kind == NodeKinds.Boss)
            {
                if (victory)
                {
                    report.Run.Ended = RunEnds.Boss;
                    NodeCleared(c, rng);
                }
                else
                {
                    report.Run.Ended = RunEnds.Returned;
                    report.Run.Recruits = Recruited(c, () => EndRun(c, rng));
                }
                return report;
            }
            NodeCleared(c, rng);
            if (c.Run != null) report.Run.Hp = new Dictionary<string, int>(c.Run.Hp);
            return report;
        }
    }
}
