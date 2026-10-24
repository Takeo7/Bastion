#nullable enable
// campaign.ts: the side quests and the rumours as they run: a quest starting, its steps (an operation or a decision),
// its end and what it leaves, the ones with their own trigger, «Caras conocidas» after a mission, and the rumours heard
// on the radio and what they turn out to be.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ----------------------------------------------------------- side quests

        /// <summary>The regional side quest a rumour in `region` can lead to.</summary>
        private static string? RumorQuest(CampaignState c, string region)
        {
            if (c.Regions.FirstOrDefault((r) => r.Id == region)?.Contacted != true) return null;
            return QuestIds.All.FirstOrDefault((id) => Quests.QuestDefs[id].Rumor && Quests.QuestDefs[id].Region == region && QuestAvailable(c, id));
        }

        /// <summary>`extra`: what a quest that starts with someone in it already knows (its owner, its soldier, its names).</summary>
        private static void StartQuest(CampaignState c, string id, Rng rng, int? owner = null, string? soldier = null, Dictionary<string, string>? names = null)
        {
            var def = Quests.QuestDefs[id];
            var contacted = c.Regions.Where((r) => r.Contacted).Select((r) => r.Id).ToList();
            var region = def.Region ?? (contacted.Count > 0 ? rng.Pick(contacted) : HomeRegion);
            var q = new QuestState
            {
                Id = id,
                Step = 0,
                Region = region,
                Owner = null,
                Soldier = null,
                Names = new Dictionary<string, string>(),
                Secret = rng.Int(2),
            };
            if (owner != null) q.Owner = owner;
            if (soldier != null) q.Soldier = soldier;
            if (names != null) q.Names = names;
            if (id == QuestIds.Mole)
            {
                // Simão is a suspect if he was let in.
                var pool = new List<string>(Quests.Suspects);
                string a;
                if (c.Story.Simon == Lore.SimonChoices.Trusted) a = "Simão Ferreira";
                else
                {
                    var i = rng.Int(pool.Count);
                    a = pool[i];
                    pool.RemoveAt(i);
                }
                var b = pool[rng.Int(pool.Count)];
                q.Names = new Dictionary<string, string> { ["a"] = a, ["b"] = b, ["culprit"] = q.Secret == 0 ? a : b };
            }
            c.Quests.Add(q);
            c.QuestLog.Delete(id);
            Log(c, Quests.Fill(def.Hook, q.Names));
            EnterStep(c, q, rng);
        }

        /// <summary>Puts the quest's current step in play: an operation on the geoscape or a decision.</summary>
        private static void EnterStep(CampaignState c, QuestState q, Rng rng)
        {
            var def = Quests.QuestDefs[q.Id];
            var step = def.Steps[q.Step];
            if (!(step is QuestMission mission))
            {
                ShowQuestEvent(c);
                return;
            }
            var offer = MakeQuestOffer(c, q, mission, rng);
            c.Offers.Add(offer);
            var intro = q.Step == 0 ? $"{Quests.Fill(def.Hook, q.Names)} " : "";
            Log(c, $"{intro}{Quests.Fill(mission.Briefing, q.Names)}", LogTones.Info, Alert($"ENCARGO · {def.Title.ToUpperInvariant()}", offer.Name));
        }

        private static MissionOffer MakeQuestOffer(CampaignState c, QuestState q, QuestMission step, Rng rng)
        {
            var difficulty = Math.Max(1, Math.Min(4, DifficultyNow(c, rng) + (step.Difficulty ?? 0)));
            // The living's, if the step says so or its named enemy is one of them (docs/ZOMBIS-PRIMERO.md).
            var living = step.Living == true || (step.Leader != null && !Content.Templates[step.Leader.Template].Undead);
            var pods = RegionPods(c, q.Region, difficulty, step.Kind, living);
            // The named enemy needs a body of its kind on the field.
            if (step.Leader != null && !pods.Any((p) => p.Contains(step.Leader.Template))) pods[0] = new[] { step.Leader.Template }.Concat(pods[0]).ToList();
            var id = NewId(c, "m");
            var name = Quests.Fill(step.Name, q.Names);
            var seed = Seed31(rng);
            return ByNight(new MissionOffer
            {
                Id = id,
                Name = name,
                Kind = step.Kind,
                Difficulty = difficulty,
                ExpiresDay = null,
                Reward = step.Reward != null ? CopyReward(step.Reward) : new MissionReward(),
                Map = MapSource.Generated(step.Biome, seed),
                Pods = pods,
                Final = false,
                Region = q.Region,
                Facility = null,
                Story = null,
                Quest = q.Id,
                Living = living ? true : (bool?)null,
                Alarm = step.Kind == MissionKinds.Sabotage ? Alarms.Police : null,
            });
        }

        /// <summary>A quest waiting at a decision becomes the pending event when nothing else is.</summary>
        private static void ShowQuestEvent(CampaignState c)
        {
            if (c.Event == CampaignEventIds.Quest && EventDef(c) == null) c.Event = null;
            if (c.Event != null) return;
            var q = c.Quests.FirstOrDefault((x) => Js.At<QuestStep?>(Quests.QuestDefs[x.Id].Steps, x.Step, null) is QuestDecision);
            if (q == null) return;
            c.Event = CampaignEventIds.Quest;
            c.QuestEvent = q.Id;
        }

        private static void AdvanceQuest(CampaignState c, QuestState q, Rng rng)
        {
            q.Step++;
            if (q.Step >= Quests.QuestDefs[q.Id].Steps.Count) FinishQuest(c, q, "success", rng);
            else EnterStep(c, q, rng);
        }

        /// <summary>The players' answer to a quest's decision.</summary>
        private static void AnswerQuest(CampaignState c, int option, Rng rng)
        {
            var q = c.Quests.FirstOrDefault((x) => x.Id == c.QuestEvent);
            if (q == null) return;
            var def = Quests.QuestDefs[q.Id];
            var choice = ((QuestDecision)def.Steps[q.Step]).Options[option];
            if (choice.Clue == true && def.Clues != null)
            {
                // What the watch showed opens the next decision.
                var name = option == 0 ? q.Names["a"] : q.Names["b"];
                var names = new Dictionary<string, string>(q.Names) { ["name"] = name };
                q.Names["clue"] = Quests.Fill(option == q.Secret ? def.Clues.Guilty : def.Clues.Innocent, names);
                Log(c, q.Names["clue"]);
            }
            if (choice.Then == QuestThens.Next) AdvanceQuest(c, q, rng);
            else if (choice.Then == QuestThens.Guess) FinishQuest(c, q, option == q.Secret ? "success" : "failure", rng);
            else FinishQuest(c, q, choice.Then, rng);
        }

        private static void FinishQuest(CampaignState c, QuestState q, string key, Rng rng)
        {
            var def = Quests.QuestDefs[q.Id];
            var outcome = def.Outcomes.TryGetValue(key, out var found) ? found : def.Outcomes["failure"];
            c.Quests = c.Quests.Where((x) => x.Id != q.Id).ToList();
            c.Offers = c.Offers.Where((o) => o.Quest != q.Id || o.Id == c.ActiveMission).ToList();
            if (c.QuestEvent == q.Id) c.QuestEvent = null;
            c.QuestLog.Set(q.Id, new QuestLogEntry { Outcome = key, Day = c.Day });
            Log(c, Quests.Fill(outcome.Text, q.Names), outcome.Tone, Alert(outcome.Tone == LogTones.Good ? "ENCARGO CUMPLIDO" : "ENCARGO CERRADO", def.Title));

            if ((outcome.Credits ?? 0) != 0) c.Credits = Math.Max(0, c.Credits + outcome.Credits!.Value);
            foreach (var e in outcome.Items ?? new Dictionary<string, int>()) c.Inventory[e.Key] = (c.Inventory.TryGetValue(e.Key, out var n) ? n : 0) + e.Value;
            if ((outcome.Doom ?? 0) != 0) c.Doom = Math.Max(0, c.Doom + outcome.Doom!.Value);
            if (!string.IsNullOrEmpty(outcome.Tech) && !c.Techs.Contains(outcome.Tech!)) c.Techs.Add(outcome.Tech!);
            if ((outcome.Delay ?? 0) != 0) foreach (var w in c.Base.Works) w.DaysLeft += OldDaysToNow(outcome.Delay!.Value);
            var soldier = c.Soldiers.FirstOrDefault((s) => s.Id == q.Soldier && s.Alive);
            if (!string.IsNullOrEmpty(outcome.Trait) && soldier != null && !soldier.Traits.Contains(outcome.Trait!)) soldier.Traits.Add(outcome.Trait!);
            if (outcome.Recruit != null)
            {
                var r = outcome.Recruit;
                var s = AddSoldier(c, q.Owner ?? NeediestPlayer(c), rng);
                if (!string.IsNullOrEmpty(r.Name))
                {
                    s.Name = Quests.Fill(r.Name!, q.Names);
                    s.Appearance = Appearances.DefaultAppearance(s.Owner, null, SexOfPerson(s.Name), s.Id);
                }
                if ((r.Rank ?? 0) != 0) Seasoned(s, r.Rank!.Value, rng);
                if (!string.IsNullOrEmpty(r.Trait)) s.Traits.Add(r.Trait!);
                Log(c, $"{s.Name} se une al Bastión ({VeterancyName(s)}).", LogTones.Good);
            }
            if (c.Doom >= DoomMax) c.Outcome = Outcomes.Defeat;
        }

        /// <summary>Side quests with their own trigger; at most one starts at a time.</summary>
        private static void RefreshQuests(CampaignState c, Rng rng)
        {
            if (c.Outcome != null) return;
            ShowQuestEvent(c);
            if (c.Quests.Count >= MaxQuests) return;
            var level = Progression.BastionLevel(c);
            bool Contacted(string id) => c.Regions.FirstOrDefault((r) => r.Id == id)?.Contacted == true;
            var due = new[]
            {
                (QuestIds.Mole, (c.Seen.TryGetValue(CampaignEventIds.Sabotage, out var sabotages) ? sabotages : 0) >= 2),
                (QuestIds.Ibarra, level >= 3 && c.Story.Done >= 1),
                (QuestIds.Amara, c.Story.Done >= Lore.NwosuChapter && Contacted(RegionIds.Afr)),
                (QuestIds.Numbers, level >= 2 && c.Day >= OldDaysToNow(20)),
            };
            foreach (var (id, ready) in due)
            {
                if (!ready || !QuestAvailable(c, id)) continue;
                StartQuest(c, id, rng);
                return;
            }
        }

        /// <summary>After a mission, a soldier with a nickname may hear of someone among the Cuervos' prisoners.</summary>
        private static void MaybeFaces(CampaignState c, MissionReport report, Rng rng)
        {
            if (c.Outcome != null || c.Quests.Count >= MaxQuests || Progression.BastionLevel(c) < 2 || !QuestAvailable(c, QuestIds.Faces) || rng.Int(4) != 0) return;
            var seen = report.Squad.Select((e) => c.Soldiers.FirstOrDefault((s) => s.Id == e.Id)).Where((s) => s != null && s.Alive && !string.IsNullOrEmpty(s.Nickname)).Select((s) => s!).ToList();
            if (seen.Count == 0) return;
            var soldier = rng.Pick(seen);
            // Never someone with the soldier's own first name (an old save's, or one the players chose).
            var words = soldier.Name.Split(' ');
            var first = words[0];
            var relative = rng.Pick(Quests.Relatives.Where((r) => r.Name != first).ToList());
            var surname = string.Join(" ", words.Skip(1));
            if (surname == "") surname = soldier.Name;
            StartQuest(c, QuestIds.Faces, rng, soldier.Owner, soldier.Id, new Dictionary<string, string>
            {
                ["soldier"] = Appearances.DisplayName(soldier.Name, soldier.Nickname),
                ["relative"] = $"{relative.Name} {surname}",
                ["relation"] = relative.Relation,
            });
        }

        // ----------------------------------------------------------------- rumours

        private static void NewRumor(CampaignState c, Rng rng)
        {
            var all = RegionIds.All.Where((id) => !c.Rumors.Any((r) => r.Region == id)).ToList();
            var contacted = all.Where((id) => c.Regions.FirstOrDefault((r) => r.Id == id)?.Contacted == true).ToList();
            var withQuest = contacted.Where((id) => RumorQuest(c, id) != null).ToList();
            // Rumours lean towards regions with something waiting, and towards the network.
            var pool = withQuest.Count > 0 && rng.Int(2) == 0 ? withQuest : contacted.Count > 0 && rng.Int(10) < 6 ? contacted : all;
            if (pool.Count == 0) return;
            var region = rng.Pick(pool);
            var id = NewId(c, "r");
            var rumor = new Rumor { Id = id, Region = region, Text = rng.Pick(Quests.RumorLines), ExpiresDay = c.Day + RumorLife, DaysLeft = 1 };
            c.Rumors.Add(rumor);
            // «unos días» (text-fixes.ts, Lore 24/10): time is counted in missions.
            Log(c, $"Se habla de {rumor.Text}. Si lo escuchamos un tiempo, sabremos qué hay de cierto.", LogTones.Info, Alert("RUMOR", Regions[region].Name, rumor.Id));
        }

        /// <summary>What a rumour turns out to be once the Bastion has listened to it.</summary>
        private static void ResolveRumor(CampaignState c, Rumor rumor, Rng rng)
        {
            c.Rumors = c.Rumors.Where((r) => r.Id != rumor.Id).ToList();
            c.Listening = null;
            var name = Regions[rumor.Region].Name;
            var quest = RumorQuest(c, rumor.Region);
            if (quest != null && c.Quests.Count < MaxQuests)
            {
                // The quest's own decision or operation tells the rest.
                Log(c, $"El rumor {Lore.OfZone(name)} era cierto: {Quests.QuestDefs[quest].Title}.", LogTones.Good);
                StartQuest(c, quest, rng);
                return;
            }
            var roll = rng.Int(100);
            if (roll < 40)
            {
                var offer = MakeOffer(c, rng, false, rumor.Region);
                offer.Reward.Credits = (offer.Reward.Credits ?? 0) + 25;
                c.Offers.Add(offer);
                Log(c, $"El rumor {Lore.OfZone(name)} era cierto: {offer.Name}, con mejor paga.", LogTones.Good, Alert("RUMOR", name));
            }
            else if (roll < 60)
            {
                var credits = 30 + rng.Int(4) * 10;
                c.Credits += credits;
                Log(c, $"El rumor {Lore.OfZone(name)} era un alijo de otros supervivientes: +{credits} suministros.", LogTones.Good, Alert("RUMOR", name));
            }
            else if (roll < 80)
            {
                Log(c, $"El rumor {Lore.OfZone(name)} era un Saqueador que hablaba demasiado: sabía adónde ir.", LogTones.Good, Alert("RUMOR", name));
                TipOff(c, rng);
            }
            else
            {
                Log(c, $"El rumor {Lore.OfZone(name)} era un bulo.", LogTones.Info, Alert("RUMOR", name));
            }
        }
    }
}
