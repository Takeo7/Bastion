using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------- searching

        /// <summary>A piece of furniture that can be searched, as the log and the button read it.</summary>
        public sealed class SearchSpot
        {
            public SearchSpot(string name, IReadOnlyList<string> supplies)
            {
                Name = name;
                Supplies = supplies;
            }

            /// <summary>With its article, as it reads after «Abrir»: «la caja de armas».</summary>
            public string Name { get; }
            /// <summary>What the supplies found in it were; one is picked by seed.</summary>
            public IReadOnlyList<string> Supplies { get; }
        }

        /// <summary>
        /// What can be opened (docs/FASE8.md §B): the marked boxes, one kind each. They
        /// always give what they say, so they need no supplies of their own.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, SearchSpot> SearchSpots = new Dictionary<string, SearchSpot>
        {
            ["lootWeapons"] = new SearchSpot("la caja de armas", new string[0]),
            ["lootMedical"] = new SearchSpot("la caja de medicinas", new string[0]),
            ["lootAmmo"] = new SearchSpot("la caja de munición", new string[0]),
            ["lootFood"] = new SearchSpot("la caja de comida", new string[0]),
        };

        /// <summary>A room as its furniture makes it (loot.ts Room.spot): what it is called, and what the supplies found in it were.</summary>
        private static readonly IReadOnlyDictionary<string, SearchSpot> Rooms = new Dictionary<string, SearchSpot>
        {
            ["home"] = new SearchSpot("la habitación", new[] { "ropa de abrigo", "mantas y un saco de dormir", "una caja de zapatos con pilas y velas", "cubiertos, cerillas y un mechero", "una linterna y pilas en la mesilla" }),
            ["office"] = new SearchSpot("el despacho", new[] { "pilas, cinta americana y un cúter", "una radio de manivela", "una linterna y un juego de llaves", "pilas detrás de los expedientes" }),
            ["shop"] = new SearchSpot("la tienda", new[] { "tabaco y mecheros", "pilas y bombillas", "jabón y lejía", "velas y botellas de agua" }),
            ["store"] = new SearchSpot("el almacén", new[] { "herramientas", "bidones de agua y lonas", "cuerda, alambre y clavos", "repuestos y cable" }),
        };

        /// <summary>When a search turns up nothing.</summary>
        public static readonly IReadOnlyList<string> EmptyFinds = new[]
        {
            "Nada. Alguien pasó antes.",
            "Vacío. Solo polvo.",
            "Nada que sirva.",
            "Nada, salvo una foto de familia.",
            "Ya lo registraron. Hace tiempo.",
        };

        /// <summary>What the medicines found were.</summary>
        public static readonly IReadOnlyList<string> MedicineFinds = new[]
        {
            "un blíster de antibióticos",
            "una caja de ibuprofeno a medias",
            "vendas sin abrir",
            "un frasco de yodo",
            "jarabe para la tos",
            "un tubo de pomada antibiótica",
        };

        /// <summary>Accessories as they read when found: with their article.</summary>
        public static readonly IReadOnlyDictionary<string, string> ItemFinds = new Dictionary<string, string>
        {
            ["fragGrenade"] = "dos granadas",
            ["smokeGrenade"] = "una granada de humo",
            ["flashbang"] = "una granada aturdidora",
            ["firstAid"] = "un botiquín",
            ["nanoVest"] = "un chaleco antibalas",
            ["mindShield"] = "unos tapones",
            ["apRounds"] = "munición perforante",
            ["tracerRounds"] = "munición trazadora",
        };

        /// <summary>What the rations found were.</summary>
        public static readonly IReadOnlyList<string> FoodFinds = new[]
        {
            "latas de lentejas",
            "un saco de arroz",
            "conservas de atún",
            "paquetes de galletas",
            "un tarro de miel",
            "pasta y tomate frito",
        };

        /// <summary>What the parts found were.</summary>
        public static readonly IReadOnlyList<string> PartsFinds = new[]
        {
            "muelles y tornillería",
            "un motor pequeño",
            "cable de cobre",
            "rodamientos",
            "una batería de coche",
            "piezas de una bicicleta",
        };

        /// <summary>«1 ración», «3 raciones»: the Bastion's food, counted.</summary>
        public static string Rations(int n)
        {
            return n == 1 ? "1 ración" : $"{n} raciones";
        }

        /// <summary>«1 pieza», «4 piezas»: what the Taller makes things with.</summary>
        public static string PartsCount(int n)
        {
            return n == 1 ? "1 pieza" : $"{n} piezas";
        }

        /// <summary>What a magazine found was (docs/FASE7.md): it fits the searcher's weapon.</summary>
        public static readonly IReadOnlyList<string> MagFinds = new[]
        {
            "un cargador lleno",
            "un cargador que le vale",
            "una caja de balas de su calibre",
        };

        /// <summary>«1 cargador», «3 cargadores»: a soldier's spares.</summary>
        public static string MagsCount(int n)
        {
            return n == 1 ? "1 cargador" : $"{n} cargadores";
        }

        private static string JoinAnd(IReadOnlyList<string> parts)
        {
            if (parts.Count > 1) return $"{string.Join(", ", Slice(parts, 0, parts.Count - 1))} y {parts[parts.Count - 1]}";
            return parts.Count == 1 ? parts[0] : "";
        }

        /// <summary>What a search turned up, as a list for the log: the supplies' flavour comes from where they were found.</summary>
        private static List<string> FindParts(Loot? find, IReadOnlyList<string> supplies, int seed)
        {
            var parts = new List<string>();
            if (find == null) return parts;
            if (find.Supplies.GetValueOrDefault() != 0) parts.Add($"{(supplies.Count > 0 ? supplies[seed % supplies.Count] : "suministros")} (+{find.Supplies} §)");
            if (!string.IsNullOrEmpty(find.Item)) parts.Add(ItemFinds[find.Item!]);
            if (find.Medicines.GetValueOrDefault() != 0) parts.Add(find.Medicines == 1 ? MedicineFinds[seed % MedicineFinds.Count] : $"{MedicineFinds[seed % MedicineFinds.Count]} ({find.Medicines} ✚)");
            if (find.Food.GetValueOrDefault() != 0) parts.Add($"{FoodFinds[seed % FoodFinds.Count]} ({Rations(find.Food!.Value)})");
            if (find.Parts.GetValueOrDefault() != 0) parts.Add($"{PartsFinds[seed % PartsFinds.Count]} ({PartsCount(find.Parts!.Value)})");
            if (!string.IsNullOrEmpty(find.Weapon)) parts.Add(Content.FoundWeapons[find.Weapon!].Find);
            if (find.Mags.GetValueOrDefault() != 0) parts.Add(find.Mags == 1 ? MagFinds[seed % MagFinds.Count] : MagsCount(find.Mags!.Value));
            return parts;
        }

        /// <summary>The log line for opening a marked box: «Ana abre la caja de armas: un fusil de asalto.»</summary>
        public static string SearchLine(string who, string decor, Loot? find, int seed)
        {
            var where = SearchSpots.TryGetValue(decor, out var spot) ? spot.Name : "la caja";
            var parts = FindParts(find, new string[0], seed);
            return parts.Count > 0 ? $"{who} abre {where}: {JoinAnd(parts)}." : $"{who} abre {where}. Está vacía.";
        }

        /// <summary>The log line for searching a room (docs/FASE8.md §B): «Ana registra el despacho: una radio de manivela (+12 §) y 1 pieza.» `spot`: loot.ts Room.spot.</summary>
        public static string RoomSearchLine(string who, string spot, Loot? find, int seed)
        {
            var room = Rooms.TryGetValue(spot, out var r) ? r : Rooms["home"];
            var parts = FindParts(find, room.Supplies, seed);
            return parts.Count > 0 ? $"{who} registra {room.Name}: {JoinAnd(parts)}." : $"{who} registra {room.Name}. {EmptyFinds[seed % EmptyFinds.Count]}";
        }

        /// <summary>Opening a marked box, for the action's title: «Abrir la caja de armas».</summary>
        public static string BoxTitle(string decor)
        {
            return $"Abrir {(SearchSpots.TryGetValue(decor, out var spot) ? spot.Name : "la caja")}";
        }

        /// <summary>What a search turned up, short, to float over the furniture: «+12 §», «Botiquín», «Medicinas», «+2 raciones», «+1 pieza», a weapon's name, «+1 cargador» or «Nada».</summary>
        public static string FindLabel(Loot? find)
        {
            var parts = new List<string>();
            if (find != null)
            {
                if (find.Supplies.GetValueOrDefault() != 0) parts.Add($"+{find.Supplies} §");
                if (!string.IsNullOrEmpty(find.Item)) parts.Add(Capital(Regex.Replace(ItemFinds[find.Item!], "^(una?|unos|unas) ", "")));
                if (find.Medicines.GetValueOrDefault() != 0) parts.Add("Medicinas");
                if (find.Food.GetValueOrDefault() != 0) parts.Add($"+{Rations(find.Food!.Value)}");
                if (find.Parts.GetValueOrDefault() != 0) parts.Add($"+{PartsCount(find.Parts!.Value)}");
                if (!string.IsNullOrEmpty(find.Weapon)) parts.Add(Content.FoundWeapons[find.Weapon!].Name);
                if (find.Mags.GetValueOrDefault() != 0) parts.Add($"+{MagsCount(find.Mags!.Value)}");
            }
            var label = string.Join(" · ", parts);
            return label.Length > 0 ? label : "Nada";
        }
    }
}
