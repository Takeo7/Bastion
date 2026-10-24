using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/appearance.test.ts: the tests on appearance.ts alone (and createMission's skirmish squads).
    /// Where TypeScript tells a piece set to null from one left out (`helmet: null` is no colour), C# can't: null and
    /// missing are the same (README), so that one check is left out, and so is the `extra` field C#'s Appearance has no
    /// place for. Not ported: «armour pieces › the barracks stores the pieces, and an untouched look is still the
    /// default one» (createCampaign, executeCampaignCommand: campaign.ts); «armour pieces › the protocol takes looks with
    /// or without pieces, and checks their colours» (protocol.ts's campaignCommandSchema); of «the soldier's sex
    /// (playtest 2026-10-13)», «every name has one, and soldiers and recruits get the sex of their name» (createCampaign),
    /// «is part of a valid look, and the protocol takes
    /// it» (protocol.ts), «customizing keeps it unless the look sends another…» (createCampaign, addPlayer) and «every
    /// soldier starts with a head and a build of its own, and a campaign of before gets them…» (createCampaign,
    /// migrateCampaign), all of them needing campaign.ts or protocol.ts.
    /// </summary>
    public class AppearanceTests
    {
        /// <summary>A look from before armour pieces could be painted.</summary>
        private static Appearance Old()
        {
            return new Appearance { Primary = "#8f2f2f", Secondary = "#c9a13a", Visor = "#7dffb0", Head = HeadIds.Helmet, Build = BuildIds.Slim };
        }

        /// <summary>`{ ...a, … }`: a copy of `a` with what `edit` changes.</summary>
        private static Appearance With(Appearance a, Action<Appearance>? edit = null)
        {
            var copy = new Appearance
            {
                Primary = a.Primary,
                Secondary = a.Secondary,
                Visor = a.Visor,
                Head = a.Head,
                Build = a.Build,
                Sex = a.Sex,
                Helmet = a.Helmet,
                Torso = a.Torso,
                Arms = a.Arms,
                Legs = a.Legs,
                Character = a.Character,
            };
            edit?.Invoke(copy);
            return copy;
        }

        /// <summary>toEqual: the same JSON (Step2Json.Diff: null and missing alike).</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            Assert.True(diff == null, diff);
        }

        /// <summary>`Object.keys(a)`: the fields its JSON has, in the order C# writes them (its properties', which is normalizeAppearance's).</summary>
        private static List<string> Keys(Appearance a)
        {
            return Step2Json.Element(a).EnumerateObject().Select((p) => p.Name).ToList();
        }

        // ------------------------------------------------------------ armour pieces

        [Fact(DisplayName = "armour pieces › an old look is still valid and every piece wears what it wore before")]
        public void AnOldLookIsStillValid()
        {
            var old = Old();
            Assert.True(Appearances.IsAppearance(old));
            Assert.True(Appearances.IsAppearance(Appearances.DefaultAppearance(1)));
            foreach (var part in new[] { ArmorParts.Helmet, ArmorParts.Torso, ArmorParts.Arms, ArmorParts.Legs }) Assert.Equal(old.Primary, Appearances.PartColor(old, part));
            // Berets, caps and hoods were painted in the trim; masks and bare heads have nothing to paint.
            foreach (var head in new[] { HeadIds.Hood, HeadIds.Beret, HeadIds.Cap }) Assert.Equal(old.Secondary, Appearances.PartColor(With(old, (a) => a.Head = head), ArmorParts.Helmet));
            Assert.Equal(old.Primary, Appearances.PartColor(With(old, (a) => a.Head = HeadIds.Hood), ArmorParts.Torso));
        }

        [Fact(DisplayName = "armour pieces › pieces take their own colour, and a bad one makes the look invalid")]
        public void PiecesTakeTheirOwnColour()
        {
            var old = Old();
            var look = With(old, (a) =>
            {
                a.Helmet = "#ffffff";
                a.Arms = "#2F6B4F";
            });
            Assert.True(Appearances.IsAppearance(look));
            Assert.Equal("#ffffff", Appearances.PartColor(look, ArmorParts.Helmet));
            Assert.Equal("#2F6B4F", Appearances.PartColor(look, ArmorParts.Arms));
            Assert.Equal(old.Primary, Appearances.PartColor(look, ArmorParts.Legs));
            Assert.Equal("#ffffff", Appearances.PartColor(With(look, (a) => a.Head = HeadIds.Beret), ArmorParts.Helmet));
            Assert.False(Appearances.IsAppearance(With(old, (a) => a.Legs = "red")));
            Assert.False(Appearances.IsAppearance(With(old, (a) => a.Torso = "#12345")));
            // `{ ...old, helmet: null }` is invalid in TypeScript (null is not undefined, nor a colour); in C# a null
            // helmet is one left out (README: null and missing alike), so the look is valid: not checked here.
            Assert.False(Appearances.IsAppearance(With(old, (a) => a.Head = "toString")));
        }

        [Fact(DisplayName = "armour pieces › a stored look keeps only the pieces that differ from what they would follow")]
        public void AStoredLookKeepsOnlyThePiecesThatDiffer()
        {
            var old = Old();
            // (`extra: 'x'`: C#'s Appearance has no place for a field it doesn't know.)
            var look = With(old, (a) =>
            {
                a.Helmet = "#8F2F2F";
                a.Torso = "#ffffff";
                a.Legs = null;
            });
            Same(With(old, (a) => a.Torso = "#ffffff"), Appearances.NormalizeAppearance(look));
            Assert.Equal(new[] { "primary", "secondary", "visor", "head", "build", "torso" }, Keys(Appearances.NormalizeAppearance(look)));
            // A hood follows the trim, so a hood in the armour colour is a choice worth keeping.
            Same(
                With(old, (a) =>
                {
                    a.Head = HeadIds.Hood;
                    a.Helmet = old.Primary;
                }),
                Appearances.NormalizeAppearance(With(old, (a) =>
                {
                    a.Head = HeadIds.Hood;
                    a.Helmet = old.Primary;
                })));
            Same(
                With(old, (a) => a.Head = HeadIds.Hood),
                Appearances.NormalizeAppearance(With(old, (a) =>
                {
                    a.Head = HeadIds.Hood;
                    a.Helmet = old.Secondary;
                })));
        }

        // ------------------------------------------------------------ the survivor model

        [Fact(DisplayName = "the survivor model › is an optional id the client resolves; the engine only checks its shape and keeps it")]
        public void IsAnOptionalIdTheClientResolves()
        {
            var look = With(Appearances.DefaultAppearance(0), (a) => a.Character = "hunter");
            Assert.True(Appearances.IsAppearance(look));
            Assert.False(Appearances.IsAppearance(With(look, (a) => a.Character = "No vale!")));
            Assert.Equal("hunter", Appearances.NormalizeAppearance(look).Character);
            Assert.DoesNotContain("character", Keys(Appearances.NormalizeAppearance(Appearances.DefaultAppearance(0))));
        }

        // ------------------------------------------------------------ the soldier's sex (playtest 2026-10-13)

        private static string? SexOf(string name)
        {
            return Content.SoldierNames.FirstOrDefault((n) => n.Name == name)?.Sex;
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › skirmish squads carry it too")]
        public void SkirmishSquadsCarryItToo()
        {
            var mission = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0, 1 }, Rng = new Rng(1) });
            var squad = mission.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            Assert.True(squad.Count > 0, "no squad");
            foreach (var u in squad)
            {
                Assert.Equal(SexOf(u.Name), u.Appearance?.Sex);
                Same(Appearances.DefaultAppearance(u.Owner!.Value, null, SexOf(u.Name), u.Name), u.Appearance);
            }
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › people who join by name take theirs: the story's, a relative's, a soldier's")]
        public void PeopleWhoJoinByNameTakeTheirs()
        {
            Assert.Equal("f", Campaign.SexOfPerson("Vera Lindqvist"));
            Assert.Equal("m", Campaign.SexOfPerson("Dr. Femi Adebayo"));
            Assert.Equal("f", Campaign.SexOfPerson("Agente Claire Dubois"));
            Assert.Equal("m", Campaign.SexOfPerson("Kenji Okafor"));
            Assert.Equal("f", Campaign.SexOfPerson("Mariam Rey"));
            Assert.Equal("m", Campaign.SexOfPerson("Hiro Sato"));
            Assert.Null(Campaign.SexOfPerson("Nadie Conocido"));
        }
    }
}
