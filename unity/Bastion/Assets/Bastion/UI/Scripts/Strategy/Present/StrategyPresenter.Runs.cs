using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The runs' base besides Almacén (H2): Mejoras (Runs.Level, NextPrice, UpgradeRefusal) and Salir, the squad that goes
    /// out (c.Squad, Runs.SeatsOf and SquadLimit, StartRunRefusal) on the Escuadra's screen and La Lata's row.
    /// </summary>
    public sealed partial class StrategyPresenter
    {
        public UpgradesView Upgrades()
        {
            var c = State;
            if (c.Stash == null) return null;
            var v = new UpgradesView
            {
                Head = StrategyText.Runs.UpgradesHead,
                Supplies = Gears.Supplies(c.Stash).ToString(),
                SuppliesTip = StrategyText.Stash.SuppliesTip,
            };
            // Its three groups (cuadro 164), as columns.
            foreach (var (gid, title) in StrategyText.Runs.UpgradeGroups) v.Groups.Add(new UpgradeGroupView { Id = gid, Title = title });
            foreach (var id in UpgradeIds.All)
            {
                if (!Content.UpgradePrices.TryGetValue(id, out var prices)) continue;
                var level = Runs.Level(c, id);
                var price = Runs.NextPrice(c, id);
                v.Rows.Add(new UpgradeRowView
                {
                    Id = id,
                    Name = StrategyText.Runs.UpgradeName(id),
                    Line = StrategyText.Runs.UpgradeLine(id, Runs.SeatsOf(c), level + 1 < Runs.Seats.Count ? Runs.Seats[level + 1] : Runs.SeatsOf(c), System.Math.Min(level + 1, prices.Count)),
                    Delta = StrategyText.Runs.UpgradeDelta(id, Runs.SeatsOf(c), level + 1 < Runs.Seats.Count ? Runs.Seats[level + 1] : Runs.SeatsOf(c), System.Math.Min(level + 1, prices.Count)),
                    Group = StrategyText.Runs.UpgradeGroupOf(id),
                    Level = level,
                    Levels = prices.Count,
                    LevelText = StrategyText.Runs.UpgradeLevel(level, prices.Count),
                    Price = price?.ToString(),
                    Button = price != null ? StrategyText.Runs.Upgrade : StrategyText.Runs.UpgradeTop,
                    Why = price == null ? "" : Runs.UpgradeRefusal(c, id),
                    Tip = price != null ? StrategyText.Runs.UpgradeTip(price.Value) : null,
                });
            }
            return v;
        }

        /// <summary>
        /// La Lata's seats in the runs (Runs.SeatsOf): the first commander's half, then the second's, each with their own of
        /// the squad and the rest free (null); alone, or with the partner away, the squad and the free ones (as
        /// Runs.SquadLimit: N4, 17/11, with 2 seats the one here took both and the cards showed one of them). Also with one
        /// of the two without soldiers (N8; R19 of Red, 23/11: two «LIBRE» while the partner's four went out): the engine
        /// halves the seats only when both bring someone.
        /// </summary>
        List<CampaignSoldier> RunPads()
        {
            var c = State;
            var seats = Runs.SeatsOf(c);
            var squad = c.Squad.Select(id => c.Soldiers.FirstOrDefault(s => s.Id == id)).Where(s => s != null && s.Alive).ToList();
            var pads = new List<CampaignSoldier>();
            if (c.Players.Count < 2 || Connected.Count < 2 || Runs.SquadLimit(c, Connected) >= seats)
            {
                pads.AddRange(squad.Take(seats));
                while (pads.Count < seats) pads.Add(null);
                return pads;
            }
            var half = seats / 2;
            foreach (var player in c.Players.Take(2))
            {
                var theirs = squad.Where(s => s.Owner == player).Take(half).ToList();
                pads.AddRange(theirs);
                for (var i = theirs.Count; i < half; i++) pads.Add(null);
            }
            return pads;
        }

        /// <summary>
        /// The first time (U16, Arturo 18/11, cuadro 70: «la primera vez que juegas se te pone en la vista de iniciar
        /// expedición sin posibilidad de volver a otros menús»): a runs' campaign with no stage ever chosen (NodesPlayed:
        /// all the campaign's, never back to 0) and none going on. Salir alone; the whole base once back from the first.
        /// </summary>
        public bool FirstTime => State is CampaignState c && c.Stash != null && c.Run == null && (c.NodesPlayed ?? 0) == 0;

        /// <summary>Salir: one's soldiers to put in or out, the partner's to see, who is ready, «Salir» (StartRunCommand).</summary>
        public SquadView RunSquad()
        {
            var c = State;
            var limit = Runs.SquadLimit(c, Connected);
            var squad = c.Squad.Select(id => c.Soldiers.FirstOrDefault(s => s.Id == id)).Where(s => s != null && s.Alive).ToList();
            var myCount = squad.Count(s => s.Owner == Slot);
            var partner = Partner;
            var chapter = c.Chapter ?? 1;
            var v = new SquadView
            {
                MineHead = $"Tus soldados · {myCount}/{limit}",
                MineTip = partner != null ? StrategyText.Runs.SquadTipTwo(partner.Name) : StrategyText.Runs.SquadTip,
                Title = StrategyText.Runs.Head(chapter),
                // And La Lata as the seats make it (N4, 17/11: the motorbike first, Lore.RunTransport).
                Briefing = StrategyText.Runs.StartTip(chapter) + " " + StrategyText.Runs.Transport(Runs.SeatsOf(c)),
            };
            foreach (var s in c.Soldiers.Where(s => s.Owner == Slot && s.Alive)) v.Mine.Add(RunRow(s));
            var theirs = c.Soldiers.Where(s => s.Owner != Slot && s.Alive).ToList();
            // None of one's own left, the partner's still (N8: nobody loses; Lore's words): whose one leads, under the plate.
            if (v.Mine.Count == 0 && theirs.Count > 0) v.Warning = Lore.LostText.NoneOfYourOwn(PlayerName(theirs[0].Owner), partner != null && partner.Connected);
            if (theirs.Count > 0)
            {
                v.PartnerHead = $"De {PlayerName(theirs[0].Owner)} · {squad.Count - myCount}";
                v.PartnerTip = "Los elige su jugador";
                foreach (var s in theirs) v.Partner.Add(RunRow(s));
            }
            var why = Runs.StartRunRefusal(c, Connected);
            v.Launch = StrategyText.Runs.Start2;
            v.LaunchEnabled = why == null;
            v.LaunchWhy = why;
            // The first time no way back to the base's tabs (U16): Esc's menu only (Ajustes, leaving the game).
            v.Back = FirstTime ? null : StrategyText.Runs.BackToStash;
            var together = c.Players.Count > 1 && partner != null && partner.Connected;
            foreach (var s in RunPads())
                v.Cards.Add(s == null
                    ? new LineupCardView { EmptyLabel = "Libre", Tip = "Elige un soldado de la izquierda." }
                    : new LineupCardView { Id = s.Id, Name = Appearances.DisplayName(s.Name, s.Nickname), Accent = PlayerColor(s.Owner), Lines = { StrategyText.Stash.Health(Runs.HpOf(c, s), Runs.MaxHp(s)) }, Removable = s.Owner == Slot });
            return v;
        }

        /// <summary>A soldier's row in Salir: in it framed; the partner's off. Its sheet in short under the pointer (M4).</summary>
        PickRowView RunRow(CampaignSoldier s) => new PickRowView
        {
            Id = s.Id, Name = Appearances.DisplayName(s.Name, s.Nickname), Accent = PlayerColor(s.Owner), Skills = SkillsOf(s),
            Rank = Bars.VeterancyRank(s.Board.Count), RankTip = RankTip(s), In = State.Squad.Contains(s.Id),
            Enabled = s.Owner == Slot, Tip = State.Squad.Contains(s.Id) ? StrategyText.Squad.InTip : null,
            Peek = RunPeek(State, s),
        };

        /// <summary>
        /// A soldier's sheet in short in Salir (M4, Arturo 23/11: «al pasar el raton por encima de un soldado tiene que
        /// aparecer la información de ese soldado y estadisticas»), the runs' own: the board's totals as Soldados sums them
        /// (TotalsOf: health as the run has it, actions, movement, PUNT. and CaC, dodge and defence always, M6), the kit
        /// worn, the pack, the experience, the points to spend, the abilities and the wounds. No figure of its own.
        /// </summary>
        SoldierPeekView RunPeek(CampaignState c, CampaignSoldier s)
        {
            var totals = TotalsOf(c, s).ToDictionary(t => t.key, t => t.value);
            var kit = s.Kit;
            var v = new SoldierPeekView
            {
                Name = Appearances.DisplayName(s.Name, s.Nickname),
                Kicker = $"{Campaign.VeterancyName(s)} · {(s.Owner == Slot ? (Female(s) ? "tuya" : "tuyo") : $"de {PlayerName(s.Owner)}")}",
            };
            v.Stats.Add(new PeekStatView { Name = "Salud", Value = $"{Runs.HpOf(c, s)}/{Runs.MaxHp(s)}" });
            v.Stats.Add(new PeekStatView { Name = "Acciones", Value = totals["ap"].ToString() });
            v.Stats.Add(new PeekStatView { Name = "Mov.", Value = totals["moves"].ToString() });
            v.Stats.Add(new PeekStatView { Name = "Voluntad", Value = totals["will"].ToString() });
            v.Stats.Add(new PeekStatView { Name = "PUNT.", Value = totals["gunAim"].ToString() });
            v.Stats.Add(new PeekStatView { Name = "CaC", Value = totals["meleeAim"].ToString() });
            v.Stats.Add(new PeekStatView { Name = "Esquiva", Value = totals["dodge"].ToString() });
            v.Stats.Add(new PeekStatView { Name = "Defensa", Value = totals["defense"].ToString() });
            string Named(Engine.Gear g) => g == null ? null : Lore.GearName(g.Kind, g.Type, g.Count);
            if (kit != null)
            {
                // Hands: the primary, the blade (the knife with none) and the pistol.
                v.Weapon = string.Join(" · ", new[] { Named(kit.Primary), Named(kit.Melee) ?? Lore.GearName(GearKinds.Weapon, FoundWeaponIds.Knife, 1), Named(kit.Pistol) }.Where(n => n != null));
                v.Vest = Named(kit.Vest);
                var belt = kit.Belt.Where(g => g != null).Select(Named).ToList();
                v.Belt = belt.Count > 0 ? string.Join(" · ", belt) : null;
                var things = kit.Pack.Items.Count;
                var full = Gears.FirstFit(kit.Pack, new Engine.Gear { Kind = GearKinds.Mags, Count = 1 }) == null;
                v.Pack = things == 0 ? "Vacía" : $"{things} {(things == 1 ? "cosa" : "cosas")}{(full ? " · llena" : " · le queda sitio")}";
            }
            else v.Weapon = Campaign.WeaponLabel(c, s);
            var lines = SkillIds.All.Select(id => (name: Bars.SkillDefs[id].Name, bars: Bars.BarsOf(s.Skills.TryGetValue(id, out var xp) ? xp : 0).Filled))
                .Where(l => l.bars > 0).Select(l => $"{l.name} {l.bars}").ToList();
            v.Experience = lines.Count > 0 ? string.Join(" · ", lines) : null;
            var points = Bars.PointsLeft(s.Skills, s.Board);
            v.Points = points > 0 ? $"{points} {(points == 1 ? "punto" : "puntos")} por gastar" : null;
            foreach (var id in s.Board)
                if (Board.BoardCells.TryGetValue(id, out var cell) && (cell.Kind == CellKinds.Passive || cell.Kind == CellKinds.Active)) v.Abilities.Add(cell.Name);
            var (health, tone, _) = Health(c, s, false);
            if (tone == "good") (health, tone) = ("Sin heridas", null);
            v.Health = health;
            v.HealthTone = tone;
            return v;
        }
    }
}
