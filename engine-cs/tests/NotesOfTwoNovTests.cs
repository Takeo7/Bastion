using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Arturo's notes of 2/11 and Ana's third report (docs/PRUEBA-2026-11-02-ARTURO.md): two soldiers each and their four
    /// arms, the stone without damage, the crowbar, the cars' boots and the police cars, the bodies, and the new blades.
    /// The horde's clock is in ZombiesTests2 and NightTests, the quantities in FoodTests. New in C#, not in the TypeScript
    /// engine.
    /// </summary>
    public class NotesOfTwoNovTests
    {
        private static CommandContext Ctx()
        {
            return new CommandContext { Slot = 0, Connected = new[] { 0 }, Rng = new Rng(1) };
        }

        /// <summary>An open field with a zombie asleep far off, so the mission goes on, and a soldier with `arms`.</summary>
        private static (GameState s, Unit u) Field(Arms arms, int x = 5, int y = 5)
        {
            var s = StateFrom(Enumerable.Range(0, 20).Select((_) => new string('.', 20)).ToArray());
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 19, 19);
            var pod = new Pod { Id = "p1", Active = false, UnitIds = new List<string> { z.Id }, Patrol = new List<Pos>(), PatrolIndex = 0 };
            z.PodId = pod.Id;
            s.Pods.Add(pod);
            var u = AddUnit(s, "s1", TemplateIds.Rookie, x, y, extra: (v) => v.Arms = arms);
            return (s, u);
        }

        /// <summary>An awake zombie of a pod of its own on (x, y), hard to kill.</summary>
        private static Unit Awake(GameState s, string id, int x, int y)
        {
            var z = AddUnit(s, id, TemplateIds.Zombie, x, y, extra: (u) => ZombiesTests.Hp(u, 50));
            var pod = new Pod { Id = $"p{id}", Active = true, UnitIds = new List<string> { id }, Patrol = new List<Pos>(), PatrolIndex = 0 };
            z.PodId = pod.Id;
            s.Pods.Add(pod);
            return z;
        }

        // ------------------------------------------------------------ the start

        [Fact(DisplayName = "the start › two soldiers each, and two pistols, a machete and a crowbar, each to a different one")]
        public void TwoSoldiersEachAndTheirArms()
        {
            for (var seed = 1; seed <= 10; seed++)
            {
                var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(seed));
                Assert.Equal(new[] { 2, 2 }, new[] { 0, 1 }.Select((p) => c.Soldiers.Count((s) => s.Owner == p)));
                var arms = c.FoundWeapons!;
                Assert.Equal(new[] { FoundWeaponIds.Crowbar, FoundWeaponIds.Machete, FoundWeaponIds.Pistol, FoundWeaponIds.Pistol }, arms.Select((w) => w.Kind).OrderBy((k) => k, System.StringComparer.Ordinal));
                Assert.Equal(4, arms.Select((w) => w.Holder).Distinct().Count());
                Assert.All(arms, (w) => Assert.Contains(c.Soldiers, (s) => s.Id == w.Holder));
            }
            // Alone, the four are one commander's.
            Assert.Equal(4, Campaign.CreateCampaign(new[] { 0 }, new Rng(1)).Soldiers.Count((s) => s.Owner == 0));
        }

        [Fact(DisplayName = "the start › a partner who joins later takes over two of the four untouched rookies")]
        public void APartnerTakesTwo()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            Assert.Equal(2, Campaign.AddPlayer(c, 1, new Rng(2)));
            Assert.Equal(new[] { 2, 2 }, new[] { 0, 1 }.Select((p) => c.Soldiers.Count((s) => s.Owner == p)));
        }

        [Fact(DisplayName = "the start › a commander left without a soldier alive gets a recruit for nothing")]
        public void ARecruitForNothing()
        {
            var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(1));
            c.NextEventDay = 999;
            foreach (var s in c.Soldiers.Where((x) => x.Owner == 1)) s.Alive = false;
            var credits = c.Credits;
            Campaign.AdvanceTime(c, new Rng(2));
            Assert.Equal(1, c.Soldiers.Count((s) => s.Owner == 1 && s.Alive));
            Assert.Equal(credits, c.Credits);
            Assert.Contains(c.Log, (l) => l.Text.EndsWith(" se une al Bastión como recluta: a un jugador no le quedaban soldados vivos.", System.StringComparison.Ordinal));
        }

        /// <summary>A mission of the campaign flown to victory, with `edit` done to it first, and the siege brought on after it.</summary>
        private static CampaignState SiegeAfter(System.Action<GameState>? edit = null)
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            c.NextEventDay = 999;
            c.Credits = 2000;
            c.Siege.In = 0;
            CampaignHelpers.Run(c, new ChooseMissionCommand { Id = c.Offers.First((o) => (o.Story ?? 0) == 0).Id });
            var offer = Campaign.ActiveOffer(c)!;
            // No survivor brought home: only the squad, as it comes back.
            offer.Reward = new MissionReward();
            var s = Setup.CreateMission(new MissionOptions { Map = Campaign.OfferMap(c, offer), Slots = c.Players, Rng = new Rng(2), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods });
            edit?.Invoke(s);
            s.Outcome = Outcomes.Victory;
            s.OutcomeReason = EndReasons.Objective;
            Campaign.ApplyMissionResult(c, s, new Rng(1));
            Assert.True(Campaign.ActiveOffer(c)?.Siege);
            return c;
        }

        [Fact(DisplayName = "the siege › one who comes once it has started defends too (Red, 2/11: the recruit was left out)", Skip = "Sin asedios por ahora (Arturo, 7/11: Campaign.SiegesOn): vuelven con ellos")]
        public void ComesToTheSiege()
        {
            var c = SiegeAfter();
            var squad = c.Squad.Count;
            Assert.True(CampaignHelpers.Run(c, new RecruitCommand()).Ok);
            Assert.Equal(squad + 1, c.Squad.Count);
            Assert.Equal(squad + 1, Campaign.BuildSquad(c).Count);
            // Asked for the squad (nobody else in it, as when nobody was fit), and not twice in it.
            c.Squad = new List<string>();
            Assert.True(CampaignHelpers.Run(c, new RecruitCommand { Squad = true }).Ok);
            Assert.Single(c.Squad);
        }

        [Fact(DisplayName = "the siege › with nobody fit, the wounded defend; the bitten stay in (Arturo, 2/11)", Skip = "Sin asedios por ahora (Arturo, 7/11: Campaign.SiegesOn): vuelven con ellos")]
        public void TheWoundedDefend()
        {
            var c = SiegeAfter((s) => { foreach (var u in s.Units.Where((x) => x.CampaignId != null)) u.Hp = 1; });
            var bitten = c.Soldiers.First((s) => s.Alive);
            Assert.True(c.Soldiers.Where((s) => s.Alive).All((s) => s.WoundedDays > 0));
            Assert.Equal(c.Soldiers.Where((s) => s.Alive).Select((s) => s.Id), c.Squad);
            bitten.Infection = new SoldierInfection { Since = c.Day };
            Assert.Equal(c.Squad.Count - 1, Campaign.BuildSquad(c).Count);
            // One fit among them, and the wounded go back in.
            Assert.True(CampaignHelpers.Run(c, new RecruitCommand()).Ok);
            Assert.Single(Campaign.BuildSquad(c));
        }

        // ------------------------------------------------------------ the stone

        [Fact(DisplayName = "the stone › only noise, until Canto afilado or the rest give it damage")]
        public void TheStoneOnlyNoise()
        {
            foreach (var bonus in new[] { 0, 1 })
            {
                var (s, u) = Field(new Arms { Pistol = false });
                u.Mods = Content.AddMods(u.Mods, new UnitMods { StoneDamage = bonus });
                u.Perks = new List<string> { PerkIds.Stones };
                var z = Awake(s, "a2", 8, 5);
                var before = z.Hp;
                Assert.True(Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Stone, tile: P(8, 5)), Ctx()).Ok);
                Assert.Equal(before - bonus, z.Hp);
            }
        }

        // ------------------------------------------------------------ the crowbar

        [Fact(DisplayName = "the crowbar › takes the boards off a window beside it: the pile is open floor; without one, no such action")]
        public void TheCrowbar()
        {
            var (s, u) = Field(new Arms { Pistol = false, Melee = FoundWeaponIds.Crowbar });
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            var pile = Grid.TileIndex(s, 6, 5);
            s.Tiles[pile] = TileKinds.High;
            s.Decor[pile] = Decor.DecorCode(DecorKinds.WindowBarricade, 1);
            Assert.Contains(AbilityIds.Pry, Abilities.AbilitiesOf(u));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Pry));
            var r = Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Pry, tile: P(6, 5)), Ctx());
            Assert.True(r.Ok, r.Error);
            Assert.Single(r.Events.OfType<BoardsRemovedEvent>());
            Assert.Equal((TileKinds.Floor, 0), (s.Tiles[pile], s.Decor[pile]));
            Assert.Equal("No hay ninguna ventana ni puerta tapiada a tu lado.", Abilities.AbilityBlocker(s, u, AbilityIds.Pry));
            var (_, knife) = Field(new Arms { Pistol = false });
            Assert.DoesNotContain(AbilityIds.Pry, Abilities.AbilitiesOf(knife));
            // A doorway's planks leave the door shut behind them.
            var door = Grid.TileIndex(s, 5, 6);
            s.Tiles[door] = TileKinds.High;
            s.Decor[door] = Decor.DecorCode(DecorKinds.DoorBoards, 2);
            u.Ap = 2;
            Assert.True(Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Pry, tile: P(5, 6)), Ctx()).Ok);
            Assert.Equal((TileKinds.Door, 0), (s.Tiles[door], s.Decor[door]));
        }

        [Fact(DisplayName = "the crowbar › some doors out of houses, shops and offices are boarded up, never a building's last")]
        public void BoardedDoors()
        {
            var boarded = 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed) });
                for (var i = 0; i < s.Width * s.Height; i++)
                {
                    if (Decor.DecorOf(s.Decor![i])?.Kind != DecorKinds.DoorBoards) continue;
                    Assert.Equal(TileKinds.High, s.Tiles[i]);
                    boarded++;
                }
            }
            Assert.True(boarded >= 3, $"{boarded}");
        }

        // ------------------------------------------------------------ cars

        [Fact(DisplayName = "cars › any whole car's boot is searched once; some are police cars, their boots a gun more often")]
        public void CarBoots()
        {
            var police = 0;
            var boots = 0;
            for (var seed = 1; seed <= 12; seed++)
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed), Pistols = true });
                var pairs = s.Mission.PoliceCar ?? new List<int>();
                Assert.Equal(0, pairs.Count % 2);
                for (var k = 0; k < pairs.Count; k += 2)
                {
                    Assert.Equal(DecorKinds.CarFront, Decor.DecorOf(s.Decor![pairs[k]])!.Kind);
                    Assert.Equal(pairs[k], Looting.BootOf(s, pairs[k + 1]));
                    Assert.Equal(Looting.PoliceBoot, Looting.SearchSpot(s, Grid.PosOf(s, pairs[k + 1])));
                    Assert.Null(Looting.SearchSpot(s, Grid.PosOf(s, pairs[k])));
                    police++;
                }
                for (var i = 0; i < s.Width * s.Height; i++)
                {
                    if (Looting.BootOf(s, i) is int nose && !pairs.Contains(nose))
                    {
                        Assert.Equal(Looting.CarBoot, Looting.SearchSpot(s, Grid.PosOf(s, i)));
                        boots++;
                    }
                }
                // A wreck's boot burst open: nothing to search.
                foreach (var w in s.Wrecks ?? new List<Wreck>())
                {
                    var car = Decor.ObjectAt(s.Decor!, s.Width, w.Anchor);
                    if (car != null) Assert.All(car.Tiles, (j) => Assert.Null(Looting.BootOf(s, j)));
                }
            }
            Assert.True(police >= 2, $"{police}");
            Assert.True(boots >= 5 * police, $"{boots} {police}");
            int Guns(string kind) => Enumerable.Range(1, 300).Count((seed) => !string.IsNullOrEmpty(Looting.RollBox(kind, new Rng(seed), false, true)?.Weapon));
            Assert.InRange(Guns(Looting.PoliceBoot), 90, 140);
            Assert.InRange(Guns(Looting.CarBoot), 4, 25);
        }

        // ------------------------------------------------------------ bodies

        [Fact(DisplayName = "bodies › a dead enemy is searched once: a living one's gun always, a uniform's one time in four, anyone else's hardly ever")]
        public void Bodies()
        {
            var (s, u) = Field(new Arms { Pistol = false });
            var raider = AddUnit(s, "a2", TemplateIds.Trooper, 6, 5, extra: (v) => v.Alive = false);
            Assert.Equal(Looting.Body, Looting.SearchSpot(s, P(6, 5)));
            var r = Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Search, tile: P(6, 5)), Ctx());
            Assert.True(r.Ok, r.Error);
            Assert.Equal(FoundWeaponIds.AssaultRifle, Assert.Single(u.Backpack!).Weapon);
            Assert.False(Looting.IsSearchable(s, P(6, 5)));
            // Standing on it is as good as beside it.
            var (t, v) = Field(new Arms { Pistol = false });
            AddUnit(t, "a2", TemplateIds.Trooper, 5, 5, extra: (w) => w.Alive = false);
            Assert.Contains(Looting.SearchableTiles(t, v), (p) => Grid.SamePos(p, v.Pos));
            // The dead: by what they wore.
            int Guns(string? outfit, bool pistols) => Enumerable.Range(1, 2000).Count((seed) => !string.IsNullOrEmpty(Looting.RollBody(new Unit { Template = TemplateIds.Zombie, Outfit = outfit }, new Rng(seed), pistols)?.Weapon));
            Assert.InRange(Guns(Outfits.Police, true), 420, 580);
            Assert.InRange(Guns(Outfits.Military, true), 420, 580);
            Assert.InRange(Guns(null, true), 5, 40);
            Assert.All(Enumerable.Range(1, 200), (seed) => Assert.NotEqual(FoundWeaponIds.Pistol, Looting.RollBody(new Unit { Template = TemplateIds.Zombie, Outfit = Outfits.Police }, new Rng(seed), false)?.Weapon));
        }

        [Fact(DisplayName = "bodies › one common zombie in ten wears a policeman's uniform and one in twenty a soldier's; nobody else does")]
        public void Uniforms()
        {
            var units = Enumerable.Range(0, 2000).Select((i) => Setup.MakeUnit($"a{i}", TemplateIds.Zombie, "Zombi", null, P(i % 40, i / 40), null)).ToList();
            Assert.InRange(units.Count((u) => u.Outfit == Outfits.Police), 140, 260);
            Assert.InRange(units.Count((u) => u.Outfit == Outfits.Military), 60, 140);
            Assert.Null(Setup.MakeUnit("a1", TemplateIds.Xenoid, "Corredor", null, P(3, 3), null).Outfit);
        }

        // ------------------------------------------------------------ blades

        [Fact(DisplayName = "blades › a long blade's Tajo strikes everyone alongside; any other, one")]
        public void LongBladesSweep()
        {
            foreach (var (melee, strikes) in new[] { (FoundWeaponIds.Katana, 2), (FoundWeaponIds.Longsword, 2), (FoundWeaponIds.Bat, 1) })
            {
                var (s, u) = Field(new Arms { Pistol = false, Melee = melee });
                Awake(s, "a2", 6, 5);
                Awake(s, "a3", 4, 5);
                var r = Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Slash, target: "a2"), Ctx());
                Assert.True(r.Ok, r.Error);
                Assert.Equal(strikes, r.Events.OfType<ShotEvent>().Count((e) => e.Unit == "s1"));
            }
        }

        [Fact(DisplayName = "blades › the rake reaches two tiles off in a line, over open ground with nobody in the way")]
        public void TheRakeReachesTwo()
        {
            var (s, u) = Field(new Arms { Pistol = false, Melee = FoundWeaponIds.Rake });
            Awake(s, "a2", 7, 5);
            Awake(s, "a3", 7, 7);
            Awake(s, "a4", 7, 6);
            Assert.Equal(new[] { "a2", "a3" }, Abilities.MeleeTargets(s, u, AbilityIds.Slash).Select((o) => o.Target.Id).OrderBy((id) => id, System.StringComparer.Ordinal));
            AddUnit(s, "s2", TemplateIds.Rookie, 6, 5);
            Assert.Equal(new[] { "a3" }, Abilities.MeleeTargets(s, u, AbilityIds.Slash).Select((o) => o.Target.Id));
            var (_, knife) = Field(new Arms { Pistol = false });
            Assert.Empty(Abilities.MeleeTargets(s, knife, AbilityIds.Slash).Where((o) => o.Target.Id == "a2"));
        }
    }
}
