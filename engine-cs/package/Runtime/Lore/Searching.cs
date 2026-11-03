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
            public SearchSpot(string name, IReadOnlyList<string> supplies, bool masculine = false)
            {
                Name = name;
                Supplies = supplies;
                Masculine = masculine;
            }

            /// <summary>With its article, as it reads after «Abrir»: «la caja de armas».</summary>
            public string Name { get; }
            /// <summary>What the supplies found in it were; one is picked by seed.</summary>
            public IReadOnlyList<string> Supplies { get; }
            /// <summary>Whether Name is masculine, for what agrees with it: «Está vacío», «ya está abierto».</summary>
            public bool Masculine { get; }
        }

        /// <summary>
        /// What can be opened (docs/FASE8.md §B): the marked boxes, one kind each. They
        /// always give what they say, so they need no supplies of their own. A boot's
        /// suministros are what was in it, as a room's are.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, SearchSpot> SearchSpots = new Dictionary<string, SearchSpot>
        {
            ["lootWeapons"] = new SearchSpot("la caja de armas", new string[0]),
            ["lootMedical"] = new SearchSpot("la caja de medicinas", new string[0]),
            ["lootAmmo"] = new SearchSpot("la caja de munición", new string[0]),
            ["lootFood"] = new SearchSpot("la caja de comida", new string[0]),
            // A car's boot, a police car's, and a dead enemy's body (Arturo, 2/11): Looting.CarBoot, PoliceBoot and Body.
            ["carBoot"] = new SearchSpot("el maletero", new[] { "una caja de herramientas", "cables de arranque y un gato", "una lona y una cuerda", "una bolsa de deporte con ropa" }, masculine: true),
            ["policeBoot"] = new SearchSpot("el maletero del coche de policía", new[] { "bengalas de carretera", "una linterna y unas esposas", "una manta térmica y cinta de balizar" }, masculine: true),
            ["body"] = new SearchSpot("el cuerpo", new string[0], masculine: true),
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

        /// <summary>
        /// The log line for opening a marked box or a boot, or searching a body: «Ana abre la caja de armas: un fusil de
        /// asalto.»
        /// </summary>
        public static string SearchLine(string who, string decor, Loot? find, int seed)
        {
            var known = SearchSpots.TryGetValue(decor, out var spot);
            var where = known ? spot.Name : "la caja";
            var parts = FindParts(find, known ? spot.Supplies : new string[0], seed);
            if (decor == Looting.Body) return parts.Count > 0 ? $"{who} registra {where}: {JoinAnd(parts)}." : $"{who} registra {where}. No lleva nada.";
            return parts.Count > 0 ? $"{who} abre {where}: {JoinAnd(parts)}." : $"{who} abre {where}. Está {(known && spot.Masculine ? "vacío" : "vacía")}.";
        }

        /// <summary>The log line for searching a room (docs/FASE8.md §B): «Ana registra el despacho: una radio de manivela (+12 §) y 1 pieza.» `spot`: loot.ts Room.spot.</summary>
        public static string RoomSearchLine(string who, string spot, Loot? find, int seed)
        {
            var room = Rooms.TryGetValue(spot, out var r) ? r : Rooms["home"];
            var parts = FindParts(find, room.Supplies, seed);
            return parts.Count > 0 ? $"{who} registra {room.Name}: {JoinAnd(parts)}." : $"{who} registra {room.Name}. {EmptyFinds[seed % EmptyFinds.Count]}";
        }

        /// <summary>
        /// What opening `decor` does, for the action's panel (Arturo, 2/11: boots and bodies too): a box's sure find, a boot's
        /// chances (Spots.Trunk; Looting.PoliceBootGun), a body's gun (Looting.RollBody, Content.Uniforms).
        /// </summary>
        public static string SearchEffect(string decor)
        {
            switch (decor)
            {
                case Looting.CarBoot:
                    return "Abre el maletero de un coche que tengas al lado. Sale lo que haya: suministros, piezas, medicinas, algún cargador o accesorio y, a veces, un arma. Hace un poco de ruido.";
                case Looting.PoliceBoot:
                    return "Abre el maletero de un coche de policía que tengas al lado. Una vez de cada tres hay un arma; si no, lo que haya en cualquier maletero. Hace un poco de ruido.";
                case Looting.Body:
                    return "Registra el cuerpo de un enemigo caído a tu lado o a tus pies. Los vivos llevan siempre su arma. Un zombi casi nunca lleva una, salvo los policías y los militares: uno de cada cuatro. Hace un poco de ruido.";
                default:
                    return "Abre una caja marcada que tengas al lado. Da siempre lo suyo: un arma, una o dos medicinas, dos cargadores o de 3 a 5 raciones. Hace un poco de ruido.";
            }
        }

        /// <summary>Opening a marked box, for the action's title: «Abrir la caja de armas»; a body's, «Registrar el cuerpo».</summary>
        public static string BoxTitle(string decor)
        {
            return $"{(decor == Looting.Body ? "Registrar" : "Abrir")} {(SearchSpots.TryGetValue(decor, out var spot) ? spot.Name : "la caja")}";
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
