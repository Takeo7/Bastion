using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // -------------------------------------------------------------- backpack

        /// <summary>
        /// What a backpack's thing is, in a sentence (docs/FASE8.md §C): «un
        /// botiquín», «2 raciones», «12 suministros», «un fusil de asalto», «un
        /// cargador».
        /// </summary>
        public static string LootName(Loot loot)
        {
            var parts = new List<string>();
            if (loot.Supplies.GetValueOrDefault() != 0) parts.Add(loot.Supplies == 1 ? "1 suministro" : $"{loot.Supplies} suministros");
            if (!string.IsNullOrEmpty(loot.Item)) parts.Add(ItemFinds[loot.Item!]);
            if (loot.Medicines.GetValueOrDefault() != 0) parts.Add(loot.Medicines == 1 ? "una medicina" : $"{loot.Medicines} medicinas");
            if (loot.Food.GetValueOrDefault() != 0) parts.Add(Rations(loot.Food!.Value));
            if (loot.Parts.GetValueOrDefault() != 0) parts.Add(PartsCount(loot.Parts!.Value));
            if (!string.IsNullOrEmpty(loot.Weapon)) parts.Add(Content.FoundWeapons[loot.Weapon!].Find);
            if (loot.Mags.GetValueOrDefault() != 0) parts.Add(loot.Mags == 1 ? "un cargador" : MagsCount(loot.Mags!.Value));
            var name = JoinAnd(parts);
            return name.Length > 0 ? name : "nada";
        }

        /// <summary>The log lines of the backpack: «Ana deja en el suelo un botiquín.», «Ana recoge 2 raciones.», «Ana le da a Kofi un cargador.»</summary>
        public static string LootDroppedLine(string who, Loot loot)
        {
            return $"{who} deja en el suelo {LootName(loot)}.";
        }

        public static string LootTakenLine(string who, Loot loot)
        {
            return $"{who} recoge {LootName(loot)}.";
        }

        public static string LootGivenLine(string who, string to, Loot loot)
        {
            return $"{who} le da a {to} {LootName(loot)}.";
        }

        /// <summary>Finding something with the backpack full, without a verb to agree: «Mochila llena: 2 medicinas, al suelo.»</summary>
        public static string PackFullLine(Loot loot)
        {
            return $"Mochila llena: {LootName(loot)}, al suelo.";
        }

        /// <summary>
        /// A medicine used in the field: «Ana se cura con una medicina: +3 de salud.»,
        /// «Ana cura a Kofi con una medicina: +3 de salud, y el contagio se corta.».
        /// `to` is null on oneself.
        /// </summary>
        public static string MedicineLine(string who, string? to, int healed, bool cured)
        {
            var what = !string.IsNullOrEmpty(to) ? $"{who} cura a {to} con una medicina" : $"{who} se cura con una medicina";
            var effects = new List<string>();
            if (healed != 0) effects.Add($"+{healed} de salud");
            if (cured) effects.Add("el contagio se corta");
            return effects.Count > 0 ? $"{what}: {string.Join(", y ", effects)}." : $"{what}.";
        }

        /// <summary>Floating over whoever is cured of the bite.</summary>
        public const string CuredFloat = "SIN CONTAGIO";

        /// <summary>Taking up a found weapon in the field: «Ana empuña un fusil de asalto.»</summary>
        public static string WeaponEquippedLine(string who, string kind)
        {
            return $"{who} empuña {Content.FoundWeapons[kind].Find}.";
        }

        /// <summary>Putting on the belt what was found (playtest 18/10 #5): the backpack's button, its tooltip and the floating word.</summary>
        public static class BeltText
        {
            public const string Button = "Al cinturón";
            public const string Tip = "Lo lleva en el cinturón y puede usarlo ya.";
            public const string Float = "AL CINTURÓN";
        }

        /// <summary>The log line: «Ana se cuelga dos granadas al cinturón.»; the earplugs go in the ears: «Ana se pone unos tapones.»</summary>
        public static string BeltLine(string who, string item)
        {
            return item == "mindShield" ? $"{who} se pone {ItemFinds[item]}." : $"{who} se cuelga {ItemFinds[item]} al cinturón.";
        }

        /// <summary>
        /// The debrief's loot line: «El botín: +34 §, un botiquín, 2 medicinas, 3 raciones
        /// y una escopeta recortada.», or null with nothing brought back.
        /// </summary>
        public static string? LootSummary(int supplies, IReadOnlyList<string> items, int medicines, int food = 0, int parts = 0, IReadOnlyList<string>? weapons = null)
        {
            var list = new List<string>();
            if (supplies != 0) list.Add($"+{supplies} §");
            foreach (var id in items) list.Add(ItemFinds[id]);
            if (medicines != 0) list.Add(medicines == 1 ? "1 medicina" : $"{medicines} medicinas");
            if (food != 0) list.Add(Rations(food));
            if (parts != 0) list.Add(PartsCount(parts));
            foreach (var w in weapons ?? new string[0]) list.Add(Content.FoundWeapons[w].Find);
            return list.Count > 0 ? $"El botín: {JoinAnd(list)}." : null;
        }
    }
}
