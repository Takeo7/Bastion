using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Arturo's notes of 18/11 (.dev-snapshots/producto/notas-2026-11-18/NOTAS.md), Gameplay's: the bolt-action rifle's own
    /// weapon (T14); a walk shown tile by tile (T18); La Lata's zone as many tiles as its seats, by its doors (T21). T20's
    /// bites are RunPastBiteTests'.
    /// </summary>
    public class NotesOf18NovTests
    {
        [Fact(DisplayName = "T14 › the bolt-action rifle fires one round, with the assault rifle's figures")]
        public void TheBoltActionRifleFiresOneRound()
        {
            var found = Content.FoundWeapons[FoundWeaponIds.HuntingRifle];
            Assert.Equal(WeaponIds.HuntingRifle, found.Weapon);
            var bolt = Content.Weapons[WeaponIds.HuntingRifle];
            var assault = Content.Weapons[WeaponIds.Rifle];
            Assert.Equal(("Fusil de cerrojo", 1, 3), (bolt.Name, bolt.Burst, assault.Burst));
            Assert.Equal((assault.Damage, assault.Clip, assault.Range, assault.ApCost, assault.Noise), (bolt.Damage, bolt.Clip, bolt.Range, bolt.ApCost, bolt.Noise));
            Assert.Contains(WeaponIds.HuntingRifle, WeaponIds.All);
        }

        [Fact(DisplayName = "T18 › a walk tile by tile ends as the whole of it")]
        public void AWalkTileByTileEndsAsTheWholeOfIt()
        {
            var s = StateFrom(new[]
            {
                "..........",
                "..........",
                "..........",
            });
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 1, 1);
            var r = Exec(Events.CloneState(s), new MoveCommand { Unit = soldier.Id, To = P(5, 1) });
            Assert.True(r.Ok, r.Error);
            var moved = Assert.Single(Of<MovedEvent>(r.Events));
            var stepped = Events.CloneState(s);
            var whole = Events.CloneState(s);
            var ap = stepped.Units[0].Ap;
            for (var i = 0; i < moved.Path.Count; i++)
            {
                Events.MoveStep(stepped, moved, i);
                Assert.True(Grid.SamePos(moved.Path[i], stepped.Units[0].Pos));
                Assert.Equal(ap, stepped.Units[0].Ap);
            }
            Events.ApplyEvent(stepped, moved);
            Events.ApplyEvent(whole, moved);
            Assert.Equal(Bastion.Net.Json.Write(whole), Bastion.Net.Json.Write(stepped));
        }

        [Fact(DisplayName = "T21 › La Lata's zone: as many tiles as seats, free, one piece against the vehicle")]
        public void AsManyTilesAsSeatsByTheVehicle()
        {
            var maps = 0;
            foreach (var level in new[] { 0, 1, 2, 3 })
            {
                foreach (var site in new[] { Places.Town, Places.City, Places.Country, Places.Factory, Places.Estate, Places.Roadside, Places.Hospital, Places.Offices, Places.Shed, Places.Police, Places.Plaza, Places.Camping })
                {
                    for (var seed = 1; seed <= 6; seed++)
                    {
                        var (c, s) = LaunchAt(NodeKinds.Normal, seed, before: (x) =>
                        {
                            x.Upgrades![UpgradeIds.Squad] = level;
                            x.Squad = Runs.DefaultSquad(x, x.Players);
                        }, node: (n) => { n.Site = site; n.Seed = seed * 101 + level; });
                        var seats = Runs.SeatsOf(c);
                        var at = $"{site} {seed} {level}";
                        Assert.Equal(seats, s.Mission.Seats);
                        Assert.Equal(Runs.VehicleOf(c), s.Vehicle);
                        var bus = s.Mission.EvacBus;
                        if (bus == null) continue;
                        maps++;
                        var zone = s.Mission.EvacZone;
                        Assert.True(seats == zone.Count, $"{at}: {zone.Count} tiles");
                        Assert.Equal(seats, zone.Select((z) => (z.X, z.Y)).Distinct().Count());
                        foreach (var z in zone) Assert.True(Flights.CanStopAt(s, new Pos { X = z.X, Y = z.Y, Z = 0 }), $"{at}: {z.X},{z.Y}");
                        // One piece against the vehicle: every tile reached from it through the zone, the eight ways.
                        var reached = new HashSet<(int, int)>();
                        var frontier = bus.Tiles.Select((t) => (t.X, t.Y)).ToList();
                        while (frontier.Count > 0)
                        {
                            frontier = zone.Where((z) => !reached.Contains((z.X, z.Y)) && frontier.Any((f) => Math.Max(Math.Abs(f.X - z.X), Math.Abs(f.Y - z.Y)) <= 1)).Select((z) => (z.X, z.Y)).ToList();
                            foreach (var f in frontier) reached.Add(f);
                        }
                        Assert.True(reached.Count == zone.Count, $"{at}: {string.Join(" ", zone.Select((z) => $"{z.X},{z.Y}"))} from {string.Join(" ", bus.Tiles.Select((t) => $"{t.X},{t.Y}"))}");
                        if (s.Vehicle == Runs.Moto)
                        {
                            // Within its place, one each side of it.
                            Assert.All(zone, (z) => Assert.Contains(bus.Tiles, (t) => t.X == z.X && t.Y == z.Y));
                            var d = Grid.Dirs4[bus.Heading];
                            Assert.Equal(2, zone.Select((z) => z.X * -d.Y + z.Y * d.X).Distinct().Count());
                        }
                        else Assert.All(zone, (z) => Assert.DoesNotContain(bus.Tiles, (t) => t.X == z.X && t.Y == z.Y));
                    }
                }
            }
            Assert.True(maps >= 200, $"{maps}");
        }

        [Fact(DisplayName = "T2 › playing alone, what asks for the other player's soldier takes any comrade")]
        public void AloneAnyComradeIsThePartner()
        {
            var s = StateFrom(new[]
            {
                "..........",
                "..........",
            });
            var a = AddUnit(s, "s1", TemplateIds.Assault, 1, 1);
            var b = AddUnit(s, "s2", TemplateIds.Assault, 3, 1);
            a.Owner = 0;
            b.Owner = 0;
            Assert.False(Aim.PartnerNear(s, a, a.Pos));
            s.Solo = true;
            Assert.True(Aim.PartnerNear(s, a, a.Pos));
            // Not themselves.
            b.Alive = false;
            Assert.False(Aim.PartnerNear(s, a, a.Pos));
            // A run's fight alone says so; with two, not.
            var (_, alone) = LaunchAt(NodeKinds.Normal);
            Assert.True(alone.Solo);
            var c = Two();
            GoTo(c, NodeKinds.Normal);
            Assert.Null(new Bastion.Net.EngineCampaignRules().LaunchMission(c, new Rng(5))!.Value.state.Solo);
        }

        [Fact(DisplayName = "T2 › «Arreglar la radio» off the maps; one in a save is a normal fight")]
        public void NoRadioOnTheMaps()
        {
            Assert.DoesNotContain(Campaign.OldRadioStage, RunMap.Specials);
            Assert.All(Enumerable.Range(1, 200).SelectMany((seed) => RunMap.Generate(1, seed)), (n) => Assert.NotEqual(Campaign.OldRadioStage, n.Kind));
            var c = Solo();
            var run = Start(c);
            var ahead = run.Nodes.First((n) => n.Row == 3);
            ahead.Kind = Campaign.OldRadioStage;
            c.Version = 29;
            var back = Campaign.MigrateCampaign(c);
            Assert.Equal(Campaign.RunsCampaignVersion, back.Version);
            Assert.Equal(NodeKinds.Normal, RunMap.Node(back.Run!, ahead.Id)!.Kind);
        }

        [Fact(DisplayName = "T23 › a weapon's reach in tiles: the best and the most")]
        public void AWeaponsReachInTiles()
        {
            WeaponReach Of(string id) => Content.ReachTiles(Content.Weapons[id]);
            // The shotgun best up to 11 (nothing off), the assault rifle all the way, the precision rifle from 11; all to 18.
            Assert.Equal((1, 11, 18), (Of(WeaponIds.Shotgun).From, Of(WeaponIds.Shotgun).To, Of(WeaponIds.Shotgun).Max));
            Assert.Equal((1, 18, 18), (Of(WeaponIds.Rifle).From, Of(WeaponIds.Rifle).To, Of(WeaponIds.Rifle).Max));
            Assert.Equal((11, 18, 18), (Of(WeaponIds.Sniper).From, Of(WeaponIds.Sniper).To, Of(WeaponIds.Sniper).Max));
            Assert.Equal((1, 18), (Of(WeaponIds.HuntingRifle).From, Of(WeaponIds.HuntingRifle).To));
            // Blades: one, the rake two.
            Assert.Equal((1, 1, 1), (Of(WeaponIds.Knife).From, Of(WeaponIds.Knife).To, Of(WeaponIds.Knife).Max));
            Assert.Equal((1, 2, 2), (Of(WeaponIds.Rake).From, Of(WeaponIds.Rake).To, Of(WeaponIds.Rake).Max));
        }

        [Fact(DisplayName = "T2 › Registrar: its bar is search luck, and its five cells")]
        public void TheSearchingBarAndItsFive()
        {
            Assert.Equal(("Registrar", "Suerte al registrar"), (Bars.SkillDefs[SkillIds.Searching].Name, Bars.SkillDefs[SkillIds.Searching].Stat));
            Assert.Equal(("De un vistazo", 1), (Board.BoardCells["tinker"].Name, Board.BoardCells["tinker"].Charges![ChargeIds.QuickLook]));
            Assert.Equal(("Gancho", 1), (Board.BoardCells["deft"].Name, Board.BoardCells["deft"].Mods!.SearchReach));
            Assert.Equal(("Ganzúa", 25), (Board.BoardCells["picklock"].Name, Board.BoardCells["picklock"].Mods!.BoxLuck));
            Assert.Equal(("Armero", 1), (Board.BoardCells["engineer"].Name, Board.BoardCells["engineer"].Mods!.ModFinds));
            Assert.Equal(("Buscavidas", 25), (Board.BoardCells["mastermind"].Name, Board.BoardCells["mastermind"].Mods!.RoomLuck));
            Assert.DoesNotContain(Board.BoardCells.Values, (c) => (c.Mods?.Hack ?? 0) != 0);
        }

        [Fact(DisplayName = "T2 › De un vistazo: a room searched without an action, once")]
        public void QuickLookSearchesARoomForNothing()
        {
            var u = new Unit { Id = "s1", Template = TemplateIds.Assault, Ap = 0, Charges = new Dictionary<string, int> { [ChargeIds.QuickLook] = 1 } };
            Assert.Equal(0, Abilities.ApNeeded(u, AbilityIds.SearchRoom));
            Assert.Equal(1, Abilities.ApNeeded(u, AbilityIds.Search));
            u.Charges[ChargeIds.QuickLook] = 0;
            Assert.Equal(1, Abilities.ApNeeded(u, AbilityIds.SearchRoom));
        }

        [Fact(DisplayName = "T2 › Gancho: a box opened from two tiles off")]
        public void HookOpensFromTwoOff()
        {
            var s = StateFrom(new[]
            {
                "......",
                "...h..",
                "......",
            });
            var u = AddUnit(s, "s1", TemplateIds.Assault, 1, 1);
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            s.Decor[Grid.TileIndex(s, 3, 1)] = Decor.DecorCode(DecorKinds.LootWeapons);
            Assert.Empty(Looting.SearchableTiles(s, u));
            u.Mods = Content.AddMods(u.Mods, new UnitMods { SearchReach = 1 });
            var tile = Assert.Single(Looting.SearchableTiles(s, u));
            Assert.Equal((3, 1), (tile.X, tile.Y));
        }

        [Fact(DisplayName = "T2 › Armero: accessories twice as often")]
        public void ArmeroDoublesAccessories()
        {
            int Mods(int times) => Enumerable.Range(1, 4000).Count((seed) => Looting.RollBox(DecorKinds.LootWeapons, new Rng(seed), false, modTimes: times)?.Mod != null);
            var once = Mods(1);
            var twice = Mods(2);
            Assert.InRange(once / 4000.0, 0.25, 0.35);
            Assert.InRange(twice / 4000.0, 0.55, 0.65);
        }
    }
}
