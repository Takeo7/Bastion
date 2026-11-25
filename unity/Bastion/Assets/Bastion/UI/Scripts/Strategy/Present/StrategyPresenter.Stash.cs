using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Gear;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Almacén (the runs' base, H2): the stash, the soldiers to choose and the chosen one's kit, from the campaign
    /// (CampaignState.Stash, CampaignSoldier.Kit). What may go where is the engine's to say (Gears.*Refusal, the same the
    /// server asks), so that the grids show what the order would meet; the orders themselves are built here and sent by
    /// the base (GearCommand).
    /// </summary>
    public sealed partial class StrategyPresenter
    {
        /// <summary>Almacén with `chosen` (a soldier's id; null or gone, the player's first); null outside a runs' campaign.</summary>
        public StashScreenView StashScreen(string chosen)
        {
            var c = State;
            if (c.Stash == null) return null;
            var alive = c.Soldiers.Where(s => s.Alive && s.Kit != null).ToList();
            var mine = alive.Where(s => Runs.Controls(c, Slot, Connected, s)).ToList();
            var pick = alive.FirstOrDefault(s => s.Id == chosen) ?? mine.FirstOrDefault() ?? alive.FirstOrDefault();
            var v = new StashScreenView
            {
                Head = StrategyText.Stash.Head,
                Supplies = Gears.Supplies(c.Stash).ToString(),
                SuppliesTip = StrategyText.Stash.SuppliesTip,
                Hint = GearText.Hint,
                Stash = Grid(c.Stash, GearPlaces.Stash, null, Gears.Reach(c, Slot, Connected, null) == null, 8, Against(c, pick)),
            };
            foreach (var s in mine.Concat(alive.Except(mine)))
            {
                var own = mine.Contains(s);
                v.Soldiers.Add(new KitTabView
                {
                    Id = s.Id, Name = FirstName(s), On = s == pick, Mine = own, Theirs = s.Owner != Slot,
                    Color = own ? null : PlayerColor(s.Owner), Tip = own ? null : StrategyText.Stash.Whose(PlayerName(s.Owner)),
                });
            }
            if (pick != null) v.Kit = Kit(pick, mine.Contains(pick));
            return v;
        }

        static string FirstName(CampaignSoldier s) => (s.Name ?? "").Split(' ')[0];

        /// <summary>
        /// A soldier a weapon is shown for (T4, O7): its kit to weigh against, whose it is, and what it would do with a
        /// weapon in the base (as it would go out with it: HandsOf); none without a kit.
        /// </summary>
        static Wielder Against(CampaignState c, CampaignSoldier s) => s?.Kit is Kit k ? new Wielder { At = slot => Gears.InSlot(k, slot), Who = FirstName(s), Hand = HandsOf(c, s) } : null;

        /// <summary>What `s` would do with a weapon in the base, as if it set out with it (O7: Runs.CardFor).</summary>
        static System.Func<Engine.Gear, HeldFigures?> HandsOf(CampaignState c, CampaignSoldier s) => c == null || s == null ? (System.Func<Engine.Gear, HeldFigures?>)null : g => Held(Runs.CardFor(c, s, g));

        /// <summary>
        /// The engine's card of a weapon in a soldier's hands (O7) as the tips show it: the damage it does; the aim and the
        /// crit the weapon's own with the soldier's named bonuses on them (a weapon's figures, as the tip always had them, not
        /// the shot's whole chance); each bonus by its name, the one without a name «otros». Null: not a weapon.
        /// </summary>
        public static HeldFigures? Held(SoldierWeaponCard card) => card == null ? (HeldFigures?)null : new HeldFigures
        {
            Min = card.Damage.Min, Max = card.Damage.Max, CritMin = card.CritDamage.Min, CritMax = card.CritDamage.Max,
            WeaponCritMin = card.Weapon.CritDamage.Min, WeaponCritMax = card.Weapon.CritDamage.Max,
            // The whole of it with this weapon (cuadro 156), the weapon's own and the soldier's base apart.
            Aim = card.Aim, Crit = card.Crit, WeaponAim = card.Weapon.Aim, WeaponCrit = card.Weapon.Crit,
            BaseAim = card.Aim - card.Weapon.Aim - card.Bonuses.Sum(b => b.Aim), BaseCrit = card.Crit - card.Weapon.Crit - card.Bonuses.Sum(b => b.Crit),
            Bonuses = card.Bonuses.Select(b => new HeldBonus { Label = string.IsNullOrEmpty(b.Label) ? StrategyText.Stash.Others : b.Label, Damage = b.Damage, Aim = b.Aim, Crit = b.Crit, CritDamage = b.CritDamage }).ToList(),
        };

        /// <summary>A soldier's six places and pack; health as it stands in a run (Run.Hp), whole in the base.</summary>
        KitView Kit(CampaignSoldier s, bool mine)
        {
            var k = s.Kit;
            var max = Runs.MaxHp(s);
            var hp = State.Run != null && State.Run.Hp.TryGetValue(s.Id, out var left) ? left : max;
            var v = new KitView
            {
                Soldier = s.Id, Name = s.Name, Line = StrategyText.Stash.Health(hp, max), Mine = mine,
                Pack = Grid(k.Pack, s.Id, KitSlots.Pack, mine, 0, Against(State, s)),
                Note = mine ? null : StrategyText.Stash.Theirs(PlayerName(s.Owner)),
            };
            foreach (var slot in KitSlots.All)
            {
                if (slot == KitSlots.Pack) continue;
                var (w, h) = SlotBox(slot);
                var g = Gears.InSlot(k, slot);
                v.Slots.Add(new GearSlotView { Owner = s.Id, Slot = slot, Label = GearText.SlotLabel(slot), W = w, H = h, Gear = g != null ? Item(g, Against(State, s)) : null, Mine = mine });
            }
            return v;
        }

        /// <summary>A kit place's box in cells: the biggest that goes in it, lying across (the primary takes the machine gun's 4×2).</summary>
        public static (int, int) SlotBox(string slot)
        {
            switch (slot)
            {
                case KitSlots.Primary: return (4, 2);
                case KitSlots.Melee: return (3, 1);
                case KitSlots.Pistol: return (2, 1);
                case KitSlots.Vest: return (2, 2);
                default: return (1, 1);
            }
        }

        public static GearGridView Grid(GearGrid g, string owner, string slot, bool mine, int rows = 0, Wielder against = null) => new GearGridView
        {
            Owner = owner, Slot = slot, W = g.W, H = g.H, Mine = mine, Rows = rows,
            Items = g.Items.Select(p => new PlacedView { Gear = Item(p.Gear, against), X = p.X, Y = p.Y, Turned = p.Turned }).ToList(),
        };

        /// <summary>
        /// A gear as the grids draw it: size lying across, its pile, its name and what it is on hover; a weapon's figures
        /// as `against`'s soldier would have them (O7) and against what it carries in its place (T4), if it is given.
        /// </summary>
        public static GearItemView Item(Engine.Gear g, Wielder against = null)
        {
            var (w, h) = Content.GearSize(g.Kind, g.Type);
            return new GearItemView
            {
                Id = g.Id, Kind = g.Kind, Type = g.Type, Name = GearName(g), The = Lore.GearThe(g.Kind, g.Type, g.Count), Icon = GearIcon(g),
                W = w, H = h, Count = g.Count, Pile = Content.PileOf(g.Kind, g.Type), Tip = GearTip(g, against), Brief = BriefOf(g, against),
                Mods = g.Mods != null && g.Mods.Count > 0 ? g.Mods.ToList() : null,
                Issued = g.Issued == true,
            };
        }

        /// <summary>Its name, one of it (Lore.GearName: «Granada», «Medicina», «Fusil de cerrojo»).</summary>
        public static string GearName(Engine.Gear g) => Lore.GearName(g.Kind, g.Type);

        static string ModName(string mod) => mod != null && Content.WeaponMods.TryGetValue(mod, out var m) ? m.Name : "Accesorio";

        /// <summary>The HUD's icon while Arte's picture is missing; the supplies, our crate «§».</summary>
        /// <summary>A gear's HUD icon, for the debrief's tiles.</summary>
        internal static string GearIconOf(Engine.Gear g) => GearIcon(g);

        static string GearIcon(Engine.Gear g)
        {
            switch (g.Kind)
            {
                case GearKinds.Weapon: return Content.GearWeapon(g) is FoundWeaponDef d ? Ui.WeaponIcon(d.Weapon) : "overwatchMelee";
                case GearKinds.Item:
                    switch (g.Type)
                    {
                        case ItemIds.SmokeGrenade: return "smoke";
                        case ItemIds.Flashbang: return "flashbang";
                        case ItemIds.Molotov: return "molotov";
                        case ItemIds.FirstAid: return "firstAid";
                        default: return "grenade";
                    }
                case GearKinds.Mod: return "stat:weapon";
                case GearKinds.Vest: return "stat:armor";
                case GearKinds.Mags: return "stat:ammo";
                // Arco y ballesta's (U29): the quiver.
                case GearKinds.Arrows: return "arrows";
                case GearKinds.Medicine: return "store:medicines";
                case GearKinds.Supplies: return "§";
                // La llave's (U22): Synty's map key, as its stage's on the map.
                case GearKinds.Key: return "key";
                // Mordido's (U23): Dark Fantasy's flask, apart from the medicines' cross, till Arte's picture.
                case GearKinds.Antiviral: return "antiviral";
                default: return null;
            }
        }

        /// <summary>
        /// What a thing changes against what `against`'s soldier wears in its place, in a line (U4, a kit place's list): a
        /// weapon's damage and aim as that soldier would have them (O7, cuadro 156), a vest's health and armour; each green
        /// when better, red when worse, with by how much; null with nothing to weigh or say.
        /// </summary>
        static string BriefOf(Engine.Gear g, Wielder against)
        {
            string Fig(string label, string value, double mine, double theirs, double by, bool weighed) =>
                !weighed || mine == theirs ? $"{label} {value}" : $"{label} <color={(mine > theirs ? Better : Worse)}>{value} ({SignedFigure(by)})</color>";
            if (g.Kind == GearKinds.Weapon && Content.GearWeapon(g) is FoundWeaponDef d)
            {
                var theirs = against?.At?.Invoke(Aim.SlotOf(d));
                var vs = theirs != null && theirs.Id != g.Id ? Content.GearWeapon(theirs) : null;
                var hand = against?.Hand?.Invoke(g);
                var vsHand = vs != null ? against.Hand?.Invoke(theirs) : null;
                if (vs != null && (hand == null) != (vsHand == null)) hand = vsHand = null;
                var a = FiguresOf(d, g.Mods, hand);
                var b = vs != null ? FiguresOf(vs, theirs.Mods, vsHand) : (WeaponFigures?)null;
                var damage = Fig(Lore.WeaponLabels.Damage, SpanOf(a.Min, a.Max), a.Min + a.Max, (b?.Min ?? 0) + (b?.Max ?? 0), b == null ? 0 : (a.Min + a.Max - b.Value.Min - b.Value.Max) / 2.0, b != null);
                var aim = Fig(Lore.WeaponLabels.Aim, hand != null ? a.Aim.ToString() : SignedFigure(a.Aim), a.Aim, b?.Aim ?? 0, a.Aim - (b?.Aim ?? 0), b != null);
                return $"{damage} · {aim}";
            }
            if (g.Kind == GearKinds.Vest && g.Type != null && Content.Vests.TryGetValue(g.Type, out var v))
            {
                var worn = against?.At?.Invoke(KitSlots.Vest);
                var w = worn != null && worn.Id != g.Id && worn.Type != null && Content.Vests.TryGetValue(worn.Type, out var wv) ? wv : null;
                return $"{Fig(StrategyText.Stash.HealthWord, v.Hp.ToString(), v.Hp, w?.Hp ?? 0, v.Hp - (w?.Hp ?? 0), w != null)} · {Fig(StrategyText.Stash.ArmorWord, v.Armor.ToString(), v.Armor, w?.Armor ?? 0, v.Armor - (w?.Armor ?? 0), w != null)}";
            }
            return null;
        }

        static string SpanOf(int min, int max) => min == max ? min.ToString() : $"{min}–{max}";

        /// <summary>On hover: its name (and how many of how many), what it is, what is mounted on it.</summary>
        static string GearTip(Engine.Gear g, Wielder against)
        {
            var pile = Content.PileOf(g.Kind, g.Type);
            var lines = new List<string> { pile > 1 ? $"{GearName(g)} · {g.Count} de {pile}" : GearName(g) };
            // Given by the base (Botiquín de salida, cuadro 165): it can't be offered.
            if (g.Issued == true) lines.Add(StrategyText.Stash.Issued);
            switch (g.Kind)
            {
                case GearKinds.Weapon:
                    var d = Content.GearWeapon(g);
                    if (d != null) lines.Add(d.Slot == FoundWeaponSlots.Primary ? StrategyText.Stash.Primary : d.Slot == FoundWeaponSlots.Sidearm ? StrategyText.Stash.Sidearm : StrategyText.Stash.Melee);
                    if (g.Mods != null && g.Mods.Count > 0) lines.Add(StrategyText.Stash.Mounted(g.Mods.Select(m => ModName(m).ToLowerInvariant()).ToList()));
                    // What it is good for and its figures, to tell one from another (Arturo, 18/11 T4; Lore's line and order).
                    if (d != null)
                    {
                        // Against the soldier chosen's weapon in the same place (Arturo, 18/11, cuadro: «verde o rojo junto a
                        // cada cifra»): not against itself, nor an empty place.
                        var place = d.Slot == FoundWeaponSlots.Primary ? KitSlots.Primary : d.Slot == FoundWeaponSlots.Sidearm ? KitSlots.Pistol : KitSlots.Melee;
                        var theirs = against?.At?.Invoke(place);
                        var vs = theirs != null && theirs.Id != g.Id ? Content.GearWeapon(theirs) : null;
                        // What this soldier would do with each (O7): its skills and perks on the weapon's own figures.
                        var hand = against?.Hand?.Invoke(g);
                        var vsHand = vs != null ? against.Hand?.Invoke(theirs) : null;
                        lines.AddRange(WeaponLines(d, g.Mods, vs, theirs?.Mods, vs != null ? StrategyText.Stash.Against(Lore.GearThe(theirs.Kind, theirs.Type, 1), against.Who) : null, hand, vsHand));
                    }
                    break;
                case GearKinds.Item:
                    // The belt's five, Lore's line with their uses (Arturo, 18/11, T4); the old armoury's others, their own.
                    if (g.Type != null && Content.Items.TryGetValue(g.Type, out var i)) lines.Add(Lore.ItemLine(g.Type) ?? i.Description);
                    break;
                case GearKinds.Mod:
                    if (g.Type != null && Content.WeaponMods.TryGetValue(g.Type, out var m)) lines.Add(m.Line);
                    break;
                case GearKinds.Vest:
                    if (g.Type != null && Content.Vests.TryGetValue(g.Type, out var v)) lines.Add(StrategyText.Stash.Vest(v.Hp, v.Armor));
                    break;
                case GearKinds.Mags: lines.Add(StrategyText.Stash.MagsLine); break;
                case GearKinds.Arrows: lines.Add(Lore.ArrowsText.Help); break;
                case GearKinds.Medicine: lines.Add(StrategyText.Stash.MedicineLine); break;
                case GearKinds.Supplies: lines.Add(StrategyText.Stash.SuppliesLine); break;
                // La llave's (U22): what it opens, and that it stays in the stage (Lore's).
                case GearKinds.Key: lines.Add(Lore.KeyText.Help); break;
                // Mordido's antiviral (U23): what it does, and that it stays in the stage (Lore's).
                case GearKinds.Antiviral: lines.Add(Lore.BiteText.AntiviralHelp); break;
            }
            return string.Join("\n", lines.Where(l => !string.IsNullOrEmpty(l)));
        }

        /// <summary>
        /// A weapon's help under its name (Arturo, 18/11 T4: «en las armas sus características para saber cuál es mejor que
        /// otra»): Lore's line for what it fires as and a long blade's or the rake's, then its figures in Lore's order, only
        /// those that say something (T4-OBJETOS.md): Daño, Puntería, Crítico, Alcance, Cargador, Ruido, the actions when two,
        /// Quita blindaje. As it is: the engine's card with what is mounted (Aim.WeaponProfileOf), no wear on an expedition;
        /// with `hand` (O7), damage, aim and crit as that soldier has them, each under it in small where it comes from, and
        /// the other weighed as the same soldier would have it (`vsHand`).
        /// </summary>
        public static List<string> WeaponLines(FoundWeaponDef d, IReadOnlyList<string> mods, FoundWeaponDef vs = null, IReadOnlyList<string> vsMods = null, string against = null, HeldFigures? hand = null, HeldFigures? vsHand = null)
        {
            var lines = new List<string>();
            if (Lore.WeaponLine(d.Weapon) is string line) lines.Add(line);
            if (Lore.MeleeLine(d.Id) is string blade) lines.Add(blade);
            // What the colours weigh against, right over the figures (Lore, 18/11).
            if (!string.IsNullOrEmpty(against)) lines.Add(against);
            // (Both as the soldier has them, or both as they are: never one of each.)
            if (vs != null && (hand == null) != (vsHand == null)) hand = vsHand = null;
            var bare = FiguresOf(d, mods, null);
            var a = FiguresOf(d, mods, hand);
            var b = vs != null ? FiguresOf(vs, vsMods, vsHand) : (WeaponFigures?)null;
            // Each figure green if it is better than the other's, red if worse (Mixta's good and danger), as it is otherwise;
            // and by it how much it goes up or down (Arturo, 18/11, cuadro: «junto a cada cifra, cuánto sube o baja»).
            void Figure(string label, string value, int better = 0, double by = 0)
            {
                var shown = better == 0 ? value : $"<color={(better > 0 ? Better : Worse)}>{value}{(by != 0 ? $" ({SignedFigure(by)})" : "")}</color>";
                lines.Add($"{label}: {shown}");
            }
            int Cmp(double mine, double theirs, bool lowIsGood = false) => b == null || mine == theirs ? 0 : (mine > theirs) != lowIsGood ? 1 : -1;
            void Small(string text) => lines.Add($"<size=90%><color={Source}>{text}</color></size>");
            // Under a figure the soldier changes, in small, its own base (aim and crit, cuadro 156), the weapon's and each of
            // the soldier's by its name (O7).
            void From(string weapon, System.Func<HeldBonus, int> of, string unit = "", string own = null)
            {
                var by = hand?.Bonuses?.Where(x => of(x) != 0).Select(x => $"{x.Label} {SignedFigure(of(x))}{unit}").ToList() ?? new List<string>();
                if (by.Count > 0 || own != null) Small(StrategyText.Stash.FromWeapon(weapon, by, own));
            }
            string Span(int min, int max) => min == max ? min.ToString() : $"{min}–{max}";
            // (Damage by its middle: 4–6 against 3–5 is +1.)
            Figure(Lore.WeaponLabels.Damage, Span(a.Min, a.Max), Cmp(a.Min + a.Max, b?.Min + b?.Max ?? 0), b == null ? 0 : (a.Min + a.Max - b.Value.Min - b.Value.Max) / 2.0);
            From(Span(bare.Min, bare.Max), x => x.Damage);
            bool Changes(System.Func<HeldBonus, int> of) => hand?.Bonuses?.Any(x => of(x) != 0) == true;
            if (hand is HeldFigures h)
            {
                // The soldier's whole aim and crit with it (Arturo, cuadro 156: «con la puntería total»), its base, the
                // weapon's and its bonuses under them; a gun's before reach and cover, said so.
                Figure(Lore.WeaponLabels.Aim, a.Aim.ToString(), Cmp(a.Aim, b?.Aim ?? 0), a.Aim - (b?.Aim ?? 0));
                From(SignedFigure(h.WeaponAim), x => x.Aim, own: h.BaseAim.ToString());
                if (!a.Melee) Small(StrategyText.Stash.NoReachNorCover);
                if (a.Crit != 0 || (b?.Crit ?? 0) != 0)
                {
                    Figure(Lore.WeaponLabels.Crit, $"{a.Crit} %", Cmp(a.Crit, b?.Crit ?? 0), a.Crit - (b?.Crit ?? 0));
                    // (Its base only when it has one: most have none.)
                    if (h.BaseCrit != 0) From($"{SignedFigure(h.WeaponCrit)} %", x => x.Crit, " %", $"{h.BaseCrit} %");
                    else From($"{h.WeaponCrit} %", x => x.Crit, " %");
                }
            }
            else
            {
                if (a.Aim != 0 || (b?.Aim ?? 0) != 0) Figure(Lore.WeaponLabels.Aim, SignedFigure(a.Aim), Cmp(a.Aim, b?.Aim ?? 0), a.Aim - (b?.Aim ?? 0));
                if (a.Crit != 0 || (b?.Crit ?? 0) != 0) Figure(Lore.WeaponLabels.Crit, $"{a.Crit} %", Cmp(a.Crit, b?.Crit ?? 0), a.Crit - (b?.Crit ?? 0));
            }
            // The crit's damage only when the soldier changes it (a perk's), with the weapon's under it (O7).
            if (Changes(x => x.CritDamage))
            {
                Figure(Lore.WeaponLabels.CritDamage, Span(a.CritMin, a.CritMax), Cmp(a.CritMin + a.CritMax, b?.CritMin + b?.CritMax ?? 0), b == null ? 0 : (a.CritMin + a.CritMax - b.Value.CritMin - b.Value.CritMax) / 2.0);
                // (The weapon's own from the card: the knife has none of the Armería's.)
                From(Span(hand.Value.WeaponCritMin, hand.Value.WeaponCritMax), x => x.CritDamage);
            }
            // In tiles (Arturo, 18/11 T23: «el número de casillas que tiene de alcance»; Gameplay's Content.ReachTiles, Lore's
            // words): how far, and where it's best when that is less; weighed by how far, then how far the best goes, then
            // how close it starts.
            var reach = a.Reach.Max != (b?.Reach.Max ?? a.Reach.Max) ? Cmp(a.Reach.Max, b.Value.Reach.Max)
                : a.Reach.To != (b?.Reach.To ?? a.Reach.To) ? Cmp(a.Reach.To, b.Value.Reach.To)
                : Cmp(a.Reach.From, b?.Reach.From ?? a.Reach.From, lowIsGood: true);
            var reachBy = b == null ? 0 : a.Reach.Max != b.Value.Reach.Max ? a.Reach.Max - b.Value.Reach.Max : a.Reach.To != b.Value.Reach.To ? a.Reach.To - b.Value.Reach.To : a.Reach.From - b.Value.Reach.From;
            Figure(Lore.WeaponLabels.Reach, StrategyText.Stash.Reach(a.Reach.From, a.Reach.To, a.Reach.Max, a.Melee), reach, reachBy);
            // (A blade has no magazine: no line for it, Lore 18/11.)
            // (The bow's no magazine is an arrow a shot: Lore's, U29.)
            if (!a.Melee) Figure(Lore.WeaponLabels.Clip, Lore.WeaponFigure("clip", a.Clip, a.Melee, Content.Weapons[d.Weapon].Arrows), b?.Melee != false ? 0 : Cmp(a.Clip, b?.Clip ?? 0), b?.Melee != false ? 0 : a.Clip - b.Value.Clip);
            Figure(Lore.WeaponLabels.Noise, Lore.WeaponFigure("noise", a.Noise), Cmp(a.Noise, b?.Noise ?? 0, lowIsGood: true), a.Noise - (b?.Noise ?? a.Noise));
            if (a.Actions >= 2 || (b?.Actions ?? 0) >= 2) Figure(a.Melee ? Lore.WeaponLabels.ActionsMelee : Lore.WeaponLabels.Actions, Lore.WeaponFigure("actions", a.Actions), Cmp(a.Actions, b?.Actions ?? 0, lowIsGood: true), a.Actions - (b?.Actions ?? a.Actions));
            if (a.Shred > 0 || (b?.Shred ?? 0) > 0) Figure(Lore.WeaponLabels.Shred, a.Shred.ToString(), Cmp(a.Shred, b?.Shred ?? 0), a.Shred - (b?.Shred ?? 0));
            return lines;
        }

        const string Better = "#8fd05a", Worse = "#f0503c", Source = "#ffffff99";

        /// <summary>A figure with its sign, as the aim always goes («+0», «+20», «−10»); a half, with a comma («+0,5»).</summary>
        static string SignedFigure(double n)
        {
            var s = System.Math.Abs(n).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
            return n < 0 ? "−" + s : "+" + s;
        }

        /// <summary>A weapon's figures as it is (the engine's card with what is mounted, no wear); the knife's, its own.</summary>
        struct WeaponFigures
        {
            public int Min, Max, CritMin, CritMax, Aim, Crit, Clip, Noise, Actions, Shred;
            public WeaponReach Reach;
            public bool Melee;
        }

        static WeaponFigures FiguresOf(FoundWeaponDef d, IReadOnlyList<string> mods, HeldFigures? hand)
        {
            var w = Content.Weapons[d.Weapon];
            // (The knife isn't one found: the engine's card of its weapon, nothing to mount on it; as Aim.CardFor's. Till O7
            // its own figures here said 3–4 where it does 2–4.)
            var p = Content.FoundWeapons.ContainsKey(d.Id) ? Aim.WeaponProfileOf(d.Id, 100, mods ?? new List<string>()) : Aim.BaseWeaponProfile(d.Weapon, d.Name);
            var f = new WeaponFigures
            {
                Min = p?.Damage.Min ?? w.Damage, Max = p?.Damage.Max ?? w.Damage + w.Spread, Aim = p?.Aim ?? w.Aim, Crit = p?.Crit ?? w.Crit,
                CritMin = p?.CritDamage.Min ?? 0, CritMax = p?.CritDamage.Max ?? 0,
                Reach = Content.ReachTiles(w), Clip = p?.Clip ?? w.Clip, Noise = p?.Noise ?? w.Noise, Actions = p?.Actions ?? w.ApCost,
                Shred = p?.Shred ?? w.Shred, Melee = w.Melee,
            };
            // What the soldier makes of it (O7): only what it changes; the rest is the weapon's.
            if (hand is HeldFigures h)
            {
                f.Min = h.Min;
                f.Max = h.Max;
                f.CritMin = h.CritMin;
                f.CritMax = h.CritMax;
                f.Aim = h.Aim;
                f.Crit = h.Crit;
            }
            return f;
        }

        // ------------------------------------------------------------ the orders

        /// <summary>The order for a move on the grids: a mod onto a weapon mounts it; part of a pile splits it; else it moves.</summary>
        public CampaignCommand GearCommand(GearMove m)
        {
            if (m.Throw) return new DropGearCommand { Gear = m.Gear };
            if (m.Onto != null) return new MountCommand { Mod = m.Gear, Weapon = m.Onto };
            var spot = Gears.Find(State, m.Gear);
            if (m.Slot == GearMove.Anywhere) m = Onto(m, spot);
            var slot = m.Slot ?? KitSlots.Pack;
            if (m.Count > 0 && spot != null && m.Count < spot.Gear.Count)
                return new SplitGearCommand { Gear = m.Gear, Count = m.Count, To = m.To, Slot = slot, X = m.X, Y = m.Y, Turned = m.Turned };
            return new MoveGearCommand { Gear = m.Gear, To = m.To, Slot = slot, X = m.X, Y = m.Y, Turned = m.Turned };
        }

        /// <summary>
        /// A thing let go on a soldier's bust (M12): where it goes on them, as Mayús + clic would (M3): its own place if
        /// empty, else a pile of the same in their pack with room for all of it, else where it first fits (Gears.FirstFit).
        /// Nowhere: their pack's corner, which the engine turns down with its reason.
        /// </summary>
        GearMove Onto(GearMove m, GearSpot spot)
        {
            var to = State.Soldiers.FirstOrDefault(s => s.Id == m.To && s.Alive);
            var placed = new GearMove { Gear = m.Gear, Count = m.Count, To = m.To, Slot = KitSlots.Pack };
            if (spot == null || to?.Kit == null) return placed;
            var g = spot.Gear;
            var count = m.Count > 0 ? m.Count : g.Count;
            foreach (var slot in new[] { KitSlots.Primary, KitSlots.Melee, KitSlots.Pistol, KitSlots.Vest, KitSlots.Belt0, KitSlots.Belt1 })
                if (Content.SlotTakes(slot, g) && Gears.InSlot(to.Kit, slot) == null)
                {
                    placed.Slot = slot;
                    return placed;
                }
            var pile = to.Kit.Pack.Items.FirstOrDefault(p => Gears.SamePile(p.Gear, g) && Gears.Room(p.Gear) >= count);
            var at = pile ?? Gears.FirstFit(to.Kit.Pack, g);
            if (at != null)
            {
                placed.X = at.X;
                placed.Y = at.Y;
                placed.Turned = at.Turned;
            }
            return placed;
        }

        /// <summary>Why that move can't be: the engine's own words, as the server would answer; null, it can.</summary>
        public string GearRefusal(GearMove m)
        {
            var c = State;
            switch (GearCommand(m))
            {
                case DropGearCommand d: return Gears.DropRefusal(c, Slot, Connected, d);
                case MountCommand mc: return Gears.MountRefusal(c, Slot, Connected, mc);
                case SplitGearCommand sc: return Gears.SplitRefusal(c, Slot, Connected, sc);
                case MoveGearCommand mv: return Gears.MoveRefusal(c, Slot, Connected, mv);
                default: return "";
            }
        }

        /// <summary>A thing's other orders: take off each mod (in the base), a medicine on each of the squad (in a run).</summary>
        public IReadOnlyList<GearActionView> GearActions(string gear)
        {
            var c = State;
            var list = new List<GearActionView>();
            var g = Gears.Find(c, gear)?.Gear;
            if (g == null) return list;
            if (g.Kind == GearKinds.Weapon && g.Mods != null)
                foreach (var mod in g.Mods)
                    list.Add(new GearActionView { Id = "unmount:" + mod, Label = StrategyText.Stash.Unmount(Lore.GearThe(GearKinds.Mod, mod)), Why = Gears.UnmountRefusal(c, Slot, Connected, new UnmountCommand { Weapon = g.Id, Mod = mod }) });
            if (g.Kind == GearKinds.Medicine && c.Run != null)
                foreach (var id in c.Run.Squad)
                {
                    var s = c.Soldiers.FirstOrDefault(x => x.Id == id);
                    if (s == null || !s.Alive) continue;
                    list.Add(new GearActionView { Id = "use:" + id, Label = StrategyText.Stash.Heal(FirstName(s)), Why = Gears.UseGearRefusal(c, Slot, Connected, new UseGearCommand { Gear = g.Id, Soldier = id }) });
                }
            return list;
        }

        /// <summary>The order of one of GearActions'.</summary>
        public CampaignCommand GearActionCommand(string gear, string action)
        {
            var at = action.IndexOf(':');
            var what = at < 0 ? action : action.Substring(0, at);
            var arg = at < 0 ? "" : action.Substring(at + 1);
            switch (what)
            {
                case "unmount": return new UnmountCommand { Weapon = gear, Mod = arg };
                case "use": return new UseGearCommand { Gear = gear, Soldier = arg };
                default: return null;
            }
        }
    }
    /// <summary>
    /// Whom a weapon is shown for (T4, O7): what it carries in each place, to weigh against (`At`), its first name, and what
    /// it would do with a weapon (`Hand`; null, the weapon as it is).
    /// </summary>
    public sealed class Wielder
    {
        public System.Func<string, Engine.Gear> At;
        public string Who;
        public System.Func<Engine.Gear, HeldFigures?> Hand;
    }

    /// <summary>
    /// What a soldier does with a weapon (O7, Gameplay's card): its damage and crit's damage, its crit chance and aim with
    /// it (before range, cover and defence), and each of its own that touches the weapon, by the name the player knows.
    /// </summary>
    public struct HeldFigures
    {
        public int Min, Max, CritMin, CritMax, Crit, Aim;
        /// <summary>The weapon's own crit damage, aim and crit (the card's Weapon), for the lines under them.</summary>
        public int WeaponCritMin, WeaponCritMax, WeaponAim, WeaponCrit;
        /// <summary>The soldier's own aim and crit, neither the weapon's nor a named bonus's (cuadro 156: «base 55»).</summary>
        public int BaseAim, BaseCrit;
        public List<HeldBonus> Bonuses;
    }

    /// <summary>One of a soldier's that touches a weapon (O7): its name («Golpe pesado») and what it adds.</summary>
    public struct HeldBonus
    {
        public string Label;
        public int Damage, Aim, Crit, CritDamage;
    }
}
