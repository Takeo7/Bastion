using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/base.test.ts: the base in the campaign, all 29 tests. The file's helpers are here: `run` (a
    /// command with Rng(11) that must go through, or throws its error), `solo` and `fly`; helpers.ts `raise` and
    /// `freeSpot` are CampaignHelpers'. `expect(() => run(…)).toThrow(x)` is Throws (a RegExp) or ThrowsText (a string,
    /// which the message must hold); `toEqual` and `toMatchObject` on objects go field by field, or by their JSON
    /// (CampaignJsonTests.Write, which leaves the nulls out as toEqual leaves out what is undefined). None is left out or
    /// skipped, and no text they check is one of golden/text-fixes.ts.
    /// </summary>
    public class BaseTests
    {
        private static readonly int[] Both = CampaignHelpers.Both;

        /// <summary>The file's `run`: a command for `slot` with who is connected (alone by default) and a fresh Rng(11); refused, it throws.</summary>
        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            var r = CampaignHelpers.Run(c, cmd, slot, connected, 11);
            if (!r.Ok) throw new InvalidOperationException(r.Error);
            return r;
        }

        /// <summary>`expect(() => run(…)).toThrow(/pattern/)`.</summary>
        private static void Throws(string pattern, CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            var e = Assert.Throws<InvalidOperationException>(() => Run(c, cmd, slot, connected));
            Assert.Matches(pattern, e.Message);
        }

        /// <summary>`expect(() => run(…)).toThrow('text')`: the error holds the text.</summary>
        private static void ThrowsText(string text, CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            var e = Assert.Throws<InvalidOperationException>(() => Run(c, cmd, slot, connected));
            Assert.Contains(text, e.Message);
        }

        private static CampaignState Solo()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            c.NextEventDay = 999;
            c.Credits = 2000;
            return c;
        }

        /// <summary>A mission flown to `outcome` (nothing else happening in it), and its report.</summary>
        private static MissionReport Fly(CampaignState c, string outcome = Outcomes.Victory, Action<GameState>? edit = null)
        {
            if (string.IsNullOrEmpty(c.ActiveMission)) Run(c, new ChooseMissionCommand { Id = c.Offers.First((o) => (o.Story ?? 0) == 0).Id });
            var offer = Campaign.ActiveOffer(c)!;
            var plan = Campaign.SiegePlan(c, offer);
            var s = Setup.CreateMission(new MissionOptions { Map = Campaign.OfferMap(c, offer), SiegePlan = plan, Slots = c.Players, Rng = new Rng(2), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods });
            edit?.Invoke(s);
            s.Outcome = outcome;
            s.OutcomeReason = outcome == Outcomes.Victory ? EndReasons.Objective : EndReasons.Wiped;
            return Campaign.ApplyMissionResult(c, s, new Rng(1));
        }

        /// <summary>An order of the base of before (clearing a plot), which no CampaignCommand class is any more.</summary>
        private sealed class ClearCommand : CampaignCommand
        {
            public override string Type => "clear";
            public string Plot { get; set; } = "";
        }

        private static bool HasPiece(CampaignState c, string piece) => c.Bastion.Pieces.Any((p) => p.Piece == piece);

        private static int Stock(CampaignState c, string item) => c.Inventory.TryGetValue(item, out var n) ? n : 0;

        private static void Fell(CampaignState c, IEnumerable<Vec2> tiles)
        {
            foreach (var t in tiles)
            {
                var i = t.Y * c.Bastion.Width + t.X;
                c.Bastion.Trees = c.Bastion.Trees.Where((x) => x != i).ToList();
            }
        }

        private static PlaceCommand Place(string piece, Vec2 at) => new PlaceCommand { Piece = piece, X = at.X, Y = at.Y, Rot = 0 };

        private static MissionOptions SiegeMission(CampaignState c, MissionOffer offer)
        {
            return new MissionOptions { Map = Campaign.OfferMap(c, offer), SiegePlan = Campaign.SiegePlan(c, offer)!, Posts = c.SiegePosts, Slots = c.Players, Rng = new Rng(2), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods };
        }

        // ------------------------------------------------ the base on the map (docs/FASE12.md §11)

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › a new campaign starts with only the caravan in a clearing, and the first siege a few missions off")]
        public void ANewCampaignStartsWithOnlyTheCaravan()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            Assert.Equal(Campaign.CampaignVersion, c.Version);
            Assert.Equal(new[] { PieceIds.Caravan }, c.Bastion.Pieces.Select((p) => p.Piece));
            Assert.Equal("{\"works\":[]}", CampaignJsonTests.Write(c.Base));
            Assert.Equal(Campaign.SiegeFirstIn, c.Siege.In);
            Assert.Equal(Campaign.SiegeOpening, c.Siege.Count);
            Assert.Equal(0, c.Siege.Held);
            // Straight after the first mission, and small (Ana and Arturo, 22/10).
            Assert.Equal(1, Campaign.SiegeFirstIn);
            Assert.True(Campaign.SiegeOpening < Campaign.SiegeFirst);
            // From three sides or all four (Ana, 23/10).
            Assert.True(c.Siege.Sides.Count >= 3);
            Assert.True(c.Siege.Sides.Count <= 4);
            // Without a Sala de radio, only when.
            Assert.Equal($"{{\"in\":{Campaign.SiegeFirstIn}}}", CampaignJsonTests.Write(Campaign.SiegeHeard(c)));
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › everything on the base goes to a vote with the other here, cheap or dear (18/10 #55); alone, at once; the Bastion level opens what it opens")]
        public void EverythingOnTheBaseGoesToAVote()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(3));
            c.Credits = 500;
            var bags = CampaignHelpers.FreeSpot(c, PieceIds.Sandbags);
            Run(c, Place(PieceIds.Sandbags, bags), 0, Both);
            Assert.Equal($"construir {Lore.PieceTexts.All[PieceIds.Sandbags].A}", c.Proposal?.Text);
            Assert.False(HasPiece(c, PieceIds.Sandbags));
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.True(HasPiece(c, PieceIds.Sandbags));
            Assert.Equal(500 - Base.Pieces[PieceIds.Sandbags].Cost.Supplies, c.Credits);
            // Mending, setting again and taking down too.
            var placed = c.Bastion.Pieces.First((p) => p.Piece == PieceIds.Sandbags);
            placed.Hp = 1;
            Run(c, new MendCommand { Piece = placed.Id }, 0, Both);
            Assert.Equal($"reparar {Lore.PieceTexts.All[PieceIds.Sandbags].The}", c.Proposal?.Text);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(Base.Pieces[PieceIds.Sandbags].Hp, placed.Hp);
            Run(c, new DismantleCommand { Piece = placed.Id }, 0, Both);
            Assert.Equal($"desmontar {Lore.PieceTexts.All[PieceIds.Sandbags].The}", c.Proposal?.Text);
            Run(c, new VoteCommand { Accept = false }, 1, Both);
            Assert.Contains(placed, c.Bastion.Pieces);
            // With the other away, it is done at once.
            Run(c, new DismantleCommand { Piece = placed.Id }, 0, new[] { 0 });
            Assert.DoesNotContain(placed, c.Bastion.Pieces);
            // The shared supplies go to a vote too (Ana, 21/10 #31): a recruit for whoever proposes it, an accessory.
            foreach (var s in c.Soldiers.Skip(4)) s.Alive = false;
            var soldiers = c.Soldiers.Count;
            Run(c, new RecruitCommand(), 1, Both);
            var recruit = Assert.IsType<RecruitAction>(c.Proposal?.Action);
            Assert.Equal(1, recruit.For);
            Assert.Null(recruit.Squad);
            Assert.Equal(soldiers, c.Soldiers.Count);
            Run(c, new VoteCommand { Accept = true }, 0, Both);
            Assert.Equal(soldiers + 1, c.Soldiers.Count);
            Assert.Equal(1, c.Soldiers.Last().Owner);
            Run(c, new BuyCommand { Item = ItemIds.SmokeGrenade }, 0, Both);
            Assert.Equal(0, Stock(c, ItemIds.SmokeGrenade));
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(1, c.Inventory[ItemIds.SmokeGrenade]);
            // The Sala de radio, from the caravan with its awning (Ana, 21/10 #36).
            var radio = CampaignHelpers.FreeSpot(c, PieceIds.Radio);
            Throws("caravana con toldo", c, Place(PieceIds.Radio, radio), 0, Both);
            c.Level = 2;
            Run(c, Place(PieceIds.Radio, radio), 0, Both);
            Assert.Equal("construir una Sala de radio", c.Proposal?.Text);
            Assert.Equal(Base.Pieces[PieceIds.Radio].Cost.Supplies, Campaign.ProposalCost(c, c.Proposal!.Action));
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.True(Campaign.HasRadio(c));
            var heard = Campaign.SiegeHeard(c);
            Assert.Equal(c.Siege.In, heard.In);
            Assert.Equal(c.Siege.Count, heard.Count);
            Assert.Equal(c.Siege.Sides, heard.Sides);
            var wall = CampaignHelpers.FreeSpot(c, PieceIds.Wall);
            c.Level = 1;
            Throws("caravana con toldo", c, Place(PieceIds.Wall, wall), 0, Both);
            c.Level = 2;
            c.Parts = 0;
            Throws("piezas", c, Place(PieceIds.Wall, wall), 0, Both);
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › a proposal that no longer goes through when accepted is dropped and said so, never left on both screens")]
        public void AProposalThatNoLongerGoesThroughIsDropped()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(3));
            c.Credits = 500;
            var bags = CampaignHelpers.FreeSpot(c, PieceIds.Sandbags);
            Run(c, Place(PieceIds.Sandbags, bags), 0, Both);
            c.Credits = 0;
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Null(c.Proposal);
            Assert.False(HasPiece(c, PieceIds.Sandbags));
            Assert.Matches("^Propuesta aceptada, pero ya no se puede: construir", c.Log[0].Text);
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › a row dragged is built at once, all or none, and to a vote when together it is dear (18/10 #35)")]
        public void ARowDraggedIsBuiltAtOnce()
        {
            var c = Solo();
            var caravan = c.Bastion.Pieces[0];
            // A clear row of four below the Bastion's lot.
            var y = Base.BastionLot(c.Bastion)!.Y + Base.BastionLot(c.Bastion)!.H + 1;
            var cells = new[] { 0, 1, 2, 3 }.Select((i) => new Vec2 { X = caravan.X + i, Y = y }).ToList();
            Fell(c, cells);
            var credits = c.Credits;
            Run(c, new PlaceLineCommand { Piece = PieceIds.Palisade, Cells = cells, Rot = 0 });
            Assert.Equal(cells.Select((t) => (t.X, t.Y)), c.Bastion.Pieces.Where((p) => p.Piece == PieceIds.Palisade).Select((p) => (p.X, p.Y)));
            Assert.Equal(credits - 4 * Base.Pieces[PieceIds.Palisade].Cost.Supplies, c.Credits);
            // One in the way, and none goes up.
            var again = cells.Select((t) => new Vec2 { X = t.X, Y = t.Y + 1 }).ToList();
            Fell(c, again);
            Throws("Ahí ya hay algo", c, new PlaceLineCommand { Piece = PieceIds.Palisade, Cells = again.Append(cells[0]).ToList(), Rot = 0 });
            Assert.Equal(4, c.Bastion.Pieces.Count((p) => p.Piece == PieceIds.Palisade));
            // Six together cost more than 30: with the partner here, a vote.
            c.Players = new List<int> { 0, 1 };
            var six = new[] { 0, 1, 2, 3, 4, 5 }.Select((i) => new Vec2 { X = caravan.X + i, Y = y + 2 }).ToList();
            Fell(c, six);
            Run(c, new PlaceLineCommand { Piece = PieceIds.Palisade, Cells = six, Rot = 0 }, 0, Both);
            Assert.Equal("construir 6 × empalizada", c.Proposal?.Text);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(10, c.Bastion.Pieces.Count((p) => p.Piece == PieceIds.Palisade));
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › felling costs and gives nothing; mending, setting again and taking down cost and give what they say")]
        public void FellingCostsAndGivesNothing()
        {
            var c = Solo();
            // The forest past the clearing's edge is not felled (Ana, 22/10 #30): where felling stops, the clearing ends.
            Vec2 Xy(int i) => new Vec2 { X = i % c.Bastion.Width, Y = i / c.Bastion.Width };
            var edge = c.Bastion.Trees.First((i) => Xy(i).X < Base.BaseEdge || Xy(i).Y < Base.BaseEdge);
            Throws("bosque", c, new FellCommand { Tiles = new List<Vec2> { Xy(edge) } });
            List<int> Fellable() => c.Bastion.Trees.Where((i) => Base.FellBlocker(c.Bastion, Xy(i).X, Xy(i).Y) == null).ToList();
            var tree = Fellable()[0];
            var credits = c.Credits;
            Run(c, new FellCommand { Tiles = new List<Vec2> { Xy(tree) } });
            // Arturo, 18/10 #41: «solo cuesta recursos y no da nada».
            Assert.Equal(credits - Base.FellCost, c.Credits);
            Assert.DoesNotContain(tree, c.Bastion.Trees);
            // A tree listed twice is one tree, paid once.
            var twice = Fellable()[0];
            var at = new Vec2 { X = twice % c.Bastion.Width, Y = twice / c.Bastion.Width };
            Run(c, new FellCommand { Tiles = new List<Vec2> { at, at } });
            Assert.Equal(credits - 2 * Base.FellCost, c.Credits);
            // Many at once, dear together: to a vote with the partner here.
            var many = Fellable().Take(12).Select(Xy).ToList();
            c.Players = new List<int> { 0, 1 };
            Run(c, new FellCommand { Tiles = many }, 0, Both);
            Assert.Equal("talar 12 árboles", c.Proposal?.Text);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(credits - 14 * Base.FellCost, c.Credits);
            c.Players = new List<int> { 0 };
            var gate = CampaignHelpers.Raise(c, PieceIds.Gate);
            Throws("No le falta nada", c, new MendCommand { Piece = gate.Id });
            gate.Hp = 9;
            Run(c, new MendCommand { Piece = gate.Id });
            Assert.Equal(Base.Pieces[PieceIds.Gate].Hp, gate.Hp);
            var mine = CampaignHelpers.Raise(c, PieceIds.Mine);
            mine.Charges = 0;
            c.Parts = 1;
            Run(c, new RearmCommand { Piece = mine.Id });
            Assert.Equal(new int?[] { 1, 0 }, new[] { mine.Charges, c.Parts });
            var before = c.Credits;
            Run(c, new DismantleCommand { Piece = gate.Id });
            Assert.Equal(before + Base.Pieces[PieceIds.Gate].Cost.Supplies / 2, c.Credits);
            Throws("caravana", c, new DismantleCommand { Piece = c.Bastion.Pieces[0].Id });
            // The plots of before are gone: their orders are refused like any order that doesn't exist.
            Throws("no existe", c, new ClearCommand { Plot = "3,5" });
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › a building works by its facility, wherever it stands (Ana, 21/10 #41: nothing for being next to another); broken, it stops")]
        public void ABuildingWorksByItsFacility()
        {
            var c = Solo();
            var far = CampaignHelpers.Raise(c, PieceIds.Workshop);
            Assert.Equal(19, Campaign.ShopPrice(c, ItemIds.SmokeGrenade));
            c.Bastion.Pieces = c.Bastion.Pieces.Where((p) => p != far).ToList();
            var caravan = c.Bastion.Pieces[0];
            CampaignHelpers.Raise(c, PieceIds.Workshop, 1, caravan);
            Assert.Equal(19, Campaign.ShopPrice(c, ItemIds.SmokeGrenade));
            Assert.Equal(30, Campaign.RecruitCost(c));
            CampaignHelpers.Raise(c, PieceIds.Training, 1, caravan);
            Assert.Equal(30, Campaign.RecruitCost(c));
            var infirmary = CampaignHelpers.Raise(c, PieceIds.Infirmary);
            var rate = Campaign.HealRate(c);
            Assert.True(rate > 1);
            infirmary.Hp = Base.Pieces[PieceIds.Infirmary].Hp / 2;
            Assert.Equal(1, Campaign.HealRate(c));
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › the caravan sleeps the first eight; a barracón 2/3/4 beds")]
        public void TheCaravanSleepsTheFirstEight()
        {
            var c = Solo();
            Assert.Equal(8, Campaign.Beds(c));
            Throws("camas", c, new RecruitCommand());
            var away = CampaignHelpers.Raise(c, PieceIds.Quarters);
            Assert.Equal(10, Campaign.Beds(c));
            away.Level = 3;
            Assert.Equal(12, Campaign.Beds(c));
            Run(c, new RecruitCommand());
            Assert.Equal(Campaign.Beds(c) - 9, Campaign.BedsFree(c));
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › a building rises a level with its facility's cost and the Bastion; the Sala de radio rises into communications and listening")]
        public void ABuildingRisesALevel()
        {
            var c = Solo();
            var shop = CampaignHelpers.Raise(c, PieceIds.Workshop);
            Throws("la casa móvil\\.", c, new RaiseCommand { Piece = shop.Id });
            c.Level = 3;
            var credits = c.Credits;
            Run(c, new RaiseCommand { Piece = shop.Id });
            Assert.Equal(2, shop.Level);
            Assert.Equal(credits - Campaign.UpgradeCost(FacilityIds.Workshop, 2).Cost, c.Credits);
            Assert.Equal((int)Js.Round(25 * 0.65), Campaign.ShopPrice(c, ItemIds.SmokeGrenade));
            var radio = CampaignHelpers.Raise(c, PieceIds.Radio);
            Assert.Equal($"{{\"cost\":{Campaign.Facilities[FacilityIds.Relay].Cost},\"level\":{Campaign.Facilities[FacilityIds.Relay].Level}}}", CampaignJsonTests.Write(Campaign.RaiseCost(radio)));
            c.Level = 7;
            var capacity = Campaign.ContactCapacity(c);
            Run(c, new RaiseCommand { Piece = radio.Id });
            Assert.Equal(1, Campaign.FacilityLevel(c, FacilityIds.Relay));
            Assert.True(Campaign.ContactCapacity(c) >= capacity + 2);
            Run(c, new RaiseCommand { Piece = radio.Id });
            Assert.Equal(1, Campaign.FacilityLevel(c, FacilityIds.Listening));
            Assert.True(Campaign.ListeningDelay(c) >= 1);
            Assert.Equal("Ya está al máximo.", Campaign.RaiseCost(radio).Error);
            Assert.Equal("Eso no se mejora.", Campaign.RaiseCost(c.Bastion.Pieces[0]).Error);
        }

        [Fact(DisplayName = "the base on the map (docs/FASE12.md §11) › range, La Mula, the simulation room and training do what they say")]
        public void RangeLaMulaTheSimulationRoomAndTraining()
        {
            var c = Solo();
            c.Level = 8;
            var s0 = c.Soldiers[0];
            var aim = Campaign.SquadMember(c, s0).Mods.Aim;
            var range = CampaignHelpers.Raise(c, PieceIds.Range);
            Assert.Equal(aim + 3, Campaign.SquadMember(c, s0).Mods.Aim);
            range.Level = 3;
            Assert.True(Campaign.SquadMember(c, s0).Mods.Aim >= aim + 10);
            CampaignHelpers.Raise(c, PieceIds.Garage, 2);
            Assert.Equal(2, Campaign.GarageCut(c).Wounds);
            Assert.True(Campaign.SquadLimit(c, new[] { 0 }) > 0);
            // The squad's size is La Lata's now (18/10 #45): the training centre no longer adds to it.
            var limit = Campaign.CreateCampaign(Both, new Rng(1));
            Assert.Equal(2, Campaign.SquadLimit(limit, Both));
            CampaignHelpers.Raise(limit, PieceIds.Training);
            Assert.Equal(2, Campaign.SquadLimit(limit, Both));
            // The Sala de mapas: the operations pay more suministros (the Bastion has no experience now: 18/10 #40).
            CampaignHelpers.Raise(c, PieceIds.Simulation);
            Run(c, new ChooseMissionCommand { Id = c.Offers.First((o) => (o.Story ?? 0) == 0 && (o.Reward.Credits ?? 0) != 0).Id });
            var pay = Campaign.ActiveOffer(c)!.Reward.Credits!.Value;
            var credits = c.Credits;
            var report = Fly(c);
            Assert.Equal(0, report.Xp);
            Assert.Equal((int)Js.Round(pay * 1.25), report.Reward?.Credits);
            Assert.True(c.Credits >= credits + (int)Js.Round(pay * 1.25));
        }

        // ------------------------------------------------ the sieges in the campaign (docs/FASE12.md §11.5)

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › the clock counts missions; at 0 the siege is the next thing the base does, by every soldier fit to fight")]
        public void TheClockCountsMissions()
        {
            var c = Solo();
            var lines = new List<string>();
            for (var i = 0; i < Campaign.SiegeFirstIn; i++)
            {
                Assert.Null(Campaign.ActiveOffer(c)?.Siege);
                Fly(c);
                lines.Add(c.Log[0].Text);
            }
            var siege = Campaign.ActiveOffer(c)!;
            Assert.True(siege.Siege);
            Assert.Equal("Asedio", siege.Name);
            Assert.Null(siege.ExpiresDay);
            // c.squad.sort(): in place, as JavaScript sorts strings (by UTF-16 code units).
            Js.Sort(c.Squad, string.CompareOrdinal);
            var fit = c.Soldiers.Where((s) => s.Alive && s.WoundedDays == 0 && s.Infection == null).Select((s) => s.Id).ToList();
            Js.Sort(fit, string.CompareOrdinal);
            Assert.Equal(fit, c.Squad);
            // Nothing else to choose, nobody to leave out, no calling it off.
            Throws("asedio", c, new ChooseMissionCommand { Id = c.Offers.First((o) => o.Siege != true).Id });
            Throws("asedio", c, new CancelMissionCommand());
            Throws("sanos", c, new SquadCommand { Soldier = c.Squad[0], On = false });
            // Fought on the base as it stands, with the clock's count.
            var map = Campaign.OfferMap(c, siege);
            Assert.Equal(MapSourceKinds.Base, map.Kind);
            Assert.Same(c.Bastion, map.Base);
            Assert.Null(map.Id);
            Assert.Null(map.Biome);
            Assert.Null(map.Seed);
            Assert.Null(map.Site);
            Assert.Null(map.Medical);
            var plan = Campaign.SiegePlan(c, siege)!;
            Assert.Equal(c.Siege.Count, plan.Count);
            Assert.Equal(c.Siege.Sides, plan.Sides);
        }

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › held: what broke stays broken, the traps spent, and the next one comes bigger")]
        public void HeldWhatBrokeStaysBroken()
        {
            var c = Solo();
            c.Siege.In = 0;
            Fly(c);
            Assert.True(Campaign.ActiveOffer(c)?.Siege);
            var palisade = CampaignHelpers.Raise(c, PieceIds.Palisade);
            var mine = CampaignHelpers.Raise(c, PieceIds.Mine);
            var report = Fly(c, Outcomes.Victory, (s) =>
            {
                var cell = Base.PieceCells(PieceIds.Palisade, palisade.X, palisade.Y, palisade.Rot)[0];
                s.Breakables![cell.Y * s.Width + cell.X] = 4;
                s.Traps!.First((t) => t.Piece == mine.Id).Charges = 0;
            });
            Assert.True(report.Siege);
            Assert.Equal(new int?[] { 4, 0 }, new int?[] { palisade.Hp, mine.Charges });
            Assert.Equal("{\"broken\":0,\"hurt\":1,\"spent\":1}", CampaignJsonTests.Write(report.Damage!));
            Assert.Equal(Campaign.SiegeEvery, c.Siege.In);
            Assert.Equal(1, c.Siege.Held);
            Assert.Equal(Campaign.SiegeFirst + Campaign.SiegeGrowth + c.Doom, c.Siege.Count);
            // What to mend and rearm, right after the operations (18/10 test: it fell off the end of the agenda).
            c.Parts = 10;
            var labels = Campaign.NextSteps(c, 0).Where((x) => x.Tone == NextStepTones.Info).Select((x) => x.Label).ToList();
            var at = labels.IndexOf("Repara la empalizada");
            Assert.True(at >= 0);
            Assert.True(at <= 1);
            Assert.Equal("Repón la mina", labels.ElementAtOrDefault(at + 1));
        }

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › the Bastion's building keeps the hits on all of it, past the caravan's own tiles")]
        public void TheBastionsBuildingKeepsTheHitsOnAllOfIt()
        {
            var c = Solo();
            c.Level = 5;
            c.Bastion.Level = 5;
            c.Siege.In = 0;
            Fly(c);
            var caravan = c.Bastion.Pieces.First((p) => p.Piece == PieceIds.Caravan);
            var own = Base.PieceCells(PieceIds.Caravan, caravan.X, caravan.Y, caravan.Rot);
            var outer = Base.BastionFootprint(c.Bastion).First((t) => !own.Any((o) => o.X == t.X && o.Y == t.Y));
            Fly(c, Outcomes.Victory, (s) =>
            {
                s.Breakables![outer.Y * s.Width + outer.X] = 5;
            });
            Assert.Equal(5, caravan.Hp);
        }

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › nobody starts a siege on a turret, a trap or the wire, posted there before it was built or not")]
        public void NobodyStartsASiegeOnATurretATrapOrTheWire()
        {
            var c = Solo();
            c.Siege.In = 0;
            Fly(c);
            CampaignHelpers.Raise(c, PieceIds.Turret);
            var spot = CampaignHelpers.FreeSpot(c, PieceIds.Mine);
            Base.FellTree(c.Bastion, spot.X, spot.Y);
            Run(c, new PostCommand { Soldier = c.Squad[0], At = spot });
            var mine = CampaignHelpers.Raise(c, PieceIds.Mine);
            Assert.Equal((spot.X, spot.Y), (mine.X, mine.Y));
            var armed = Base.ArmedTiles(c.Bastion);
            Assert.Equal(2, armed.Count);
            var offer = Campaign.ActiveOffer(c)!;
            var s = Setup.CreateMission(SiegeMission(c, offer));
            var defenders = s.Units.Where((u) => u.Team == Teams.Xcom && u.Automaton == null).ToList();
            Assert.Equal(c.Squad.Count, defenders.Count);
            foreach (var u in defenders) Assert.False(armed.Has(u.Pos.Y * s.Width + u.Pos.X));
        }

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › fallen with someone left: another clearing, the stores lost, the soldiers keep what they carry")]
        public void FallenWithSomeoneLeft()
        {
            var c = Solo();
            c.Siege.In = 0;
            Fly(c);
            // One soldier stayed in, wounded: the rest defend and fall.
            var home = c.Soldiers[0];
            home.WoundedDays = 20;
            c.Squad = c.Squad.Where((id) => id != home.Id).ToList();
            CampaignHelpers.Raise(c, PieceIds.Radio);
            var credits = c.Credits;
            Assert.True(credits > 0);
            var report = Fly(c, Outcomes.Defeat, (s) =>
            {
                foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom && x.Automaton == null))
                {
                    u.Alive = false;
                    u.Hp = 0;
                }
            });
            Assert.True(report.Fallen);
            Assert.Null(c.Outcome);
            Assert.Equal(new[] { PieceIds.Caravan }, c.Bastion.Pieces.Select((p) => p.Piece));
            Assert.Equal(new int?[] { 0, 0 }, new int?[] { c.Credits, c.Parts });
            Assert.True(home.Alive);
            Assert.Contains(c.Log, (l) => l.Alert?.Title == "El Bastión ha caído");
            // Only the wounded left and nothing to recruit with: time would stand still, so one waits for them to heal.
            Assert.True(Campaign.MustWait(c));
            Assert.Contains(Campaign.NextSteps(c, 0), (x) => x.View == BaseViews.Barracks && x.Label == "Nadie puede salir: esperad a que se curen" && x.Tone == NextStepTones.Urgent && x.Count == null && x.Go == null);
            var day = c.Day;
            // (The Marea, which goes on meanwhile, held back: it is not what is tested here.)
            c.NextDoomDay = 999;
            Run(c, new AdvanceCommand { Ready = true });
            // Until someone has healed, or enough suministros have come in to recruit.
            Assert.True(home.WoundedDays == 0 || c.Credits >= Campaign.RecruitCost(c));
            Assert.True(c.Day > day);
            // Once someone can go out, waiting is a day at a time (Ana and Arturo, 22/10).
            Assert.False(Campaign.MustWait(c));
            var now = c.Day;
            Run(c, new AdvanceCommand { Ready = true });
            Assert.Equal(now + 1, c.Day);
        }

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › one siege in three by night, never the first")]
        public void OneSiegeInThreeByNight()
        {
            var nights = new[] { 0, 1 }.Select((held) =>
            {
                var n = 0;
                for (var seed = 1; seed <= 60; seed++)
                {
                    var c = Solo();
                    c.Siege.In = 1;
                    c.Siege.Held = held;
                    Run(c, new ChooseMissionCommand { Id = c.Offers.First((o) => (o.Story ?? 0) == 0).Id });
                    var s = Setup.CreateMission(new MissionOptions { Map = Campaign.OfferMap(c, Campaign.ActiveOffer(c)!), Slots = c.Players, Rng = new Rng(2), Squad = Campaign.BuildSquad(c), Pods = Campaign.ActiveOffer(c)!.Pods });
                    s.Outcome = Outcomes.Victory;
                    s.OutcomeReason = EndReasons.Objective;
                    Campaign.ApplyMissionResult(c, s, new Rng(seed));
                    var siege = Campaign.ActiveOffer(c)!;
                    Assert.True(siege.Siege);
                    // Lore's line for the dark, after the rest.
                    Assert.Equal(siege.Night == true, Campaign.OfferBriefing(c, siege).EndsWith(Lore.BaseSiegeText.Night, StringComparison.Ordinal));
                    if (siege.Night == true) n++;
                }
                return n;
            }).ToList();
            Assert.Equal(0, nights[0]);
            Assert.True(nights[1] > 10, $"{nights[1]}");
            Assert.True(nights[1] < 30, $"{nights[1]}");
        }

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › each places their own defenders anywhere free on the clearing, and the siege begins with them there (18/10 #58)")]
        public void EachPlacesTheirOwnDefenders()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(3));
            c.NextEventDay = 999;
            c.Siege.In = 0;
            Fly(c);
            Assert.True(Campaign.ActiveOffer(c)?.Siege);
            var mine = c.Soldiers.First((s) => s.Owner == 0 && c.Squad.Contains(s.Id));
            var theirs = c.Soldiers.First((s) => s.Owner == 1 && c.Squad.Contains(s.Id));
            var lot = Base.BastionLot(c.Bastion)!;
            var spot = new Vec2 { X = lot.X - 3, Y = lot.Y - 3 };
            for (var x = spot.X - 1; x <= spot.X + 1; x++) Base.FellTree(c.Bastion, x, spot.Y);
            ThrowsText("Solo puedes colocar a tus soldados.", c, new PostCommand { Soldier = theirs.Id, At = spot }, 0, Both);
            Throws("borde", c, new PostCommand { Soldier = mine.Id, At = new Vec2 { X = 0, Y = 5 } }, 0, Both);
            var wall = Base.BastionFootprint(c.Bastion)[0];
            ThrowsText("Ahí no se puede estar.", c, new PostCommand { Soldier = mine.Id, At = wall }, 0, Both);
            Run(c, new PostCommand { Soldier = mine.Id, At = spot }, 0, Both);
            ThrowsText("Ahí ya hay alguien.", c, new PostCommand { Soldier = theirs.Id, At = spot }, 1, Both);
            var post = Assert.Single(c.SiegePosts!);
            Assert.Equal(mine.Id, post.Key);
            Assert.Equal((spot.X, spot.Y), (post.Value.X, post.Value.Y));
            var offer = Campaign.ActiveOffer(c)!;
            var s = Setup.CreateMission(SiegeMission(c, offer));
            var pos = s.Units.First((u) => u.CampaignId == mine.Id).Pos;
            Assert.Equal((spot.X, spot.Y), (pos.X, pos.Y));
            // The rest, by the Bastion, none on the posted tile.
            Assert.Single(s.Units, (u) => u.Team == Teams.Xcom && u.Pos.X == spot.X && u.Pos.Y == spot.Y);
            // Once the siege is over, the posts go.
            Fly(c);
            Assert.Null(c.SiegePosts);
        }

        [Fact(DisplayName = "the sieges in the campaign (docs/FASE12.md §11.5) › fallen with nobody left: the campaign is lost")]
        public void FallenWithNobodyLeft()
        {
            var c = Solo();
            c.Siege.In = 0;
            Fly(c);
            Fly(c, Outcomes.Defeat, (s) =>
            {
                foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom))
                {
                    u.Alive = false;
                    u.Hp = 0;
                }
            });
            Assert.DoesNotContain(c.Soldiers, (s) => s.Alive);
            Assert.Equal(Outcomes.Defeat, c.Outcome);
        }

        // ------------------------------------------------------------ the Camposanto

        [Fact(DisplayName = "the Camposanto › keeps the fallen with the day they fell, steadies everyone, and at level 3 the survivors learn from the losses")]
        public void KeepsTheFallenWithTheDayTheyFell()
        {
            var c = Solo();
            var memorial = CampaignHelpers.Raise(c, PieceIds.Memorial);
            void Put(int level) => memorial.Level = level;
            var s0 = c.Soldiers[0];
            Put(1);
            var will = Campaign.SquadMember(c, s0).Will;
            Put(2);
            Assert.Equal(will + 5, Campaign.SquadMember(c, s0).Will);
            Put(3);
            Assert.Equal(will + 10, Campaign.SquadMember(c, s0).Will);
            var offer = c.Offers[0];
            Run(c, new ChooseMissionCommand { Id = offer.Id });
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods });
            var xcom = mission.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            var fallen = xcom[0];
            var survivor = xcom[1];
            fallen.Alive = false;
            fallen.Hp = 0;
            survivor.Deeds = new Deeds { [DeedIds.Hit] = 2 };
            mission.Outcome = Outcomes.Victory;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(1));
            var dead = c.Soldiers.First((s) => s.Id == fallen.CampaignId);
            Assert.False(dead.Alive);
            Assert.Equal(report.Day, dead.FellDay);
            Assert.Contains(c.Log, (l) => l.Text == $"{dead.Name} descansa en el Camposanto.");
            // What it did, and one more of it for the fallen.
            Assert.Equal(3, report.Squad.First((e) => e.Id == survivor.CampaignId).Learned![SkillIds.Shooting]);
        }

        // ------------------------------------------------------------ the world

        [Fact(DisplayName = "the world › a sabotage mended slowly leaves a building without power, not working until it is mended")]
        public void ASabotageMendedSlowly()
        {
            var c = Solo();
            var radio = CampaignHelpers.Raise(c, PieceIds.Radio);
            Assert.True(Campaign.HasRadio(c));
            c.Event = CampaignEventIds.Sabotage;
            Run(c, new AnswerEventCommand { Option = 1 });
            Assert.Equal(Base.Pieces[PieceIds.Radio].Hp / 2, radio.Hp);
            Assert.False(Campaign.HasRadio(c));
            Run(c, new MendCommand { Piece = radio.Id });
            Assert.True(Campaign.HasRadio(c));
        }

        [Fact(DisplayName = "the world › contacting regions costs credits, grows the monthly payment and is capped by communications")]
        public void ContactingRegionsCostsCredits()
        {
            var c = Solo();
            Assert.Equal(2, Campaign.ContactCapacity(c));
            Run(c, new ContactCommand { Region = RegionIds.Oce });
            Throws("Sala de radio", c, new ContactCommand { Region = RegionIds.Afr });
            var credits = c.Credits;
            c.NextSupplyDay = c.Day + 1;
            Campaign.AdvanceTime(c, new Rng(11));
            Assert.Equal(credits + 60 + 30 + 25, c.Credits);
        }

        [Fact(DisplayName = "the world › alien facilities push the project until a squad destroys them")]
        public void AlienFacilitiesPushTheProject()
        {
            var c = Solo();
            // Low activity, held there: no zone under the Marea to get in the way of the contacts.
            c.Doom = 2;
            c.NextDoomDay = 999;
            c.NextFacilityDay = c.Day + 1;
            Campaign.AdvanceTime(c, new Rng(11));
            var region = c.Regions.FirstOrDefault((r) => r.Facility != null);
            Assert.NotNull(region);
            if (!region.Contacted)
            {
                Assert.DoesNotContain(c.Offers, (o) => o.Facility != null);
                c.Credits = 2000;
                c.Level = 7;
                CampaignHelpers.Raise(c, PieceIds.Radio, 3);
                // Contacts go by road: every zone on the way first.
                foreach (var id in Campaign.RoadRoute(RegionIds.Weu, region.Id).Skip(1))
                    if (!c.Regions.First((r) => r.Id == id).Contacted) Run(c, new ContactCommand { Region = id });
            }
            var assault = c.Offers.First((o) => o.Facility == region.Id);
            Assert.Equal(MissionKinds.Sabotage, assault.Kind);
            Assert.Null(assault.ExpiresDay);
            Run(c, new ChooseMissionCommand { Id = assault.Id });
            var doom = c.Doom;
            var mission = Setup.CreateMission(new MissionOptions { Map = assault.Map, Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = assault.Kind, Pods = assault.Pods });
            mission.Outcome = Outcomes.Victory;
            Campaign.ApplyMissionResult(c, mission, new Rng(1));
            Assert.Null(region.Facility);
            Assert.True(c.Doom <= doom - 1);
        }

        [Fact(DisplayName = "the world › events stop the clock until both answer, and the answer does what it says")]
        public void EventsStopTheClockUntilBothAnswer()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(5));
            c.NextEventDay = 2;
            Campaign.AdvanceTime(c, new Rng(11));
            Assert.NotNull(c.Event);
            // Nor can the squad take off with the event unanswered.
            var activeMission = c.ActiveMission;
            var squad = c.Squad;
            c.ActiveMission = "pending";
            c.Squad = new List<string> { c.Soldiers[0].Id };
            Throws("acontecimiento", c, new LaunchCommand { Ready = true }, 0, Both);
            c.ActiveMission = activeMission;
            c.Squad = squad;
            c.Event = CampaignEventIds.Defector;
            var offers = c.Offers.Count;
            Run(c, new AnswerEventCommand { Option = 0 }, 0, Both);
            Assert.Matches("Interrogarlo", c.Proposal?.Text);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            // What he knows: where to go, a new operation (18/10 #40).
            Assert.Equal(offers + 1, c.Offers.Count);
            Assert.Null(c.Event);
            Assert.True(Campaign.Events.Count >= 8);
        }

        // ------------------------------------------------------------ alien bodies

        [Fact(DisplayName = "alien bodies › are sold after a won mission, and left behind after a lost one")]
        public void AreSoldAfterAWonMission()
        {
            var c = Solo();
            // Two missions in a row, no siege between them.
            c.Siege.In = 5;
            var offer = c.Offers[0];
            Run(c, new ChooseMissionCommand { Id = offer.Id });
            var mission = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(2), Pods = new List<List<string>> { new List<string> { AlienIds.Sectoid, AlienIds.Trooper } } });
            foreach (var u in mission.Units) if (u.Team == Teams.Alien) u.Alive = false;
            mission.Outcome = Outcomes.Victory;
            var credits = c.Credits;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(1));
            Assert.Equal(2, report.Bodies);
            Assert.Equal(5 + 2, report.Salvage);
            Assert.Equal(credits + (offer.Reward.Credits ?? 0) + 7, c.Credits);

            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id });
            mission.Outcome = Outcomes.Defeat;
            var lost = Campaign.ApplyMissionResult(c, mission, new Rng(1));
            Assert.Equal(0, lost.Bodies);
            Assert.Equal(0, lost.Salvage);
        }

        // ------------------------------------------------------------ equipment

        [Fact(DisplayName = "equipment › a proposal says what it will cost, paid only when the partner accepts (playtest 2026-10-16)")]
        public void AProposalSaysWhatItWillCost()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            c.Credits = 500;
            // (The clearing is small since 28/10: the pines out of the way, not to fell them first.)
            c.Bastion.Trees = new List<int>();
            // A barracón is dear: a vote (docs/FASE12.md §11.7).
            var spot = CampaignHelpers.FreeSpot(c, PieceIds.Quarters);
            Run(c, Place(PieceIds.Quarters, spot), 0, Both);
            Assert.Equal(500, c.Credits);
            Assert.Equal(Base.Pieces[PieceIds.Quarters].Cost.Supplies, Campaign.ProposalCost(c, c.Proposal!.Action));
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(500 - Base.Pieces[PieceIds.Quarters].Cost.Supplies, c.Credits);
            Assert.Equal(0, Campaign.ProposalCost(c, new ChooseMissionAction { Id = "x" }));
        }

        [Fact(DisplayName = "equipment › accessories are bought into a shared stock and each player equips their own soldiers")]
        public void AccessoriesAreBoughtIntoASharedStock()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            c.Credits = 500;
            var mine = c.Soldiers.First((s) => s.Owner == 0);
            var theirs = c.Soldiers.First((s) => s.Owner == 1);
            Run(c, new BuyCommand { Item = ItemIds.SmokeGrenade }, 0, Both);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Run(c, new EquipCommand { Soldier = mine.Id, Slot = ItemSlots.Utility, Item = ItemIds.SmokeGrenade }, 0, Both);
            Assert.Equal(0, Campaign.ItemsFree(c, ItemIds.SmokeGrenade));
            Throws("libres", c, new EquipCommand { Soldier = theirs.Id, Slot = ItemSlots.Utility, Item = ItemIds.SmokeGrenade }, 1, Both);
            Throws("tus soldados", c, new EquipCommand { Soldier = theirs.Id, Slot = ItemSlots.Utility, Item = ItemIds.FirstAid }, 0, Both);
            Throws("albergue ampliado", c, new BuyCommand { Item = ItemIds.MindShield }, 0, Both);
            // The belt has two places for any accessory (docs/FASE9.md §4); special rounds go no more.
            Run(c, new BuyCommand { Item = ItemIds.SmokeGrenade }, 0, Both);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Run(c, new EquipCommand { Soldier = mine.Id, Slot = ItemSlots.Ammo, Item = ItemIds.SmokeGrenade }, 0, Both);
            Assert.Equal(0, Campaign.ItemsFree(c, ItemIds.SmokeGrenade));
            c.Inventory[ItemIds.ApRounds] = 1;
            Throws("cinturón", c, new EquipCommand { Soldier = mine.Id, Slot = ItemSlots.Ammo, Item = ItemIds.ApRounds }, 0, Both);
            // Nor are they sold, nor listed among what a level brings (UI, 23/10: they stood «new» for ever).
            c.Level = 4;
            Throws("Taller", c, new BuyCommand { Item = ItemIds.TracerRounds }, 0, Both);
            Assert.Contains("Granada aturdidora", Progression.LevelUnlocks(2));
            Assert.DoesNotContain("Munición trazadora", Progression.LevelUnlocks(2));
            Assert.DoesNotContain("Munición perforante", Progression.LevelUnlocks(4));
        }

        [Fact(DisplayName = "equipment › equipment reaches the battlefield and is lost with its bearer")]
        public void EquipmentReachesTheBattlefield()
        {
            var c = Solo();
            var s = c.Soldiers[0];
            c.Level = 4;
            Run(c, new BuyCommand { Item = ItemIds.FragGrenade });
            Run(c, new BuyCommand { Item = ItemIds.SmokeGrenade });
            Run(c, new EquipCommand { Soldier = s.Id, Slot = ItemSlots.Ammo, Item = ItemIds.FragGrenade });
            Run(c, new EquipCommand { Soldier = s.Id, Slot = ItemSlots.Utility, Item = ItemIds.SmokeGrenade });
            var grenades = c.Inventory[ItemIds.FragGrenade];
            var member = Campaign.SquadMember(c, s);
            Assert.Equal(2, member.Charges[ChargeIds.Grenade]);
            Assert.Equal(1, member.Charges[ChargeIds.Smoke]);
            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id });
            Run(c, new SquadCommand { Soldier = s.Id, On = true });
            var mission = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(1), Squad = Campaign.BuildSquad(c) });
            var unit = mission.Units.First((u) => u.CampaignId == s.Id);
            Assert.Contains("smoke", Abilities.AbilitiesOf(unit));
            unit.Alive = false;
            unit.Hp = 0;
            mission.Outcome = Outcomes.Defeat;
            Campaign.ApplyMissionResult(c, mission, new Rng(1));
            Assert.Equal(grenades - 1, c.Inventory[ItemIds.FragGrenade]);
            Assert.Equal(0, Stock(c, ItemIds.SmokeGrenade));
        }

        // ------------------------------------------------------------ customization

        private static Appearance Look() => new Appearance { Primary = "#8f2f2f", Secondary = "#c9a13a", Visor = "#7dffb0", Head = "beret", Build = "slim" };

        /// <summary>`toMatchObject({ appearance: look })`: what `look` has, the same (the rest of `actual` may be anything).</summary>
        private static void HasLook(Appearance? actual)
        {
            var look = Look();
            Assert.NotNull(actual);
            Assert.Equal((look.Primary, look.Secondary, look.Visor, look.Head, look.Build), (actual!.Primary, actual.Secondary, actual.Visor, actual.Head, actual.Build));
        }

        [Fact(DisplayName = "customization › players restyle their own soldiers, and the look reaches the mission")]
        public void PlayersRestyleTheirOwnSoldiers()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(4));
            var mine = c.Soldiers.First((s) => s.Owner == 0);
            var theirs = c.Soldiers.First((s) => s.Owner == 1);
            Run(c, new CustomizeCommand { Soldier = mine.Id, Name = "  Ada Ríos ", Nickname = "Halcón", Appearance = Look() }, 0, Both);
            Assert.Equal("Ada Ríos", mine.Name);
            Assert.Equal("Halcón", mine.Nickname);
            HasLook(mine.Appearance);
            // Personal: no proposal for the partner to vote on.
            Assert.Null(c.Proposal);
            Throws("tus soldados", c, new CustomizeCommand { Soldier = theirs.Id, Name = "X", Nickname = "", Appearance = Look() }, 0, Both);
            Throws("nombre", c, new CustomizeCommand { Soldier = mine.Id, Name = "   ", Nickname = "", Appearance = Look() }, 0, Both);
            Throws("apodo", c, new CustomizeCommand { Soldier = mine.Id, Name = "Ada", Nickname = new string('x', 17), Appearance = Look() }, 0, Both);
            var red = Look();
            red.Primary = "red";
            Throws("aspecto", c, new CustomizeCommand { Soldier = mine.Id, Name = "Ada", Nickname = "", Appearance = red }, 0, Both);

            c.Squad = new List<string> { mine.Id };
            var mission = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0, 1 }, Rng = new Rng(1), Squad = Campaign.BuildSquad(c) });
            var unit = mission.Units.First((u) => u.CampaignId == mine.Id);
            Assert.Equal("Ada Ríos", unit.Name);
            Assert.Equal("Halcón", unit.Nickname);
            HasLook(unit.Appearance);
        }

        [Fact(DisplayName = "customization › the default look is the armour soldiers had before customization")]
        public void TheDefaultLookIsTheArmourOfBefore()
        {
            // The client used to mix the player colour into grey with THREE.Color.lerp (linear space).
            Assert.Equal("#826cbd", Appearances.DefaultAppearance(0).Primary);
            Assert.Equal("#bc854b", Appearances.DefaultAppearance(1).Primary);
        }

        [Fact(DisplayName = "customization › new recruits come without a class, with a recruit look in their player colours")]
        public void NewRecruitsComeWithoutAClass()
        {
            var c = Solo();
            foreach (var x in c.Soldiers.Skip(2)) x.Alive = false;
            Run(c, new RecruitCommand());
            var s = c.Soldiers.Last();
            Assert.Empty(s.Board);
            Assert.Equal(CampaignJsonTests.Write(Appearances.DefaultAppearance(0, null, Content.SexOfName(s.Name), s.Id)), CampaignJsonTests.Write(s.Appearance));
        }
    }
}
