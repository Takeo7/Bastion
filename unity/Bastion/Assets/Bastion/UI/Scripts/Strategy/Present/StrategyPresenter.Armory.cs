using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>What the Armería has open, which only this screen knows (screen.ts engTab, engPick).</summary>
    public sealed class ArmoryPick
    {
        /// <summary>items or gear.</summary>
        public string Tab = "items";
        /// <summary>The row picked: an item's id; «f:» a weapon, «x:» a loose mod, «a1»–«a3» an armour, «m:» a work; null, the first.</summary>
        public string Pick;
    }

    public sealed partial class StrategyPresenter
    {
        /// <summary>The «Nuevo» marks of this player and campaign (fresh.ts); the controller tells it what was looked at.</summary>
        public FreshMarks Fresh = new FreshMarks();

        bool IsNew(string key) => Fresh.IsFresh(key, Level);

        /// <summary>The Armería (renderEngineering, arms.ts): the two tabs and their lists, and what is picked on the right.</summary>
        public ArmoryView Armory(ArmoryPick pick)
        {
            pick = pick ?? new ArmoryPick();
            var c = State;
            Fresh.Load(c);
            var level = Level;
            var fresh = Fresh.Fresh(level).Where(t => t.View == "engineering").ToList();
            var v = new ArmoryView
            {
                Head = "Armería", Tab = pick.Tab,
                ItemsNew = fresh.Any(t => t.Key.StartsWith("item:")), GearNew = fresh.Any(t => !t.Key.StartsWith("item:")),
            };
            var picked = pick.Pick;
            if (pick.Tab == "gear")
            {
                // No weapon tiers any more (docs/FASE9.md §5): the armour's, made in the Taller, for everyone; only the
                // tiers the Bastion's level allows (#13).
                var baseTier = Progression.BaseArmorTier(c);
                var tiers = new[] { "a1", "a2", "a3" }.Select((k, i) => (key: k, name: Progression.ArmorTiers[i].Name, made: baseTier >= i + 1)).Where(t => t.made || FreshMarks.ArmorLevels[t.key] <= level).ToList();
                var crafts = Progression.Crafts.Values.Where(d => (d.Kind == CraftKinds.Arms || d.Kind == CraftKinds.Ammo || d.Kind == CraftKinds.Mod) && FreshMarks.CraftLevel(d) <= level).ToList();
                bool Shown(string k) => k.StartsWith("a") ? tiers.Any(t => t.key == k) : k.StartsWith("m:") ? crafts.Any(d => "m:" + d.Id == k) : true;
                if (picked != null && !Shown(picked)) picked = null;
                var found = c.FoundWeapons ?? new List<FoundWeapon>();
                picked = picked ?? (found.Count > 0 ? "f:" + found[0].Id : tiers.FirstOrDefault(t => !t.made).key ?? "a1");
                pick.Pick = picked;
                // The Armería's weapons first (docs/FASE8.md §A): each for one soldier; its state and improvements by its name.
                if (found.Count > 0)
                {
                    var rack = new ArmorySectionView { Title = "En la Armería" };
                    foreach (var w in found)
                    {
                        var def = Content.FoundWeapons[w.Kind];
                        var holder = w.Holder != null ? c.Soldiers.FirstOrDefault(x => x.Id == w.Holder) : null;
                        var row = new ArmoryRowView { Id = "f:" + w.Id, Icon = WeaponIcon(def.Weapon), Name = def.Name, Place = PlaceOf(w.Kind), Holder = holder != null ? Appearances.DisplayName(holder.Name, holder.Nickname) : "En la Armería", Selected = picked == "f:" + w.Id };
                        row.Tags.AddRange(WearTags(w));
                        rack.Rows.Add(row);
                    }
                    v.Sections.Add(rack);
                }
                // The improvements loose on the rack, to put on a firearm.
                var loose = Content.WeaponMods.Keys.Where(m => c.WeaponMods != null && c.WeaponMods.TryGetValue(m, out var n) && n > 0).ToList();
                if (loose.Count > 0)
                {
                    var mods = new ArmorySectionView { Title = "Mejoras sueltas" };
                    foreach (var m in loose) mods.Rows.Add(new ArmoryRowView { Id = "x:" + m, Icon = "store:parts", Name = Content.WeaponMods[m].Name, Right = $"× {c.WeaponMods[m]}", Selected = picked == "x:" + m });
                    v.Sections.Add(mods);
                }
                var armor = new ArmorySectionView { Title = "Armadura" };
                foreach (var (key, name, made) in tiers)
                {
                    var row = new ArmoryRowView { Id = key, Tier = int.Parse(key.Substring(1)), Name = name, Dim = !made, Selected = picked == key, New = IsNew("armor:" + key), NewKey = "armor:" + key };
                    if (made) row.Right = "✓";
                    else row.Chip = CraftChip(c, int.Parse(key.Substring(1))) ?? new ChipView { Text = "En el Taller", Tip = "Se hace en el Taller" };
                    armor.Rows.Add(row);
                }
                v.Sections.Add(armor);
                // The Taller's weapons, munition and improvements (docs/FASE9.md §5): munition three times, the rest again and again.
                var make = new ArmorySectionView { Title = "Hacer en el Taller" };
                foreach (var def in crafts)
                {
                    var work = c.Base.Works.FirstOrDefault(x => x.Project == def.Id);
                    var done = def.Kind == CraftKinds.Ammo && c.Crafted != null && c.Crafted.Contains(def.Id);
                    var row = new ArmoryRowView
                    {
                        Id = "m:" + def.Id, Name = def.Name, Selected = picked == "m:" + def.Id, New = IsNew("craft:" + def.Id), NewKey = "craft:" + def.Id,
                        Icon = def.Kind == CraftKinds.Mod ? "store:parts" : def.Kind == CraftKinds.Ammo || def.Makes == null ? "stat:ammo" : WeaponIcon(Content.FoundWeapons[def.Makes].Weapon),
                        Dim = !(done || Campaign.CraftBlocker(c, def.Id) == null || work != null),
                    };
                    if (done) row.Right = "✓";
                    else if (work != null) row.Chip = new ChipView { Text = $"Taller · {MissionsText(MissionsFor(work.DaysLeft))}", Tone = "good" };
                    else if (def.Makes != null) row.Place = PlaceOf(def.Makes);
                    make.Rows.Add(row);
                }
                v.Sections.Add(make);
                v.Detail = picked.StartsWith("f:") ? FoundDetail(c, picked.Substring(2))
                    : picked.StartsWith("x:") ? ModDetail(c, picked.Substring(2))
                    : picked.StartsWith("m:") ? MakeDetail(c, picked.Substring(2))
                    : TierDetail(c, picked);
                return v;
            }
            // Only what goes on the belt (docs/FASE9.md §4), and what the Bastion's level allows (#13).
            var belt = Content.Items.Values.Where(i => i.Slot == ItemSlots.Utility && Progression.ItemUnlocked(c, i.Id)).ToList();
            if (picked != null && !belt.Any(i => i.Id == picked)) picked = null;
            picked = picked ?? belt.FirstOrDefault()?.Id;
            pick.Pick = picked;
            var list = new ArmorySectionView();
            foreach (var item in belt)
                list.Rows.Add(new ArmoryRowView
                {
                    Id = item.Id, Name = item.Name, Price = Campaign.CostText(Campaign.ShopPrice(c, item.Id)), Right = StockText(c, item.Id),
                    Selected = picked == item.Id, New = IsNew("item:" + item.Id), NewKey = "item:" + item.Id, Tip = "Se equipan en la ficha de cada soldado",
                });
            v.Sections.Add(list);
            if (Campaign.FacilityCount(c, "workshop") > 0) v.Note = $"Taller: −{Js.Round((1 - (double)Campaign.ShopPrice(c, ItemIds.SmokeGrenade) / Content.Items[ItemIds.SmokeGrenade].Cost) * 100)} %";
            if (picked != null) v.Detail = ItemDetail(c, Content.Items[picked]);
            return v;
        }

        /// <summary>Where a weapon of the Armería goes (placeOf): the hand or the belt.</summary>
        static string PlaceOf(string kind) => Content.FoundWeapons[kind].Slot == "melee" ? "Cinto" : "Mano";

        /// <summary>«Gastada» or «Rota», and «+2» for its improvements (wearMarks): a firearm's only.</summary>
        static List<ChipView> WearTags(FoundWeapon w)
        {
            var out_ = new List<ChipView>();
            if (Content.FoundWeapons[w.Kind].Slot != "primary") return out_;
            var condition = w.Condition ?? 100;
            if (condition <= 0) out_.Add(new ChipView { Text = "Rota", Tone = "bad", Tip = $"Rota: no dispara. Se arregla en la Armería. (estado {System.Math.Max(0, Js.Round(condition))})" });
            else if (condition < Content.WornBelow) out_.Add(new ChipView { Text = "Gastada", Tone = "warn", Tip = $"Gastada: −{Content.WornAim} de puntería. Pide piezas. (estado {Js.Round(condition)})" });
            var mods = w.Mods ?? new List<string>();
            if (mods.Count > 0) out_.Add(new ChipView { Text = $"+{mods.Count}", Tone = "good", Tip = string.Join("\n", mods.Select(m => $"{Content.WeaponMods[m].Name}: {Content.WeaponMods[m].Line}")) });
            return out_;
        }

        /// <summary>«Tienes 2 (1 libre)», or nothing when there are none (stockText).</summary>
        static string StockText(CampaignState c, string item)
        {
            var have = c.Inventory.TryGetValue(item, out var n) ? n : 0;
            var free = Campaign.ItemsFree(c, item);
            if (have == 0) return null;
            return free == have ? $"Tienes {have}" : $"Tienes {have} ({free} {(free == 1 ? "libre" : "libres")})";
        }

        /// <summary>
        /// The Taller's project behind a row of Equipo (its id as Armory lists it): «m:…» the project itself, «a1»–«a3» the
        /// armour tier's; null for anything else.
        /// </summary>
        public static string CraftOf(string picked) =>
            picked == null ? null : picked.StartsWith("m:") ? picked.Substring(2) : picked.Length == 2 && picked[0] == 'a' && char.IsDigit(picked[1]) ? CraftFor(picked[1] - '0')?.Id : null;

        /// <summary>The Taller's project that makes an armour tier (craftFor).</summary>
        static CraftDef CraftFor(int tier) => Progression.Crafts.Values.FirstOrDefault(d => d.Kind == CraftKinds.Armor && d.Tier == tier);

        /// <summary>The days left while the Taller makes a tier, or nothing (craftChip).</summary>
        ChipView CraftChip(CampaignState c, int tier)
        {
            var def = CraftFor(tier);
            var work = def != null ? c.Base.Works.FirstOrDefault(w => w.Project == def.Id) : null;
            return work != null ? new ChipView { Text = $"Taller · {MissionsText(MissionsFor(work.DaysLeft))}", Tone = "good", Tip = $"El Taller está en ello: sale {Lore.InMissions(MissionsFor(work.DaysLeft))}." } : null;
        }

        /// <summary>A belt's accessory: what it does, how many there are, and Comprar with its reason.</summary>
        ArmoryDetailView ItemDetail(CampaignState c, ItemDef item)
        {
            var unlocked = Progression.ItemUnlocked(c, item.Id);
            var cost = Campaign.ShopPrice(c, item.Id);
            var v = new ArmoryDetailView { Kicker = "Accesorio · Cinturón", Name = item.Name, Art = ItemIcon(item.Id), Text = { item.Description } };
            v.Chips.Add(new ChipView { Text = StockText(c, item.Id) ?? "No tienes ninguno", Tip = "Se equipa en la ficha del soldado; si cae, se pierde" });
            v.Chips.Add(unlocked ? new ChipView { Text = Campaign.CostText(cost) } : new ChipView { Text = Lore.NeedsLevelLine(item.Level), Tone = "warn" });
            if (!unlocked) return v;
            // With the other one here a purchase is proposed (Ana, 21/10 #31): one at a time, as the rest.
            var why = PendingWhy(c);
            v.Button = "Comprar";
            v.Action = "buy";
            v.Enabled = Campaign.CanAfford(c, cost) && why == "";
            var said = why != "" ? why : Shortfall(c, cost);
            v.Why = said != "" ? said : null;
            return v;
        }

        // ------------------------------------------------------------ weapons

        sealed class Figure
        {
            public string Label, Value, Title, Unit;
            public double? N;
            public bool More;
        }

        static string Signed(double n) => n > 0 ? $"+{n}" : n < 0 ? $"−{-n}" : "±0";
        static string Span(DamageRange d) => d.Min == d.Max ? d.Min.ToString() : $"{d.Min}–{d.Max}";
        static double Mean(DamageRange d) => (d.Min + d.Max) / 2.0;

        /// <summary>A weapon's figures (arms.ts figures; Arturo, 17/10: «para saber qué elegir»).</summary>
        static List<Figure> Figures(WeaponProfile p)
        {
            var out_ = new List<Figure>
            {
                new Figure { Label = Lore.WeaponLabels.Damage, Value = Span(p.Damage), N = Mean(p.Damage), More = true },
                new Figure { Label = Lore.WeaponLabels.CritDamage, Value = Span(p.CritDamage), N = Mean(p.CritDamage), More = true },
                new Figure { Label = Lore.WeaponLabels.Crit, Value = $"{Signed(p.Crit)} %", N = p.Crit, More = true, Unit = " %" },
                new Figure { Label = Lore.WeaponLabels.Aim, Value = Signed(p.Aim), Title = "Lo que el arma suma o resta a la puntería: la mira, el desgaste", N = p.Aim, More = true, Unit = "" },
                p.Clip != 0 && !p.Melee
                    ? new Figure { Label = Lore.WeaponLabels.Clip, Value = Lore.WeaponFigure("clip", p.Clip), N = p.Clip, More = true, Unit = "" }
                    : new Figure { Label = Lore.WeaponLabels.Clip, Value = Lore.WeaponFigure("clip", 0, p.Melee) },
                new Figure { Label = p.Melee ? Lore.WeaponLabels.ActionsMelee : Lore.WeaponLabels.Actions, Value = Lore.WeaponFigure("actions", p.Actions), N = p.Actions, More = false, Unit = "" },
                new Figure { Label = Lore.WeaponLabels.Noise, Value = Lore.WeaponFigure("noise", p.Noise), N = p.Noise, More = false, Unit = "" },
            };
            if (p.Shred != 0) out_.Add(new Figure { Label = Lore.WeaponLabels.Shred, Value = p.Shred.ToString(), N = p.Shred, More = true, Unit = "" });
            return out_;
        }

        /// <summary>Better or worse than another's: good, bad or null when the same or not comparable (versus).</summary>
        static string Versus(Figure f, Figure vs)
        {
            if (f.N == null || vs?.N == null || f.N == vs.N) return null;
            return (f.N > vs.N) == f.More ? "good" : "bad";
        }

        /// <summary>The figures for the card, their help on hover (weaponStats).</summary>
        static List<RowView> FigureRows(WeaponProfile p) => Figures(p).Select(f => new RowView { Key = f.Label, Value = f.Value, Tip = f.Title }).ToList();

        /// <summary>The aim a firearm adds or takes at each distance, over and under a line (rangeChart); null for a blade.</summary>
        static RangeView RangeOf(WeaponProfile p)
        {
            if (p.Melee || p.Range.Count == 0) return null;
            const double Max = 40;
            var v = new RangeView { Name = Lore.Reaches[p.Reach].Name, Line = Lore.Reaches[p.Reach].Line };
            foreach (var r in p.Range)
                v.Steps.Add(new RangeStepView { Distance = r.Tiles.ToString(), Value = Signed(r.Aim), Height = (float)(System.Math.Sign(r.Aim) * System.Math.Min(1, System.Math.Abs(r.Aim) / Max)) });
            return v;
        }

        static double Near(WeaponProfile p) => (AimAt(p, 2) + AimAt(p, 5)) / 2;
        static double Far(WeaponProfile p) => (AimAt(p, 12) + AimAt(p, 16)) / 2;
        static double AimAt(WeaponProfile p, int tiles) => p.Range.FirstOrDefault(r => r.Tiles == tiles)?.Aim ?? 0;

        /// <summary>
        /// What changes for a soldier from what they carry to `p` (weaponDiff): each figure that changes (the damage, its
        /// new span), green if better, red if worse, and near and far aim for firearms; «Igual» when nothing does.
        /// </summary>
        static List<ChipView> Diff(WeaponProfile p, WeaponProfile vs)
        {
            var theirs = Figures(vs);
            var out_ = new List<ChipView>();
            foreach (var f in Figures(p))
            {
                var other = theirs.FirstOrDefault(x => x.Label == f.Label);
                var tone = Versus(f, other);
                if (tone == null) continue;
                out_.Add(new ChipView { Text = $"{f.Label} {(f.Unit == null ? f.Value : Signed(f.N.Value - other.N.Value) + f.Unit)}", Tone = tone, Tip = $"{f.Label}: {other.Value} → {f.Value}" });
            }
            if (!p.Melee && !vs.Melee)
                foreach (var (label, a, b) in new[] { ("De cerca", Near(p), Near(vs)), ("De lejos", Far(p), Far(vs)) })
                    if (Js.Round(a) != Js.Round(b)) out_.Add(new ChipView { Text = $"{label} {Signed(Js.Round(a - b))}", Tone = a > b ? "good" : "bad", Tip = $"Puntería {label.ToLowerInvariant()}: {Signed(Js.Round(b))} → {Signed(Js.Round(a))}" });
            if (out_.Count == 0) out_.Add(new ChipView { Text = "Igual" });
            return out_;
        }

        /// <summary>A weapon of the Armería (foundPanel): what it does, its state and improvements, who carries it and who could.</summary>
        ArmoryDetailView FoundDetail(CampaignState c, string id)
        {
            var w = (c.FoundWeapons ?? new List<FoundWeapon>()).FirstOrDefault(x => x.Id == id);
            if (w == null) return new ArmoryDetailView { Kicker = "Arma", Name = "", Text = { "Esa arma ya no está." } };
            var def = Content.FoundWeapons[w.Kind];
            var holder = w.Holder != null ? c.Soldiers.FirstOrDefault(x => x.Id == w.Holder) : null;
            var melee = def.Slot == "melee";
            var profile = Aim.WeaponProfileOf(w.Kind, w.Condition ?? 100, w.Mods);
            var v = new ArmoryDetailView
            {
                Kicker = $"{(def.Better ? "Arma buena" : melee ? "Cuerpo a cuerpo" : "Arma")} · {PlaceOf(w.Kind)}", Name = def.Name, Art = WeaponIcon(def.Weapon),
                Text = { melee ? "Para un soldado, en lugar del cuchillo. Si cae, se pierde." : $"Para un soldado: la lleva en la mano y la pistola al cinto.{(def.Better && def.Mod != null ? $" Vino con {Content.WeaponMods[def.Mod].Name.ToLowerInvariant()}." : "")} Si cae, se pierde." },
                Figures = FigureRows(profile), Range = RangeOf(profile),
            };
            if (!melee) Care(c, w, v);
            v.Holder = new HolderView { Head = "La lleva", Name = holder != null ? Appearances.DisplayName(holder.Name, holder.Nickname) : "Nadie: está en la Armería.", Button = holder != null && Commands(holder.Owner) ? "Quitársela" : null };
            // One's own, then the partner's while away (their name says whose).
            var mine = c.Soldiers.Where(s => s.Alive && Commands(s.Owner)).OrderBy(s => s.Owner != Slot ? 1 : 0).ToList();
            v.SoldiersHead = mine.Any(s => s.Owner != Slot) ? $"Tus soldados y los de {Partner?.Name ?? "el otro jugador"}" : "Tus soldados";
            v.SoldiersEmpty = "No tienes soldados que puedan llevarla.";
            foreach (var s in mine)
            {
                var carries = melee ? (Progression.MeleeWeaponOf(c, s) != null ? "Machete" : "Cuchillo") : Campaign.WeaponLabel(c, s);
                var row = new CarrierView { Id = s.Id, Name = Appearances.DisplayName(s.Name, s.Nickname) + (s.Owner == Slot ? "" : $" · de {PlayerName(s.Owner)}"), Note = $"· {(melee ? "al cinto" : "en la mano")}: {carries}" };
                if (s.Id == w.Holder) row.Chip = "La lleva";
                else
                {
                    row.Button = "Dársela";
                    row.Diff = Diff(profile, Carried(c, s, melee));
                }
                v.Soldiers.Add(row);
            }
            return v;
        }

        /// <summary>A firearm's care (arms.ts weaponCare): its state and «Arreglar», and its improvements' two places.</summary>
        void Care(CampaignState c, FoundWeapon w, ArmoryDetailView v)
        {
            var condition = w.Condition ?? 100;
            var missing = Campaign.RepairParts(w);
            var have = c.Parts ?? 0;
            var why = Campaign.RepairBlocker(c, Slot, w.Id, Connected);
            var line = condition <= 0 ? "Rota: no dispara. Se arregla en la Armería." : condition < Content.WornBelow ? $"Gastada: −{Content.WornAim} de puntería. Pide piezas." : null;
            v.Condition = new ConditionView { Value = (int)System.Math.Max(0, System.Math.Min(100, condition)), Text = ConditionText(condition), Line = line };
            if (missing > 0)
            {
                var parts = System.Math.Min(missing, have);
                v.Condition.Button = $"Arreglar: 1 pieza cada {Content.RepairPerPart}";
                v.Condition.Enabled = why == null;
                v.Condition.Why = why != null && why != "No hay piezas."
                    ? why
                    : $"Como nueva, {missing} {(missing == 1 ? "pieza" : "piezas")}; tenéis {have}.{(have > 0 && have < missing ? $" Con {parts} llega a {System.Math.Min(100, condition + parts * Content.RepairPerPart)}." : "")}";
            }
            else v.Condition.Line = v.Condition.Line ?? "Está como nueva.";
            var mounted = w.Mods ?? new List<string>();
            var loose = Content.WeaponMods.Keys.Where(m => c.WeaponMods != null && c.WeaponMods.TryGetValue(m, out var n) && n > 0 && !mounted.Contains(m)).ToList();
            v.Mods = new ModsView { Head = $"Mejoras · {mounted.Count}/{Content.ModSlots}" };
            foreach (var m in mounted)
                v.Mods.Mounted.Add(new ModRowView { Id = m, Name = Content.WeaponMods[m].Name, Line = Content.WeaponMods[m].Line, Button = "Quitar", Enabled = Campaign.ModBlocker(c, Slot, w.Id, m, false, Connected) == null });
            if (mounted.Count < Content.ModSlots)
            {
                v.Mods.FreeHead = "Hueco libre";
                foreach (var m in loose)
                    v.Mods.Options.Add(new ModRowView { Id = m, Button = $"Poner {Content.WeaponMods[m].Name.ToLowerInvariant()} ({c.WeaponMods[m]})", Enabled = Campaign.ModBlocker(c, Slot, w.Id, m, true, Connected) == null });
            }
        }

        /// <summary>An improvement loose in the Armería (modPanel): what it does, how many, and the firearms to put it on.</summary>
        ArmoryDetailView ModDetail(CampaignState c, string mod)
        {
            var def = Content.WeaponMods[mod];
            var count = c.WeaponMods != null && c.WeaponMods.TryGetValue(mod, out var n) ? n : 0;
            var v = new ArmoryDetailView { Kicker = "Mejora · Suelta", Name = def.Name, Art = "store:parts", Text = { def.Line }, Chips = { new ChipView { Text = count == 1 ? "Tenéis una" : $"Tenéis {count}" } } };
            v.SoldiersHead = "Ponérsela a";
            v.SoldiersEmpty = "No hay armas de fuego en la Armería.";
            foreach (var w in (c.FoundWeapons ?? new List<FoundWeapon>()).Where(x => Content.FoundWeapons[x.Kind].Slot == "primary"))
            {
                var holder = w.Holder != null ? c.Soldiers.FirstOrDefault(s => s.Id == w.Holder) : null;
                v.Soldiers.Add(new CarrierView
                {
                    Id = w.Id, Name = Content.FoundWeapons[w.Kind].Name, Note = $"· {(holder != null ? Appearances.DisplayName(holder.Name, holder.Nickname) : "en la Armería")}",
                    Button = "Ponérsela", Enabled = Campaign.ModBlocker(c, Slot, w.Id, mod, true, Connected) == null,
                });
            }
            return v;
        }

        /// <summary>A weapon, munition or improvement the Taller can make (makePanel): what it does, and «Hacer» with its cost.</summary>
        ArmoryDetailView MakeDetail(CampaignState c, string id)
        {
            if (!Progression.Crafts.TryGetValue(id, out var def)) return null;
            var work = c.Base.Works.FirstOrDefault(w => w.Project == def.Id);
            var done = def.Kind == CraftKinds.Ammo && c.Crafted != null && c.Crafted.Contains(def.Id);
            var blocker = Campaign.CraftBlocker(c, def.Id);
            var text = def.Kind == CraftKinds.Ammo
                ? $"Un cargador de repuesto más para cada soldado que salga de misión. Ahora cada uno sale con {Progression.SpareMags(c)} de repuesto."
                : def.Kind == CraftKinds.Mod && def.MakesMod != null
                    ? $"{Content.WeaponMods[def.MakesMod].Line} Una para la Armería: va en cualquier arma de fuego, {Content.ModSlots} mejoras como mucho en cada una. Se puede hacer tantas veces como haga falta."
                    : "Una para la Armería, para el soldado que la lleve. Se puede hacer tantas veces como haga falta.";
            var v = new ArmoryDetailView
            {
                Kicker = def.Kind == CraftKinds.Ammo ? "Taller · Munición" : def.Kind == CraftKinds.Mod ? "Taller · Mejora" : "Taller · Arma", Name = def.Name, Text = { text },
                Art = def.Makes != null ? WeaponIcon(Content.FoundWeapons[def.Makes].Weapon) : def.Kind == CraftKinds.Mod ? "store:parts" : "stat:ammo",
            };
            // What the weapon it makes does, as it comes out (Arturo, 17/10).
            if (def.Kind == CraftKinds.Arms && def.Makes != null)
            {
                var profile = Aim.WeaponProfileOf(def.Makes);
                v.Figures = FigureRows(profile);
                v.Range = RangeOf(profile);
            }
            if (done) v.Text.Add("Ya está hecha.");
            else if (work != null) v.Text.Add($"El Taller está en ello: sale {Lore.InMissions(MissionsFor(work.DaysLeft))}.");
            else
            {
                v.Chips.Add(new ChipView { Text = Campaign.CostText(def.Cost) });
                v.Chips.Add(new ChipView { Text = $"{def.Parts} piezas", Tip = $"Tenéis {c.Parts ?? 0}" });
                v.Chips.Add(new ChipView { Text = MissionsText(MissionsFor(def.Days)), Tip = $"Sale {Lore.InMissions(MissionsFor(def.Days))}" });
                v.Button = $"Hacer {def.The}";
                v.Action = "craft";
                v.Enabled = blocker == null;
                v.Why = blocker;
            }
            return v;
        }

        /// <summary>An armour tier (tierPanel; docs/FASE9.md §5): what it gives; once the Taller has made it, everyone wears it.</summary>
        ArmoryDetailView TierDetail(CampaignState c, string key)
        {
            var tier = int.Parse(key.Substring(1));
            var unlocked = Progression.BaseArmorTier(c) >= tier;
            var armour = Progression.ArmorTiers[tier - 1];
            var v = new ArmoryDetailView
            {
                Kicker = $"Armadura · Nivel {tier}", Name = armour.Name, Art = "stat:armor",
                Text = { tier == 1 ? "La protección de serie." : $"+{armour.Hp} de salud{(armour.Armor != 0 ? $" y +{armour.Armor} de blindaje" : "")}." },
                Chips = { unlocked ? new ChipView { Text = "La llevan todos", Tone = "good", Tip = "No se compra: en cuanto el Taller la hace, la llevan todos" } : new ChipView { Text = "Para todos, cuando el Taller la haga", Tip = "No se compra: en cuanto el Taller la hace, la llevan todos" } },
            };
            if (tier == 1 || unlocked) return v;
            var def = CraftFor(tier);
            if (def == null) return v;
            var work = c.Base.Works.FirstOrDefault(w => w.Project == def.Id);
            if (work != null)
            {
                v.Text.Add($"El Taller está en ello: sale {Lore.InMissions(MissionsFor(work.DaysLeft))}.");
                return v;
            }
            var blocker = Campaign.CraftBlocker(c, def.Id);
            v.Chips.Add(new ChipView { Text = Campaign.CostText(def.Cost) });
            v.Chips.Add(new ChipView { Text = $"{def.Parts} piezas", Tip = $"Tenéis {c.Parts ?? 0}" });
            v.Chips.Add(new ChipView { Text = MissionsText(MissionsFor(def.Days)), Tip = $"Sale {Lore.InMissions(MissionsFor(def.Days))}" });
            v.Button = $"Hacer {def.Name.ToLowerInvariant()}";
            v.Action = "craft";
            v.Enabled = blocker == null;
            v.Why = blocker;
            return v;
        }
    }
}
