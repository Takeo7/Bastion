#nullable enable
// campaign.ts: the events (EVENTS), the side quests' decisions shown as one (eventDef), and whether an answer can be
// taken. Texts fixed as text-fixes.ts has them (Lore, 24/10): «la Armería», the Cuervos' ambush, the effects' «;».
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ------------------------------------------------------------------- events

        private static void Credit(CampaignState c, int amount)
        {
            c.Credits += amount;
        }

        private static EventOption Option(string label, string effect, Action<CampaignState, Rng> apply, Func<CampaignState, bool>? available = null)
        {
            return new EventOption { Label = label, Effect = effect, Apply = apply, Available = available };
        }

        /// <summary>EVENTS (the side quests' decisions are eventDef's): its keys in CampaignEventIds.All's order, `quest` apart.</summary>
        /// <remarks>Not the class Events (events.ts): inside Campaign, `Events` is this table.</remarks>
        public static readonly IReadOnlyDictionary<string, EventDef> Events = new Dictionary<string, EventDef>
        {
            [CampaignEventIds.Smugglers] = new EventDef
            {
                Id = CampaignEventIds.Smugglers,
                Title = "Contrabandistas",
                Text = "Unos contrabandistas han vaciado un cuartel abandonado y ofrecen lo que sacaron.",
                Options = new[]
                {
                    Option("Comprárselo", "−40 suministros; dos granadas extra para la Armería", (c, rng) =>
                    {
                        Credit(c, -40);
                        c.Inventory[ItemIds.FragGrenade] = (c.Inventory.TryGetValue(ItemIds.FragGrenade, out var n) ? n : 0) + 2;
                    }, (c) => c.Credits >= 40),
                    Option("Rechazar la oferta", "Sin efecto", (c, rng) => { }),
                },
            },
            [CampaignEventIds.Refugees] = new EventDef
            {
                Id = CampaignEventIds.Refugees,
                Title = "Refugiados",
                Text = "Familias huidas de un enclave caído piden asilo en el Bastión. Entre ellas hay antiguos soldados.",
                Options = new[]
                {
                    // Either way they tell what they saw (Ana, 20/10: taken in, they would tell it too); taking them in costs, and gives a soldier.
                    Option("Acogerlas", "−30 suministros; se une un recluta, y cuentan lo que vieron: una misión nueva", (c, rng) =>
                    {
                        Credit(c, -30);
                        var s = AddSoldier(c, NeediestPlayer(c), rng);
                        Log(c, $"{s.Name} se une al Bastión ({VeterancyName(s)}).", LogTones.Good);
                        TipOff(c, rng);
                    }, (c) => c.Credits >= 30),
                    Option("Que sigan su camino", "Antes de irse, cuentan lo que vieron: una misión nueva", (c, rng) => TipOff(c, rng)),
                },
            },
            [CampaignEventIds.Signal] = new EventDef
            {
                Id = CampaignEventIds.Signal,
                Title = "Transmisión interceptada",
                Radio = true,
                Text = "Morse ha captado en onda corta una transmisión cifrada de los Cuervos.",
                Options = new[]
                {
                    Option("Descifrarla", "Una misión nueva", (c, rng) => TipOff(c, rng)),
                    Option("Venderla en el mercado negro", "+30 suministros", (c, rng) => Credit(c, 30)),
                },
            },
            [CampaignEventIds.Convoy] = new EventDef
            {
                Id = CampaignEventIds.Convoy,
                Title = "Convoy emboscado",
                Text = "Un convoy de los Camioneros ha caído en una emboscada de los Cuervos cerca del Bastión. Pagan si alguien los saca de ahí.",
                Options = new[]
                {
                    Option("Enviar a alguien a socorrerlo", "Un soldado al azar, de baja una misión; +60 suministros", (c, rng) =>
                    {
                        var s = rng.Pick(c.Soldiers.Where(SoldierReady).ToList());
                        s.WoundedDays = 4;
                        Credit(c, 60);
                        Log(c, $"{s.Name} vuelve con heridas del rescate del convoy.", LogTones.Bad);
                    }, (c) => c.Soldiers.Any(SoldierReady)),
                    Option("No arriesgar a nadie", "Los muertos avanzan (+1)", (c, rng) => c.Doom += 1),
                },
            },
            [CampaignEventIds.Defector] = new EventDef
            {
                Id = CampaignEventIds.Defector,
                Title = "Un desertor",
                Text = "Un Saqueador de los Cuervos quiere desertar y ofrece lo que sabe.",
                Options = new[]
                {
                    Option("Interrogarlo", "Una misión nueva", (c, rng) => TipOff(c, rng)),
                    Option("Mandarlo a llevarse una horda lejos", "Los muertos retroceden (−1)", (c, rng) => c.Doom = Math.Max(0, c.Doom - 1)),
                },
            },
            [CampaignEventIds.BlackMarket] = new EventDef
            {
                Id = CampaignEventIds.BlackMarket,
                Title = "Mercado negro",
                Text = "Los Camioneros han montado mercado en la carretera, con género interesante.",
                Options = new[]
                {
                    Option("Comprar mapas de la zona", "−40 suministros; dos misiones nuevas", (c, rng) =>
                    {
                        Credit(c, -40);
                        TipOff(c, rng);
                        TipOff(c, rng);
                    }, (c) => c.Credits >= 40),
                    Option("Venderles la munición que sobra", "+35 suministros", (c, rng) => Credit(c, 35)),
                },
            },
            [CampaignEventIds.Sabotage] = new EventDef
            {
                Id = CampaignEventIds.Sabotage,
                Title = "Sabotaje en la base",
                Text = "Alguien ha cortado los cables del generador. Desde dentro.",
                Options = new[]
                {
                    Option("Repararlo a toda prisa", "−35 suministros", (c, rng) => Credit(c, -35), (c) => c.Credits >= 35),
                    // The new base builds at once: no works to put back (18/10). A building is left without power instead,
                    // at half its strength: it doesn't work until it is mended.
                    Option("Repararlo con calma", "Un edificio se queda sin luz hasta repararlo", (c, rng) =>
                    {
                        var buildings = c.Bastion.Pieces.Where(BuildingWorks).ToList();
                        var others = buildings.Where((p) => p.Piece != PieceIds.Caravan).ToList();
                        var pool = others.Count > 0 ? others : buildings;
                        // The roll is made even with nothing to hit, as rng.int(0) is.
                        var k = rng.Int(pool.Count);
                        if (k >= pool.Count) return;
                        var hit = pool[k];
                        hit.Hp = Base.Pieces[hit.Piece].Hp / 2;
                        Log(c, $"{Lore.PieceTexts.All[hit.Piece].Name}: sin luz hasta que se repare.", LogTones.Bad);
                    }),
                },
            },
            [CampaignEventIds.Scientist] = new EventDef
            {
                Id = CampaignEventIds.Scientist,
                Title = "Una científica fugada",
                // Kept for campaigns saved with it pending; the story now tells it (chapter 2).
                Story = true,
                Text = "Una científica huida de un laboratorio de la Congregación pide protección. Dice que la obligaban a probar curas en gente.",
                Options = new[]
                {
                    Option("Darle asilo", "−20 suministros; una misión nueva", (c, rng) =>
                    {
                        Credit(c, -20);
                        TipOff(c, rng);
                    }, (c) => c.Credits >= 20),
                    Option("Llevarla con otro enclave", "+15 suministros", (c, rng) => Credit(c, 15)),
                },
            },
            [CampaignEventIds.Simon] = new EventDef
            {
                Id = CampaignEventIds.Simon,
                Title = "Simão Ferreira",
                Text = "Un desertor de los Cuervos llama a la puerta del Bastión. Se llama Simão Ferreira. Dice que sabe dónde tienen los Cuervos a la niña inmune, y cómo entrar sin que se enteren.",
                Story = true,
                Options = new[]
                {
                    Option("Confiar en él", "Simão os guiará por dentro: «Los inmunes» a la dificultad prevista", (c, rng) => c.Story.Simon = Lore.SimonChoices.Trusted),
                    Option("Interrogarlo y echarlo", "Una misión nueva; «Los inmunes» sin ayuda, más difícil", (c, rng) =>
                    {
                        c.Story.Simon = Lore.SimonChoices.Refused;
                        TipOff(c, rng);
                    }),
                },
            },
            [CampaignEventIds.Finale] = new EventDef
            {
                Id = CampaignEventIds.Finale,
                Title = "La vacuna",
                Text = "La Marea se aleja detrás de la sirena de La Lata y el campamento de la Congregación ha caído. Nwosu tiene en la mano las primeras dosis de la vacuna, y no hay para todos. Hay que decidir ahora.",
                Story = true,
                Options = new[]
                {
                    Option("Para todos", "Morse lee la fórmula por todas las frecuencias. La oyen los enclaves, y también los Cuervos.", (c, rng) =>
                    {
                        c.Ending = Endings.Break;
                        c.Outcome = Outcomes.Victory;
                    }),
                    Option("Para quienes cumplieron", "La Lata reparte las dosis solo a quienes cumplieron sus tratos. La fórmula se queda en el Bastión, bajo llave.", (c, rng) =>
                    {
                        c.Ending = Endings.Record;
                        c.Outcome = Outcomes.Victory;
                    }),
                },
            },
        };

        /// <summary>The pending event, side-quest decisions included.</summary>
        public static EventDef? EventDef(CampaignState c)
        {
            if (c.Event == null) return null;
            if (c.Event != CampaignEventIds.Quest) return Events.TryGetValue(c.Event, out var def) ? def : null;
            var q = c.Quests.FirstOrDefault((x) => x.Id == c.QuestEvent);
            if (q == null || !(Js.At<QuestStep?>(Quests.QuestDefs[q.Id].Steps, q.Step, null) is QuestDecision step)) return null;
            EventOption OptionOf(int i) => new EventOption
            {
                Label = Quests.Fill(step.Options[i].Label, q.Names),
                Effect = Quests.Fill(step.Options[i].Effect, q.Names),
                Apply = (cc, rng) => AnswerQuest(cc, i, rng),
            };
            return new EventDef { Id = CampaignEventIds.Quest, Title = Quests.Fill(step.Title, q.Names), Text = Quests.Fill(step.Text, q.Names), Story = true, Options = new[] { OptionOf(0), OptionOf(1) } };
        }

        public static bool EventOptionAvailable(CampaignState c, int option)
        {
            var def = EventDef(c);
            var o = def != null && option >= 0 && option < def.Options.Count ? def.Options[option] : null;
            return o != null && (o.Available == null || o.Available(c));
        }
    }
}
