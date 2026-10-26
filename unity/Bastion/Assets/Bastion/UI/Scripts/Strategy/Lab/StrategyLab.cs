using System.Collections.Generic;
using System.Linq;
using Bastion.UI.Hud;
using UnityEngine;
using EngineBase = Bastion.Engine.Base;
using EngineLore = Bastion.Engine.Lore;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base's screens with made-up views, as the 0.10.1 showed its sample campaign (day 20, the Bastion a «casa
    /// móvil», two players; .dev-snapshots/ui/estrategia-0101/): for captures next to it with the same framing.
    /// `BASTION_ESTRATEGIA` picks what is open: propuesta, propuesta-mia, espera, acontecimiento, acontecimiento-voto,
    /// diario, fin, menu, soldados, personalizar, tablero, armeria, armeria-arma, escuadra, parte, mapa, mapa-mision,
    /// mapa-zona, asentamiento, asentamiento-mano, asentamiento-talar, asentamiento-bastion, asentamiento-barracon,
    /// asentamiento-muro, asentamiento-foso, volver (none: the plain base); any with «-radio» after it, the radio's recap
    /// over it (asentamiento-radio, tablero-radio). «muestra-…»: the same from the sample campaign through the presenter
    /// (muestra-soldados, -esperar, -esperar-pedido, -mapa-encargo, -escuadra-listo, -parte-caido, -armeria, -armeria-arma, -escuadra, -parte, -mapa, -mapa-mision, -mapa-zona, -asentamiento,
    /// -asentamiento-bastion, -propuesta, -propuesta-mia, -diario).
    /// </summary>
    public sealed class StrategyLab : MonoBehaviour, IStrategyHandlers
    {
        public string Case = "";
        StrategyRoot root;
        bool shown;

        /// <summary>«…-radio»: the case with the radio playing over it, its first line typed (as the 0.10.1's shots had it).</summary>
        bool withRadio;

        void Start()
        {
            root = GetComponent<StrategyRoot>();
            if (string.IsNullOrEmpty(Case)) Case = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA") ?? "";
            // BASTION_TINTE=1: «Ropa con el color del jugador» on, for this capture only.
            if (System.Environment.GetEnvironmentVariable("BASTION_TINTE") == "1")
            {
                App.GameSettings.TintForLab = true;
                Bastion.Client.Units.SquadTint.On = true;
            }
            // BASTION_INTERFAZ=1.3: Ajustes' «Interfaz» for this capture (the panel put back as play stops).
            App.GameSettings.ApplyInterface();
            withRadio = Case.EndsWith("-radio");
            if (withRadio) Case = Case.Substring(0, Case.Length - "-radio".Length);
        }

        /// <summary>BASTION_ESTRATEGIA_PULSA: a button's text, pressed a few frames after the case is up (TestSteps.Press); the order goes to the log.</summary>
        string press = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_PULSA");
        int pressAt = -1;
        bool blinked;

        void Update()
        {
            // BASTION_ESTRATEGIA_PARPADEO / _LATIDO: an element by its class blinking (StrategyRoot.Blink) or pulsing (Pulse), with the press.
            if (shown && pressAt >= 0 && Time.frameCount >= pressAt && !blinked)
            {
                blinked = true;
                var blink = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_PARPADEO");
                if (!string.IsNullOrEmpty(blink)) root.Blink(blink);
                var pulse = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_LATIDO");
                var beat = string.IsNullOrEmpty(pulse) ? null : UnityEngine.UIElements.UQueryExtensions.Q(root.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement, className: pulse);
                // A key: the element itself if it is one, else the first inside it (a sheet's care: its main one).
                if (beat != null && !(beat is UnityEngine.UIElements.Button)) beat = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(beat);
                if (beat != null) StrategyRoot.Pulse(beat);
                // BASTION_ESTRATEGIA_DESPLEGABLE=hand|belt: that choice of the sheet open, as a click on its head.
                var choice = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_DESPLEGABLE");
                if (!string.IsNullOrEmpty(choice)) root.OpenSoldierChoice(choice);
                // BASTION_ESTRATEGIA_AYUDA=strat-well--credits[:n|:off]: that element's help open (the n-th with the class, or the
                // first whose key is off), scrolled into sight.
                var help = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_AYUDA");
                if (!string.IsNullOrEmpty(help))
                {
                    var bits = help.Split(':');
                    var all = UnityEngine.UIElements.UQueryExtensions.Query(root.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement, className: bits[0]).ToList();
                    var off = bits.Length > 1 && bits[1] == "off";
                    var at = !off && bits.Length > 1 && int.TryParse(bits[1], out var n) ? n : 0;
                    var over = off
                        ? all.FirstOrDefault(e => UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(e) is var b && b != null && !b.enabledSelf)
                        : at < all.Count ? all[at] : null;
                    over?.GetFirstAncestorOfType<UnityEngine.UIElements.ScrollView>()?.ScrollTo(over);
                    // Pinned, not a synthetic enter: the editor's own pointer enters other elements with help as it likes.
                    if (over != null) root.PinTip(over);
                    Debug.Log($"[estrategia] ayuda de «{help}»: {(over != null ? "abierta" : "no está")} ({all.Count} con la clase)");
                }
            }
            if (shown && !string.IsNullOrEmpty(press) && pressAt >= 0 && Time.frameCount >= pressAt)
            {
                Debug.Log($"[estrategia] pulsado «{press}»: {Bastion.UI.TestSteps.Press(press)}");
                press = null;
            }
            if (shown || root == null || !root.Ready) return;
            shown = true;
            pressAt = Time.frameCount + 10;
            root.Bind(this);
            if (Case.StartsWith("muestra-"))
            {
                Sample(Case.Substring("muestra-".Length));
                return;
            }
            var mineAsked = Case == "propuesta-mia";
            root.ClockOf = () => (EngineLore.ProposalClock("0:24", mineAsked), 0.62f, false);
            root.SetTop(Top(Case == "propuesta" ? "theirs" : mineAsked ? "mine" : null));
            var screen = Case.StartsWith("armeria") ? "engineering" : Case == "escuadra" ? "squad" : Case == "parte" ? "debrief" : Case.StartsWith("mapa") ? "geoscape" : Case.StartsWith("asentamiento") ? "facilities" : "barracks";
            root.ValleyCamera = GameObject.Find("Cámara del valle")?.GetComponent<Camera>() ?? Camera.main;
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
            if (screen == "facilities") root.SetSettlement(Settlement(Case));
            var soldiers = Soldiers();
            if (Case == "personalizar") soldiers.Customizer = Customizer();
            root.SetSoldiers(soldiers);
            if (Case == "tablero") root.SetSkillBoard(new SkillBoardView { Kicker = "Tablero de habilidades · Novato", SoldierId = "s6", Name = "Rafael Costa", Rank = "Novato", RankFilled = 2, RankTotal = 5, Will = "+10 de voluntad", Points = 10, Editable = true, Board = { "steadyHand", "grip" } });
            root.SetProposal(Case == "propuesta" ? Proposal(false) : Case == "propuesta-mia" ? Proposal(true) : null, Case == "espera" ? new WaitingView { Text = "Ana está a punto para salir: El colegio de La Vega.", Button = "Ir a la Escuadra" } : null);
            if (Case.StartsWith("acontecimiento")) root.SetModal(Event(Case == "acontecimiento-voto"));
            if (Case == "diario") root.SetModal(Diary());
            if (Case == "fin") root.SetModal(new StrategyModalView { Kind = "end", Kicker = "Fin de la campaña", Title = "Campaña perdida", Text = "No queda nadie que conteste por radio. El Bastión se queda a oscuras.", Buttons = { "Volver al menú" } });
            if (Case == "menu") root.ToggleMenu(new GameMenuView { Abandon = false, QuitLabel = "Salir de Bastión" });
            if (withRadio)
            {
                root.RadioCard.Play(Recap());
                root.RadioCard.Finish();
            }
            if (Case == "volver") root.SetModal(new StrategyModalView { Kind = "leave", Kicker = StrategyText.Leave.Kicker, Title = StrategyText.Leave.Title, Text = StrategyText.Leave.Text("Ana"), Buttons = { StrategyText.Leave.Yes, StrategyText.Leave.No } });
        }

        static TipView Tip(string name, string text) => new TipView { Name = name, Lines = StrategyText.Sentences(text) };

        /// <summary>The strip as the sample's (renderTop), its tooltips from Lore; `proposal`: «theirs», «mine» or null.</summary>
        static TopView Top(string proposal)
        {
            var v = new TopView
            {
                Outings = 8, OutingsTip = Tip("Salidas", EngineLore.TimeHelp(Level)),
                Siege = "ASEDIO EN 3 MISIONES", SiegeTip = Tip(EngineLore.SiegeText.Name, EngineLore.SiegeWarning(3)),
                Day = 20, DayTip = Tip("Día 20", EngineLore.TimeHelp(Level)),
                Level = EngineLore.BastionLevelName(Level).ToUpperInvariant(), LevelFilled = 6, LevelTotal = 10,
                // The sample's nextLevel: the casa móvil con porche, 240 §, 6 missions (levelBadge).
                LevelTip = new TipView { Name = EngineLore.BastionLevelName(Level), Lines = { $"Para {EngineLore.BastionLevelThe(Level + 1)}: 240 § y 6 misiones hechas (llevas 8).", EngineLore.LevelText.About } },
                Doom = EngineLore.DoomName(Level).ToUpperInvariant(), DoomFilled = 5, DoomMax = 12, DoomFigure = "5/12",
                // Before level 4 nobody knows of the Marea: its name and the glossary's line only from then (renderTop).
                DoomTip = Tip(EngineLore.DoomName(Level), $"{EngineLore.DoomStatus(Level, 5)}. Si se llena, la campaña está perdida. Quemar Nidos la hace retroceder."),
            };
            var credits = new StoreView { Id = "credits", Value = "540", TipName = "Suministros", Tip = { $"+140 con el convoy, {EngineLore.InMissions(1)}" } };
            credits.Tip.AddRange(StrategyText.Sentences(EngineLore.Glossary["Suministros"]));
            if (proposal != null)
            {
                credits.Owed = "−30";
                credits.OwedTip = proposal == "mine" ? "Pendiente: se paga cuando Ana diga que sí" : "Pendiente: se paga si aceptas la propuesta de Ana";
            }
            v.Stores.Add(credits);
            v.Stores.Add(new StoreView { Id = "medicines", Value = "4", TipName = "Medicinas", Tip = StrategyText.Sentences(EngineLore.Glossary["Medicinas"]) });
            // The sample's rations: 12, 8 a mission, no Huerto (rationsTip).
            var food = new StoreView { Id = "food", Value = "12", TipName = "Raciones", Tip = { "−8 por misión", "Hay para 1 misión." } };
            food.Tip.AddRange(StrategyText.Sentences(EngineLore.RationsHelp()));
            v.Stores.Add(food);
            v.Stores.Add(new StoreView { Id = "parts", Value = "9", TipName = "Piezas", Tip = StrategyText.Sentences(EngineLore.Glossary["Piezas"]) });
            return v;
        }

        const string Arturo = "#f2a23a";
        const string Ana = "#a678ff";

        static SoldierTileView Tile(string id, string name, string accent, float[] skills, int rank = 0, bool points = false, string tag = null, string tone = null, bool selected = false) =>
            new SoldierTileView { Id = id, Name = name, Accent = accent, Skills = skills, Rank = rank, Points = points, PointsTip = "Puntos por gastar en el tablero", Tag = tag, TagTone = tone, Selected = selected };

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
                Chips = { new ChipView { Text = "30 §" }, new ChipView { Text = "Tus soldados 4/6", Tip = "Como mucho 6 soldados por jugador" }, new ChipView { Text = "Camas libres 0", Tone = "warn", Tip = "Cada soldado necesita una cama: la caravana tiene 8, y cada Barracón suma más." } },
                Button = "Reclutar · 30 §",
                Enabled = false,
                Why = "No quedan camas: para reclutar más, construid un Barracón.",
            };
            var sheet = new SoldierSheetView
            {
                Kicker = "Novato · de Ana", Name = "Rafael Costa", Skills = new float[] { 10, 10, 10, 60, 5, 10 }, NameTip = "Misiones: 3 · Bajas: 2",
                Subtitle = "Fusil de asalto", Note = "Ana no está: lo preparas tú hasta que vuelva.", Customize = "Personalizar",
                Health = "Listo", HealthTone = "good",
                ArmsHead = "Arma y armadura · se eligen aquí", BeltHead = "Cinturón · se elige aquí",
            };
            sheet.Stats.Add(new SheetStatView { Icon = "stat:hp", Value = "10", Color = "#8fd05a", Tip = "Salud" });
            sheet.Stats.Add(new SheetStatView { Icon = "stat:aim", Value = "68", Color = "#ffffff", Tip = "Puntería con armas de fuego: 60 de base, más lo que suman las barras de Disparo y el tablero" });
            sheet.Stats.Add(new SheetStatView { Icon = "stat:melee", Value = "63", Color = "#ffffff", Tip = "Puntería cuerpo a cuerpo: 60 de base, más lo que suman las barras de Cuerpo a cuerpo y el tablero" });
            sheet.Stats.Add(new SheetStatView { Icon = "stat:ap", Value = "40", Color = "#a678ff", Tip = "Voluntad" });
            var hand = new PickView { Id = "hand", Label = "Mano", Value = "Fusil de asalto", Lines = { "Daño 3–5 · Alcance medio", "y la pistola al cinto" }, Condition = 100, ConditionText = "Estado 100", Tags = { "Mira" }, Editable = true };
            hand.Options.Add(new PickOptionView { Id = "none", Name = "Sin arma: la pistola", Note = "Fusil de asalto vuelve a la Armería" });
            hand.Options.Add(new PickOptionView { Id = "w103", Name = "Fusil de precisión", Note = "En la Armería" });
            hand.Options.Add(new PickOptionView { Id = "w104", Name = "Escopeta", Note = "La lleva Hiro Sato, de Arturo", Enabled = false });
            sheet.Arms.Add(hand);
            sheet.Arms.Add(new PickView { Id = "belt", Label = "Cinto", Value = "Cuchillo", Lines = { "Daño 2–4" }, Editable = true });
            sheet.Armor = new ArmorView { Tier = 3, Name = "Equipo antidisturbios", Figures = "· 10 de salud · 1 de blindaje" };
            sheet.Belt.Add(new PickView { Id = "slot0", Label = "Hueco 1", Value = "Nada", Editable = true });
            sheet.Belt.Add(new PickView { Id = "slot1", Label = "Hueco 2", Value = "Nada", Editable = true });
            var next = EngineLore.RankReward(0, "m");
            var xp = new SkillsView
            {
                Head = "Experiencia", Rank = "Novato", RankFilled = 4, RankTotal = 8, Will = next?.Short ?? "Rango máximo", WillTip = next?.Long ?? "No hay rango más alto.",
                RankLine = "Sin rango: menos de 5 puntos gastados en el tablero.", Gives = $"Voluntad {Bastion.Engine.Bars.VeteranWill(0)} · 2 acciones por turno",
                Board = "Tablero de habilidades · 3 puntos por gastar",
            };
            var filled = new[] { 0, 18, 10, 3, 2, 0 };
            var growth = new[] { "+8 %", null, null, null, null, "+10 %" };
            for (var i = 0; i < 6; i++)
            {
                var skill = Bastion.Engine.SkillIds.All[i];
                var reward = EngineLore.BarReward(skill);
                var def = Bastion.Engine.Bars.SkillDefs[skill];
                xp.Rows.Add(new SkillRowView { Line = i, Name = def.Name, Tip = def.Line, Growth = growth[i], GrowthTip = def.Stat, Filled = filled[i], Total = 20, Reward = reward.Short, RewardTip = reward.Long });
            }
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
            Row("trim", "Detalles", "capucha, boina y gorra");
            return c;
        }

        static NavView Nav(string on)
        {
            var v = new NavView { Hidden = on == "squad" || on == "debrief" };
            v.Tabs.Add(new TabView { Id = "geoscape", Label = "Mapa", On = on == "geoscape" });
            v.Tabs.Add(new TabView { Id = "engineering", Label = "Armería", New = on != "engineering", On = on == "engineering" });
            v.Tabs.Add(new TabView { Id = "barracks", Label = "Soldados", On = on == "barracks", Count = "▲4", Tip = string.Join(" · ", new[] { "Yuki Tanaka", "Dmitri Volkov", "Hiro Sato", "Lucía Ortega" }.Select(n => $"{n}: puntos por gastar en el tablero")) });
            v.Tabs.Add(new TabView { Id = "facilities", Label = "Asentamiento", Partner = "Ana", PartnerColor = "#a678ff" });
            v.Tabs.Add(new TabView { Id = "squad", Label = "Escuadra", Urgent = true });
            return v;
        }

        static ArmoryView Items()
        {
            var v = new ArmoryView { Head = "Armería", Tab = "items", GearNew = true };
            var list = new ArmorySectionView();
            const string equip = "Se equipan en la ficha de cada soldado";
            list.Rows.Add(new ArmoryRowView { Id = "i:grenade", Name = "Granadas", Price = "20 §", Right = "Tienes 2", Selected = true, Tip = equip });
            list.Rows.Add(new ArmoryRowView { Id = "i:smoke", Name = "Granada de humo", Price = "25 §", Tip = equip });
            list.Rows.Add(new ArmoryRowView { Id = "i:firstAid", Name = "Botiquín", Price = "25 §", Right = "Tienes 1", Tip = equip });
            list.Rows.Add(new ArmoryRowView { Id = "i:flashbang", Name = "Granada aturdidora", Price = "30 §", Tip = equip });
            v.Sections.Add(list);
            v.Detail = new ArmoryDetailView
            {
                Kicker = "Accesorio · Cinturón", KickerTip = "Se equipa en la ficha del soldado; si cae, se pierde", Name = "Granadas", Art = "grenade",
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
                    new RowView { Key = "Crítico", Value = "±0 %" }, new RowView { Key = "Puntería", Value = "+10", Tip = "Lo que el arma suma o resta a la puntería: la mira, el desgaste" },
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
            d.Soldiers.Add(new CarrierView { Id = "s1", Name = "Yuki Tanaka", Note = "· en la mano: Pistola", Diff = { new ChipView { Text = "Daño 3–5", Tone = "good" }, new ChipView { Text = "De lejos +6", Tone = "good" }, new ChipView { Text = "Ruido +6", Tone = "bad" } }, Button = "Dársela" });
            d.Soldiers.Add(new CarrierView { Id = "s3", Name = "Dmitri Volkov", Note = "· en la mano: Pistola", Diff = { new ChipView { Text = "Daño 3–5", Tone = "good" }, new ChipView { Text = "De lejos +6", Tone = "good" } }, Button = "Dársela" });
            d.Soldiers.Add(new CarrierView { Id = "s6", Name = "Rafael Costa", Note = "· en la mano: Fusil de asalto", Chip = "La lleva" });
            v.Detail = d;
            return v;
        }

        static PickRowView Pick(string id, string name, string accent, int rank, string chip = null, string tone = null, bool inSquad = false, bool enabled = true) =>
            new PickRowView
            {
                Id = id, Name = name, Accent = accent, Skills = new float[] { 30, 20, 10, 10, 5, 10 }, Rank = rank, Chip = chip, ChipTone = tone, In = inSquad, Enabled = enabled,
                ChipTip = chip == "✓" ? StrategyText.Squad.InTip : chip == "Fiebre" ? "Mordisco: necesita el tratamiento antes de salir." : chip != null ? "Heridas" : null,
            };

        static SquadView Squad()
        {
            var v = new SquadView
            {
                MineHead = "Tus soldados · 3/4", MineTip = "Pulsa un soldado para meterlo o sacarlo. Los de Ana los elige Ana; los huecos que no llene se quedan vacíos. El equipo se cambia en Soldados.", PartnerHead = "De Ana · 0",
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
            v.Haul.Add(new HaulView { Icon = "store:credits", Figure = "+7", Tip = "Suministros · Botín: 7" });
            // The sample's soldiers who are women: «ILESA» (Lore, C2; the 0.10.1 said «ILESO» to everyone).
            var women = new HashSet<string> { "s0", "s1", "s4", "s7" };
            LineupCardView Card(string id, string name, string accent, string kills, params LearnedView[] learned)
            {
                var c = new LineupCardView { Id = id, Name = name, Accent = accent, Lines = { women.Contains(id) ? "ILESA" : "ILESO", kills } };
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
            v.Cards.Add(Card("s6", "Rafael Costa", Ana, "2 bajas", Skill("+10 Disparo"), new LearnedView { Kind = "bar", Line = 0, Text = "▲ DISPARO +8 %" }, new LearnedView { Kind = "point", Count = 1 }));
            return v;
        }

        const string Story = "#4a7dff";
        const string Op = "#ffb030";
        const string Nest = "#ff4a5a";

        static MapView Map(string which)
        {
            var mission = which == "mapa-mision";
            var zone = which == "mapa-zona";
            var v = new MapView
            {
                Level = 5, Lata = "weu", Selected = mission ? "oce" : zone ? "afr" : null, Contacts = 4, ContactsMax = 2,
                ContactsTip = "Zonas contactadas / máximo (la Sala de radio da más)",
                // Not the glossary's Nido, which names the Marea before level 4 (Lore.NestTip).
                NestsTip = EngineLore.NestTip(Level), QuestsTip = EngineLore.Glossary["Encargo"],
                // One is being listened to in the sample (screen.ts rumorHead).
                RumorsTip = EngineLore.Glossary["Rumor"] + " Solo se escucha uno a la vez.",
            };
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
            // The row's help as screen.ts renderMissions: «Historia · Rescate · Bielsa», the kind by its name (MISSION_NAMES).
            MissionRowView Row(string id, string tone, string kind, string name, string zoneName, string where, bool on = false) =>
                new MissionRowView { Id = id, Tone = tone, Kind = kind, Name = name, Where = where, Selected = on, Tip = $"{(tone == Story ? "Historia · " : "")}{Bastion.Engine.Setup.MissionNames[kind]} · {zoneName}" };
            v.Missions.Add(Row("o1", Story, "rescue", "El consultorio de Bielsa", "Bielsa", "Bielsa"));
            v.Missions.Add(Row("o2", Op, "rescue", "El colegio de La Vega", "La Vega", "La Vega · De noche", mission));
            v.Missions.Add(Row("o3", Op, "recovery", "El hospital de Bielsa", "Bielsa", "Bielsa · De noche"));
            v.Missions.Add(Row("o4", Op, "scavenge", "El centro comercial de Bielsa", "Bielsa", "Bielsa"));
            v.Wait = new WaitDayView { Label = "Esperar un día", Tip = $"Un día sin salir: se come, se curan las heridas y avanzan el Taller y {EngineLore.DoomSubject(Level)}." };
            v.Nests.Add(new MapRowView { Id = "me", Name = "Koper", Chip = "Sin contacto", ChipTip = EngineLore.NestNoContactTip("me", 110) });
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
                    // Who is there and what they send, from Lore (screen.ts regionInfo: REGION_LORE[id].ally, regionIncome).
                    Text = { EngineLore.RegionLores["afr"].Situation },
                    Lines = { new MapLineView { Text = $"{Capital(EngineLore.RegionLores["afr"].Ally)} · +25 § en cada convoy", Tip = "Lo que deja en cada convoy, una vez contactada. El convoy llega cada 6 misiones." } },
                    Meter = 4, MeterMax = 2,
                    Buttons = { new MapButtonView { Action = "contact", Label = "Contactar · 110 §", Primary = true, Enabled = false } },
                    Why = "No caben más contactos: construid o mejorad la Sala de radio.",
                };
            return v;
        }

        /// <summary>
        /// «muestra-…»: the presenter's views from the sample campaign itself (SampleCampaign), not made-up ones: as Arturo
        /// (slot 1) with Ana away, as the 0.10.1's references. For now the strip, the tabs and the Asentamiento
        /// (muestra-asentamiento; -bastion with the caravan picked); with «-radio», the recap over it.
        /// </summary>
        void Sample(string which)
        {
            // Ana here only for the proposals (70- and 71-propuesta), as the 0.10.1's steps set her connected.
            var p = new StrategyPresenter { State = SampleCampaign.Load(), Slot = 1, Seats = SampleCampaign.Seats(which.StartsWith("propuesta") || which == "esperar-pedido" || which == "escuadra-listo" || which == "armeria-arma-ana") };
            // «escuadra-listo»: Ana here and Arturo ready (his cards' «LISTO»); «parte-caido»: the debrief's first soldier fallen («CAÍDO»).
            if (which == "escuadra-listo") p.State.LaunchReady[1] = true;
            if (which == "parte-caido" && p.State.LastReport?.Squad.Count > 0) p.State.LastReport.Squad[0].Status = "dead";
            // «mapa-encargo»: the errand «numbers» with its step's mission (the first offer), for its row's click (BASTION_ESTRATEGIA_PULSA).
            if (which == "mapa-encargo") p.State.Offers[0].Quest = "numbers";
            // «esperar»: nobody can go out (Campaign.MustWait: all of them hurt, no suministros to recruit); «esperar-pedido»,
            // with Ana here and asking.
            if (which.StartsWith("esperar"))
            {
                foreach (var s in p.State.Soldiers) if (s.Alive) s.WoundedDays = System.Math.Max(s.WoundedDays, 2);
                p.State.Credits = 0;
                if (which == "esperar-pedido") p.State.AdvanceReady[0] = true;
            }
            var screen = which.StartsWith("asentamiento") || which.StartsWith("radio") ? "facilities" : which == "escuadra" || which == "escuadra-listo" || which == "lanzar" ? "squad" : which.StartsWith("parte") ? "debrief"
                : which.StartsWith("armeria") ? "engineering" : which.StartsWith("mapa") ? "geoscape" : "barracks";
            // «parte-6», «parte-4»: the debrief with the first so many of its squad (the 0.10.1's shot the same way).
            if (which.StartsWith("parte-") && int.TryParse(which.Substring("parte-".Length), out var kept) && p.State.LastReport != null)
                p.State.LastReport.Squad = p.State.LastReport.Squad.Take(kept).ToList();
            // 70- and 71-propuesta: a recruit proposed by Ana or by Arturo, with the engine's sentence (Lore).
            if (which.StartsWith("propuesta"))
                p.State.Proposal = new Bastion.Engine.Proposal { By = which == "propuesta-mia" ? 1 : 0, Action = new Bastion.Engine.RecruitAction { For = which == "propuesta-mia" ? 1 : 0 }, Text = "reclutar un soldado (30 §)" };
            // 50-escuadra: the first offer chosen, as its steps sent chooseMission (the command, with Red's room: the
            // squad the engine fills for whoever is connected).
            if (screen == "squad")
            {
                p.State.ActiveMission = p.State.Offers[0].Id;
                p.State.Squad = Bastion.Engine.Campaign.DefaultSquad(p.State, new List<int> { 1 });
            }
            root.SetTop(p.Top());
            root.SetNav(p.Nav(screen));
            root.SetScreen(screen);
            if (screen == "facilities") root.SetSettlement(p.Settlement(new SettlementPick { Piece = which == "asentamiento-bastion" ? "b1" : null }));
            // The 0.10.1's 10-soldados: the first of Ana's soldiers picked, as its steps did.
            // 72-acontecimiento was shot after 50-escuadra (its mission chosen), on Arturo's first soldier.
            if (which.StartsWith("acontecimiento"))
            {
                p.State.ActiveMission = p.State.Offers[0].Id;
                root.SetNav(p.Nav(screen));
            }
            var soldiersPick = new SoldiersPick { Soldier = which.StartsWith("acontecimiento") ? null : p.State.Soldiers.Find(s => s.Owner == 0 && s.Alive)?.Id };
            // 11-personalizar: Personalizar open on that soldier, the draft as they are.
            if (which == "personalizar" && p.State.Soldiers.Find(s => s.Id == soldiersPick.Soldier) is Bastion.Engine.CampaignSoldier own)
                soldiersPick.Draft = new CustomDraft { Soldier = own.Id, Name = own.Name, Nickname = own.Nickname ?? "", Look = CustomDraft.Copy(own.Appearance) ?? Bastion.Engine.Appearances.DefaultAppearance(own.Owner, null, null, own.Id) };
            if (screen == "barracks") root.SetSoldiers(p.Soldiers(soldiersPick));
            // In Arte's scene «Base» (HudShots.EstrategiaBase): the clearing as the sample has it, the tab's shot, Soldados'
            // soldier (as the draft has them), and the busts.
            Bastion.Client.Portraits.Know(p.State);
            var clearing = Bastion.Client.Settlement.Instance;
            if (clearing != null)
            {
                clearing.Show(p.State);
                clearing.SetArmory(p.State);
                var (mirror, halves) = p.LineupSides();
                clearing.SetLineup(p.State, p.Lineup(screen == "debrief"), mirror, halves);
                clearing.SetView(screen, true);
                if (screen == "barracks") clearing.Showcase(p.State, soldiersPick.Soldier, soldiersPick.Draft?.Look);
                // «llegada»: back from the mission onto Soldados (the debrief read), La Lata up its track to the caravan;
                // «lanzar»: the Escuadra's take-off three seconds in (BaseSession.Depart: the stamp, the squad getting in, La Lata away).
                if (which == "llegada") clearing.Arrive();
                if (which == "lanzar") LaunchSoon(clearing);
            }
            if (screen == "squad") root.SetSquad(p.Squad());
            if (screen == "debrief") root.SetDebrief(p.Debrief());
            // 20- to 22-armeria: the belt with its first item, and the gear with the Armería's first weapon; «armeria-arma-ana»,
            // with Ana here: Rafael, who carries it, is hers, so its keys are off.
            if (screen == "engineering") root.SetArmory(p.Armory(which.StartsWith("armeria-arma") ? new ArmoryPick { Tab = "gear", Pick = "f:" + p.State.FoundWeapons[0].Id } : new ArmoryPick()));
            // 30- to 32-mapa: nothing picked, the first offer, the first zone without contact.
            if (screen == "geoscape")
            {
                // HudShots.EstrategiaBase's own camera for the valley when there is one (the clearing's goes off on the Mapa).
                root.ValleyCamera = GameObject.Find("Cámara del valle")?.GetComponent<Camera>() ?? Camera.main;
                var offer = which == "mapa-mision" ? p.State.Offers[0] : null;
                var zone = which == "mapa-zona" ? (p.State.Regions.Find(r => !r.Contacted) ?? p.State.Regions[0]).Id : offer?.Region;
                root.SetMap(p.Map(new MapPick { Offer = offer?.Id, Region = zone }));
            }
            root.SetProposal(p.Proposal(), p.Waiting(screen));
            if (p.State.Proposal != null) root.ClockOf = () => (EngineLore.ProposalClock("0:24", p.State.Proposal.By == 1), 0.62f, false);
            // 72-acontecimiento: the smugglers' event from the state (Campaign.EventDef), over Soldados; «-voto», Ana's
            // answer on the table for Arturo to vote.
            if (which.StartsWith("acontecimiento"))
            {
                p.State.Event = Bastion.Engine.CampaignEventIds.Smugglers;
                if (which == "acontecimiento-voto")
                {
                    p.Seats = SampleCampaign.Seats(true);
                    p.State.Proposal = new Bastion.Engine.Proposal { By = 0, Action = new Bastion.Engine.EventAction { Option = 0 }, Text = $"responder «{Bastion.Engine.Campaign.Events["smugglers"].Options[0].Label}»" };
                }
                root.SetProposal(p.Proposal(), p.Waiting(screen));
                root.SetModal(p.Event(screen));
            }
            // 12-tablero: Rafael's board, as Arturo sees a soldier of Ana's.
            if (which == "tablero") root.SetSkillBoard(p.SkillBoard(p.State.Soldiers.Find(s => s.Owner == 0 && s.Alive)?.Id));
            // «radio-N»: the story's scene N (Transmissions.Scenes, Lore's own lines) through the radio's controller, its
            // placeholders filled as for Arturo with Ana; its first line typed.
            if (which.StartsWith("radio"))
            {
                var n = which.Length > "radio-".Length && int.TryParse(which.Substring("radio-".Length), out var k) ? k : 0;
                var scene = Bastion.Engine.Transmissions.Scenes[n];
                var radio = new Radio(root.RadioCard, 1, () => new Dictionary<string, string> { ["yo"] = StrategyText.Radio.Greeting(new[] { "Arturo" }), ["saludo"] = StrategyText.Radio.Greeting(new[] { "Ana", "Arturo" }) });
                root.RadioCard.Play(radio.View(scene, Bastion.Engine.Transmissions.SceneLines(scene, p.State)));
                root.RadioCard.Finish();
            }
            // 73-diario: the diary as the level before had bought this one (collectAlerts with level 2).
            if (which == "diario")
            {
                var before = SampleCampaign.Load();
                before.Level = 2;
                var alerts = p.Alerts(before);
                if (alerts.Count > 0) root.SetModal(alerts[0]);
            }
            // The line-up's pads where the 0.10.1's shots have them, right to left as its hangar numbers them (Arte's
            // Settlement.PadWorld, projected, in the game).
            var squadPads = new[] { 454f, 606f, 835f, 986f };
            var debriefPads = new[] { 223f, 355f, 487f, 619f, 820f, 952f, 1085f, 1217f };
            // Without the clearing (the plain sky) the 0.10.1's places; with it, its own (StrategyRoot.ClearingPad).
            if (clearing == null) root.PadAt = (i, n) => screen == "squad" ? new Vector2(squadPads[(squadPads.Length - 1 - i + squadPads.Length) % squadPads.Length], 497) : new Vector2(debriefPads[(n - 1 - i + debriefPads.Length) % debriefPads.Length], 403);
            if (withRadio)
            {
                root.RadioCard.Play(Recap());
                root.RadioCard.Finish();
            }
        }

        /// <summary>The sample's stores and level: the palette's costs against them (pieceBlocker).</summary>
        const int Credits = 540;
        const int Parts = 9;
        const int Level = 3;

        /// <summary>
        /// The Asentamiento as the 0.10.1 showed the sample (40-asentamiento and after): the palette from the engine's
        /// pieces and Lore's words, the Bastion's figures as the sample's (campaign.ts isn't in C# yet), and the card of
        /// the caravan, of a Barracón, of a Muro being taken down or of a spent Foso.
        /// </summary>
        static SettlementView Settlement(string which)
        {
            var inHand = which == "asentamiento-mano" ? "quarters" : null;
            var felling = which == "asentamiento-talar";
            var v = new SettlementView { StatsHead = "Bastión", Walk = "Andar por la base" };
            v.Fell = new FellView { Label = EngineLore.BuildText.Fell, Supplies = EngineBase.FellCost, Per = "por árbol", On = felling, Keys = EngineLore.BuildText.Felling, Tip = EngineLore.BuildText.Felling };
            var groups = new[] { (EngineLore.BuildText.Tabs.Building, new[] { "building" }), (EngineLore.BuildText.Tabs.Defence, new[] { "defence", "trap" }) };
            foreach (var (title, kinds) in groups)
            {
                var g = new PaletteGroupView { Title = title };
                foreach (var id in EngineBase.PieceOrder)
                {
                    var def = EngineBase.Pieces[id];
                    if (id == "caravan" || def.Level > Level || System.Array.IndexOf(kinds, def.Kind) < 0) continue;
                    var shortOf = (System.Math.Max(0, def.Cost.Supplies - Credits), System.Math.Max(0, (def.Cost.Parts ?? 0) - Parts));
                    var why = shortOf.Item1 > 0 || shortOf.Item2 > 0 ? EngineLore.BuildShortLine(shortOf.Item1, shortOf.Item2) + "." : null;
                    var line = EngineLore.PieceTexts.All[id].Line;
                    g.Tiles.Add(new PieceTileView { Id = id, Name = EngineLore.PieceTexts.All[id].Name, Supplies = def.Cost.Supplies, Parts = def.Cost.Parts ?? 0, Short = why != null, On = id == inHand, Tip = why != null ? line + "\n" + why : line });
                    if (id == inHand) g.Keys = EngineLore.HoldingLine(id) + (id == "palisade" || id == "wall" || id == "sandbags" || id == "logs" || id == "wire" ? " Arrastra para poner una fila." : "");
                }
                v.Groups.Add(g);
            }
            var barracks = which == "asentamiento-barracon" || which == "asentamiento-muro" || which == "asentamiento-foso";
            v.Stats.Add(new BastionStatView { Icon = "base:beds", IconColor = "#b2c3d0", Value = barracks ? "8/10" : "8/8", Label = "Camas", Tone = barracks ? null : "warn" });
            v.Stats.Add(new BastionStatView { Icon = "store:food", IconColor = "#eab96c", Value = "−8", Label = "Raciones por misión" });
            v.Stats.Add(new BastionStatView { Icon = "store:credits", Value = "+140", Label = "Convoy tras la próxima misión" });
            if (which == "asentamiento-bastion") v.Card = Card(new Bastion.Engine.BasePiece { Id = "b1", Piece = "caravan", Hp = 40, Level = 1 });
            if (which == "asentamiento-barracon") v.Card = Card(new Bastion.Engine.BasePiece { Id = "b90", Piece = "quarters", Hp = 22, Level = 1 });
            if (which == "asentamiento-muro") v.Card = Card(new Bastion.Engine.BasePiece { Id = "b91", Piece = "wall", Hp = 20 }, true);
            if (which == "asentamiento-foso") v.Card = Card(new Bastion.Engine.BasePiece { Id = "b92", Piece = "spikes", Hp = 9, Charges = 0 });
            return v;
        }

        /// <summary>
        /// buildMode.ts pieceCard over a piece of the sample, with the engine's costs and Lore's words; what campaign.ts
        /// says (raiseCost, nextLevel, levelUpBlocker) as the 0.10.1 showed it.
        /// </summary>
        static PieceCardView Card(Bastion.Engine.BasePiece p, bool dismantling = false)
        {
            var def = EngineBase.Pieces[p.Piece];
            var text = EngineLore.PieceTexts.All[p.Piece];
            var level = p.Level ?? 1;
            var building = def.Kind == "building" && p.Piece != "caravan";
            var c = new PieceCardView { Title = p.Piece == "caravan" ? EngineLore.BastionLevelName(Level) : text.Name, CloseTip = "Soltarla (Esc)" };
            c.Figure = building ? EngineLore.PieceFigure(p.Piece, level) : null;
            if (c.Figure == null) c.Line = text.Line;
            if (building)
            {
                var top = text.Levels?.Count ?? 3;
                for (var n = 1; n <= top; n++)
                    c.Ladder.Add(new LadderStepView { Label = $"Nivel {n}", Figure = EngineLore.PieceFigureShort(p.Piece, n) ?? "—", Now = n == level, Past = n < level });
                if (text.Levels != null && level <= text.Levels.Count) c.LevelLine = text.Levels[level - 1];
            }
            c.Holds = EngineLore.HoldsLine(p.Hp, def.Hp);
            c.HoldsTip = "Lo que aguanta los golpes de los muertos";
            if (def.Charges != null)
            {
                c.Uses = EngineLore.UsesLine(p.Charges ?? 0, def.Charges.Value);
                if ((p.Charges ?? 0) == 0) c.State = p.Piece == "turret" ? EngineLore.PieceState.SilentLine : EngineLore.PieceState.SpentLine;
            }
            if (p.Hp < def.Hp)
            {
                var cost = EngineBase.RepairCost(p);
                c.Actions.Add(new PieceActionView { Action = "mend", Label = EngineLore.ActionPrice("repair", cost.Supplies, cost.Parts ?? 0) });
            }
            // The sample's raiseCost: a Barracón to level 2 for 40 §.
            if (building) c.Actions.Add(new PieceActionView { Action = "raise", Label = EngineLore.ActionPrice("upgrade", 40), Tip = EngineLore.UpgradeLine(p.Piece, level + 1) });
            // The sample's nextLevel: the casa móvil con porche, 240 §, 6 missions, and it has both (8 flown, 540 §), so
            // nothing under the button (Lore, 24/10: not the 0.10.1's «8 de 6 misiones…»). When it can't, its Why is
            // what is missing, levelUpBlocker's: «Para la casa móvil con porche hacen falta 6 misiones hechas: lleváis
            // 3.» or «Faltan N §.».
            if (p.Piece == "caravan")
                c.Actions.Add(new PieceActionView { Action = "levelUp", Label = $"Subir a {EngineLore.BastionLevelThe(Level + 1)} · 240 §", Hover = EngineLore.LevelUpLine(Level + 1, 240) });
            if (def.Charges != null && (p.Charges ?? 0) < def.Charges)
                c.Actions.Add(new PieceActionView { Action = "rearm", Label = EngineLore.ActionPrice("rearm", 0, EngineBase.RearmCost(p)) });
            if (EngineBase.DismantleBlocker(p) == null)
            {
                var back = EngineBase.DismantleRefund(p);
                c.Actions.Add(new PieceActionView { Action = "dismantle", Label = EngineLore.ActionPrice("dismantle", back) });
                if (dismantling) c.Confirm = new PieceConfirmView { Text = $"{EngineLore.BuildText.DismantleAsk} {EngineLore.DismantleBack(back)}", Yes = EngineLore.BuildText.DismantleYes, No = EngineLore.BuildText.DismantleNo };
            }
            return c;
        }

        /// <summary>The recap of the sample campaign for Arturo, as the engine says it (Transmissions.RecapLines).</summary>
        static RadioView Recap()
        {
            var radio = new Radio(null, 1, () => new Dictionary<string, string> { ["yo"] = StrategyText.Radio.Greeting(new[] { "Arturo" }), ["saludo"] = StrategyText.Radio.Greeting(new[] { "Ana", "Arturo" }) });
            return radio.View(Bastion.Engine.Transmissions.Recap, Bastion.Engine.Transmissions.RecapLines(SampleCampaign.Load(), 1));
        }

        async void LaunchSoon(Bastion.Client.Settlement clearing)
        {
            // Three seconds in: the busts drawn and the row standing, for a strip that starts just before.
            await Awaitable.WaitForSecondsAsync(3f, destroyCancellationToken);
            root.Launching(StrategyText.Launch);
            await clearing.Leave(destroyCancellationToken);
            Debug.Log("[estrategia] La Lata se ha ido");
        }

        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static ProposalView Proposal(bool mine)
        {
            var p = new ProposalView { Mine = mine, Partner = "Ana", PartnerColor = "#a678ff", Text = "reclutar un soldado (30 §)", Cost = "30 §", CanSee = true };
            p.Rows.Add(new RowView { Key = "Suministros", Value = "30 §" });
            p.Rows.Add(new RowView { Key = "Qué hace", Value = "Un recluta para Arturo." });
            return p;
        }

        static StrategyModalView Event(bool proposed)
        {
            var v = new StrategyModalView { Kind = "event", Kicker = "Acontecimiento", Title = "Contrabandistas", Text = "Unos contrabandistas han vaciado un cuartel abandonado y ofrecen lo que sacaron." };
            v.Options.Add(new OptionView { Label = "Comprárselo", Effect = "−40 suministros; dos granadas extra para la Armería", Proposed = proposed });
            v.Options.Add(new OptionView { Label = "Rechazar la oferta", Effect = "Sin efecto" });
            if (proposed) v.Vote = new ProposalView { Partner = "Ana", PartnerColor = "#a678ff", Text = "responder «Comprárselo»", Cost = "40 §" };
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
            build.Items.Add(new BroughtView { Icon = "tab:geoscape", Name = "Sala de mapas", Brief = EngineLore.NewBrief["piece:simulation"] });
            build.Items.Add(new BroughtView { Icon = "map:lata", Name = "Chapistería", Brief = EngineLore.NewBrief["piece:garage"] });
            build.Items.Add(new BroughtView { Icon = "grenade", Name = "Mina", Brief = EngineLore.NewBrief["piece:mine"] });
            var up = new BroughtGroupView { Title = "Suben de nivel" };
            up.Items.Add(new BroughtView { Icon = "tab:barracks", Name = "Barracón, nivel 2", Brief = EngineLore.PieceFigure("quarters", 2) });
            up.Items.Add(new BroughtView { Icon = "store:parts", Name = "Taller, nivel 2", Brief = EngineLore.PieceFigure("workshop", 2) });
            up.Items.Add(new BroughtView { Icon = "store:medicines", Name = "Enfermería, nivel 2", Brief = EngineLore.PieceFigure("infirmary", 2) });
            up.Items.Add(new BroughtView { Icon = "store:food", Name = "Huerto, nivel 2", Brief = EngineLore.PieceFigure("garden", 2) });
            var tech = new BroughtGroupView { Title = "Avances" };
            tech.Items.Add(new BroughtView { Icon = "weapon:rifle", Name = "Culatas y silenciadores", Brief = EngineLore.NewBrief["tech:magWeapons"] });
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
        public void OnPostHome() => Debug.Log("[estrategia] post home");
        public void OnGoMap() => Debug.Log("[estrategia] go map");
        public void OnDebriefContinue() => Debug.Log("[estrategia] debrief continue");
        public void OnDebriefLine(int index) => Debug.Log("[estrategia] debrief line " + index);
        public void OnZone(string id, bool mark) => Debug.Log($"[estrategia] zone {id}{(mark ? " (marca)" : "")}");
        public void OnOffer(string id) => Debug.Log("[estrategia] offer " + id);
        public void OnWait() => Debug.Log("[estrategia] wait");
        public void OnListen(string rumor) => Debug.Log("[estrategia] listen " + rumor);
        public void OnMapAction(string action) => Debug.Log("[estrategia] map " + action);
        public void OnPieceTake(string piece) => Debug.Log("[estrategia] take " + piece);
        public void OnFell() => Debug.Log("[estrategia] fell");
        public void OnBuildDrop() => Debug.Log("[estrategia] drop");
        public void OnPieceAction(string action) => Debug.Log("[estrategia] piece " + action);
        public void OnPieceClose() => Debug.Log("[estrategia] piece close");
        public void OnWalk() => Debug.Log("[estrategia] walk");
        public void OnSeen(string key) => Debug.Log("[estrategia] seen " + key);
    }
}
