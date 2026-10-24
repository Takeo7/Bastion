using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // -------------------------------------------------------------------- base

        /// <summary>
        /// The base in the clearing (docs/FASE12.md §11; the draft in docs/LORE-BASE.md):
        /// each piece's name, what it is for (the figures come from base.ts), and a
        /// building's line of the world at each of its levels.
        /// </summary>
        public sealed class PieceText
        {
            public PieceText(string name, string the, string a, string line, IReadOnlyList<string>? levels = null)
            {
                Name = name;
                The = the;
                A = a;
                Line = line;
                Levels = levels;
            }

            public string Name { get; }
            /// <summary>With its article, for a sentence: «Repara la empalizada», «mejorar la Sala de radio»; buildings keep their capital, as names.</summary>
            public string The { get; }
            /// <summary>«construir una empalizada», «un Barracón».</summary>
            public string A { get; }
            public string Line { get; }
            /// <summary>A building's line of the world at each of its three levels.</summary>
            public IReadOnlyList<string>? Levels { get; }
        }

        /// <summary>PIECE_TEXT: a nested class, so the bus's name is ready when the table is.</summary>
        public static class PieceTexts
        {
            public static readonly IReadOnlyDictionary<string, PieceText> All = new Dictionary<string, PieceText>
            {
                ["caravan"] = new PieceText("Caravana", "la caravana", "una caravana", "Camas para los primeros soldados y el almacén. No se desmonta."),
                ["radio"] = new PieceText("Sala de radio", "la Sala de radio", "una Sala de radio", "Avisa de los asedios, por dónde y cuántos, y trae misiones y zonas que contactar.", new[] { "Una radio de coche, una batería y Morse con los cascos.", "Una antena en el pino más alto.", "La antena llega hasta el mar: Morse oye antes lo que viene." }),
                ["quarters"] = new PieceText("Barracón", "el Barracón", "un Barracón", "Camas para soldados.", new[] { "Literas de madera y una estufa que solo se enciende de noche.", "Un segundo piso de literas.", "Mantas de verdad." }),
                ["infirmary"] = new PieceText("Enfermería", "la Enfermería", "una Enfermería", "Las heridas curan antes.", new[] { "Una camilla y un hornillo para hervir el agua.", "Agua caliente en la sala de curas.", "Un quirófano de verdad, con lámpara y generador propio." }),
                ["workshop"] = new PieceText("Taller", "el Taller", "un Taller", "Accesorios y armas más baratos.", new[] { "Un banco de trabajo y las herramientas de Teo.", "Un torno de verdad.", "Teo deja de quejarse." }),
                ["garden"] = new PieceText("Huerto", "el Huerto", "un Huerto", "Da 5 raciones por misión a nivel 1, 10 a nivel 2 y 15 a nivel 3.", new[] { "Bancales bajo plástico y bidones para el agua de lluvia.", "Un invernadero de verdad, con estufa.", "Riego por goteo y semillas de La Vega." }),
                ["quarantine"] = new PieceText("Cuarentena", "la Cuarentena", "una Cuarentena", $"Sin ella, un mordido no sube {Bus.To}.", new[] { "Una nave aparte, con cerrojo por fuera.", "Catres separados y una ventana con rejas.", "Una sala de guardia, y alguien despierto toda la noche." }),
                ["memorial"] = new PieceText("Camposanto", "el Camposanto", "un Camposanto", "Los caídos descansan aquí y salen de la lista de Soldados; sus nombres se guardan.", new[] { "Cruces de pino bajo la nieve, una por cada uno.", "Una cerca y un banco.", "Los nombres, grabados en una tabla." }),
                ["range"] = new PieceText("Campo de tiro", "el Campo de tiro", "un Campo de tiro", "Puntería para todos.", new[] { "Dianas de cartón en una trinchera del pinar.", "Siluetas de zombi a cincuenta metros.", "Blancos móviles, con una polea de Teo." }),
                ["training"] = new PieceText("Centro de entrenamiento", "el Centro de entrenamiento", "un Centro de entrenamiento", "Los reclutas llegan sabiendo más.", new[] { "Un patio despejado y un horario.", "Una semana de prácticas antes de salir.", "Alguien que ya ha vuelto de fuera les enseña." }),
                ["simulation"] = new PieceText("Sala de mapas", "la Sala de mapas", "una Sala de mapas", "Las misiones dan más suministros.", new[] { "Un tablero con los caminos del valle.", "Cada casa registrada, marcada con una chincheta.", "Las rutas de los Camioneros, y dónde paraban." }),
                ["garage"] = new PieceText("Chapistería", "la Chapistería", "una Chapistería", "Las heridas curan un día antes por nivel, y la mochila lleva un hueco más.", new[] { $"{Bus.Name} lleva camilla.", "Mantas y un botiquín fijo atrás.", $"{Bus.Name} ya no para a repostar con heridos dentro." }),
                ["echoes"] = new PieceText("Taller de oficios", "el Taller de oficios", "un Taller de oficios", "Las barras se llenan más en cada misión.", new[] { "Lo que se aprende fuera, aquí se repasa.", "Más herramientas, y Nwosu repasa las curas.", "Gente de otros enclaves enseña lo suyo." }),
                ["palisade"] = new PieceText("Empalizada", "la empalizada", "una empalizada", "Barata y débil: cede pronto."),
                ["wall"] = new PieceText("Muro", "el muro", "un muro", "Caro, pero aguanta mucho."),
                ["gate"] = new PieceText("Puerta", "la puerta", "una puerta", "Se abre y se cierra con una acción. Cerrada, los muertos tienen que tirarla."),
                ["sandbags"] = new PieceText("Barricada de sacos", "la barricada de sacos", "una barricada de sacos", "Cobertura para disparar detrás."),
                ["logs"] = new PieceText("Pila de troncos", "la pila de troncos", "una pila de troncos", "Cobertura baja. Lo más barato."),
                ["tower"] = new PieceText("Torre de vigía", "la torre de vigía", "una torre de vigía", "Una planta de altura: más vista, y ventaja de altura al disparar."),
                ["floodlight"] = new PieceText("Foco", "el foco", "un foco", "De noche, alumbra a su alrededor: ahí se ve como de día."),
                ["wire"] = new PieceText("Alambre de espino", "el alambre de espino", "un tramo de alambre de espino", "Quien lo cruza va más despacio, vivo o muerto."),
                ["mine"] = new PieceText("Mina", "la mina", "una mina", "Estalla una vez, cuando algo la pisa. Se repone con piezas."),
                ["spikes"] = new PieceText("Foso con pinchos", "el foso con pinchos", "un foso con pinchos", "Hiere a quien cae dentro. Aguanta unas cuantas veces; luego se repone con piezas."),
                ["snare"] = new PieceText("Cepo", "el cepo", "un cepo", "Atrapa al primero que lo pisa y lo retiene. Se repone con piezas."),
                ["turret"] = new PieceText("Torreta", "la torreta", "una torreta", "Dispara sola, en el turno enemigo, a lo que se acerque. Sin munición se calla; se recarga con piezas."),
            };
        }

        /// <summary>A building's figure at a level: the number alone, and its line.</summary>
        private sealed class Figure
        {
            public Figure(string @short, string full)
            {
                Short = @short;
                Full = full;
            }

            public string Short { get; }
            public string Full { get; }
        }

        /// <summary>
        /// What a building gives at a level, for its card (Ana, 21/10 #42: the figure
        /// first, big): `short` for the ladder of levels («+5»), `full` for the line
        /// («+5 raciones por misión»). The engine sums them over the base (campaign.ts:
        /// gardenYield, beds, healRate, shopPrice, rangeAim, rewardMultiplier,
        /// garageCut, memorialWill, the training's seasoned, learningBonus,
        /// contactCapacity, the Cuarentena's fever days); these are one building on its
        /// own, and change with them. Defences, traps and the caravan count nothing.
        /// </summary>
        private static Figure? PieceFigureOf(string piece, int l)
        {
            switch (piece)
            {
                case "garden": return new Figure($"+{5 * l}", $"+{5 * l} raciones por misión");
                case "quarters": return new Figure($"+{1 + l}", $"+{1 + l} camas");
                case "infirmary": return new Figure($"×{1 + l}", $"Heridas: curan {1 + l} veces más rápido"); // Fixed in C# only (text-fixes.ts): lore.ts says «curan ×3 de rápido»
                case "workshop": return new Figure($"−{25 + 10 * (l - 1)} %", $"Accesorios −{25 + 10 * (l - 1)} %");
                case "range":
                {
                    var aim = l >= 1 && l <= 3 ? new[] { 3, 6, 10 }[l - 1] : 10;
                    return new Figure($"+{aim}", $"Puntería +{aim}");
                }
                case "simulation": return new Figure($"+{25 + 15 * (l - 1)} %", $"Suministros +{25 + 15 * (l - 1)} %");
                case "garage": return new Figure($"−{l}", $"Heridas: −{l} {(l == 1 ? "día" : "días")}");
                case "memorial": return l >= 2 ? new Figure($"+{(l == 2 ? 5 : 10)}", $"Voluntad +{(l == 2 ? 5 : 10)}") : null;
                case "quarantine": return l >= 2 ? new Figure($"+{l - 1}", $"Fiebre: +{l - 1} {(l == 2 ? "día" : "días")}") : null;
                case "training": return new Figure($"+{l}", $"Reclutas: +{l} {(l == 1 ? "nivel" : "niveles")}");
                case "echoes": return new Figure($"+{20 * l} %", $"Barras +{20 * l} %");
                case "radio": return new Figure($"+{1 + l}", $"+{1 + l} contactos");
                default: return null;
            }
        }

        /// <summary>A building's figure at `level`, for its card: «+5 raciones por misión»; null when it counts nothing.</summary>
        public static string? PieceFigure(string piece, int level)
        {
            return PieceFigureOf(piece, level)?.Full;
        }

        /// <summary>The same, the number alone, for the ladder of levels: «+5», «×2», «−25 %».</summary>
        public static string? PieceFigureShort(string piece, int level)
        {
            return PieceFigureOf(piece, level)?.Short;
        }

        /// <summary>
        /// A line of 40 letters at most for each new thing a level brings, under its
        /// name in the diary (Ana, 23/10 #38: «una muuuy pequeña descripción»). Keys:
        /// `piece:<PieceId>`, `tech:<TechId>`, `item:<ItemId>`; no full stop.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NewBrief = new Dictionary<string, string>
        {
            ["piece:caravan"] = "Camas y almacén",
            ["piece:radio"] = "Avisa de los asedios y trae misiones",
            ["piece:quarters"] = "Camas para soldados",
            ["piece:infirmary"] = "Las heridas curan antes",
            ["piece:workshop"] = "Armas y accesorios más baratos",
            ["piece:garden"] = "Raciones en cada misión",
            ["piece:quarantine"] = "Sin ella, un mordido no sube a La Lata",
            ["piece:memorial"] = "Los caídos salen de la lista",
            ["piece:range"] = "Puntería para todos",
            ["piece:training"] = "Reclutas con más nivel",
            ["piece:simulation"] = "Más suministros por misión",
            ["piece:garage"] = "Heridas más cortas y +1 en la mochila",
            ["piece:echoes"] = "Las barras se llenan más",
            ["piece:palisade"] = "Barata y débil",
            ["piece:wall"] = "Caro, pero aguanta mucho",
            ["piece:gate"] = "Se abre y se cierra",
            ["piece:sandbags"] = "Cobertura para disparar",
            ["piece:logs"] = "Cobertura baja y barata",
            ["piece:tower"] = "Más vista y ventaja de altura",
            ["piece:floodlight"] = "De noche, alumbra a su alrededor",
            ["piece:wire"] = "Quien lo cruza va más despacio",
            ["piece:mine"] = "Estalla al pisarla; un uso",
            ["piece:spikes"] = "Hiere a quien cae dentro",
            ["piece:snare"] = "Atrapa al primero que lo pisa",
            ["piece:turret"] = "Dispara sola a lo que se acerque",
            ["tech:magWeapons"] = "El Taller hace culatas y silenciadores",
            ["tech:plateArmor"] = "El Taller hace armadura: +2 de salud",
            ["tech:plasmaGrenades"] = "Las granadas hacen +2 de daño",
            ["tech:encryption"] = "Arreglar +20 y un contacto más",
            ["tech:plasmaWeapons"] = "El Taller hace miras y cargadores",
            ["tech:nanoMedkit"] = "Curas +2 y botiquines con un uso más",
            ["tech:predatorArmor"] = "Armadura: +4 de salud y +1 de blindaje",
            ["tech:signal"] = "Abre la misión final",
            ["item:fragGrenade"] = "Dos granadas para el cinturón",
            ["item:smokeGrenade"] = "Humo: −20 a acertar a quien está dentro",
            ["item:firstAid"] = "Cura 4; dos usos",
            ["item:flashbang"] = "Desorienta a los enemigos de la zona",
            ["item:tracerRounds"] = "+10 de puntería",
            ["item:nanoVest"] = "+2 de salud",
            ["item:apRounds"] = "Ignora 1 de blindaje",
            ["item:mindShield"] = "El grito del Chillón no afecta",
        };

        /// <summary>A piece's state, a tag beside its name («Foso con pinchos · por reponer») and its line in the card; never gendered, the names are of both.</summary>
        public static class PieceState
        {
            public const string Spent = "por reponer";
            public const string SpentLine = "No le quedan usos: se repone con piezas.";
            public const string Silent = "sin munición";
            public const string SilentLine = "No dispara hasta que se recargue con piezas.";
            public const string Broken = "por reparar";
            public const string BrokenLine = "No sirve hasta que se repare.";

            /// <summary>PIECE_STATE by its key: "spent", "silent", "broken" and their lines.</summary>
            public static readonly IReadOnlyDictionary<string, string> ByKey = new Dictionary<string, string>
            {
                ["spent"] = Spent,
                ["spentLine"] = SpentLine,
                ["silent"] = Silent,
                ["silentLine"] = SilentLine,
                ["broken"] = Broken,
                ["brokenLine"] = BrokenLine,
            };
        }

        /// <summary>The base screen (UI: strategy/buildMode.ts, clearing.ts, screen.ts): its labels and its help.</summary>
        public static class BuildText
        {
            public const string Build = "Construir";
            public const string Fell = "Talar";
            public const string Repair = "Reparar";
            public const string Dismantle = "Desmontar";
            public const string Rotate = "Girar";
            public const string Upgrade = "Mejorar";
            public const string Rearm = "Reponer";

            /// <summary>The palette's panel titles.</summary>
            public static class Tabs
            {
                public const string Building = "Edificios";
                public const string Defence = "Defensas";
            }

            public const string PaletteHelp = "Elige una pieza y ponla en el claro: R la gira; Esc o el botón derecho la sueltan.";
            /// <summary>The bottom line, by what is in hand: felling, or nothing (a piece in hand: HoldingLine).</summary>
            public const string Felling = "Arrastra sobre los árboles que quieras talar. Esc para dejarlo.";
            public const string NoTrees = "Aquí no hay árboles: arrastra sobre el pinar.";
            /// <summary>A piece with the partner connected: its tag (it goes to a vote).</summary>
            public const string Vote = "se propone";
            /// <summary>Dismantling's confirmation, and its two buttons.</summary>
            public const string DismantleAsk = "¿Desmontar?";
            public const string DismantleYes = "Desmontar";
            public const string DismantleNo = "Dejarlo";
        }

        /// <summary>BUILD_TEXT[action], for the card's buttons: "repair", "upgrade", "rearm", "dismantle".</summary>
        private static string BuildAction(string action)
        {
            switch (action)
            {
                case "repair": return BuildText.Repair;
                case "upgrade": return BuildText.Upgrade;
                case "rearm": return BuildText.Rearm;
                case "dismantle": return BuildText.Dismantle;
                default: throw new ArgumentException($"Unknown build action: {action}", nameof(action));
            }
        }

        /// <summary>One of the Bastion's levels: its name, and with its articles.</summary>
        public sealed class BastionLevel
        {
            public BastionLevel(string name, string the, string a)
            {
                Name = name;
                The = the;
                A = a;
            }

            public string Name { get; }
            public string The { get; }
            public string A { get; }
        }

        /// <summary>
        /// The Bastion's eight levels by name, never by number (playtest 20/10 #13):
        /// the four buildings it grows through (base.ts BASTION_STAGES, Arte's), each
        /// raised once, and the raised one told apart by what it gains.
        /// </summary>
        public static readonly IReadOnlyList<BastionLevel> BastionLevels = new[]
        {
            new BastionLevel("Caravana", "la caravana", "una caravana"),
            new BastionLevel("Caravana con toldo", "la caravana con toldo", "una caravana con toldo"),
            new BastionLevel("Casa móvil", "la casa móvil", "una casa móvil"),
            new BastionLevel("Casa móvil con porche", "la casa móvil con porche", "una casa móvil con porche"),
            new BastionLevel("Albergue", "el albergue", "un albergue"),
            new BastionLevel("Albergue ampliado", "el albergue ampliado", "un albergue ampliado"),
            new BastionLevel("Casa", "la casa", "una casa"),
            new BastionLevel("Casa fortificada", "la casa fortificada", "una casa fortificada"),
        };

        private static BastionLevel BastionLevelOf(int level)
        {
            return BastionLevels[Math.Max(1, Math.Min(BastionLevels.Count, level)) - 1];
        }

        /// <summary>«Casa móvil»: the Bastion's level as it is shown.</summary>
        public static string BastionLevelName(int level)
        {
            return BastionLevelOf(level).Name;
        }

        /// <summary>With its article, for a sentence: «la casa móvil», «una casa móvil».</summary>
        public static string BastionLevelThe(int level)
        {
            return BastionLevelOf(level).The;
        }

        public static string BastionLevelA(int level)
        {
            return BastionLevelOf(level).A;
        }

        /// <summary>The Bastion's level, bought now (playtest 18/10 #40: suministros and missions done, no experience).</summary>
        public static class LevelText
        {
            public const string About = "Cada nivel del Bastión abre piezas nuevas para construir y el siguiente capítulo de la historia.";
            public const string Top = "El Bastión ya está en su nivel más alto.";
        }

        /// <summary>The button's line: «Subir a la casa móvil: 160 §.» (what blocks it is levelUpBlocker's, in campaign.ts).</summary>
        public static string LevelUpLine(int level, int cost)
        {
            return $"Subir a {BastionLevelThe(level)}: {cost} §.";
        }

        /// <summary>The mark at the top, where «DÍA n» was (§11.6): «7 SALIDAS», «1 SALIDA», «AÚN SIN SALIR».</summary>
        public static string OutingsMark(int n)
        {
            if (n <= 0) return "AÚN SIN SALIR";
            return n == 1 ? "1 SALIDA" : $"{n} SALIDAS";
        }

        /// <summary>Its tooltip: time is missions now, and building is free of it (`level`: the Bastion's, for the doom's name).</summary>
        public static string TimeHelp(int level)
        {
            return $"Cada misión es un día, y esperar uno también: se curan las heridas, se come y {DoomSubject(level)} avanza. Construir, comprar o reclutar no gasta tiempo.";
        }

        /// <summary>What locks a piece, an upgrade or a craft, by the Bastion's level: «Se desbloquea con la casa móvil.»; the palette's tag is its name (BastionLevelName).</summary>
        public static string NeedsLevelLine(int level)
        {
            return $"Se desbloquea con {BastionLevelThe(level)}.";
        }

        /// <summary>The bottom line with a piece in hand: «Empalizada: clic para poner, R para girar, Esc para soltar.»</summary>
        public static string HoldingLine(string piece)
        {
            return $"{PieceTexts.All[piece].Name}: clic para poner, R para girar, Esc para soltar.";
        }

        /// <summary>By the pointer, where it fits: «Empalizada · Cuesta 6 §», « · se propone» when dear; where it doesn't: «Sala de radio · Hay árboles: tala antes.»</summary>
        public static string PlaceHint(string piece, int supplies, int parts = 0, bool vote = false, string? blocked = null)
        {
            var name = PieceTexts.All[piece].Name;
            if (!string.IsNullOrEmpty(blocked)) return $"{name} · {blocked}";
            return $"{name} · {BuildCostLine(supplies, parts)}{(vote ? $" · {BuildText.Vote}" : "")}";
        }

        /// <summary>
        /// By the pointer, felling: «Talar un árbol · Cuesta 3 §», «Talar 12 árboles · Cuesta 36 §». lore.ts said only
        /// «· 3 §», which doesn't say whether it is paid or earned; «Cuesta», as PlaceHint (Gameplay, 24/10).
        /// </summary>
        public static string FellHint(int trees)
        {
            return $"Talar {(trees == 1 ? "un árbol" : $"{trees} árboles")} · {BuildCostLine(trees * Base.FellCost)}";
        }

        /// <summary>A piece under the pointer: «Barricada de sacos · 5/15», «Foso con pinchos · por reponer», «Taller · nivel 2». `state`: a PieceState key.</summary>
        public static string PieceHint(string piece, (int Left, int Full)? hp = null, string? state = null, int level = 0)
        {
            var name = PieceTexts.All[piece].Name;
            if (!string.IsNullOrEmpty(state)) return $"{name} · {PieceState.ByKey[state!]}";
            if (level != 0) return $"{name} · nivel {level}";
            return hp.HasValue ? $"{name} · {hp.Value.Left}/{hp.Value.Full}" : name;
        }

        /// <summary>The card's figures: «Aguante 12/30», «Usos 0/3».</summary>
        public static string HoldsLine(int hp, int full)
        {
            return $"Aguante {hp}/{full}";
        }

        public static string UsesLine(int left, int full)
        {
            return $"Usos {left}/{full}";
        }

        /// <summary>The card's buttons with their price: «Reparar · 4 §», «Mejorar · 60 §», «Reponer · 1 pieza», «Desmontar · +12 §». `action`: "repair", "upgrade", "rearm", "dismantle".</summary>
        public static string ActionPrice(string action, int supplies = 0, int parts = 0)
        {
            var what = string.Join(" y ", new[] { supplies != 0 ? $"{(action == "dismantle" ? "+" : "")}{supplies} §" : "", parts != 0 ? PartsCount(parts) : "" }.Where(s => s.Length > 0));
            return what.Length > 0 ? $"{BuildAction(action)} · {what}" : BuildAction(action);
        }

        /// <summary>Under «Mejorar»: what the next level brings, as the world sees it: «Nivel 2: Un torno de verdad.»</summary>
        public static string UpgradeLine(string piece, int to)
        {
            var levels = PieceTexts.All[piece].Levels;
            var line = levels != null && to >= 1 && to <= levels.Count ? levels[to - 1] : null;
            return !string.IsNullOrEmpty(line) ? $"Nivel {to}: {line}" : $"Nivel {to}";
        }

        /// <summary>Dismantling's confirmation line: «Se recuperan 12 §.», «Se recupera 1 §.», «No se recupera nada.»</summary>
        public static string DismantleBack(int supplies)
        {
            if (supplies <= 0) return "No se recupera nada.";
            return supplies == 1 ? "Se recupera 1 §." : $"Se recuperan {supplies} §.";
        }

        /// <summary>«Cuesta 15 § y 1 pieza», «Cuesta 6 §».</summary>
        public static string BuildCostLine(int supplies, int parts = 0)
        {
            return parts != 0 ? $"Cuesta {supplies} § y {PartsCount(parts)}" : $"Cuesta {supplies} §";
        }

        /// <summary>What is short: «Faltan 5 §», «Falta 1 pieza», «Faltan 5 § y 2 piezas».</summary>
        public static string BuildShortLine(int supplies, int parts = 0)
        {
            var what = new[] { supplies > 0 ? $"{supplies} §" : "", parts > 0 ? PartsCount(parts) : "" }.Where(s => s.Length > 0).ToList();
            var one = what.Count == 1 && (supplies == 1 || (supplies == 0 && parts == 1));
            return $"{(one ? "Falta" : "Faltan")} {string.Join(" y ", what)}";
        }

        /// <summary>
        /// What the rations are for (playtest 18/10 #37), for the top mark's tooltip:
        /// a ration a soldier a day, and a mission is one day (playtest 21/10 #34).
        /// The figures are on the bar above it (UI), so none here.
        /// </summary>
        public static string RationsHelp(int? perMission = null)
        {
            return "Cada soldado come una ración al día; una misión es un día. Sin raciones hay hambre: −10 de voluntad en las misiones y las heridas curan a la mitad de velocidad.";
        }

        /// <summary>The agenda's line when there is food for less than two missions (`missions`: how many it lasts, 0 or 1).</summary>
        public static string RationsLowLine(int missions)
        {
            return missions >= 1 ? "Hay raciones para una misión más: traed comida o levantad un Huerto." : "No llegan las raciones para la próxima misión: traed comida o levantad un Huerto.";
        }

        /// <summary>When the next siege comes, in missions (§11.6): «tras la próxima misión», «dentro de 3 misiones».</summary>
        public static string InMissions(int n)
        {
            return n <= 1 ? "tras la próxima misión" : $"dentro de {n} misiones";
        }

        /// <summary>How many, as the radio hears it: never the exact count.</summary>
        private static string Roughly(int n)
        {
            if (n < 8) return "unos pocos";
            if (n >= 50) return "más de cincuenta";
            return $"unos {new[] { "diez", "veinte", "treinta", "cuarenta" }[Math.Min(3, (int)Js.Round(n / 10.0) - 1)]}";
        }

        /// <summary>Map edges as the radio says them: «por el norte y el este»; all four, «por todos lados».</summary>
        private static string BySides(IReadOnlyList<int> sides)
        {
            // [...new Set(sides)].sort() in JS: sorted as text.
            var names = sides.Distinct().OrderBy(i => i.ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal).Select(i => $"el {Sides[i]}").ToList();
            if (names.Count >= 4) return "por todos lados";
            return $"por {(names.Count > 1 ? $"{string.Join(", ", names.Take(names.Count - 1))} y {names[names.Count - 1]}" : names[0])}";
        }

        /// <summary>
        /// The siege's warning (§11.5): without a Sala de radio, only when; with one,
        /// how many and by where. «Viene un asedio: llegará dentro de 3 misiones.»,
        /// «Por la radio: unos veinte, por el norte, dentro de 3 misiones.»
        /// `count` and `sides`: what the radio heard, both or neither.
        /// </summary>
        public static string SiegeWarning(int missions, int? count = null, IReadOnlyList<int>? sides = null)
        {
            if (!count.HasValue || sides == null) return $"Viene un asedio: llegará {InMissions(missions)}.";
            return $"Por la radio: {Roughly(count.Value)}, {BySides(sides)}, {InMissions(missions)}.";
        }

        /// <summary>The same, short for the strip: «ASEDIO EN 3 MISIONES», «ASEDIO TRAS LA PRÓXIMA».</summary>
        public static string SiegeWarningShort(int missions)
        {
            return missions <= 1 ? "ASEDIO TRAS LA PRÓXIMA" : $"ASEDIO EN {missions} MISIONES";
        }

        /// <summary>One of La Lata's vehicles: its name, with its articles, and its line.</summary>
        public sealed class Vehicle
        {
            public Vehicle(string name, string the, string a, bool female, string line)
            {
                Name = name;
                The = the;
                A = a;
                Female = female;
                Line = line;
            }

            public string Name { get; }
            public string The { get; }
            public string A { get; }
            public bool Female { get; }
            public string Line { get; }
        }

        /// <summary>
        /// La Lata as it grows (Arturo, 18/10 #45; Lore's story: the bus stayed at
        /// Atalaya, and they fled in Teo's 4×4): the vehicle by `c.vehicle`. Lore
        /// writes the lines; these are the names.
        /// </summary>
        public static readonly IReadOnlyList<Vehicle> Vehicles = new[]
        {
            new Vehicle("Todoterreno", "el todoterreno", "un todoterreno", false, "El de Teo, el que tiró de la caravana. Cuatro plazas y el maletero."),
            new Vehicle("Furgoneta", "la furgoneta", "una furgoneta", true, "De reparto, con las ventanillas tapadas con chapa."),
            new Vehicle("Camión", "el camión", "un camión", false, "De mudanzas: la caja atrás, con bancos y una trampilla."),
            new Vehicle("Autobús", "el autobús", "un autobús", false, "El de línea de Atalaya, con la chapa que Teo le soldó. La Lata de verdad."),
        };

        /// <summary>`VEHICLES[tier] ?? VEHICLES[1]`.</summary>
        private static Vehicle VehicleOf(int tier)
        {
            return tier >= 0 && tier < Vehicles.Count ? Vehicles[tier] : Vehicles[1];
        }

        /// <summary>A zone's name after «en»: «en el Pantano Gris», «en La Vega».</summary>
        public static string InZone(string name)
        {
            return name.StartsWith("El ") ? $"en el {name.Substring(3)}" : $"en {name}";
        }

        /// <summary>The operation's name: «La furgoneta del Pantano Gris», «El camión de La Vega»; the bus's is always «El autobús de Atalaya».</summary>
        public static string VehicleMissionName(int tier, string zone)
        {
            if (tier >= 3) return "El autobús de Atalaya";
            var the = VehicleOf(tier).The;
            return $"{Capital(the)} {OfZone(zone)}";
        }

        /// <summary>An operation to bring back a better vehicle (#45), a Limpieza: who saw it, where, and that it is ours once the zone is clear.</summary>
        public static string VehicleBriefing(int tier, string zone)
        {
            if (tier >= 3) return "**El autobús** sigue en Atalaya, en la cochera, donde lo dejamos al huir. Desde la cuesta se ve entero. ==Despejad la zona== y La Lata vuelve a ser La Lata.";
            if (tier == 2) return $"Teo ha visto **un camión de mudanzas** {InZone(zone)}, con medio depósito. Arranca, dice, si alguien empuja. ==Despejad la zona:== el camión es nuestro.";
            return $"Teo ha visto **una furgoneta de reparto** entera {InZone(zone)}. Las ruedas aguantan. ==Despejad la zona:== la furgoneta es nuestra.";
        }

        private static readonly IReadOnlyList<string> Seats = new[] { "cero", "una", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez" };

        /// <summary>What La Lata is now, said when it is won (`seats`: soldiers it takes): «La Lata ya es una furgoneta: caben seis.»</summary>
        public static string VehicleWonLine(int tier, int seats)
        {
            var n = seats >= 0 && seats < Seats.Count ? Seats[seats] : seats.ToString();
            return tier >= 3 ? $"La Lata vuelve a ser el autobús: caben {n}." : $"La Lata ya es {VehicleOf(tier).A}: caben {n}.";
        }

        /// <summary>
        /// What a fallen soldier carried (#49): it stays on the ground, marked with
        /// whose it was, and anyone can search it. Never «caja»: those are the loot's.
        /// </summary>
        public static string FallenPackLine(string who)
        {
            return $"{who} cae. Su mochila se queda en el suelo, con sus armas.";
        }

        public static string FallenPackMark(string who)
        {
            return $"MOCHILA DE {who.Split(' ')[0].ToUpperInvariant()}";
        }

        public static string FallenPackAction(string who)
        {
            return $"Registrar la mochila de {who.Split(' ')[0]}";
        }

        /// <summary>«Mateo recoge lo que llevaba Chiara: un fusil de asalto y dos granadas.»</summary>
        public static string FallenPackTakenLine(string taker, string fallen, string what)
        {
            return $"{taker} recoge lo que llevaba {fallen.Split(' ')[0]}: {what}.";
        }

        /// <summary>Missions with the living (#47): the dead go for anyone alive, them too.</summary>
        public const string LivingTip = "Los muertos van a por cualquier vivo, también a por ellos, y el tiroteo los atrae.";

        /// <summary>When it comes, and when the base falls: with someone left (to another clearing) or with nobody (the campaign is over).</summary>
        public static class BaseSiegeText
        {
            public const string Here = "Ya están aquí.";
            /// <summary>The notice's line under its title, and the offer's text.</summary>
            public const string Arrival = "Salen del pinar y no dejan de salir. Coged las armas.";
            /// <summary>A siege by night (one in three from the second), after `arrival`: what the dark does, not its rule.</summary>
            public const string Night = "Es de noche: no se les ve hasta que están en la valla.";
            /// <summary>The rule, for the help and the errors (not for the notice).</summary>
            public const string Defenders = "Defienden los sanos. Los heridos y los mordidos se quedan dentro.";
            public const string Fallen = "El Bastión ha caído";
            public const string FallenLine = "Otra vez. Se pierde lo levantado y lo del almacén. Los que quedan huyen con la caravana y lo puesto, y vuelven a empezar en otro claro.";
            public const string Nobody = "No queda nadie";
            public const string NobodyLine = "El Bastión ha caído con todos dentro. La campaña termina aquí.";
        }
    }
}
