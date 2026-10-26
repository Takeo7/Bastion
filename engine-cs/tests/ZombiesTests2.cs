using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;
using static Bastion.Engine.Tests.ZombiesTests;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/zombies.test.ts, second half (searching, the Saqueo, loot in the campaign, the backpack,
    /// replication, the dead and the living); ZombiesTests has the first and the helpers. `report.loot toEqual` is compared
    /// through its JSON, nulls apart (CampaignJsonTests.FirstDifference).
    /// </summary>
    public class ZombiesTests2
    {
        private static Loot Supplies(int n) => new Loot { Supplies = n };
        private static Loot Medicines(int n) => new Loot { Medicines = n };
        private static Loot Food(int n) => new Loot { Food = n };
        private static Loot Parts(int n) => new Loot { Parts = n };
        private static Loot ItemLoot(string item) => new Loot { Item = item };

        private static List<Loot> Pack(params Loot[] loot) => loot.ToList();

        // ------------------------------------------------------------ searching

        /// <summary>A medicine box at (3,1) against a wall line, a car outside (docs/FASE8.md §B).</summary>
        private static GameState House()
        {
            var s = StateFrom(new[] { "........", "...H....", "..##....", "........", ".....h.." });
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            s.Decor[1 * 8 + 3] = Decor.DecorCode(DecorKinds.LootMedical);
            s.Decor[4 * 8 + 5] = Decor.DecorCode(DecorKinds.CarRear);
            return s;
        }

        [Fact(DisplayName = "searching › one action beside a loot box: always what it is marked for, and it makes a little noise")]
        public void OneActionBesideALootBox()
        {
            var s = House();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 2, 1);
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 4, 2);
            PodOf(s, "p1", false, z);
            SamePlaces(new[] { P(3, 1) }, Looting.SearchableTiles(s, u));
            Assert.True(Abilities.CanTargetTile(s, u, AbilityIds.Search, P(3, 1)));
            var r = Exec(s, Ability("s1", AbilityIds.Search, tile: P(3, 1)), seed: 3);
            Assert.True(r.Ok);
            var found = Of<SearchedEvent>(r.Events!)[0];
            Assert.Equal("s1", found.Unit);
            Assert.Equal((3, 1), (found.Tile.X, found.Tile.Y));
            Assert.NotNull(found.Loot?.Medicines);
            Assert.True(found.Loot!.Medicines >= 1);
            SameJson(new[] { found.Loot }, u.Backpack);
            var noise = Of<NoiseEvent>(r.Events!);
            Assert.NotEmpty(noise);
            Assert.Equal((3, 1), (noise[0].Tile.X, noise[0].Tile.Y));
            Assert.Equal(Louder(2), noise[0].Radius);
            Assert.Equal(new[] { "a1" }, noise[0].Heard);
            Assert.Equal(1, u.Ap);
            Assert.Equal(new[] { 1 * 8 + 3 }, s.Searched);
            Assert.False(Looting.IsSearchable(s, P(3, 1)));
            Assert.Equal("Ya está abierta.", Abilities.AbilityBlocker(s, u, AbilityIds.Search));
        }

        [Fact(DisplayName = "searching › a full backpack leaves the find on the floor; the box at hand, no wall in between; furniture and cars are no longer searched")]
        public void AFullBackpackLeavesTheFindOnTheFloor()
        {
            var s = House();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 2, 1, extra: (x) => x.Backpack = Pack(Supplies(5), Medicines(1), ItemLoot(ItemIds.FirstAid)));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Search));
            var r = Exec(s, Ability("s1", AbilityIds.Search, tile: P(3, 1)), seed: 3);
            Assert.True(r.Ok);
            Assert.Equal(3, u.Backpack!.Count);
            Assert.NotNull(s.Dropped);
            Assert.Single(s.Dropped!);
            var dropped = s.Dropped![0];
            Assert.Equal("l1", dropped.Id);
            SamePlace(P(2, 1), dropped.Pos);
            Assert.NotNull(dropped.Loot.Medicines);
            Assert.Null(dropped.From);
            s.Searched = new List<int>();
            u.Pos = P(0, 0);
            Assert.Equal("No hay ninguna caja al lado.", Abilities.AbilityBlocker(s, u, AbilityIds.Search));
            // Diagonally round the corner of a wall: out of reach.
            u.Pos = P(4, 2);
            SamePlaces(new[] { P(3, 1) }, Looting.SearchableTiles(s, u));
            s.Tiles[2 * 8 + 4] = TileKinds.Floor;
            s.Tiles[1 * 8 + 4] = TileKinds.Wall;
            s.Tiles[2 * 8 + 3] = TileKinds.Wall;
            Assert.Empty(Looting.SearchableTiles(s, u));
            // A car's boot is not searched any more.
            u.Pos = P(4, 4);
            Assert.Empty(Looting.SearchableTiles(s, u));
            foreach (var kind in new[] { "wardrobe", "table", "desk", "shelf", "counter", "cabinet", "bed", "crates", "rack" }) Assert.Contains(kind, Looting.Searchable);
        }

        [Fact(DisplayName = "searching › every map has its special boxes: up to 3 (4 on the maps of 64 and 72), each of a different kind, away from the start (docs/FASE8.md §B)")]
        public void EveryMapHasItsSpecialBoxes()
        {
            var kinds = new[] { DecorKinds.LootWeapons, DecorKinds.LootMedical, DecorKinds.LootAmmo, DecorKinds.LootFood };
            foreach (var biome in new[] { Biomes.City, Biomes.Wilds, Biomes.Facility })
            {
                for (var seed = 1; seed <= 4; seed++)
                {
                    var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, seed), Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Scavenge });
                    var name = $"{biome} {seed}";
                    var boxes = s.Decor!.SelectMany((d, i) =>
                    {
                        var k = Decor.DecorOf(d)?.Kind;
                        return k != null && kinds.Contains(k) ? new[] { (k, x: i % s.Width, y: i / s.Width) } : new (string k, int x, int y)[0];
                    }).ToList();
                    Assert.True(boxes.Count >= 1, name);
                    Assert.True(boxes.Count <= (biome == Biomes.Facility ? 3 : 4), name);
                    Assert.Equal(boxes.Count, boxes.Select((b) => b.k).Distinct().Count());
                    var squad = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
                    foreach (var b in boxes) Assert.True(squad.Min((u) => Js.Hypot(b.x - u.Pos.X, b.y - u.Pos.Y)) >= 10, $"{name}: {b}");
                }
            }
        }

        [Fact(DisplayName = "searching › a room is searched once for the whole squad, from inside it; its furniture says what kind of place it is")]
        public void ARoomIsSearchedOnceForTheWholeSquad()
        {
            // Two rooms (an office with a desk, and a home with a bed) parted by a door, the street outside.
            var s = StateFrom(new[] { "..........", ".########.", ".#.H#..h#.", ".#..+...#.", ".########.", ".........." });
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            s.Decor[2 * 10 + 3] = Decor.DecorCode(DecorKinds.Desk);
            s.Decor[2 * 10 + 7] = Decor.DecorCode(DecorKinds.Bed);
            s.Ground = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            foreach (var (x, y) in new[] { (2, 2), (2, 3), (3, 3), (5, 2), (6, 2), (5, 3), (6, 3), (7, 3) }) s.Ground[y * 10 + x] = 2;
            var office = Looting.RoomAt(s, P(2, 2))!;
            var home = Looting.RoomAt(s, P(6, 3))!;
            Assert.Equal(Spots.Office, office.Spot);
            Assert.Equal(Spots.Home, home.Spot);
            Assert.NotEqual(office.Id, home.Id);
            Assert.Null(Looting.RoomAt(s, P(4, 3)));
            Assert.Null(Looting.RoomAt(s, P(0, 0)));
            var a = AddUnit(s, "s1", TemplateIds.Rookie, 3, 3);
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 2, 2);
            Assert.Null(Abilities.AbilityBlocker(s, a, AbilityIds.SearchRoom));
            var r = Exec(s, Ability("s1", AbilityIds.SearchRoom), seed: 5);
            Assert.True(r.Ok);
            var searched = Of<RoomSearchedEvent>(r.Events!);
            Assert.NotEmpty(searched);
            Assert.Equal(("s1", office.Id), (searched[0].Unit, searched[0].Room));
            Assert.Equal(new[] { office.Id }, s.RoomsSearched);
            Assert.Equal("Esta habitación ya está registrada.", Abilities.AbilityBlocker(s, b, AbilityIds.SearchRoom));
            b.Pos = P(6, 3);
            Assert.Null(Abilities.AbilityBlocker(s, b, AbilityIds.SearchRoom));
            b.Pos = P(0, 0);
            Assert.Equal("Solo dentro de una habitación.", Abilities.AbilityBlocker(s, b, AbilityIds.SearchRoom));
        }

        /// <summary>`Object.keys(loot)[0]`: the loot's first field, as JSON writes it (in the order TypeScript makes it).</summary>
        private static string FirstKey(Loot loot)
        {
            var json = JsonSerializer.SerializeToElement(loot, Options);
            return json.EnumerateObject().First().Name;
        }

        [Fact(DisplayName = "searching › the loot comes from a seeded table: suministros (5–20), an accessory, medicines or nothing")]
        public void TheLootComesFromASeededTable()
        {
            Loot? Roll(int seed, int difficulty = 2) => Looting.RollLoot(DecorKinds.CarRear, difficulty, new Rng(seed));
            for (var seed = 1; seed < 20; seed++) SameJson(Roll(seed), Roll(seed));
            var kinds = new HashSet<string>();
            for (var seed = 1; seed < 400; seed++)
            {
                foreach (var difficulty in new[] { 1, 4 })
                {
                    var loot = Roll(seed, difficulty);
                    kinds.Add(loot != null ? FirstKey(loot) : "nothing");
                    if (loot?.Supplies != null && loot.Supplies != 0)
                    {
                        Assert.True(loot.Supplies >= 5);
                        Assert.True(loot.Supplies <= 20);
                    }
                    // Only what the difficulty opens: no armour-piercing rounds in an easy town.
                    if (difficulty == 1) Assert.NotEqual(ItemIds.ApRounds, loot?.Item);
                }
            }
            // Since phase 4, rations, parts and now and then a good weapon too (docs/FASE4.md); since Fase 9, an improvement for one.
            Assert.Equal(new[] { "food", "item", "medicines", "mod", "nothing", "parts", "supplies", "weapon" }, kinds.OrderBy((k) => k, StringComparer.Ordinal));
            Assert.Null(Looting.RollLoot(DecorKinds.Tree, 2, new Rng(1)));
        }

        // ------------------------------------------------------------ the Saqueo

        private static void Nothing(LootTotals t)
        {
            Assert.Equal((0, 0, 0, 0), (t.Supplies, t.Medicines, t.Food, t.Parts));
            Assert.Empty(t.Items);
            Assert.Empty(t.Weapons);
            Assert.Null(t.Arms);
            Assert.Null(t.Returned);
            Assert.Null(t.Mods);
        }

        [Fact(DisplayName = "the Saqueo › a town with plenty to search, an evac and no timer; the dead scattered about, the horde to come")]
        public void ATownWithPlentyToSearch()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 5), Slots = new List<int> { 0, 1 }, Rng = new Rng(1), Kind = MissionKinds.Scavenge, Difficulty = 2 });
            Assert.Equal("Saqueo", Setup.MissionNames[MissionKinds.Scavenge]);
            Assert.Equal((MissionKinds.Scavenge, (int?)null, true, (int?)2, 0), (s.Mission.Kind, s.Mission.TurnsLeft, s.Mission.Item == null, s.Mission.Difficulty, s.Mission.Reinforcements.Count));
            Assert.NotEmpty(s.Mission.EvacZone);
            Assert.Equal(Setup.HordeWave(2), s.Mission.Horde);
            Assert.True(s.Units.Where((u) => u.Team == Teams.Alien).All((u) => Content.Templates[u.Template].Undead));
            // Rooms to search (docs/FASE8.md §B), and a box or so.
            // On every storey (the flats and the houses upstairs are searched too: docs/MAPAS.md phase 4).
            var rooms = new HashSet<int>();
            for (var z = 0; z <= (s.Levels?.Count ?? 0); z++)
            {
                for (var i = 0; i < s.Tiles.Count; i++)
                {
                    var room = Looting.RoomAt(s, P(i % s.Width, i / s.Width, z));
                    if (room != null) rooms.Add(room.Id);
                }
            }
            Assert.True(rooms.Count > 20, $"{rooms.Count} rooms");
        }

        [Fact(DisplayName = "the Saqueo › is won by getting somebody out; what they carry counts, a fallen soldier’s is lost")]
        public void IsWonByGettingSomebodyOut()
        {
            var s = Field();
            s.Mission.Kind = MissionKinds.Scavenge;
            s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 0 } };
            var out_ = AddUnit(s, "s1", TemplateIds.Rookie, 0, 0, extra: (u) => u.Backpack = Pack(Supplies(12), ItemLoot(ItemIds.FirstAid)));
            var fallen = AddUnit(s, "s2", TemplateIds.Rookie, 5, 5, extra: (u) => u.Backpack = Pack(Supplies(20), Medicines(1)));
            AddUnit(s, "a1", TemplateIds.Zombie, 15, 15);
            var run = NewSim(s);
            // The bus leaves with s1 in it; s2, out in the street, is lost with what it carries (docs/PRUEBA-2026-10-14.md P1).
            run.CallEvac(0, out_);
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.True(fallen.Lost);
            var back = Looting.BroughtBack(s);
            Assert.Equal(12, back.Supplies);
            Assert.Equal(new[] { ItemIds.FirstAid }, back.Items);
            Assert.Equal(0, back.Medicines);

            // Everyone down: lost, and nothing comes home.
            var t = Field();
            t.Mission.Kind = MissionKinds.Scavenge;
            t.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 0 } };
            var last = AddUnit(t, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => u.Backpack = Pack(Supplies(9)));
            AddUnit(t, "a1", TemplateIds.Zombie, 15, 15);
            var lose = NewSim(t);
            lose.Damage(last, 99, 0);
            lose.CheckOutcome();
            Assert.Equal(Outcomes.Defeat, t.Outcome);
            Nothing(Looting.BroughtBack(t));
        }

        [Fact(DisplayName = "the Saqueo › the standing squad brings its loot home from any mission it wins")]
        public void TheStandingSquadBringsItsLootHome()
        {
            var s = Field();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 0, 0, extra: (x) => x.Backpack = Pack(Supplies(7), Medicines(1)));
            var a = AddUnit(s, "a1", TemplateIds.Zombie, 3, 3, extra: (x) => x.Hp = 1);
            var win = NewSim(s);
            win.Damage(a, 5, 0, u);
            win.CheckOutcome();
            Assert.Equal(Outcomes.Victory, s.Outcome);
            var back = Looting.BroughtBack(s);
            Assert.Equal(7, back.Supplies);
            Assert.Empty(back.Items);
            Assert.Equal(1, back.Medicines);
        }

        [Fact(DisplayName = "the Saqueo › the horde comes in by the edges every few turns, sooner the more noise, on the prowl")]
        public void TheHordeComesInByTheEdges()
        {
            GameState Setup()
            {
                var st = Field(40, 40);
                st.Mission.Kind = MissionKinds.Scavenge;
                st.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 39, Y = 39 } };
                st.Mission.Horde = new List<string> { TemplateIds.Zombie, TemplateIds.Zombie };
                st.Concealed = true;
                AddUnit(st, "s1", TemplateIds.Rookie, 20, 20);
                return st;
            }
            List<GameEvent> Turns(GameState st, int n)
            {
                var events = new List<GameEvent>();
                for (var i = 0; i < n; i++)
                {
                    var turn = NewSim(st, i + 1);
                    turn.RunAlienTurn();
                    events.AddRange(turn.Events);
                }
                return events;
            }
            // Quiet: the first wave is announced once the turns alone fill the clock (the fifth), to land a turn later.
            var quiet = Setup();
            var first = Content.Horde.Wave / Content.Horde.PerTurn;
            // (The few that come every turn, playtest 2026-10-15 #11, apart.)
            List<ReinforcementsIncomingEvent> Waves(List<GameEvent> events) => Of<ReinforcementsIncomingEvent>(events).Where((e) => e.R.Trickle != true).ToList();
            var announced = Waves(Turns(quiet, first));
            Assert.Equal(new[] { first + 1 }, announced.Select((e) => e.R.Turn));
            var r = announced[0].R;
            Assert.True(r.Horde);
            // No exact spot is ever announced (playtest 2026-10-13).
            Assert.Null(r.Pos);
            // It lands unaware by an edge, far from the squad, heading for it: the squad stays hidden until they see it.
            var landing = Turns(quiet, 1);
            var spawned = Of<SpawnedEvent>(landing).First((e) => e.Units.Count == r.Units.Count);
            foreach (var u in spawned.Units)
            {
                Assert.True(u.Pos.X < 2 || u.Pos.Y < 2 || u.Pos.X >= 38 || u.Pos.Y >= 38);
                Assert.True(Js.Hypot(u.Pos.X - 20, u.Pos.Y - 20) >= 12);
            }
            Assert.False(spawned.Pod.Active);
            Assert.True(spawned.Units.All((u) => u.Heard != null && u.Heard.X == 20 && u.Heard.Y == 20));
            // (The few that came before it, every turn, may have found the squad by now: the wave itself doesn't.)
            Assert.DoesNotContain(Of<ConcealmentBrokenEvent>(landing), (e) => spawned.Units.Any((u) => u.Id == e.By));
            // Noisy: a firefight's worth of noise brings it on the first turn.
            var loud = Setup();
            loud.Mission.Noise = Content.Horde.Wave - Content.Horde.PerTurn;
            Assert.Equal(new[] { 2 }, Waves(Turns(loud, 1)).Select((e) => e.R.Turn));
        }

        [Fact(DisplayName = "the Saqueo › a few of the dead come in every turn on any mission left by bus, twice as many while a sabotage alarm sounds (playtest 2026-10-15, #11, #14)")]
        public void AFewOfTheDeadComeInEveryTurn()
        {
            List<int> Trickles(string kind, int difficulty)
            {
                var s = Field(40, 40);
                s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 39, Y = 39 } };
                s.Mission.Horde = new List<string> { TemplateIds.Zombie, TemplateIds.Zombie };
                AddUnit(s, "s1", TemplateIds.Rookie, 20, 20);
                s.Mission.Kind = kind;
                s.Mission.Difficulty = difficulty;
                if (kind == MissionKinds.Elimination) s.Mission.Horde = null;
                var t = NewSim(s, 1);
                t.RunAlienTurn();
                return Of<ReinforcementsIncomingEvent>(t.Events).Where((e) => e.R.Trickle == true).Select((e) => e.R.Units.Count).ToList();
            }
            Assert.Equal(new[] { Content.Horde.Trickle[1] }, Trickles(MissionKinds.Scavenge, 1));
            Assert.Equal(new[] { Content.Horde.Trickle[4] }, Trickles(MissionKinds.Scavenge, 4));
            Assert.Equal(new[] { 2 * Content.Horde.Trickle[2] }, Trickles(MissionKinds.Sabotage, 2));
            // A Limpieza ends when they're all down: nobody keeps coming.
            Assert.Empty(Trickles(MissionKinds.Elimination, 3));
        }

        // ------------------------------------------------------------ loot in the campaign

        /// <summary>A campaign of two whose first offer, made a mission of `kind`, has been chosen; and that mission.</summary>
        private static (CampaignState c, MissionOffer offer, GameState mission) Flown(string kind)
        {
            var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(2));
            var offer = c.Offers[0];
            offer.Kind = kind;
            CampaignHelpers.Ok(c, new ChooseMissionCommand { Id = offer.Id });
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0, 1 }, Rng = new Rng(3), Kind = kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods, Difficulty = offer.Difficulty });
            return (c, offer, mission);
        }

        [Fact(DisplayName = "loot in the campaign › suministros go to the credits, accessories to the inventory, medicines to their counter; the report says it")]
        public void SuministrosGoToTheCreditsAccessoriesToTheInventory()
        {
            var (c, offer, mission) = Flown(MissionKinds.Scavenge);
            var squad = mission.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            var (a, b, d) = (squad[0], squad[1], squad[2]);
            var carried = Pack(Supplies(12), ItemLoot(ItemIds.SmokeGrenade), Medicines(1));
            a.Alive = false;
            a.Evacuated = true;
            a.Backpack = carried;
            b.Alive = false;
            b.Hp = 0;
            b.Backpack = Pack(Supplies(20));
            d.Alive = false;
            d.Evacuated = true;
            d.Backpack = Pack(Medicines(1));
            foreach (var u in mission.Units)
            {
                if (u.Team == Teams.Xcom && u.Alive)
                {
                    u.Alive = false;
                    u.Evacuated = true;
                }
            }
            mission.Outcome = Outcomes.Victory;
            mission.OutcomeReason = EndReasons.Objective;
            var credits = c.Credits;
            var smoke = c.Inventory.TryGetValue(ItemIds.SmokeGrenade, out var copies) ? copies : 0;
            // No enclave convoy while the squad flies back, to count only the loot (and the reward's).
            c.NextMedicineDay = 999;
            var medicines = c.Medicines ?? 0;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            var expected = new MissionLoot { Supplies = 12, Items = new List<string> { ItemIds.SmokeGrenade }, Medicines = 2, Food = 0, Parts = 0, Weapons = new List<string>(), Line = Lore.LootSummary(12, new[] { ItemIds.SmokeGrenade }, 2) };
            Newtonsoft.Json.Linq.JToken Json(object x) => CampaignJsonTests.Clean(Newtonsoft.Json.Linq.JToken.Parse(CampaignJsonTests.Write(x)));
            Assert.NotNull(report.Loot);
            var difference = CampaignJsonTests.FirstDifference(Json(expected), Json(report.Loot!));
            Assert.True(difference == null, difference);
            Assert.Matches("12", report.Loot!.Line);
            Assert.Equal(medicines + 2 + (offer.Reward.Medicines ?? 0), c.Medicines);
            Assert.Equal(smoke + 1, c.Inventory[ItemIds.SmokeGrenade]);
            // The reward and the loot; no bodies from a Saqueo: the squad left by the evac.
            Assert.Equal(0, report.Bodies);
            Assert.True(c.Credits >= credits + 12 + (offer.Reward.Credits ?? 0), $"{c.Credits} credits");
        }

        [Fact(DisplayName = "loot in the campaign › a lost mission brings home only what was evacuated")]
        public void ALostMissionBringsHomeOnlyWhatWasEvacuated()
        {
            var (c, _, mission) = Flown(MissionKinds.Elimination);
            var squad = mission.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            var (a, b) = (squad[0], squad[1]);
            a.Alive = false;
            a.Evacuated = true;
            a.Backpack = Pack(Supplies(8));
            b.Backpack = Pack(Supplies(30));
            mission.Outcome = Outcomes.Defeat;
            mission.OutcomeReason = EndReasons.Abandoned;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            Assert.NotNull(report.Loot);
            Assert.Equal((8, 0), (report.Loot!.Supplies, report.Loot.Medicines));
            Assert.Empty(report.Loot.Items);
        }

        [Fact(DisplayName = "loot in the campaign › Saqueos turn up among the offers, in towns and full of the dead; older campaigns keep working")]
        public void SaqueosTurnUpAmongTheOffers()
        {
            var offers = new List<MissionOffer>();
            for (var seed = 1; seed < 30; seed++)
            {
                var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(seed));
                for (var i = 0; i < 6; i++) Campaign.ExecuteCampaignCommand(c, new AdvanceCommand { Ready = true }, new CampaignContext { Slot = 0, Connected = new[] { 0 }, Rng = new Rng(seed * 10 + i) });
                offers.AddRange(c.Offers.Where((o) => o.Kind == MissionKinds.Scavenge));
            }
            Assert.NotEmpty(offers);
            foreach (var o in offers)
            {
                Assert.Equal((MapSourceKinds.Generated, Biomes.City), (o.Map.Kind, o.Map.Biome));
                Assert.True(o.Pods.SelectMany((p) => p).All((t) => Content.Templates[t].Undead));
            }
            var old = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            old.Medicines = null;
            var migrated = Campaign.MigrateCampaign(old);
            Assert.Equal(Campaign.CampaignVersion, migrated.Version);
            Assert.Equal(0, migrated.Medicines ?? 0);
        }

        // ------------------------------------------------------------ the backpack (docs/FASE8.md §C)

        private static CommandResult Item(GameState s, string unit, string action, int? index = null, string? id = null, string? to = null)
        {
            return Exec(s, new ItemCommand { Unit = unit, Action = action, Index = index, Id = id, To = to });
        }

        private static void Refused(string error, CommandResult r)
        {
            Assert.False(r.Ok);
            Assert.Equal(error, r.Error);
        }

        // A zombie asleep far away, so the mission goes on.
        private static GameState BackpackField()
        {
            var s = StateFrom(Enumerable.Range(0, 20).Select((_) => new string('.', 20)).ToArray());
            PodOf(s, "p1", false, AddUnit(s, "a1", TemplateIds.Zombie, 19, 19));
            return s;
        }

        [Fact(DisplayName = "the backpack (docs/FASE8.md §C) › dropping and taking are free, even with no actions left; what is dropped stays on the tile for anyone")]
        public void DroppingAndTakingAreFree()
        {
            var s = BackpackField();
            var a = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => { u.Ap = 0; u.Backpack = Pack(Food(3), Medicines(1)); });
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 9, 9, extra: (u) => u.Backpack = Pack(Supplies(4), Parts(2), Food(1)));
            Assert.True(Item(s, "s1", ItemActions.Drop, index: 0).Ok);
            SameJson(Pack(Medicines(1)), a.Backpack);
            SameJson(new[] { new DroppedLoot { Id = "l1", Pos = P(5, 5), Loot = Food(3) } }, s.Dropped);
            Refused("Aquí no hay nada que coger.", Item(s, "s2", ItemActions.Take, id: "l1"));
            b.Pos = P(5, 5);
            Refused("La mochila está llena: suelta algo antes.", Item(s, "s2", ItemActions.Take, id: "l1"));
            Assert.True(Item(s, "s1", ItemActions.Take, id: "l1").Ok);
            SameJson(Pack(Medicines(1), Food(3)), a.Backpack);
            Assert.NotNull(s.Dropped);
            Assert.Empty(s.Dropped!);
            Assert.Equal(0, a.Ap);
            // The Chapistería's one more slot.
            b.PackSlots = 4;
            a.Backpack!.RemoveAt(a.Backpack.Count - 1);
            s.Dropped = new List<DroppedLoot> { new DroppedLoot { Id = "l9", Pos = P(5, 5), Loot = Supplies(9) } };
            Assert.True(Item(s, "s2", ItemActions.Take, id: "l9").Ok);
            Assert.Equal(4, b.Backpack!.Count);
        }

        [Fact(DisplayName = "the backpack (docs/FASE8.md §C) › giving takes an action and a partner next to you with room")]
        public void GivingTakesAnAction()
        {
            var s = BackpackField();
            var a = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => u.Backpack = Pack(Parts(2)));
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 7, 5, extra: (u) => u.Backpack = Pack(Supplies(4), Parts(2), Food(1)));
            Refused("Tiene que estar a tu lado.", Item(s, "s1", ItemActions.Give, index: 0, to: "s2"));
            b.Pos = P(6, 6);
            Refused("No le cabe: lleva la mochila llena.", Item(s, "s1", ItemActions.Give, index: 0, to: "s2"));
            b.Backpack!.RemoveAt(b.Backpack.Count - 1);
            var ap = a.Ap;
            Assert.True(Item(s, "s1", ItemActions.Give, index: 0, to: "s2").Ok);
            SameJson(Pack(), a.Backpack);
            SameJson(Pack(Supplies(4), Parts(2), Parts(2)), b.Backpack);
            Assert.Equal(ap - 1, a.Ap);
            Refused("No lleva eso en la mochila.", Item(s, "s1", ItemActions.Give, index: 0, to: "s2"));
        }

        [Fact(DisplayName = "the backpack (docs/FASE8.md §C) › a medicine heals 3 on you or a partner next to you, for an action; the contagion only stops once unlocked")]
        public void AMedicineHeals3()
        {
            var s = BackpackField();
            var a = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => u.Backpack = Pack(Medicines(2), Food(1)));
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 6, 5, extra: (u) => { u.Hp = 1; u.Infected = true; });
            Refused("Solo se usan las medicinas.", Item(s, "s1", ItemActions.Use, index: 1));
            Refused("No tienes heridas.", Item(s, "s1", ItemActions.Use, index: 0));
            var r = Item(s, "s1", ItemActions.Use, index: 0, to: "s2");
            Assert.True(r.Ok);
            Assert.Equal(4, b.Hp);
            Assert.Equal(true, b.Infected);
            Assert.Empty(Of<CuredEvent>(r.Events!));
            SameJson(Pack(Medicines(1), Food(1)), a.Backpack);
            // Healthy but bitten: with the Enfermería at level 2, the medicine is worth it again, and it stops the bite.
            b.Hp = b.MaxHp;
            Refused("No tiene heridas.", Item(s, "s1", ItemActions.Use, index: 0, to: "s2"));
            s.Mission.MedicineCures = true;
            Assert.True(Item(s, "s1", ItemActions.Use, index: 0, to: "s2").Ok);
            Assert.Equal(false, b.Infected);
            SameJson(Pack(Food(1)), a.Backpack);
        }

        [Fact(DisplayName = "the backpack (docs/FASE8.md §C) › the support talent stops the bite for its own soldier only")]
        public void TheSupportTalentStopsTheBite()
        {
            var s = BackpackField();
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => { u.Perks = new List<string> { PerkIds.LightHands }; u.Backpack = Pack(Medicines(1)); });
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 6, 5, extra: (u) => { u.Hp = 2; u.Infected = true; });
            Assert.True(Item(s, "s1", ItemActions.Use, index: 0, to: "s2").Ok);
            Assert.Equal(false, b.Infected);
        }

        [Fact(DisplayName = "the backpack (docs/FASE8.md §C) › the Chapistería gives one more slot, and the Enfermería at level 2 makes medicines stop the bite")]
        public void TheChapisteriaGivesOneMoreSlot()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            Assert.True(Campaign.BuildSquad(c).All((m) => m.PackSlots == null));
            Assert.False(Campaign.MedicineCures(c));
            CampaignHelpers.Raise(c, PieceIds.Garage);
            CampaignHelpers.Raise(c, PieceIds.Infirmary, 2);
            Assert.True(Campaign.BuildSquad(c).All((m) => m.PackSlots == 4));
            Assert.True(Campaign.MedicineCures(c));
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 3), Slots = new List<int> { 0 }, Rng = new Rng(1), Squad = Campaign.BuildSquad(c), MedicineCures = Campaign.MedicineCures(c) });
            Assert.Equal(true, s.Mission.MedicineCures);
            Assert.True(s.Units.Where((u) => u.Team == Teams.Xcom).All((u) => u.PackSlots == 4));
        }

        [Fact(DisplayName = "the backpack (docs/FASE8.md §C) › a client replaying the backpack ends with the host state")]
        public void AClientReplayingTheBackpack()
        {
            var s = BackpackField();
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => u.Backpack = Pack(Medicines(2), Food(3)));
            AddUnit(s, "s2", TemplateIds.Rookie, 6, 5, extra: (u) => { u.Hp = 2; u.Infected = true; u.Perks = new List<string> { PerkIds.LightHands }; });
            var client = Events.CloneState(s);
            var orders = new[]
            {
                new ItemCommand { Unit = "s1", Action = ItemActions.Drop, Index = 1 },
                new ItemCommand { Unit = "s1", Action = ItemActions.Take, Id = "l1" },
                new ItemCommand { Unit = "s1", Action = ItemActions.Give, Index = 1, To = "s2" },
                new ItemCommand { Unit = "s2", Action = ItemActions.Drop, Index = 0 },
                new ItemCommand { Unit = "s1", Action = ItemActions.Use, Index = 0, To = "s2" },
            };
            foreach (var o in orders)
            {
                var r = Exec(s, o);
                Assert.True(r.Ok, r.Error);
                if (r.Ok) foreach (var e in r.Events!) Events.ApplyEvent(client, e);
            }
            SameJson(s, client);
        }

        // ------------------------------------------------------------ replication of the new events

        [Fact(DisplayName = "replication of the new events › clients replaying noise, searches and bursts end with the host state")]
        public void ClientsReplayingNoiseSearchesAndBursts()
        {
            var s = Field();
            s.Decor = Enumerable.Repeat(0, 400).ToList();
            s.Decor[5 * 20 + 6] = Decor.DecorCode(DecorKinds.LootFood);
            s.Tiles[5 * 20 + 6] = TileKinds.Low;
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 5);
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 15, 5);
            var b = AddUnit(s, "a2", TemplateIds.Bloater, 6, 6, extra: (u) => u.Hp = 1);
            PodOf(s, "p1", false, z, b);
            var client = Events.CloneState(s);
            var host = s;
            var r1 = Exec(host, Ability("s1", AbilityIds.Search, tile: P(6, 5)), seed: 9);
            var r2 = Exec(host, Ability("s1", AbilityIds.Shoot, target: "a2"), seed: 9);
            foreach (var r in new[] { r1, r2 }) if (r.Ok) foreach (var e in r.Events!) Events.ApplyEvent(client, e);
            SameJson(host, client);
        }

        // ------------------------------------------------------------ the dead and the living fight each other (Arturo, 18/10 #47)

        [Fact(DisplayName = "the dead and the living fight each other (Arturo, 18/10 #47) › the dead are the living's enemies, and the other way round; the squad is everyone's")]
        public void TheDeadAreTheLivingsEnemies()
        {
            var s = Field();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 5, 5);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 9, 9);
            var other = AddUnit(s, "z2", TemplateIds.Zombie, 12, 12);
            Assert.Equal(new[] { true, true, true, true }, new[] { Content.HostileTo(zombie, raider), Content.HostileTo(raider, zombie), Content.HostileTo(zombie, soldier), Content.HostileTo(raider, soldier) });
            Assert.False(Content.HostileTo(zombie, other));
        }

        [Fact(DisplayName = "the dead and the living fight each other (Arturo, 18/10 #47) › one of the dead with a Saqueador beside it and no soldier near bites the Saqueador")]
        public void OneOfTheDeadBitesTheSaqueador()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 15, 15, extra: (u) => u.Facing = Math.PI);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 14, 15, extra: (u) => u.Facing = 0);
            PodOf(s, "pz", true, zombie);
            PodOf(s, "pr", false, raider);
            s.ActiveTeam = Teams.Alien;
            var run = NewSim(s);
            run.RunAlienTurn();
            var bites = Of<ShotEvent>(run.Events).Where((e) => e.Unit == "z1").ToList();
            Assert.NotEmpty(bites);
            Assert.True(bites.All((e) => e.Target == "r1"));
            // Woken by the dead, not by the squad: the squad stays hidden.
            Assert.Empty(Of<ConcealmentBrokenEvent>(run.Events));
        }

        [Fact(DisplayName = "the dead and the living fight each other (Arturo, 18/10 #47) › a Saqueador on watch shoots one of the dead coming at him")]
        public void ASaqueadorOnWatchShootsOneOfTheDead()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 10, 10, extra: (u) => { u.Facing = 0; u.Overwatch = true; });
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 16, 10, extra: (u) => u.Facing = Math.PI);
            PodOf(s, "pr", true, raider);
            PodOf(s, "pz", true, zombie);
            s.Concealed = false;
            s.ActiveTeam = Teams.Alien;
            var run = NewSim(s);
            run.RunAlienTurn();
            var shots = Of<ShotEvent>(run.Events).Where((e) => e.Unit == "r1").ToList();
            Assert.NotEmpty(shots);
            Assert.True(shots.All((e) => e.Target == "z1" && e.Reaction));
        }
    }
}
