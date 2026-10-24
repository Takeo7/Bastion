using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base's screens with made-up views, as the 0.10.1 showed its sample campaign (day 20, the Bastion a «casa
    /// móvil», two players; .dev-snapshots/ui/estrategia-0101/): for captures next to it with the same framing.
    /// `BASTION_ESTRATEGIA` picks what is open: propuesta, propuesta-mia, espera, acontecimiento, acontecimiento-voto,
    /// diario, fin, menu (none: the plain base).
    /// </summary>
    public sealed class StrategyLab : MonoBehaviour, IStrategyHandlers
    {
        public string Case = "";
        StrategyRoot root;
        bool shown;

        void Start()
        {
            root = GetComponent<StrategyRoot>();
            if (string.IsNullOrEmpty(Case)) Case = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA") ?? "";
        }

        void Update()
        {
            if (shown || root == null || !root.Ready) return;
            shown = true;
            root.Bind(this);
            root.ClockOf = () => ("Quedan 24 s: si no contestas, cuenta como un sí.", 0.62f, false);
            root.SetTop(Top(Case.StartsWith("propuesta")));
            var screen = Case.StartsWith("armeria") ? "engineering" : Case == "escuadra" ? "squad" : Case == "parte" ? "debrief" : Case.StartsWith("mapa") ? "geoscape" : "barracks";
            root.ValleyCamera = Camera.main;
            root.SetNav(Nav(screen));
            // Soldados under everything else, as the 0.10.1's shots (Rafael, Ana's, picked while she is away).
            root.SetScreen(screen);
            if (screen == "engineering") root.SetArmory(Case == "armeria-arma" ? Gear() : Items());
            // The pads where the 0.10.1's shots have them (Arte's Settlement.PadWorld, projected, in the game).
            var squadPads = new[] { 454f, 606f, 835f, 986f };
            var debriefPads = new[] { 223f, 355f, 487f, 619f, 820f, 952f, 1085f, 1217f };
            root.PadAt = (i, n) => screen == "squad" ? new Vector2(squadPads[i % squadPads.Length], 497) : new Vector2(debriefPads[i % debriefPads.Length], 403);
            if (screen == "squad") root.SetSquad(Squad());
            if (screen == "debrief") root.SetDebrief(Debrief());
            if (screen == "geoscape") root.SetMap(Map(Case));
            var soldiers = Soldiers();
            if (Case == "personalizar") soldiers.Customizer = Customizer();
            root.SetSoldiers(soldiers);
            if (Case == "tablero") root.SetSkillBoard(new SkillBoardView { Kicker = "Tablero de habilidades · Novato", SoldierId = "s6", Name = "Rafael Costa", Rank = "Novato", RankFilled = 2, RankTotal = 5, Will = "+10 de voluntad", Points = 10, Editable = true, Board = { "steadyHand", "grip" } });
            root.SetProposal(Case == "propuesta" ? Proposal(false) : Case == "propuesta-mia" ? Proposal(true) : null, Case == "espera" ? new WaitingView { Text = "Ana está a punto para salir: El colegio de La Vega.", Button = "Ir a la Escuadra" } : null);
            if (Case.StartsWith("acontecimiento")) root.SetModal(Event(Case == "acontecimiento-voto"));
            if (Case == "diario") root.SetModal(Diary());
            if (Case == "fin") root.SetModal(new StrategyModalView { Kind = "end", Kicker = "Fin de la campaña", Title = "Campaña perdida", Text = "No queda nadie que conteste por radio. El Bastión se queda a oscuras.", Buttons = { "Volver al menú" } });
            if (Case == "menu") root.ToggleMenu(new GameMenuView { Abandon = false, QuitLabel = "Salir de Bastión" });
        }

        static TopView Top(bool owed)
        {
            var v = new TopView
            {
                Outings = 8, OutingsTip = "Salidas: misiones hechas. Cada una cuenta para el nivel del Bastión.",
                Siege = "ASEDIO EN 3 MISIONES", SiegeTip = "Los muertos vienen a por el Bastión dentro de 3 misiones.",
                Day = 20, DayTip = "Día 20. Cada misión son 2 días.",
                Level = "CASA MÓVIL", LevelFilled = 6, LevelTotal = 10, LevelTip = "Para la Casa grande: 150 § y 10 misiones hechas (llevas 8).",
                Doom = "INFESTACIÓN", DoomFilled = 5, DoomMax = 12, DoomFigure = "5/12", DoomTip = "La Marea avanza. Si se llena, la campaña está perdida. Quemar Nidos la hace retroceder.",
            };
            v.Stores.Add(new StoreView { Id = "credits", Value = "540", Owed = owed ? "−30" : null, OwedTip = "Pendiente: se paga cuando Arturo diga que sí", TipName = "Suministros", Tip = { "+40 con el convoy, en 2 misiones.", "La moneda del Bastión: construir, comprar, reclutar y contactar." } });
            v.Stores.Add(new StoreView { Id = "medicines", Value = "4", TipName = "Medicinas", Tip = { "Curan heridas y tratan a los mordidos." } });
            v.Stores.Add(new StoreView { Id = "food", Value = "12", TipName = "Raciones", Tip = { "−6 por misión.", "+4 de los Huertos.", "Hay para 6 misiones." } });
            v.Stores.Add(new StoreView { Id = "parts", Value = "9", TipName = "Piezas", Tip = { "Para el Taller: arreglar armas y hacer lo suyo." } });
            return v;
        }

        const string Arturo = "#f2a23a";
        const string Ana = "#a678ff";

        static SoldierTileView Tile(string id, string name, string accent, float[] skills, int rank = 0, bool points = false, string tag = null, string tone = null, bool selected = false) =>
            new SoldierTileView { Id = id, Name = name, Accent = accent, Skills = skills, Rank = rank, Points = points, PointsTip = "Talento por elegir", Tag = tag, TagTone = tone, Selected = selected };

        static SoldiersView Soldiers()
        {
            var v = new SoldiersView { MineHead = "Tus soldados", PartnerHead = "De Ana", PartnerNote = "no está: los preparas tú" };
            v.Mine.Add(Tile("s1", "Yuki", Arturo, new float[] { 20, 10, 55, 10, 5, 10 }, 1, true));
            v.Mine.Add(Tile("s3", "Dmitri", Arturo, new float[] { 60, 10, 10, 5, 5, 10 }, 1, true));
            v.Mine.Add(Tile("s5", "Hiro", Arturo, new float[] { 10, 50, 5, 5, 5, 10 }, 0, true, "Fiebre · ahora", "bad"));
            v.Mine.Add(Tile("s7", "Lucía", Arturo, new float[] { 10, 55, 5, 5, 5, 10 }, 0, true));
            v.Partner.Add(Tile("s0", "Anja", Ana, new float[] { 60, 10, 10, 5, 5, 10 }));
            v.Partner.Add(Tile("s2", "Arjun", Ana, new float[] { 10, 10, 55, 10, 5, 10 }, 0, false, "2 misiones", "warn"));
            v.Partner.Add(Tile("s4", "Chiara", Ana, new float[] { 10, 10, 10, 5, 60, 10 }));
            v.Partner.Add(Tile("s6", "Rafael", Ana, new float[] { 10, 10, 10, 60, 5, 10 }, 0, false, null, null, true));
            v.Recruit = new RecruitView
            {
                Title = "Reclutar un soldado",
                Blurb = "Un recluta nuevo: sale con cuchillo y pistola y con algo de práctica en una o dos habilidades. Llega al momento.",
                Chips = { new ChipView { Text = "30 §" }, new ChipView { Text = "Tus soldados 4/6" }, new ChipView { Text = "Camas libres 0", Tone = "warn" } },
                Button = "Reclutar · 30 §",
                Enabled = false,
                Why = "No quedan camas: para reclutar más, construid un Barracón.",
            };
            var sheet = new SoldierSheetView
            {
                Kicker = "Novato · de Ana", Name = "Rafael Costa", Skills = new float[] { 10, 10, 10, 60, 5, 10 }, NameTip = "2 misiones · 6 bajas",
                Subtitle = "Fusil de asalto", Note = "Ana no está: lo preparas tú hasta que vuelva.", Customize = "Personalizar",
                Health = "Listo", HealthTone = "good",
                ArmsHead = "Arma y armadura · se eligen aquí", BeltHead = "Cinturón · se elige aquí",
            };
            sheet.Stats.Add(new SheetStatView { Icon = "stat:hp", Value = "10", Color = "#8fd05a", Tip = "Salud" });
            sheet.Stats.Add(new SheetStatView { Icon = "stat:aim", Value = "68", Color = "#ffffff", Tip = "Puntería con armas de fuego" });
            sheet.Stats.Add(new SheetStatView { Icon = "stat:melee", Value = "63", Color = "#ffffff", Tip = "Puntería cuerpo a cuerpo" });
            sheet.Stats.Add(new SheetStatView { Icon = "stat:ap", Value = "40", Color = "#a678ff", Tip = "Voluntad" });
            var hand = new PickView { Id = "hand", Label = "Mano", Value = "Fusil de asalto", Lines = { "Daño 3-5 · Alcance medio", "y la pistola al cinto" }, Condition = 100, ConditionText = "Estado 100", Tags = { "Mira" }, Editable = true };
            hand.Options.Add(new PickOptionView { Id = "w100", Name = "Fusil de asalto", Note = "La lleva Rafael", Current = true });
            hand.Options.Add(new PickOptionView { Id = "w103", Name = "Fusil de precisión", Note = "En la Armería" });
            hand.Options.Add(new PickOptionView { Id = "w104", Name = "Escopeta", Note = "La lleva Hiro, de Arturo", Enabled = false });
            sheet.Arms.Add(hand);
            sheet.Arms.Add(new PickView { Id = "belt", Label = "Cinto", Value = "Cuchillo", Lines = { "Daño 2-4" }, Editable = true });
            sheet.Armor = new ArmorView { Tier = 3, Name = "Equipo antidisturbios", Figures = "· 10 de salud · 1 de blindaje" };
            sheet.Belt.Add(new PickView { Id = "slot0", Label = "Hueco 1", Value = "Nada", Editable = true });
            sheet.Belt.Add(new PickView { Id = "slot1", Label = "Hueco 2", Value = "Nada", Editable = true });
            var xp = new SkillsView { Head = "Experiencia", Rank = "Novato", RankFilled = 4, RankTotal = 8, Will = "+10 de voluntad", RankLine = "Sin rango: menos de 5 puntos gastados en el tablero.", Board = "Tablero de habilidades · 3 puntos por gastar" };
            var names = new[] { "Disparo", "Cuerpo a cuerpo", "Lanzar", "Curar", "Investigar", "Forma física" };
            var filled = new[] { 0, 18, 10, 3, 2, 0 };
            var growth = new[] { "+8 %", null, null, null, null, "+10 %" };
            for (var i = 0; i < 6; i++) xp.Rows.Add(new SkillRowView { Line = i, Name = names[i], Growth = growth[i], Filled = filled[i], Total = 20, Reward = "+5–10 % · +1 punto", RewardTip = "Al llenar la barra: +5–10 % y un punto para el tablero." });
            sheet.Experience = xp;
            v.Sheet = sheet;
            return v;
        }

        static CustomizerView Customizer()
        {
            var c = new CustomizerView { Kicker = "Personalizar · Novato", Title = "Rafael Costa", Name = "Rafael Costa", Nickname = "", ColorsHead = "Colores", ColorsOff = "Para teñir la ropa, activa «Ropa con el color del jugador» en Ajustes.", CanSave = true };
            ChoiceGroupView Group(string id, string title, string on, params string[] labels)
            {
                var g = new ChoiceGroupView { Id = id, Title = title };
                foreach (var l in labels) g.Choices.Add(new ChoiceView { Id = l, Label = l, On = l == on });
                return g;
            }
            c.Groups.Add(Group("outfit", "Ropa", "Al azar", "Al azar", "Chaleco de cuero", "Chaleco verde", "Camuflaje y cartuchos", "Chaqueta marrón", "Mangas remendadas", "Cazadora de borrego", "Chaqueta oscura", "Verde oliva", "Jersey amarillo", "Chaleco acolchado", "Ropa de calle", "Chaleco vaquero", "Equipo de asalto", "Chaleco azul", "Chaqueta de policía"));
            c.Groups.Add(Group("head", "Cabeza", "A cara descubierta", "Casco táctico", "Capucha", "Boina", "Gorra", "Máscara", "A cara descubierta"));
            c.Groups.Add(Group("build", "Complexión", "Normal", "Delgada", "Normal", "Robusta"));
            var swatches = new List<string> { "#5d5a7a", "#6b6b4a", "#7a6a4a", "#4a4f4a", "#4f5f4a", "#6f6f6f", "#2f3236", "#5a3a3a", "#3f4f3a", "#4a5a7a", "#59606a" };
            void Row(string id, string label, string note, bool locked = false) => c.Colors.Add(new ColorRowView { Id = id, Label = label, Note = note, Color = "#5d5a7a", Swatches = new List<string>(swatches), Locked = locked });
            Row("all", "Toda la armadura", "todas las piezas");
            Row("helmet", "Casco", "A cara descubierta: nada que pintar", true);
            Row("torso", "Torso", "como la armadura");
            Row("arms", "Brazos", "como la armadura");
            Row("legs", "Piernas", "como la armadura");
            Row("trim", "Detalles", "como la armadura");
            return c;
        }

        static NavView Nav(string on)
        {
            var v = new NavView { Hidden = on == "squad" || on == "debrief" };
            v.Tabs.Add(new TabView { Id = "geoscape", Label = "Mapa", On = on == "geoscape" });
            v.Tabs.Add(new TabView { Id = "engineering", Label = "Armería", New = on != "engineering", On = on == "engineering" });
            v.Tabs.Add(new TabView { Id = "barracks", Label = "Soldados", On = on == "barracks", Count = "▲4", Tip = "Yuki Tanaka: talento por elegir" });
            v.Tabs.Add(new TabView { Id = "facilities", Label = "Asentamiento", Partner = "Ana", PartnerColor = "#a678ff" });
            v.Tabs.Add(new TabView { Id = "squad", Label = "Escuadra", Urgent = true });
            return v;
        }

        static ArmoryView Items()
        {
            var v = new ArmoryView { Head = "Armería", Tab = "items", GearNew = true };
            var list = new ArmorySectionView();
            list.Rows.Add(new ArmoryRowView { Id = "i:grenade", Name = "Granadas", Price = "20 §", Right = "Tienes 2", Selected = true });
            list.Rows.Add(new ArmoryRowView { Id = "i:smoke", Name = "Granada de humo", Price = "25 §" });
            list.Rows.Add(new ArmoryRowView { Id = "i:firstAid", Name = "Botiquín", Price = "25 §", Right = "Tienes 1" });
            list.Rows.Add(new ArmoryRowView { Id = "i:flashbang", Name = "Granada aturdidora", Price = "30 §" });
            v.Sections.Add(list);
            v.Detail = new ArmoryDetailView
            {
                Kicker = "Accesorio · Cinturón", Name = "Granadas", Art = "grenade",
                Text = { "Dos granadas de fragmentación, en el cinturón. Quien no las lleva, no puede lanzar." },
                Chips = { new ChipView { Text = "Tienes 2" }, new ChipView { Text = "20 §" } },
                Button = "Comprar", Action = "buy",
            };
            return v;
        }

        static ArmoryView Gear()
        {
            var v = new ArmoryView { Head = "Armería", Tab = "gear", ItemsNew = true };
            var rack = new ArmorySectionView { Title = "En la Armería" };
            ArmoryRowView Gun(string id, string icon, string name, string place, string holder, ChipView tag = null, bool selected = false)
            {
                var r = new ArmoryRowView { Id = id, Icon = icon, Name = name, Place = place, Holder = holder, Selected = selected };
                if (tag != null) r.Tags.Add(tag);
                return r;
            }
            rack.Rows.Add(Gun("f:w100", "weapon:rifle", "Fusil de asalto", "Mano", "Rafael Costa", new ChipView { Text = "+1", Tone = "good" }, true));
            rack.Rows.Add(Gun("f:w101", "weapon:shotgun", "Escopeta", "Mano", "Lucía Ortega"));
            rack.Rows.Add(Gun("f:w102", "weapon:sniper", "Fusil de precisión", "Mano", "Arjun Mehta", new ChipView { Text = "Gastada", Tone = "warn" }));
            rack.Rows.Add(Gun("f:w103", "weapon:cannon", "Ametralladora", "Mano", "En la Armería"));
            rack.Rows.Add(Gun("f:w104", "overwatchMelee", "Machete", "Cinto", "En la Armería"));
            rack.Rows.Add(Gun("f:w105", "weapon:sniper", "Fusil de cerrojo", "Mano", "En la Armería"));
            rack.Rows.Add(Gun("f:w106", "weapon:shotgun", "Escopeta recortada", "Mano", "En la Armería"));
            v.Sections.Add(rack);
            var mods = new ArmorySectionView { Title = "Mejoras sueltas" };
            mods.Rows.Add(new ArmoryRowView { Id = "x:stock", Icon = "store:parts", Name = "Culata", Right = "× 2" });
            mods.Rows.Add(new ArmoryRowView { Id = "x:bigMag", Icon = "store:parts", Name = "Cargador ampliado", Right = "× 1" });
            v.Sections.Add(mods);
            var armor = new ArmorySectionView { Title = "Armadura" };
            armor.Rows.Add(new ArmoryRowView { Id = "a1", Tier = 1, Name = "Ropa reforzada", Right = "✓" });
            armor.Rows.Add(new ArmoryRowView { Id = "a2", Tier = 2, Name = "Armadura de placas", Right = "✓" });
            armor.Rows.Add(new ArmoryRowView { Id = "a3", Tier = 3, Name = "Equipo antidisturbios", Right = "✓" });
            v.Sections.Add(armor);
            var make = new ArmorySectionView { Title = "Hacer en el Taller" };
            make.Rows.Add(new ArmoryRowView { Id = "m:rifle", Icon = "weapon:rifle", Name = "Fusil de asalto", Place = "Mano", Dim = true });
            make.Rows.Add(new ArmoryRowView { Id = "m:shotgun", Icon = "weapon:shotgun", Name = "Escopeta", Chip = new ChipView { Text = "Taller · 2 misiones", Tone = "warn" } });
            make.Rows.Add(new ArmoryRowView { Id = "m:machete", Icon = "overwatchMelee", Name = "Machete", Place = "Cinto", Dim = true });
            make.Rows.Add(new ArmoryRowView { Id = "m:sniper", Icon = "weapon:sniper", Name = "Fusil de precisión", Place = "Mano", Dim = true });
            make.Rows.Add(new ArmoryRowView { Id = "m:mags", Icon = "stat:ammo", Name = "Primeros cargadores", Dim = true });
            make.Rows.Add(new ArmoryRowView { Id = "m:mags2", Icon = "stat:ammo", Name = "Más cargadores", Dim = true });
            make.Rows.Add(new ArmoryRowView { Id = "m:stock", Icon = "store:parts", Name = "Culata", Dim = true });
            make.Rows.Add(new ArmoryRowView { Id = "m:silencer", Icon = "store:parts", Name = "Silenciador", Dim = true });
            v.Sections.Add(make);
            var d = new ArmoryDetailView
            {
                Kicker = "Arma · Mano", Name = "Fusil de asalto", Art = "weapon:rifle",
                Text = { "Para un soldado: la lleva en la mano y la pistola al cinto. Si cae, se pierde." },
                Figures =
                {
                    new RowView { Key = "Daño", Value = "3–5" }, new RowView { Key = "Daño crítico", Value = "5–7" },
                    new RowView { Key = "Crítico", Value = "±0 %" }, new RowView { Key = "Puntería", Value = "+10" },
                    new RowView { Key = "Cargador", Value = "4 balas" }, new RowView { Key = "Acciones por disparo", Value = "1 acción" },
                    new RowView { Key = "Ruido", Value = "18 casillas" },
                },
                Range = new RangeView
                {
                    Name = "Medio", Line = "Vale para todo: suma de cerca y no resta de lejos.",
                    Steps =
                    {
                        new RangeStepView { Distance = "2", Value = "+19", Height = 0.95f }, new RangeStepView { Distance = "5", Value = "+13", Height = 0.65f },
                        new RangeStepView { Distance = "8", Value = "+7", Height = 0.35f }, new RangeStepView { Distance = "12", Value = "+2", Height = 0.1f },
                        new RangeStepView { Distance = "16", Value = "±0", Height = 0f },
                    },
                },
                Condition = new ConditionView { Value = 100, Text = "Estado 100", Line = "Está como nueva." },
                Mods = new ModsView
                {
                    Head = "Mejoras · 1/2",
                    Mounted = { new ModRowView { Id = "scope", Name = "Mira", Line = "+10 de puntería", Button = "Quitar" } },
                    FreeHead = "Hueco libre",
                    Options = { new ModRowView { Id = "stock", Button = "Poner culata (2)" }, new ModRowView { Id = "bigMag", Button = "Poner cargador ampliado (1)" } },
                },
                Holder = new HolderView { Head = "La lleva", Name = "Rafael Costa", Button = "Quitársela" },
                SoldiersHead = "Tus soldados y los de Ana",
            };
            d.Soldiers.Add(new CarrierView { Id = "s1", Name = "Yuki Tanaka", Note = "· en la mano: Pistola", Diff = { new ChipView { Text = "Daño +1", Tone = "good" }, new ChipView { Text = "De lejos +6", Tone = "good" }, new ChipView { Text = "Ruido +6", Tone = "bad" } }, Button = "Dársela" });
            d.Soldiers.Add(new CarrierView { Id = "s3", Name = "Dmitri Volkov", Note = "· en la mano: Pistola", Diff = { new ChipView { Text = "Daño +1", Tone = "good" }, new ChipView { Text = "De lejos +6", Tone = "good" } }, Button = "Dársela" });
            d.Soldiers.Add(new CarrierView { Id = "s6", Name = "Rafael Costa", Note = "· en la mano: Fusil de asalto", Chip = "La lleva" });
            v.Detail = d;
            return v;
        }

        static PickRowView Pick(string id, string name, string accent, int rank, string chip = null, string tone = null, bool inSquad = false, bool enabled = true) =>
            new PickRowView { Id = id, Name = name, Accent = accent, Skills = new float[] { 30, 20, 10, 10, 5, 10 }, Rank = rank, Chip = chip, ChipTone = tone, In = inSquad, Enabled = enabled };

        static SquadView Squad()
        {
            var v = new SquadView
            {
                MineHead = "Tus soldados · 3/4", MineTip = "Elige quién sale: clic para meterlo o sacarlo.", PartnerHead = "De Ana · 0",
                Title = "El colegio de La Vega", Night = "De noche",
                Briefing = "Aviso de las Bodegas. Los Cuervos se han llevado al Dr. Femi Adebayo. Si llegan a su campamento, **no habrá otra ocasión**. ==Que salga con vida.== De noche: a oscuras, tus soldados ven 10 casillas en vez de 18. Hay más zombis, y el botín vale la mitad más.",
                Warning = "Los muertos van a por cualquier vivo, también a por ellos, y el tiroteo los atrae.",
                Launch = "Lanzar misión", Back = "Volver al Bastión",
            };
            v.Mine.Add(Pick("s7", "Lucía Ortega", Arturo, 0, "✓", "good", true));
            v.Mine.Add(Pick("s1", "Yuki Tanaka", Arturo, 1, "✓", "good", true));
            v.Mine.Add(Pick("s5", "Hiro Sato", Arturo, 0, "Fiebre", "bad", false, false));
            v.Mine.Add(Pick("s3", "Dmitri Volkov", Arturo, 1, "✓", "good", true));
            v.Partner.Add(Pick("s6", "Rafael Costa", Ana, 0, null, null, false, false));
            v.Partner.Add(Pick("s2", "Arjun Mehta", Ana, 0, "2 misiones", "warn", false, false));
            v.Partner.Add(Pick("s4", "Chiara Rossi", Ana, 0, null, null, false, false));
            v.Partner.Add(Pick("s0", "Anja Kowalska", Ana, 0, null, null, false, false));
            v.Cards.Add(new LineupCardView { Id = "s1", Name = "Yuki Tanaka", Accent = Arturo, Lines = { "Fogueada" }, Removable = true });
            v.Cards.Add(new LineupCardView { Id = "s7", Name = "Lucía Ortega", Accent = Arturo, Lines = { "Novata" }, Removable = true });
            v.Cards.Add(new LineupCardView { Id = "s3", Name = "Dmitri Volkov", Accent = Arturo, Lines = { "Fogueado" }, Removable = true });
            v.Cards.Add(new LineupCardView { Tip = "Elige un soldado a la izquierda" });
            return v;
        }

        static DebriefView Debrief()
        {
            var v = new DebriefView { Victory = true, Result = "Misión cumplida", Mission = "Asedio", Continue = "Continuar" };
            v.Haul.Add(new HaulView { Icon = "store:credits", Figure = "+7", Tip = "Suministros: 7 de botín" });
            LineupCardView Card(string id, string name, string accent, string kills, params LearnedView[] learned)
            {
                var c = new LineupCardView { Id = id, Name = name, Accent = accent, Lines = { "ILESO", kills } };
                c.Learned.AddRange(learned);
                return c;
            }
            LearnedView Skill(string text) => new LearnedView { Kind = "skill", Line = 0, Text = text };
            v.Cards.Add(Card("s3", "Dmitri Volkov", Ana, "0 bajas"));
            v.Cards.Add(Card("s0", "Anja Kowalska", Ana, "0 bajas"));
            v.Cards.Add(Card("s5", "Hiro Sato", Ana, "1 baja", Skill("+4 Disparo")));
            v.Cards.Add(Card("s4", "Chiara Rossi", Ana, "0 bajas", Skill("+3 Disparo")));
            v.Cards.Add(Card("s1", "Yuki Tanaka", Ana, "0 bajas"));
            v.Cards.Add(Card("s2", "Arjun Mehta", Ana, "2 bajas", Skill("+7 Disparo")));
            v.Cards.Add(Card("s7", "Lucía Ortega", Ana, "0 bajas", Skill("+7 Disparo")));
            v.Cards.Add(Card("s6", "Rafael Costa", Ana, "2 bajas", Skill("+10 Disparo"), new LearnedView { Kind = "bar", Line = 0, Text = "▲ DISPARO +8 %" }, new LearnedView { Kind = "point", Text = "Habilidad" }));
            return v;
        }

        const string Story = "#4a7dff";
        const string Op = "#ffb030";
        const string Nest = "#ff4a5a";

        static MapView Map(string which)
        {
            var mission = which == "mapa-mision";
            var zone = which == "mapa-zona";
            var v = new MapView { Level = 5, Lata = "weu", Selected = mission ? "oce" : zone ? "afr" : null, Contacts = 4, ContactsMax = 2, NestsTip = "Nido: un foco de la Marea.", QuestsTip = "Encargo: lo que os piden en una zona.", RumorsTip = "Rumor: se escucha uno a la vez, o se enfría." };
            ZoneMarkView Zone(string id, string name, bool contacted, bool flooded = false) => new ZoneMarkView { Id = id, Name = name, Contacted = contacted, Flooded = flooded, Selected = v.Selected == id };
            var weu = Zone("weu", "Bielsa", true);
            weu.Missions.AddRange(new[] { Story, Op, Story, Op });
            weu.Kinds.AddRange(new[] { "rescue", "recovery", "rescue", "scavenge" });
            var oce = Zone("oce", "La Vega", true);
            oce.Missions.Add(Op);
            oce.Kinds.Add("rescue");
            var me = Zone("me", "Koper", false);
            me.Nest = true;
            me.Rumor = true;
            me.Listening = true;
            var eas = Zone("eas", "El Pantano Gris", false);
            eas.Rumor = true;
            v.Zones.AddRange(new[] { Zone("na", "Los Llanos de Ceniza", false, true), Zone("sa", "Rudnik", true), weu, Zone("eeu", "Sappada", false), Zone("afr", "Tarbes", false), me, eas, oce });
            MissionRowView Row(string id, string tone, string kind, string name, string where, bool on = false) => new MissionRowView { Id = id, Tone = tone, Kind = kind, Name = name, Where = where, Selected = on };
            v.Missions.Add(Row("o1", Story, "rescue", "El consultorio de Bielsa", "Bielsa"));
            v.Missions.Add(Row("o2", Op, "rescue", "El colegio de La Vega", "La Vega · De noche", mission));
            v.Missions.Add(Row("o3", Op, "recovery", "El hospital de Bielsa", "Bielsa · De noche"));
            v.Missions.Add(Row("o4", Op, "scavenge", "El centro comercial de Bielsa", "Bielsa"));
            v.Wait = new WaitDayView { Label = "Esperar un día", Tip = "Un día sin salir: avanzan el Taller y la Marea." };
            v.Nests.Add(new MapRowView { Id = "me", Name = "Koper", Chip = "Sin contacto", ChipTip = "Para asaltar un Nido, antes hay que contactar con su zona." });
            v.Quests.Add(new MapRowView { Id = "q1", Name = "La estación de números", Line = "Paso 1 de 2 · Esperando vuestra decisión" });
            v.Rumors.Add(new RumorRowView { Id = "r1", Region = "Koper", Text = "Se habla de una camioneta de la cruz roja volcada en la cuneta.", Listening = "Escuchando · 3 misiones" });
            v.Rumors.Add(new RumorRowView { Id = "r2", Region = "El Pantano Gris", Text = "Se habla de luces en la vieja fábrica de conservas.", Button = "Escuchar · 2 misiones", Chip = "en 2", ChipTone = "warn" });
            if (mission)
                v.Detail = new MapDetailView
                {
                    Kicker = "Misión", Title = "El colegio de La Vega", Subtitle = "La Vega · Ciudad · De noche",
                    Text = { "Aviso de las Bodegas. Los Cuervos se han llevado al Dr. Femi Adebayo. Si llegan a su campamento, **no habrá otra ocasión**. ==Que salga con vida.== De noche: a oscuras, tus soldados ven 10 casillas en vez de 18. Hay más zombis, y el botín vale la mitad más." },
                    Note = "— N. A.", Warning = "Los muertos van a por cualquier vivo, también a por ellos, y el tiroteo los atrae.",
                    Rows = { new RowView { Key = "Tipo", Value = "Rescate" }, new RowView { Key = "Dificultad", Value = "<color=#f0503c>★★</color> Difícil" }, new RowView { Key = "Recompensa", Value = "158 suministros · un recluta" } },
                    Buttons = { new MapButtonView { Action = "deploy", Label = "Desplegar", Primary = true } },
                };
            if (zone)
                v.Detail = new MapDetailView
                {
                    Kicker = "Zona sin contacto", Title = "Tarbes",
                    Text = { "La primera ciudad caída. Calles llenas de zombis y farmacias que nadie ha terminado de saquear.", "Sin enclave · +25 § al mes" },
                    Meter = 4, MeterMax = 2,
                    Buttons = { new MapButtonView { Action = "contact", Label = "Contactar · 110 §", Primary = true, Enabled = false } },
                    Why = "No caben más contactos: construid o mejorad la Sala de radio.",
                };
            return v;
        }

        static ProposalView Proposal(bool mine)
        {
            var p = new ProposalView { Mine = mine, Partner = "Ana", PartnerColor = "#a678ff", Text = "reclutar un soldado", Cost = "30 §", CanSee = true };
            p.Rows.Add(new RowView { Key = "Suministros", Value = "30 §" });
            p.Rows.Add(new RowView { Key = "Qué hace", Value = "Un recluta para Arturo." });
            return p;
        }

        static StrategyModalView Event(bool proposed)
        {
            var v = new StrategyModalView { Kind = "event", Kicker = "Acontecimiento", Title = "Contrabandistas", Text = "Unos contrabandistas han vaciado un cuartel abandonado y ofrecen lo que sacaron." };
            v.Options.Add(new OptionView { Label = "Comprárselo", Effect = "−40 suministros: dos granadas extra para la armería", Proposed = proposed });
            v.Options.Add(new OptionView { Label = "Rechazar la oferta", Effect = "Sin efecto" });
            if (proposed) v.Vote = new ProposalView { Partner = "Ana", PartnerColor = "#a678ff", Text = "comprárselo", Cost = "40 §" };
            else v.Foot = "El tiempo no avanza hasta que decidáis.";
            return v;
        }

        static StrategyModalView Diary()
        {
            var v = new StrategyModalView
            {
                Kind = "alert", Kicker = "Diario", Tone = "good", Title = "Los Cuervos",
                Body = { "Los Cuervos atraen hordas con sirenas contra quien no les paga. Sin sirena, la zona respira." },
                Text = "El Bastión es una casa móvil.",
                Note = "Las sirenas primero. Siempre. — N. A.",
                Buttons = { "Entendido" },
            };
            var build = new BroughtGroupView { Title = "Para construir" };
            build.Items.Add(new BroughtView { Icon = "tab:geoscape", Name = "Sala de mapas", Brief = "Más suministros por misión" });
            build.Items.Add(new BroughtView { Icon = "grenade", Name = "Mina", Brief = "Estalla al pisarla: un uso" });
            var up = new BroughtGroupView { Title = "Suben de nivel" };
            up.Items.Add(new BroughtView { Icon = "tab:barracks", Name = "Barracón, nivel 2", Brief = "+3 camas" });
            up.Items.Add(new BroughtView { Icon = "store:parts", Name = "Taller, nivel 2", Brief = "Accesorios −35 %" });
            up.Items.Add(new BroughtView { Icon = "store:medicines", Name = "Enfermería, nivel 2", Brief = "Heridas: curan +3 de rápido" });
            up.Items.Add(new BroughtView { Icon = "store:food", Name = "Huerto, nivel 2", Brief = "+10 raciones por misión" });
            var tech = new BroughtGroupView { Title = "Avances" };
            tech.Items.Add(new BroughtView { Icon = "weapon:rifle", Name = "Culatas y silenciadores", Brief = "El Taller hace culatas y silenciadores" });
            v.Brought.AddRange(new List<BroughtGroupView> { build, up, tech });
            return v;
        }

        // The lab answers in the log: the clicks reach here.
        public void OnTab(string view) => Debug.Log("[estrategia] tab " + view);
        public void OnMenu() => root.ToggleMenu(new GameMenuView { Abandon = false, QuitLabel = "Salir de Bastión" });
        public void OnVote(bool accept) => Debug.Log("[estrategia] vote " + accept);
        public void OnWithdraw() => Debug.Log("[estrategia] withdraw");
        public void OnSeeProposal() => Debug.Log("[estrategia] see");
        public void OnWaitingGo() => Debug.Log("[estrategia] waiting go");
        public void OnOption(int index) => Debug.Log("[estrategia] option " + index);
        public void OnModalButton(int index) => root.SetModal(null);
        public void OnSoldier(string id) => Debug.Log("[estrategia] soldier " + id);
        public void OnRecruit() => Debug.Log("[estrategia] recruit");
        public void OnCustomize() => Debug.Log("[estrategia] customize");
        public void OnCare(int index) => Debug.Log("[estrategia] care " + index);
        public void OnPick(string pick, string option) => Debug.Log($"[estrategia] pick {pick} {option}");
        public void OnBoard() => Debug.Log("[estrategia] board");
        public void OnCustomName(string name, string nickname) => Debug.Log($"[estrategia] name {name} «{nickname}»");
        public void OnCustomChoice(string group, string id, bool preview) => Debug.Log($"[estrategia] choice {group} {id} {preview}");
        public void OnCustomColor(string part, string color, bool preview) => Debug.Log($"[estrategia] color {part} {color} {preview}");
        public void OnCustomAction(string action) => Debug.Log("[estrategia] custom " + action);
        public void OnUnlock(string cell) => Debug.Log("[estrategia] unlock " + cell);
        public void OnBoardClose() => root.SetSkillBoard(null);
        public void OnArmoryTab(string tab) => root.SetArmory(tab == "gear" ? Gear() : Items());
        public void OnArmoryPick(string id) => Debug.Log("[estrategia] armory pick " + id);
        public void OnArmoryAction(string action, string arg) => Debug.Log($"[estrategia] armory {action} {arg}");
        public void OnSquadToggle(string id, bool on) => Debug.Log($"[estrategia] squad {id} {on}");
        public void OnLaunch() => Debug.Log("[estrategia] launch");
        public void OnSquadBack() => Debug.Log("[estrategia] squad back");
        public void OnSquadRecruit() => Debug.Log("[estrategia] squad recruit");
        public void OnGoMap() => Debug.Log("[estrategia] go map");
        public void OnDebriefContinue() => Debug.Log("[estrategia] debrief continue");
        public void OnDebriefLine(int index) => Debug.Log("[estrategia] debrief line " + index);
        public void OnZone(string id) => Debug.Log("[estrategia] zone " + id);
        public void OnOffer(string id) => Debug.Log("[estrategia] offer " + id);
        public void OnWait() => Debug.Log("[estrategia] wait");
        public void OnListen(string rumor) => Debug.Log("[estrategia] listen " + rumor);
        public void OnMapAction(string action) => Debug.Log("[estrategia] map " + action);
    }
}
