using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    public sealed partial class StrategyPresenter
    {
        // --------------------------------------------------------------- Escuadra

        /// <summary>
        /// Escuadra (renderSquad): one's own soldiers to pick on the left, the partner's to see on the right, who is ready,
        /// the mission and LANZAR under the row, a card under each figure of it. The siege's posts are the clearing's (Arte).
        /// </summary>
        public SquadView Squad()
        {
            var c = State;
            var offer = c.ActiveMission != null ? c.Offers.FirstOrDefault(o => o.Id == c.ActiveMission) : null;
            if (offer == null) return new SquadView { Empty = "No hay ninguna misión elegida." };
            // The siege (docs/FASE12.md §11.5): every soldier fit to fight defends; nobody picks, nobody leaves.
            var siege = offer.Siege == true;
            var squad = c.Squad.Select(id => c.Soldiers.FirstOrDefault(s => s.Id == id)).Where(s => s != null).ToList();
            var limit = Campaign.SquadLimit(c, Connected);
            var myCount = squad.Count(s => s.Owner == Slot);
            var partner = Partner;
            var v = new SquadView
            {
                MineHead = siege ? $"Tus soldados · {myCount}" : $"Tus soldados · {myCount}/{limit}",
                // Lore, C3: the partner by name, not «él».
                MineTip = siege ? Lore.BaseSiegeText.Defenders : $"Pulsa un soldado para meterlo o sacarlo. Los de {partner?.Name ?? "el otro jugador"} los elige {partner?.Name ?? "él"}; los huecos que no llene se quedan vacíos. El equipo se cambia en Soldados.",
                Title = offer.Name,
                Night = offer.Night == true ? Lore.NightText.Label : null,
                Briefing = Campaign.OfferBriefing(c, offer),
                Warning = offer.Living == true ? Lore.LivingTip : null,
            };
            foreach (var s in c.Soldiers.Where(s => s.Owner == Slot && s.Alive)) v.Mine.Add(PickRow(c, s, siege));
            // The other player's, to see but not to pick: each puts in and takes out only their own (playtest 18/10 #43).
            var lent = c.Soldiers.Where(s => s.Owner != Slot && s.Alive).ToList();
            if (lent.Count > 0)
            {
                v.PartnerHead = $"De {PlayerName(lent[0].Owner)} · {squad.Count - myCount}";
                v.PartnerTip = "Los elige su jugador";
                foreach (var s in lent) v.Partner.Add(PickRow(c, s, siege));
            }
            var ready = c.LaunchReady[Slot];
            var together = c.Players.Count > 1 && partner != null && partner.Connected;
            if (together)
                foreach (var slot in new[] { Slot, partner.Slot })
                    v.Ready.Add(new ReadyView
                    {
                        Name = PlayerName(slot), Color = PlayerColor(slot), Ready = c.LaunchReady[slot],
                        Tip = c.LaunchReady[slot] ? (siege ? "A punto para defender" : "A punto para salir") : "Aún elige la escuadra",
                    });
            v.Launch = ready ? "A punto" : siege ? "Defender el Bastión" : "Lanzar misión";
            v.LaunchReady = ready;
            v.LaunchWaiting = together && !ready && c.LaunchReady[partner.Slot];
            v.LaunchEnabled = c.Squad.Count > 0;
            // The first mission is the only way in: no going back to the base before it (Ana, 22/10).
            v.Back = c.Story.Done == 0 && offer.Story == 1 ? null : "Volver al Bastión";
            // One card under each pad of the row; a free one this player could take but has nobody for: recruit into it (#39).
            var pads = siege ? squad : Campaign.SquadPads(c).Select(id => squad.FirstOrDefault(s => s.Id == id)).ToList();
            var roster = c.Soldiers.Where(s => s.Owner == Slot && s.Alive).ToList();
            var spare = roster.Any(s => Campaign.SoldierReady(s) && !c.Squad.Contains(s.Id));
            var room = !siege && !spare && roster.Count < Campaign.SoldierCap(c) && Campaign.SquadPlace(c, Slot, Connected).Error == null;
            var recruitable = room && Campaign.BedsFree(c) > 0;
            var cost = Campaign.RecruitCost(c);
            for (var i = 0; i < pads.Count; i++)
            {
                var s = pads[i];
                if (s != null)
                {
                    v.Cards.Add(new LineupCardView
                    {
                        Id = s.Id, Name = Appearances.DisplayName(s.Name, s.Nickname), Accent = PlayerColor(s.Owner),
                        Lines = { Campaign.VeterancyName(s) }, Removable = s.Owner == Slot && !siege, Ready = together && c.LaunchReady[s.Owner],
                    });
                    continue;
                }
                if (recruitable)
                {
                    var pending = PendingWhy(c);
                    var afford = Campaign.CanAfford(c, cost);
                    v.Cards.Add(new LineupCardView { EmptyLabel = "Libre", Recruit = $"Reclutar · {cost} §", RecruitEnabled = afford && pending == "", Tip = pending != "" ? pending : afford ? "Un recluta nuevo, directo a la escuadra." : Shortfall(c, cost) });
                    continue;
                }
                v.Cards.Add(new LineupCardView
                {
                    EmptyLabel = "Libre",
                    EmptyHint = room && PadSide(c, i) == Slot ? "Para reclutar más soldados, construid un Barracón" : null,
                    Tip = spare ? "Elige un soldado a la izquierda" : null,
                });
            }
            return v;
        }

        /// <summary>A soldier's row in a squad list (pickRow): in it ✓, out with what keeps it in, the partner's off.</summary>
        PickRowView PickRow(CampaignState c, CampaignSoldier s, bool siege)
        {
            var on = c.Squad.Contains(s.Id);
            var ready = Campaign.SoldierReady(s);
            var row = new PickRowView
            {
                Id = s.Id, Name = Appearances.DisplayName(s.Name, s.Nickname), Accent = PlayerColor(s.Owner), Skills = SkillsOf(s),
                Rank = Bars.VeterancyRank(s.Board.Count), RankTip = RankTip(s), In = on,
                Enabled = !(siege || s.Owner != Slot || (!ready && !on)),
                Tip = string.Join(" · ", new[] { s.Loadout.Utility, s.Loadout.Ammo }.Where(g => g != null).Select(g => Content.Items[g].Name)),
            };
            if (on) (row.Chip, row.ChipTone, row.ChipTip) = ("✓", "good", StrategyText.Squad.InTip);
            else if (ready) { }
            // Lore, C2: «Mordisco», «Heridas».
            else if (s.Infection != null) (row.Chip, row.ChipTone, row.ChipTip) = ("Fiebre", "bad", "Mordisco: necesita el tratamiento antes de salir.");
            else (row.Chip, row.ChipTone, row.ChipTip) = (MissionsText(MissionsFor(s.WoundedDays)), "warn", "Heridas");
            if (row.Tip == "") row.Tip = null;
            return row;
        }

        /// <summary>Whose half of the hangar a pad is in (padSide).</summary>
        static int PadSide(CampaignState c, int pad) => c.Players.Count < 2 || pad < (int)System.Math.Ceiling(Campaign.SquadMax(c) / 2.0) ? c.Players[0] : c.Players[1];

        // ----------------------------------------------------------------- Parte

        /// <summary>
        /// The debrief (renderDebrief): the result, the mission, what came back in tiles and what changed as chips, the few
        /// lines pictures can't say, the story's page and CONTINUAR; a card under each figure with how they came back and
        /// what they learnt. Null without a report.
        /// </summary>
        public DebriefView Debrief()
        {
            var c = State;
            var report = c.LastReport;
            if (report == null) return null;
            var v = new DebriefView { Victory = report.Victory, Result = report.Victory ? "Misión cumplida" : "Misión fracasada", Mission = report.MissionName, Continue = "Continuar" };
            v.Haul.AddRange(Haul(report));
            if (report.LevelTo > report.LevelFrom)
                v.Events.Add(new ChipView { Text = $"¡{Lore.BastionLevelName(report.LevelTo)}!", Tone = "good", Tip = string.Join(" ", Enumerable.Range(report.LevelFrom + 1, report.LevelTo - report.LevelFrom).Select(UnlocksText)) });
            // Lore, C9: the doom by its name at the level, never «Reloj».
            if (report.DoomChange != 0)
            {
                var line = Lore.DoomChangeText(report.LevelTo, report.DoomChange);
                var known = report.LevelTo >= Lore.PartituraLevel;
                v.Events.Add(new ChipView
                {
                    Text = known ? line.TrimEnd('.') : $"Infestación {(report.DoomChange > 0 ? "+" : "−")}{System.Math.Abs(report.DoomChange)}",
                    Tone = report.DoomChange > 0 ? "bad" : "good", Tip = line,
                });
            }
            if (report.Fallen == true) v.Lines.Add(new DebriefLineView { Text = $"<b>{Lore.BaseSiegeText.Fallen}.</b> {Lore.BaseSiegeText.FallenLine}", Bad = true });
            var damage = DamageLine(report.Damage);
            if (damage != null) v.Lines.Add(damage);
            if (report.Night == true) v.Lines.Add(new DebriefLineView { Text = Lore.NightText.Report });
            if (report.Victory && report.Story is int chapter && chapter >= 1 && chapter <= Lore.Story.Count) v.Story = Lore.Story[chapter - 1].After;
            foreach (var e in report.Squad) v.Cards.Add(ReportCard(c, report, e));
            return v;
        }

        /// <summary>«Desbloquea: Sala de mapas · Mina.» (unlocksText).</summary>
        static string UnlocksText(int level)
        {
            var list = Progression.LevelUnlocks(level);
            return list.Count > 0 ? $"Desbloquea: {string.Join(" · ", list)}." : "Sin desbloqueos nuevos.";
        }

        /// <summary>The base after a siege (damageLine): what broke, what was touched, what was spent; «Construir» to it.</summary>
        static DebriefLineView DamageLine(SiegeDamage d)
        {
            if (d == null) return null;
            var parts = new List<string>();
            if (d.Broken != 0) parts.Add($"{d.Broken} {(d.Broken == 1 ? "rota" : "rotas")}");
            if (d.Hurt != 0) parts.Add($"{d.Hurt} {(d.Hurt == 1 ? "tocada" : "tocadas")}");
            if (d.Spent != 0) parts.Add($"{d.Spent} {Lore.PieceState.Spent}");
            if (parts.Count == 0) return new DebriefLineView { Text = "En la base: todo en pie." };
            return new DebriefLineView { Text = $"En la base: {string.Join(", ", parts)}.", Button = Lore.BuildText.Build };
        }

        /// <summary>
        /// What came back, as tiles of one size (haulTiles; Ana, 22/10 #40): the mission's own thing first and bigger, the
        /// supplies (reward, bodies and haul in one), rations, medicines, parts, each weapon, each accessory, a recruit, La
        /// Lata's new vehicle.
        /// </summary>
        static List<HaulView> Haul(MissionReport report)
        {
            var out_ = new List<HaulView>();
            var reward = report.Reward ?? new MissionReward();
            var loot = report.Loot;
            var goal = report.Victory && report.Story is int chapter && chapter >= 1 && chapter <= Lore.Story.Count ? Lore.Story[chapter - 1].Item : null;
            if (goal != null) out_.Add(new HaulView { Icon = goal == "la insulina" ? "store:medicines" : "map:zone", Figure = Lore.ItemTag(goal), Big = true, Tip = Capital(goal) });
            var credits = (reward.Credits ?? 0) + report.Salvage + (loot?.Supplies ?? 0);
            var why = string.Join(" · ", new[]
            {
                reward.Credits is int r && r != 0 ? $"Recompensa: {r}" : "",
                report.Bodies != 0 ? $"{report.Bodies} cuerpos registrados: {report.Salvage}" : "",
                loot != null && loot.Supplies != 0 ? $"Botín: {loot.Supplies}" : "",
            }.Where(x => x != ""));
            if (credits != 0) out_.Add(new HaulView { Icon = "store:credits", Figure = $"+{credits}", Tip = $"Suministros · {why}" });
            var food = (reward.Food ?? 0) + (loot?.Food ?? 0);
            if (food != 0) out_.Add(new HaulView { Icon = "store:food", Figure = $"+{food}", Tip = "Raciones" });
            var medicines = (reward.Medicines ?? 0) + (loot?.Medicines ?? 0);
            if (medicines != 0) out_.Add(new HaulView { Icon = "store:medicines", Figure = $"+{medicines}", Tip = "Medicinas" });
            var parts = (reward.Parts ?? 0) + (loot?.Parts ?? 0) + (report.Scrap ?? 0);
            if (parts != 0) out_.Add(new HaulView { Icon = "store:parts", Figure = $"+{parts}", Tip = "Piezas" });
            foreach (var w in loot?.Weapons ?? new List<string>())
            {
                var def = Content.FoundWeapons[w];
                out_.Add(new HaulView { Icon = WeaponIcon(def.Weapon), Figure = "", Tip = def.Name, Better = def.Better == true });
            }
            foreach (var g in (loot?.Items ?? new List<string>()).GroupBy(x => x))
                out_.Add(new HaulView { Icon = ItemIcon(g.Key), Figure = g.Count() > 1 ? $"×{g.Count()}" : "", Tip = Content.Items[g.Key].Name });
            if (reward.Recruit != null && !(reward.Recruit is bool b && !b)) out_.Add(new HaulView { Icon = "tab:barracks", Figure = "+1", Tip = "Un recluta" });
            if (reward.Vehicle is int vehicle && vehicle > 0)
                out_.Add(new HaulView { Icon = "map:lata", Figure = "", Tip = Capital(vehicle < Lore.Vehicles.Count ? Lore.Vehicles[vehicle].The : Lore.Bus.Name) });
            return out_;
        }

        /// <summary>A weapon's silhouette among the HUD's icons (the bar's: rifle, shotgun…; a blade's).</summary>
        static string WeaponIcon(string weapon)
        {
            switch (weapon)
            {
                case WeaponIds.Rifle: case WeaponIds.Pistol: case WeaponIds.Shotgun: case WeaponIds.Sniper: case WeaponIds.Cannon: return "weapon:" + weapon;
                default: return "overwatchMelee";
            }
        }

        /// <summary>An accessory's icon among the HUD's (its ability's).</summary>
        static string ItemIcon(string item)
        {
            switch (item)
            {
                case ItemIds.FragGrenade: return "grenade";
                case ItemIds.SmokeGrenade: return "smoke";
                case ItemIds.Flashbang: return "flashbang";
                case ItemIds.FirstAid: return "firstAid";
                default: return "search";
            }
        }

        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>
        /// A soldier's card in the debrief: how they came back (in the feminine for a woman, and «SE QUEDÓ FUERA» for one
        /// left behind: Lore, C2), their kills, what they learnt (learnedTags); only the living bring it home.
        /// </summary>
        LineupCardView ReportCard(CampaignState c, MissionReport report, ReportSoldier e)
        {
            var s = c.Soldiers.FirstOrDefault(x => x.Id == e.Id);
            var female = s != null && Female(s);
            var bitten = report.Infected != null && report.Infected.Contains(e.Id) ? $" · {Lore.BiteTag}" : "";
            var status = e.Status == "dead" ? (female ? "CAÍDA EN COMBATE" : "CAÍDO EN COMBATE")
                : e.Status == "lost" ? "SE QUEDÓ FUERA"
                : e.Status == "wounded" ? $"HERIDAS · {MissionsText(MissionsFor(e.WoundedDays)).ToUpperInvariant()}"
                : female ? "ILESA" : "ILESO";
            var v = new LineupCardView
            {
                Id = e.Id, Name = Appearances.DisplayName(e.Name, s?.Nickname), Accent = PlayerColor(e.Owner), Fallen = e.Status == "dead",
                Lines = { status + bitten, $"{e.Kills} baja{(e.Kills == 1 ? "" : "s")}" },
            };
            if (e.Status == "dead" || e.Status == "lost") return v;
            for (var i = 0; i < SkillIds.All.Count; i++)
            {
                var id = SkillIds.All[i];
                var gained = e.Learned != null && e.Learned.TryGetValue(id, out var g) ? g : 0;
                if (gained > 0) v.Learned.Add(new LearnedView { Kind = "skill", Line = i, Text = $"+{gained} {Bars.SkillDefs[id].Name}", Tip = Lore.LearnedLine(id, gained, (IReadOnlyDictionary<string, int>)e.Deeds ?? new Dictionary<string, int>()) });
            }
            foreach (var bar in e.Bars ?? new List<BarFilled>())
                v.Learned.Add(new LearnedView { Kind = "bar", Line = SkillIds.All.ToList().IndexOf(bar.Skill), Text = $"▲ {Bars.SkillDefs[bar.Skill].Name.ToUpperInvariant()} +{bar.Growth.Sum()} %", Tip = Lore.BarLine(e.Name, bar.Skill, bar.Growth) });
            var points = (e.Bars ?? new List<BarFilled>()).Sum(b => b.Growth.Count);
            if (points > 0) v.Learned.Add(new LearnedView { Kind = "point", Count = points });
            return v;
        }
    }
}
