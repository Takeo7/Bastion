using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// A weapon's figures and how they compare (arms.ts figures, versus, weaponDiff), and its wear (wearMarks): Soldados'
    /// choice of weapon, out of the Armería when it went (the runs, 16/11).
    /// </summary>
    public sealed partial class StrategyPresenter
    {
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
    }
}
