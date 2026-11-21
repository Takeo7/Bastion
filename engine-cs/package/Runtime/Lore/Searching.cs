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
            public SearchSpot(string name, IReadOnlyList<string> supplies, bool masculine = false, bool plural = false, bool open = true, bool body = false)
            {
                Name = name;
                Supplies = supplies;
                Masculine = masculine;
                Plural = plural;
                Opens = open;
                Body = body;
            }

            /// <summary>With its article, as it reads after its verb: «la caja de armas», «el armero».</summary>
            public string Name { get; }
            /// <summary>What the supplies found in it were; one is picked by seed.</summary>
            public IReadOnlyList<string> Supplies { get; }
            /// <summary>Whether Name is masculine, for what agrees with it: «Está vacío».</summary>
            public bool Masculine { get; }
            /// <summary>Whether Name is plural («las taquillas»): «Están vacías».</summary>
            public bool Plural { get; }
            /// <summary>
            /// Opened, what has a door or a lid (a box, a boot, a locker); searched, what is open (a rack, a shelf, a cart, a
            /// body). Arturo, 18/11, cuadro 66.
            /// </summary>
            public bool Opens { get; }
            /// <summary>A body's: «No lleva nada.» when it has nothing.</summary>
            public bool Body { get; }
            /// <summary>The action's verb: «Abrir», «Registrar».</summary>
            public string Verb => Opens ? "Abrir" : "Registrar";
            /// <summary>As the log says it: «Ana abre», «Ana registra».</summary>
            public string Does => Opens ? "abre" : "registra";
            /// <summary>When nothing was in it: «Está vacía.», «Están vacíos.», a body's «No lleva nada.»</summary>
            public string Empty => Body ? "No lleva nada." : $"{(Plural ? "Están" : "Está")} {(Masculine ? "vacío" : "vacía")}{(Plural ? "s" : "")}.";
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
            ["body"] = new SearchSpot("el cuerpo", new string[0], masculine: true, open: false, body: true),
            // The police station's and the medical furniture, searched one by one (Arturo, 18/11, cuadros 65 to 67; Gameplay's
            // G19, .dev-snapshots/gameplay/muebles/CONTRATO.md): Looting.SearchSpot gives the piece's own kind.
            [SearchPieces.WeaponRack] = new SearchSpot("el armero", new string[0], masculine: true, open: false),
            [SearchPieces.WeaponLocker] = new SearchSpot("el armario de armas", new string[0], masculine: true),
            [SearchPieces.GunCase] = new SearchSpot("los maletines de armas", new string[0], masculine: true, plural: true),
            [SearchPieces.AmmoCase] = new SearchSpot("las cajas de munición", new string[0], plural: true),
            [SearchPieces.LockerRow] = new SearchSpot("las taquillas", new string[0], plural: true),
            [SearchPieces.EvidenceShelf] = new SearchSpot("el estante de pruebas", new[] { "tabaco de contrabando", "móviles y relojes en bolsas de pruebas", "una caja de pilas y linternas" }, masculine: true, open: false),
            [SearchPieces.CorpseOfficer] = new SearchSpot("el cuerpo del agente", new string[0], masculine: true, open: false, body: true),
            [SearchPieces.MedicalCabinet] = new SearchSpot("el armario de medicinas", new string[0], masculine: true),
            [SearchPieces.MedicalShelf] = new SearchSpot("el estante de medicinas", new string[0], masculine: true, open: false),
            [SearchPieces.MedicalCart] = new SearchSpot("el carro de curas", new string[0], masculine: true, open: false),
            // Saqueadores' stash (G23.5, cuadro 98: orange crates on pallets; Looting.RollPiece): what the Cuervos took from others.
            [SearchPieces.StashCrate] = new SearchSpot("la caja del alijo", new[] { "latas de conserva y botellas de agua", "garrafas de gasolina", "tabaco, alcohol y pilas", "herramientas, cuerda y lonas" }),
        };

        /// <summary>What isn't in SearchSpots reads as a box.</summary>
        private static readonly SearchSpot AnyBox = new SearchSpot("la caja", new string[0]);

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
            // Two bottles, as the frag grenades are two (Content.Items; Arturo, 1/11). It had no find, and a molotov found in
            // a shop or a boot had no words (Lore, 7/11).
            ["molotov"] = "dos cócteles molotov",
        };

        /// <summary>
        /// Accessories piled in one slot (Loot.Count; Arturo, 7/11: «lo que es igual, sin límite»), counted as they are
        /// held: how many each find is, and the word for one and for several. Two finds of «dos granadas» are «4 granadas».
        /// </summary>
        private static readonly IReadOnlyDictionary<string, (int Each, string One, string Many)> ItemPiles = new Dictionary<string, (int Each, string One, string Many)>
        {
            ["fragGrenade"] = (2, "granada", "granadas"),
            ["molotov"] = (2, "cóctel molotov", "cócteles molotov"),
            ["smokeGrenade"] = (1, "granada de humo", "granadas de humo"),
            ["flashbang"] = (1, "granada aturdidora", "granadas aturdidoras"),
            ["firstAid"] = (1, "botiquín", "botiquines"),
            ["nanoVest"] = (1, "chaleco antibalas", "chalecos antibalas"),
            ["mindShield"] = (1, "par de tapones", "pares de tapones"),
            ["apRounds"] = (1, "caja de munición perforante", "cajas de munición perforante"),
            ["tracerRounds"] = (1, "caja de munición trazadora", "cajas de munición trazadora"),
        };

        /// <summary>«un botiquín», «3 botiquines», «dos granadas», «4 granadas»: `count` finds of the accessory `item`.</summary>
        public static string ItemsCount(string item, int count)
        {
            if (count <= 1) return ItemFinds[item];
            var pile = ItemPiles[item];
            var n = pile.Each * count;
            return $"{n} {(n == 1 ? pile.One : pile.Many)}";
        }

        /// <summary>A weapon's improvement found loose (Loot.Mod; Looting.FoundMod), with its article, and several of it.</summary>
        private static readonly IReadOnlyDictionary<string, (string One, string Many)> ModFinds = new Dictionary<string, (string One, string Many)>
        {
            ["scope"] = ("una mira", "miras"),
            ["stock"] = ("una culata", "culatas"),
            ["extMag"] = ("un cargador ampliado", "cargadores ampliados"),
            ["silencer"] = ("un silenciador", "silenciadores"),
        };

        /// <summary>«una mira», «2 miras»: `count` of the improvement `mod`. It had no words, and a search that found one said «Está vacía» (Lore, 7/11).</summary>
        public static string ModsCount(string mod, int count)
        {
            return count <= 1 ? ModFinds[mod].One : $"{count} {ModFinds[mod].Many}";
        }

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
            if (!string.IsNullOrEmpty(find.Item)) parts.Add(ItemsCount(find.Item!, Looting.CountOf(find)));
            if (!string.IsNullOrEmpty(find.Mod)) parts.Add(ModsCount(find.Mod!, Looting.CountOf(find)));
            if (find.Medicines.GetValueOrDefault() != 0) parts.Add(find.Medicines == 1 ? MedicineFinds[seed % MedicineFinds.Count] : $"{MedicineFinds[seed % MedicineFinds.Count]} ({find.Medicines} ✚)");
            if (find.Food.GetValueOrDefault() != 0) parts.Add($"{FoodFinds[seed % FoodFinds.Count]} ({Rations(find.Food!.Value)})");
            if (find.Parts.GetValueOrDefault() != 0) parts.Add($"{PartsFinds[seed % PartsFinds.Count]} ({PartsCount(find.Parts!.Value)})");
            if (!string.IsNullOrEmpty(find.Weapon)) parts.Add(Content.FoundWeapons[find.Weapon!].Find);
            if (find.Mags.GetValueOrDefault() != 0) parts.Add(find.Mags == 1 ? MagFinds[seed % MagFinds.Count] : MagsCount(find.Mags!.Value));
            // Arco y ballesta: a box's bundle, or those had back from a body (and a Cazador's own).
            if (find.Arrows.GetValueOrDefault() != 0) parts.Add(ArrowsText.Count(find.Arrows!.Value));
            return parts;
        }

        /// <summary>
        /// The log line for opening a marked box or a boot, or searching a body: «Ana abre la caja de armas: un fusil de
        /// asalto.»
        /// </summary>
        public static string SearchLine(string who, string decor, Loot? find, int seed)
        {
            var spot = SearchSpots.TryGetValue(decor, out var known) ? known : AnyBox;
            var parts = FindParts(find, spot.Supplies, seed);
            return parts.Count > 0 ? $"{who} {spot.Does} {spot.Name}: {JoinAnd(parts)}." : $"{who} {spot.Does} {spot.Name}. {spot.Empty}";
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
        /// chances (Spots.Trunk; Looting.PoliceBootGun), a body's gun (Looting.RollBody, Content.Uniforms), a piece of
        /// furniture's own (G19).
        /// </summary>
        public static string SearchEffect(string decor)
        {
            switch (decor)
            {
                case Looting.CarBoot:
                    return "Abre el maletero de un coche que tengas al lado. Sale lo que haya: suministros, piezas, medicinas, algún cargador o accesorio y, a veces, un arma. Hace un poco de ruido.";
                case Looting.PoliceBoot:
                    return "Abre el maletero de un coche de policía que tengas al lado. Una vez de cada cinco hay un arma; si no, lo que haya en cualquier maletero. A veces, también un chaleco. Hace un poco de ruido.";
                case Looting.Body:
                    return "Registra el cuerpo de un enemigo caído a tu lado o a tus pies. Los vivos llevan siempre su arma. Un zombi casi nunca lleva una, salvo los policías y los militares: uno de cada cuatro. Hace un poco de ruido.";
                // The furniture searched one by one (G19, CONTRATO.md §3): only one with something is marked, so the racks and
                // the medicine cabinets «dan siempre».
                case SearchPieces.WeaponRack:
                    return "Registra un armero que tengas al lado. Da siempre un arma larga: un fusil de asalto o una escopeta. Hace un poco de ruido.";
                case SearchPieces.WeaponLocker:
                    return "Abre un armario de armas que tengas al lado. Da siempre una pistola. Hace un poco de ruido.";
                case SearchPieces.GunCase:
                    return "Abre unos maletines de armas que tengas al lado. Dan siempre una pistola o un accesorio de arma. Hace un poco de ruido.";
                case SearchPieces.AmmoCase:
                    return "Abre unas cajas de munición que tengas al lado. Dan siempre dos cargadores. Hace un poco de ruido.";
                case SearchPieces.LockerRow:
                    return "Abre unas taquillas que tengas al lado. Puede haber un chaleco o un cargador, o nada. Hace un poco de ruido.";
                case SearchPieces.EvidenceShelf:
                    return "Registra un estante de pruebas que tengas al lado. Puede haber suministros o algo para el cinturón, o nada. Hace un poco de ruido.";
                case SearchPieces.CorpseOfficer:
                    return "Registra el cuerpo de un agente que tengas al lado. A veces lleva una pistola o un chaleco. Hace un poco de ruido.";
                case SearchPieces.MedicalCabinet:
                    return "Abre un armario de medicinas que tengas al lado. Da siempre una medicina o un botiquín. Hace un poco de ruido.";
                case SearchPieces.MedicalShelf:
                    return "Registra un estante de medicinas que tengas al lado. Puede haber una medicina o un botiquín, o nada. Hace un poco de ruido.";
                case SearchPieces.MedicalCart:
                    return "Registra un carro de curas que tengas al lado. Puede haber una medicina, o nada. Hace un poco de ruido.";
                // G23.5: a long gun with a magazine one time in two, else twice a box's supplies and a medicine (kept after the
                // R2, Gameplay and Producto, 21/11).
                case SearchPieces.StashCrate:
                    return "Abre una caja del alijo que tengas al lado. Da siempre algo: un arma larga con un cargador, o suministros y una medicina. Hace un poco de ruido.";
                default:
                    return "Abre una caja marcada que tengas al lado. Da siempre lo suyo: un arma, una o dos medicinas, dos cargadores o de 3 a 5 raciones. Hace un poco de ruido.";
            }
        }

        /// <summary>Opening a marked box, for the action's title: «Abrir la caja de armas»; a body's, «Registrar el cuerpo»; a rack's, «Registrar el armero».</summary>
        public static string BoxTitle(string decor)
        {
            var spot = SearchSpots.TryGetValue(decor, out var known) ? known : AnyBox;
            return $"{spot.Verb} {spot.Name}";
        }

        /// <summary>
        /// Why Registrar can't be done (Abilities, Search): nothing beside to search, or what is beside already searched (it said
        /// «No hay ninguna caja al lado.» and «Ya está abierta.», for a boot, a body and the furniture too).
        /// </summary>
        public const string NothingBeside = "No hay nada que registrar a tu lado.";
        public const string SearchedBeside = "Lo que tienes al lado ya está registrado.";

        /// <summary>Why a room with furniture searched one by one isn't searched whole (SearchRoom; G19, cuadro 65).</summary>
        public const string RoomByPieces = "En esta habitación se registra mueble a mueble.";

        /// <summary>What a search turned up, short, to float over the furniture: «+12 §», «Botiquín», «Mira», «Medicinas», «+2 raciones», «+1 pieza», a weapon's name, «+1 cargador» or «Nada».</summary>
        public static string FindLabel(Loot? find)
        {
            var parts = new List<string>();
            if (find != null)
            {
                if (find.Supplies.GetValueOrDefault() != 0) parts.Add($"+{find.Supplies} §");
                if (!string.IsNullOrEmpty(find.Item)) parts.Add(Capital(Regex.Replace(ItemsCount(find.Item!, Looting.CountOf(find)), "^(una?|unos|unas) ", "")));
                if (!string.IsNullOrEmpty(find.Mod)) parts.Add(Capital(Regex.Replace(ModsCount(find.Mod!, Looting.CountOf(find)), "^(una?|unos|unas) ", "")));
                if (find.Medicines.GetValueOrDefault() != 0) parts.Add("Medicinas");
                if (find.Food.GetValueOrDefault() != 0) parts.Add($"+{Rations(find.Food!.Value)}");
                if (find.Parts.GetValueOrDefault() != 0) parts.Add($"+{PartsCount(find.Parts!.Value)}");
                if (!string.IsNullOrEmpty(find.Weapon)) parts.Add(Content.FoundWeapons[find.Weapon!].Name);
                if (find.Mags.GetValueOrDefault() != 0) parts.Add($"+{MagsCount(find.Mags!.Value)}");
                if (find.Arrows.GetValueOrDefault() != 0) parts.Add($"+{ArrowsText.Count(find.Arrows!.Value)}");
            }
            var label = string.Join(" · ", parts);
            return label.Length > 0 ? label : "Nada";
        }
    }
}
