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
            var hp = c.Run != null && c.Run.Hp.TryGetValue(s.Id, out var left) ? Math.Max(1, Math.Min(max, left)) : max;
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
            var squad = run.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)).Where((s) => s != null && s.Alive && s.Kit != null).Select((s) => SquadMemberOf(c, s!)).ToList();
            var seed = Math.Abs(node.Seed ?? 0);
            var alarm = kind == MissionKinds.Sabotage ? AlarmOf(node) : null;
            var name = RunTexts.FightName(node, run.Chapter);
            var state = Setup.CreateMission(new MissionOptions
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
                // A normal node's La Lata near the squad (Gameplay, 16/11): far was for the Travesía.
                EvacNear = node.Kind == NodeKinds.Normal ? true : (bool?)null,
            });
            // Nothing to search out of everyone's reach (Red, R3 17/11: a police car walled in).
            Setup.OnlyWithinReach(state);
            // As many tiles to get out by as La Lata has seats, by its doors (T21, Arturo 18/11).
            SeatZone(state);
            // One in RunMap.LooseOdds of the other fights, a survivor loose on the map, unmarked (N2, Arturo 17/11).
            if (RunMap.LooseSurvivor(node)) PlaceLoose(state, SurvivorOf(c, node), new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0x10057))));
            state.Run = new RunFight { Node = node.Id, NodeKind = node.Kind, Row = node.Row, Modifier = node.Modifier, Added = node.Added, FirstGear = c.NextId, NextGear = c.NextId };
            return (state, name);
        }

        // ------------------------------------------------------------ the result

        /// <summary>
        /// A Rescate's survivor (N2) as they wait on the map: a recruit with a knife and nothing else, a soldier's name
        /// (Lore, N8: not a VIP's), drawn from the node's seed so the fight and its end agree; nobody's yet, and not among
        /// the campaign's soldiers (nothing of `c` changes).
        /// </summary>
        public static SquadMember SurvivorOf(CampaignState c, RunNode node)
        {
            var s = Campaign.RunsSurvivor(c, new Rng(unchecked((uint)((node.Seed ?? 0) ^ 0x5a17))), SurvivorLines(node));
            var member = SquadMemberOf(c, s);
            member.CampaignId = "";
            member.Pack = null;
            member.Kit = null;
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
                    return new[] { (-0.5, -1.5), (-0.5, 1.5) };
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
        /// La Lata's zone cut to its seats (T21, Arturo 18/11: «solo la cantidad de huecos que hay y que estén donde
        /// corresponda con el vehículo, de sus puertas»): MissionState.Seats tiles, by its doors (DoorsOf); where a door's
        /// tile can't be stood on, the nearest free one within two; short of them, the free tiles nearest the vehicle. Who
        /// doesn't fit finds no tile (Rules.NoSeat as well). Without seats or the vehicle's place, as it was.
        /// </summary>
        public static void SeatZone(GameState s)
        {
            var m = s.Mission;
            var bus = m.EvacBus;
            var seats = m.Seats;
            if (seats == null || bus == null || bus.Tiles.Count == 0 || m.EvacZone.Count == 0) return;
            var d = Grid.Dirs4[bus.Heading];
            var o = new Vec2 { X = -d.Y, Y = d.X };
            double cx = bus.Tiles.Average((t) => t.X), cy = bus.Tiles.Average((t) => t.Y);
            // The side its zone was on: the doors that are on one side face it.
            var lean = m.EvacZone.Average((t) => (t.X - cx) * o.X + (t.Y - cy) * o.Y);
            var side = lean < 0 ? -1 : 1;
            var chosen = new List<Vec2>();
            bool Free(Vec2 p) => Grid.InBounds(s, p.X, p.Y) && !bus.Tiles.Any((t) => t.X == p.X && t.Y == p.Y) && !chosen.Any((t) => t.X == p.X && t.Y == p.Y)
                && Flights.CanStopAt(s, new Pos { X = p.X, Y = p.Y, Z = 0 });
            Vec2 At(double along, double out_) => new Vec2 { X = (int)Math.Floor(cx + d.X * along + o.X * out_ * side + 0.5), Y = (int)Math.Floor(cy + d.Y * along + o.Y * out_ * side + 0.5) };
            foreach (var (along, out_) in DoorsOf(s.Vehicle ?? 3))
            {
                if (chosen.Count >= seats.Value) break;
                var door = At(along, out_);
                if (Free(door))
                {
                    chosen.Add(door);
                    continue;
                }
                var near = new List<Vec2>();
                for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++) near.Add(new Vec2 { X = door.X + dx, Y = door.Y + dy });
                // The nearest to the door, and of those the nearest to the vehicle: the zone stays round it.
                int ToVehicle(Vec2 p) => bus.Tiles.Min((t) => Math.Max(Math.Abs(t.X - p.X), Math.Abs(t.Y - p.Y)));
                var other = near.Where(Free).OrderBy((p) => Math.Max(Math.Abs(p.X - door.X), Math.Abs(p.Y - door.Y))).ThenBy(ToVehicle).ThenBy((p) => Math.Abs(p.X - door.X) + Math.Abs(p.Y - door.Y)).FirstOrDefault();
                if (other != null) chosen.Add(other);
            }
            // Short of tiles (a door walled in; a run of before with more of the squad than seats): the free ones nearest it,
            // its old zone's first.
            if (chosen.Count < seats.Value)
            {
                var around = new List<Vec2>();
                for (var y = (int)cy - 6; y <= (int)cy + 6; y++) for (var x = (int)cx - 6; x <= (int)cx + 6; x++) around.Add(new Vec2 { X = x, Y = y });
                var old = m.EvacZone.ToList();
                foreach (var p in around.Where((p) => Grid.InBounds(s, p.X, p.Y)).OrderBy((p) => old.Any((t) => t.X == p.X && t.Y == p.Y) ? 0 : 1).ThenBy((p) => Js.Hypot(p.X - cx, p.Y - cy)).ThenBy((p) => p.Y).ThenBy((p) => p.X))
                {
                    if (chosen.Count >= seats.Value) break;
                    if (Free(p)) chosen.Add(p);
                }
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
            bool Bed(int p) => c.Soldiers.Count((s) => s.Owner == p && s.Alive) < Campaign.SoldierCap(c);
            var talker = vip.Owner ?? Campaign.RunsNeediest(c);
            int? owner = Bed(talker) ? talker : c.Players.Where((p) => p != talker && Bed(p)).Select((p) => (int?)p).FirstOrDefault();
            if (owner == null) return null;
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

        /// <summary>
        /// The recruits a run's end gives (Runs.NobodyLeft: a player left with nobody gets one with a knife), by id; null with
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
        private static Kit KitBack(Unit u, CampaignSoldier s)
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
                Pack = Gears.CloneGrid(u.Pack ?? s.Kit?.Pack ?? new GearGrid { W = PackW, H = PackH }),
            };
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
            bool Fell(Unit u) => (!u.Alive && !u.Evacuated && u.Hp <= 0) || u.Lost == true;
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
                if (Fell(u))
                {
                    s.Alive = false;
                    s.FellDay = c.Day;
                    entry.Status = u.Lost == true ? ReportStatuses.Lost : ReportStatuses.Dead;
                    // What it carried: left on the floor where it fell (nobody took it up), or with it, left behind.
                    var lost = u.Lost == true ? (u.Kit != null ? new[] { u.Kit.Primary, u.Kit.Melee, u.Kit.Pistol, u.Kit.Vest }.Concat(u.Kit.Belt).Where((g) => g != null).Select((g) => g!).Concat(u.Pack?.Items.Select((p) => p.Gear) ?? new Gear[0]).ToList() : new List<Gear>())
                        : (mission.FloorGear ?? new List<DroppedGear>()).Where((d) => d.From == u.Id).Select((d) => d.Gear).ToList();
                    report.Run.Fallen.Add(new RunFallen { Id = s.Id, Name = s.Name, Lost = lost.Select(Gears.CloneGear).ToList() });
                    s.Kit = null;
                    run.Hp.Remove(s.Id);
                    run.Hands?.Remove(s.Id);
                    Campaign.RunsLog(c, $"{s.Name} ha caído en combate.", LogTones.Bad);
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
            // health they had; the squad fallen
            // round them too.
            if (!string.IsNullOrEmpty(mission.Mission.Survivor))
            {
                var vip = mission.Units.FirstOrDefault((u) => u.Id == mission.Mission.Survivor);
                if (vip != null && vip.Evacuated) report.Run.Joined = Rescued(c, node, vip, rng, report.Run)?.Id;
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
            // halved seen; a Guarida's was in its box), the boss's 150 § and a good weapon (H5, H6).
            int? won = null;
            if (victory && node.Kind == NodeKinds.Boss) won = BossReward;
            else if (victory && node.Kind != NodeKinds.Elite && RunMap.Specials.Concat(RunMap.NewSpecials).Contains(node.Kind))
            {
                won = rng.Range(RewardMin, RewardMax);
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
