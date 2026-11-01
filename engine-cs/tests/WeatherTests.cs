using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The weather (new in C#, not in the TypeScript engine): Arte's five in different missions, and what a storm, a
    /// blizzard and a downpour change (Arturo, 1/11; Content.Weather).
    /// </summary>
    public class WeatherTests
    {
        private static GameState Open(int side = 30)
        {
            return StateFrom(Enumerable.Range(0, side).Select((_) => new string('.', side)).ToArray());
        }

        [Fact(DisplayName = "weather › by the battlefield's seed: two in three clear, rain out of winter, snow in it")]
        public void ByTheSeed()
        {
            var seeds = Enumerable.Range(1, 3000).ToList();
            var summer = seeds.Select((n) => Campaign.WeatherFor(n, false)).ToList();
            var winter = seeds.Select((n) => Campaign.WeatherFor(n, true)).ToList();
            double Share(List<string?> w, string? kind) => w.Count((x) => x == kind) / (double)w.Count;
            Assert.InRange(Share(summer, null), 0.6, 0.72);
            Assert.InRange(Share(winter, null), 0.6, 0.72);
            Assert.InRange(Share(summer, Weathers.Drizzle), 0.10, 0.18);
            Assert.InRange(Share(summer, Weathers.Downpour), 0.08, 0.16);
            Assert.InRange(Share(summer, Weathers.Storm), 0.05, 0.11);
            Assert.InRange(Share(winter, Weathers.Snowfall), 0.18, 0.26);
            Assert.InRange(Share(winter, Weathers.Blizzard), 0.09, 0.15);
            Assert.DoesNotContain(summer, (x) => x == Weathers.Snowfall || x == Weathers.Blizzard);
            Assert.DoesNotContain(winter, (x) => x == Weathers.Drizzle || x == Weathers.Downpour || x == Weathers.Storm);
            Assert.Equal(summer, seeds.Select((n) => Campaign.WeatherFor(n, false)).ToList());
        }

        [Fact(DisplayName = "weather › an operation's, on its card and in the mission launched; none for the story, the final mission or a siege; a skirmish's, as chosen")]
        public void OnTheCardAndInTheMission()
        {
            var golden = Path.Combine(AppContext.BaseDirectory, "golden");
            var states = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(golden, "campaign", "states.json")))["states"]!;
            var c = states.Single((s) => (string)s["name"]! == "duo-new")["campaign"]!.ToObject<CampaignState>(Json.Serializer)!;
            var offer = Campaign.ActiveOffer(c)!;
            // The opening chapter of the story: clear.
            Assert.Null(Campaign.OfferWeather(c, offer));
            offer.Story = null;
            var winter = Campaign.Wintry(c);
            offer.Map.Seed = Enumerable.Range(1, 500).First((n) => Campaign.WeatherFor(n, winter) != null);
            var weather = Campaign.OfferWeather(c, offer);
            Assert.NotNull(weather);
            var state = new EngineCampaignRules().LaunchMission(c, new Rng(1))!.Value.state;
            Assert.Equal(weather, state.Weather);
            Assert.Equal(weather, Events.CloneState(state).Weather);
            offer.Siege = true;
            Assert.Null(Campaign.OfferWeather(c, offer));
            offer.Siege = null;
            offer.Final = true;
            Assert.Null(Campaign.OfferWeather(c, offer));
            var skirmish = EngineRules.MissionOptionsOf(new SkirmishOptions { Biome = Biomes.City, Seed = 7, Slots = new List<int> { 0 }, Weather = Weathers.Storm }, new Rng(1));
            Assert.Equal(Weathers.Storm, Setup.CreateMission(skirmish).Weather);
            Assert.Null(Setup.CreateMission(EngineRules.MissionOptionsOf(new SkirmishOptions { Biome = Biomes.City, Seed = 7, Slots = new List<int> { 0 } }, new Rng(1))).Weather);
        }

        [Fact(DisplayName = "weather › a storm or a blizzard cuts sight to 70 %, for everyone, by day and by night (the smaller); a drizzle and a snowfall don't")]
        public void ACutInSight()
        {
            var s = Open();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 2, 17);
            var full = Los.SightOf(soldier);
            Assert.True(full >= 15);
            Assert.Equal(full, Los.SightTo(s, soldier, zombie.Pos));
            Assert.True(Los.CanSee(s, soldier, zombie));
            Assert.True(Los.TeamVisibility(s, Teams.Xcom)[17 * 30 + 2]);
            var detection = Los.DetectionTo(s, zombie, soldier.Pos);
            foreach (var calm in new[] { Weathers.Drizzle, Weathers.Downpour, Weathers.Snowfall })
            {
                s.Weather = calm;
                Assert.Equal(full, Los.SightTo(s, soldier, zombie.Pos));
            }
            foreach (var wild in new[] { Weathers.Storm, Weathers.Blizzard })
            {
                s.Weather = wild;
                Assert.Equal((int)Js.Round(full * Content.Weather.Sight), Los.SightTo(s, soldier, zombie.Pos));
                Assert.Equal((int)Js.Round(Los.SightOf(zombie) * Content.Weather.Sight), Los.SightTo(s, zombie, soldier.Pos));
                Assert.False(Los.CanSee(s, soldier, zombie));
                Assert.False(Los.TeamVisibility(s, Teams.Xcom)[17 * 30 + 2]);
                Assert.Equal(detection * Content.Weather.Sight, Los.DetectionTo(s, zombie, soldier.Pos), 6);
            }
            // By night, the smaller: a soldier's night sight under a storm's, the dead's storm sight under their night one.
            s.Weather = Weathers.Storm;
            s.Mission.Night = true;
            Assert.Equal(Math.Min(Los.NightSight(soldier), Los.SightIn(s, soldier)), Los.SightTo(s, soldier, zombie.Pos));
            Assert.Equal(Los.SightIn(s, zombie), Los.SightTo(s, zombie, soldier.Pos));
        }

        [Fact(DisplayName = "weather › a downpour carries a noise 80 % as far, a storm 60 %; the dead beyond don't hear it")]
        public void RainMuffles()
        {
            NoiseEvent Noise(string? weather)
            {
                var s = Open();
                s.Weather = weather;
                AddUnit(s, "a1", TemplateIds.Zombie, 2, 11);
                var sim = NewSim(s);
                sim.MakeNoise(P(2, 2), 10);
                return sim.Events.OfType<NoiseEvent>().Last();
            }
            Assert.Equal(10, Noise(null).Radius);
            Assert.Contains("a1", Noise(null).Heard);
            Assert.Equal(10, Noise(Weathers.Drizzle).Radius);
            Assert.Equal(10, Noise(Weathers.Blizzard).Radius);
            Assert.Equal(8, Noise(Weathers.Downpour).Radius);
            Assert.DoesNotContain("a1", Noise(Weathers.Downpour).Heard);
            Assert.Equal(6, Noise(Weathers.Storm).Radius);
        }

        [Fact(DisplayName = "weather › the squad's noise fills the horde's meter less in a downpour and less still in a storm (Arturo, 1/11)")]
        public void RainFillsTheMeterSlower()
        {
            int Meter(string? weather)
            {
                var s = StateFrom(new[] { "............", "............", "............", "............", "............" });
                s.Weather = weather;
                var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2);
                var sim = NewSim(s);
                // One big noise: the meter tops out at the horde.
                sim.MakeNoise(P(2, 2), 25, soldier);
                return sim.S.Mission.Noise ?? 0;
            }
            Assert.True(Meter(Weathers.Downpour) < Meter(null), $"{Meter(Weathers.Downpour)} against {Meter(null)}");
            Assert.True(Meter(Weathers.Storm) < Meter(Weathers.Downpour), $"{Meter(Weathers.Storm)} against {Meter(Weathers.Downpour)}");
            Assert.Equal(Meter(null), Meter(Weathers.Drizzle));
        }
    }
}
