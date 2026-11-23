using System.Collections.Generic;
using System.Linq;
using Bastion.UI.Gear;
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
    /// asentamiento-muro, asentamiento-foso, volver (none: the plain base); with «-propuesta» after it, Ana's proposal's plate
    /// too. «muestra-…»: the same from the sample campaign through the presenter
    /// (muestra-soldados, -esperar, -esperar-pedido, -mapa-encargo, -escuadra-listo, -parte-caido, -armeria, -armeria-arma, -escuadra, -parte, -mapa, -mapa-mision, -mapa-zona, -asentamiento,
    /// -asentamiento-bastion, -propuesta, -propuesta-mia, -diario).
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
            // BASTION_TINTE=1: «Ropa con el color del jugador» on, for this capture only.
            if (System.Environment.GetEnvironmentVariable("BASTION_TINTE") == "1")
            {
                App.GameSettings.TintForLab = true;
                Bastion.Client.Units.SquadTint.On = true;
            }
            // BASTION_INTERFAZ=1.3: Ajustes' «Interfaz» for this capture (the panel put back as play stops).
            App.GameSettings.ApplyInterface();
            // BASTION_LUZ_COMPANERO=#rrggbb: the partner's soldiers' studio light in Soldados, to propose one (14/11).
            if (ColorUtility.TryParseHtmlString(System.Environment.GetEnvironmentVariable("BASTION_LUZ_COMPANERO") ?? "", out var light)) SoldiersScreen.TheirLight = light;
        }

        /// <summary>BASTION_ESTRATEGIA_PULSA: a button's text, pressed a few frames after the case is up (TestSteps.Press); the order goes to the log.</summary>
        string press = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_PULSA");
        string gearDrag = System.Environment.GetEnvironmentVariable("BASTION_RUNS_ARRASTRE");
        int pressAt = -1;
        /// <summary>
        /// BASTION_ESTRATEGIA_ARRASTRE=points[:seconds]: a drag on Soldados' clearing that far to the right (left if
        /// negative), 10 points a frame from when the case is up (or from that game time, for a capture's frames), through
        /// the turning pad's own pointer events.
        /// </summary>
        float drag = Number(System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_ARRASTRE"), 0);
        /// <summary>
        /// BASTION_ESTRATEGIA_FIGURA=encima|clic: the pointer on Soldados' figure (the pad's middle), lighting it, or a click
        /// there, into Personalizar (0.12.24), through the pad's own pointer events, as the case is up.
        /// </summary>
        string figure = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_FIGURA");
        readonly float dragAt = Number(System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_ARRASTRE"), 1);
        float dragged;

        static float Number(string list, int i)
        {
            var bits = list?.Split(':');
            return bits != null && i < bits.Length && float.TryParse(bits[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
        }
        bool blinked;

        void Update()
        {
            // BASTION_RUNS_SERIE="path[|scene];…": one sample after another, 2 s apart from 11 s (a capture's first frame comes
            // at 10 s), two frames each at 1 fps: a whole expedition's states, the engine's (the pass of 16/11).
            if (series != null && shown && Time.time >= seriesAt && seriesNext < series.Length)
            {
                var item = series[seriesNext++].Split('|');
                seriesScene = item.Length > 1 && item[1] != "" ? item[1] : null;
                runsChosen = null;
                Debug.Log($"[serie] {seriesNext} {item[0]} {seriesScene} a {Time.time:0.0} s");
                root.UnpinTip();
                RunsSample(item[0]);
                // «path|scene|help» (revisión de la UI, 22/11): that help pinned once it is laid out, as BASTION_ESTRATEGIA_AYUDA.
                seriesHelp = item.Length > 2 && item[2] != "" ? item[2] : null;
                seriesHelpAt = Time.frameCount + 3;
                seriesAt += 2;
            }
            if (seriesHelp != null && Time.frameCount >= seriesHelpAt)
            {
                PinHelp(seriesHelp);
                seriesHelp = null;
            }
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
                // BASTION_TABLERO_RATON=cell[:share]: the pointer resting on that cell of the skill board (its wave and card),
                // and that share of its hold run.
                var rest = System.Environment.GetEnvironmentVariable("BASTION_TABLERO_RATON")?.Split(':');
                var hexes = UnityEngine.UIElements.UQueryExtensions.Q<SkillBoard>(root.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement);
                // «cruce»: across the row over the core (its bought cells), left to right in 2 s, then off the board.
                if (rest != null && rest.Length > 0 && rest[0] == "cruce") hexes?.LabSweep(new Vector2(-150, -45), new Vector2(150, -45), 2);
                else if (rest != null && rest.Length > 0 && rest[0] != "")
                    hexes?.LabPoint(rest[0], rest.Length > 1 ? float.Parse(rest[1], System.Globalization.CultureInfo.InvariantCulture) : 0);
                // BASTION_ESTRATEGIA_ENCIMA=s3: the pointer on that soldier's row of Escuadra, their sheet in short beside the list.
                var pointed = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_ENCIMA");
                if (!string.IsNullOrEmpty(pointed)) root.PeekSquad(pointed);
                // BASTION_ESTRATEGIA_DESPLEGABLE=hand|belt: that choice of the sheet open, as a click on its head.
                var choice = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_DESPLEGABLE");
                if (!string.IsNullOrEmpty(choice)) root.OpenSoldierChoice(choice);
                // BASTION_ESTRATEGIA_AYUDA=strat-well--credits[:n|:off]: that element's help open (the n-th with the class, or the
                // first whose key is off), scrolled into sight.
                var help = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_AYUDA");
                if (!string.IsNullOrEmpty(help)) PinHelp(help);
            }
            if (shown && drag != 0 && pressAt >= 0 && Time.frameCount >= pressAt && Time.timeSinceLevelLoad >= dragAt) Drag();
            if (shown && figure != null && pressAt >= 0 && Time.frameCount >= pressAt) Figure();
            // BASTION_RUNS_ARRASTRE=id>owner/slot[@x,y][~][!]: a thing of a runs sample dragged there and held (! lets it go).
            if (shown && !string.IsNullOrEmpty(gearDrag) && pressAt >= 0 && Time.frameCount >= pressAt)
            {
                var parts = gearDrag.Split('>');
                var where = parts[1].TrimEnd('!', '~');
                var release = parts[1].EndsWith("!");
                var turned = parts[1].Contains("~");
                var at = where.Split('@');
                var place = at[0].Split('/');
                var xy = at.Length > 1 ? at[1].Split(',') : new[] { "0", "0" };
                var done = root.Gear.TestDragTo(parts[0], place[0], place.Length > 1 ? place[1] : null, int.Parse(xy[0]), int.Parse(xy[1]), turned, release);
                Debug.Log($"[estrategia] arrastre {gearDrag}: {(done ? "hecho" : "no está")}");
                gearDrag = null;
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
            if (Case.StartsWith("runs-"))
            {
                RunsSample(Case.Substring("runs-".Length));
                return;
            }
            var mineAsked = Case == "propuesta-mia";
            root.ClockOf = () => (EngineLore.ProposalClock("0:24", mineAsked), 0.62f, false);
            root.SetTop(Top(Case == "propuesta" ? "theirs" : mineAsked ? "mine" : null));
            var screen = Case == "almacen" ? "stash" : Case == "etapas" || Case.StartsWith("parada") ? "runmap" : Case == "escuadra" ? "squad" : Case == "parte" ? "debrief" : "barracks";
            root.SetNav(Nav(screen));
            // Soldados under everything else, as the 0.10.1's shots (Rafael, Ana's, picked while she is away).
            root.SetScreen(screen);
            // The pads where the 0.10.1's shots have them (Arte's Settlement.PadWorld, projected, in the game).
            var squadPads = new[] { 454f, 606f, 835f, 986f };
            var debriefPads = new[] { 223f, 355f, 487f, 619f, 820f, 952f, 1085f, 1217f };
            root.PadAt = (i, n) => screen == "squad" ? new Vector2(squadPads[i % squadPads.Length], 497) : new Vector2(debriefPads[i % debriefPads.Length], 403);
            if (screen == "squad") root.SetSquad(Squad());
            if (screen == "debrief") root.SetDebrief(Debrief());
            if (screen == "stash") root.SetStash(Stash());
            if (screen == "runmap") root.SetRunMap(Expedition(Case));
            var soldiers = Soldiers();
            if (Case == "personalizar") soldiers.Customizer = Customizer();
            root.SetSoldiers(soldiers);
            if (Case == "tablero") root.SetSkillBoard(new SkillBoardView { Kicker = "Tablero de habilidades · Novato", SoldierId = "s6", Name = "Rafael Costa", Rank = "Novato", RankFilled = 2, RankTotal = 5, Will = "+10 de voluntad", Points = 10, Editable = true, Board = { "steadyHand", "grip" } });
            root.SetProposal(Case == "propuesta" || Case.EndsWith("-propuesta") ? Proposal(false) : Case == "propuesta-mia" ? Proposal(true) : null, Case == "espera" ? new WaitingView { Text = "Ana está a punto para salir: El colegio de La Vega.", Button = "Ir a la Escuadra" } : null);
            if (Case.StartsWith("acontecimiento")) root.SetModal(Event(Case == "acontecimiento-voto"));
            if (Case == "diario") root.SetModal(Diary());
            if (Case == "fin") root.SetModal(new StrategyModalView { Kind = "end", Kicker = "Fin de la campaña", Title = "Campaña perdida", Text = "No queda nadie que conteste por radio. El Bastión se queda a oscuras.", Buttons = { "Volver al menú" } });
            if (Case == "menu") root.ToggleMenu(new GameMenuView { Abandon = false, QuitLabel = "Salir de Bastión" });
            if (Case == "menu-invitacion") root.ToggleMenu(new GameMenuView { Abandon = false, QuitLabel = "Salir de Bastión", Invite = Invite() });
            if (Case == "volver") root.SetModal(new StrategyModalView { Kind = "leave", Kicker = StrategyText.Leave.Kicker, Title = StrategyText.Leave.Title, Text = StrategyText.Leave.Text("Ana"), Buttons = { StrategyText.Leave.Yes, StrategyText.Leave.No } });
        }

        static TipView Tip(string name, string text) => new TipView { Name = name, Lines = StrategyText.Sentences(text) };

        /// <summary>
        /// «etapas»: the map of an expedition of chapter 1, two stages in (Saqueo, then a Trueque), choosing the third:
        /// a Limpieza at night, a Sigilo, a stop; row 5 not seen yet; Rafael's kit with the squad.
        /// </summary>
        static RunMapView Expedition(string which = "etapas")
        {
            RunNodeView N(string id, int row, int col, int cols, string kind, string icon, bool stop = false) => new RunNodeView
            {
                Id = id, Row = row, Col = col, Cols = cols, Name = StrategyText.Runs.NodeName(kind), Icon = icon, Stop = stop, Boss = kind == "boss",
                Card = { StrategyText.Runs.NodeLine(kind), stop ? null : StrategyText.Runs.Difficulty(row < 3 ? 1 : 2) },
            };
            var v = new RunMapView
            {
                Head = StrategyText.Runs.Head(1), Line = StrategyText.Runs.Choose, Start = StrategyText.Runs.Start,
                Back = StrategyText.Runs.Back, BackTip = StrategyText.Runs.BackTip, SquadHead = StrategyText.Runs.Squad,
            };
            v.Nodes.Add(N("n1-0", 1, 0, 3, "normal", "kind:scavenge"));
            v.Nodes.Add(N("n1-1", 1, 1, 3, "normal", "kind:scavenge"));
            v.Nodes.Add(N("n1-2", 1, 2, 3, "normal", "kind:scavenge"));
            v.Nodes.Add(N("n2-0", 2, 0, 4, "rescue", "kind:rescue"));
            v.Nodes.Add(N("n2-1", 2, 1, 4, "trade", "node:trade", true));
            v.Nodes.Add(N("n2-2", 2, 2, 4, "normal", "kind:scavenge"));
            v.Nodes.Add(N("n2-3", 2, 3, 4, "event", "node:event", true));
            v.Nodes.Add(N("n3-0", 3, 0, 3, "elimination", "kind:elimination"));
            v.Nodes.Add(N("n3-1", 3, 1, 3, "stealth", "hide"));
            v.Nodes.Add(N("n3-2", 3, 2, 3, "refuge", "node:refuge", true));
            v.Nodes.Add(N("n4-0", 4, 0, 4, "hold", "node:hold"));
            v.Nodes.Add(N("n4-1", 4, 1, 4, "normal", "kind:scavenge"));
            v.Nodes.Add(N("n4-2", 4, 2, 4, "elite", "node:elite"));
            v.Nodes.Add(N("n4-3", 4, 3, 4, "event", "node:event", true));
            for (var c = 0; c < 3; c++) v.Nodes.Add(new RunNodeView { Id = $"n5-{c}", Row = 5, Col = c, Cols = 3, Hidden = true, Card = { StrategyText.Runs.Unseen } });
            v.Nodes.Add(new RunNodeView { Id = "n6-0", Row = 6, Col = 0, Cols = 1, Hidden = true, Card = { StrategyText.Runs.Unseen } });
            v.Nodes.Add(N("n7-0", 7, 0, 1, "boss", "node:boss"));
            var byId = v.Nodes.ToDictionary(n => n.Id);
            byId["n1-1"].Done = true;
            byId["n2-1"].Done = byId["n2-1"].At = true;
            // BASTION_SIN_PLAZA=1: La Lata full, the Rescate's warning under its name and on its card (N7, 17/11).
            if (System.Environment.GetEnvironmentVariable("BASTION_SIN_PLAZA") == "1")
            {
                byId["n2-0"].Warn = EngineLore.RescueNoSeatTag;
                byId["n2-0"].Card.Add(EngineLore.RescueNoSeat);
            }
            foreach (var id in new[] { "n3-0", "n3-1" }) byId[id].Open = true;
            byId["n3-0"].ModifierId = "night";
            byId["n3-0"].Modifier = StrategyText.Runs.ModifierName("night");
            byId["n3-0"].ModifierIcon = "night";
            byId["n3-0"].Card.Add($"{byId["n3-0"].Modifier}: {StrategyText.Runs.ModifierLine("night").ToLowerInvariant()}");
            byId["n4-0"].ModifierId = "awake";
            byId["n4-0"].ModifierIcon = "node:awake";
            byId["n4-1"].ModifierId = "storm";
            byId["n4-1"].ModifierIcon = "weather:storm";
            void Link(string a, string b, bool taken = false, bool open = false, bool dim = false) => v.Links.Add(new RunLinkView { From = a, To = b, Taken = taken, Open = open, Dim = dim });
            Link(null, "n1-0"); Link(null, "n1-1", taken: true); Link(null, "n1-2");
            Link("n1-0", "n2-0"); Link("n1-0", "n2-1"); Link("n1-1", "n2-1", taken: true); Link("n1-1", "n2-2"); Link("n1-2", "n2-2"); Link("n1-2", "n2-3");
            Link("n2-0", "n3-0"); Link("n2-1", "n3-0", open: true); Link("n2-1", "n3-1", open: true); Link("n2-2", "n3-1"); Link("n2-3", "n3-2");
            Link("n3-0", "n4-0"); Link("n3-0", "n4-1"); Link("n3-1", "n4-1"); Link("n3-1", "n4-2"); Link("n3-2", "n4-3");
            Link("n4-0", "n5-0", dim: true); Link("n4-1", "n5-0", dim: true); Link("n4-1", "n5-1", dim: true); Link("n4-2", "n5-1", dim: true); Link("n4-3", "n5-2", dim: true);
            for (var c = 0; c < 3; c++) Link($"n5-{c}", "n6-0", dim: true);
            Link("n6-0", "n7-0", dim: true);
            var stash = Stash();
            v.Squad.Add(new KitTabView { Id = "s6", Name = "Rafael", On = true, Mine = true, Hp = 9, HpMax = 12 });
            v.Squad.Add(new KitTabView { Id = "s2", Name = "Inés", Mine = true, Hp = 3, HpMax = 10 });
            v.Squad.Add(new KitTabView { Id = "s4", Name = "Marta", Mine = false, Theirs = true, Color = "#c58cff", Hp = 10, HpMax = 10 });
            v.Squad.Add(new KitTabView { Id = "s5", Name = "Teo", Mine = false, Theirs = true, Color = "#c58cff", Hp = 7, HpMax = 10 });
            v.Kit = stash.Kit;
            v.Stop = Stop(which);
            return v;
        }

        /// <summary>«parada-trueque», «parada-suceso», «parada-suceso-hecho», «parada-refugio»: the stop over the map.</summary>
        static StopView Stop(string which)
        {
            var leave = new StopView { Leave = StrategyText.Runs.Leave };
            switch (which)
            {
                case "parada-trueque":
                    leave.Kind = "trade";
                    leave.Kicker = StrategyText.Runs.NodeName("trade");
                    leave.Title = "Un vendedor en una gasolinera";
                    leave.Text = StrategyText.Runs.NodeLine("trade");
                    leave.Purse = "40";
                    leave.For = StrategyText.Runs.For("Rafael");
                    leave.Wares.Add(new WareView { Gear = new GearItemView { Id = "w1", Kind = "weapon", Name = "Fusil de asalto", Icon = "weapon:rifle", W = 4, H = 1, Tip = "Fusil de asalto" }, Price = "90", Why = "Faltan 50 §." });
                    leave.Wares.Add(new WareView { Gear = new GearItemView { Id = "w2", Kind = "item", Name = "Botiquín", Icon = "firstAid", Pile = 5, Count = 1, Tip = "Botiquín" }, Price = "25" });
                    leave.Wares.Add(new WareView { Gear = new GearItemView { Id = "w3", Kind = "item", Name = "Molotov", Icon = "molotov", Pile = 5, Count = 2, Tip = "Molotov" }, Price = "20" });
                    leave.Wares.Add(new WareView { Gear = new GearItemView { Id = "w4", Kind = "vest", Name = "Chaleco antibalas", Icon = "stat:armor", W = 2, H = 2, Tip = "Chaleco antibalas" }, Price = "60", Why = "Faltan 20 §." });
                    break;
                case "parada-suceso":
                case "parada-suceso-hecho":
                    leave.Kind = "event";
                    leave.Kicker = StrategyText.Runs.NodeName("event");
                    leave.Title = "La gasolinera";
                    leave.Text = "Un coche sigue en el surtidor de una gasolinera. Al depósito le queda gasolina.";
                    if (which == "parada-suceso")
                    {
                        leave.Options.Add(new StopOptionView { Index = 0, Label = "Sacarla con la bomba", Short = "+25 § · La siguiente etapa, con la horda despierta" });
                        leave.Options.Add(new StopOptionView { Index = 1, Label = "Dejarla", Short = "Nada" });
                        leave.LeaveWhy = "Antes hay que responder.";
                    }
                    else leave.Outcome = "La bomba hace ruido, y se oye lejos. En la siguiente etapa, horda despierta.";
                    break;
                case "parada-refugio":
                    leave.Kind = "refuge";
                    leave.Kicker = StrategyText.Runs.NodeName("refuge");
                    leave.Title = "Una ermita con la puerta entera";
                    leave.Text = StrategyText.Runs.NodeLine("refuge");
                    leave.Outcome = "La escuadra descansa. Todos curados.";
                    leave.LeaveOn = true;
                    leave.Waiting = "Esperando a Ana.";
                    break;
                default:
                    return null;
            }
            return leave;
        }

        /// <summary>«almacen»: Almacén with a made-up stash and Rafael's kit (the runs, 16/11); the grids' rules are GearLab's.</summary>
        static StashScreenView Stash()
        {
            GearItemView G(string id, string kind, string type, string name, string icon, int w, int h, int count = 1, int pile = 1) =>
                new GearItemView { Id = id, Kind = kind, Type = type, Name = name, Icon = icon, W = w, H = h, Count = count, Pile = pile, Tip = name };
            PlacedView P(GearItemView g, int x, int y, bool turned = false) => new PlacedView { Gear = g, X = x, Y = y, Turned = turned };
            GearSlotView S(string slot, int w, int h, GearItemView g = null) => new GearSlotView { Owner = "s6", Slot = slot, W = w, H = h, Gear = g };
            return new StashScreenView
            {
                Head = "Almacén",
                Supplies = "135",
                SuppliesTip = "Suministros en el almacén: pagan las mejoras de la base.",
                Hint = GearText.Hint,
                Stash = new GearGridView
                {
                    Owner = "stash", W = 10, H = 6, Rows = 8,
                    Items =
                    {
                        P(G("g1", "weapon", "rifle", "Fusil de asalto", "weapon:rifle", 4, 1), 0, 0),
                        P(G("g2", "weapon", "shotgun", "Escopeta", "weapon:shotgun", 4, 1), 0, 1),
                        P(G("g3", "weapon", "lmg", "Ametralladora", "weapon:cannon", 4, 2), 4, 0),
                        P(G("g4", "weapon", "pistol", "Pistola", "weapon:pistol", 2, 1), 8, 0),
                        P(G("g5", "weapon", "bat", "Bate", "weapon:bat", 3, 1), 0, 2),
                        P(G("g8", "item", "grenade", "Granada", "grenade", 1, 1, 3, 5), 3, 2),
                        P(G("g6", "weapon", "sniper", "Fusil de precisión", "weapon:sniper", 4, 1), 4, 2),
                        P(G("g9", "item", "molotov", "Molotov", "molotov", 1, 1, 2, 5), 0, 3),
                        P(G("g10", "medicine", null, "Medicinas", "store:medicines", 1, 1, 4, 5), 1, 3),
                        P(G("g12", "mags", null, "Cargadores", "stat:ammo", 1, 1, 5, 5), 2, 3),
                        P(G("g13", "mod", "scope", "Mira", "aimedShot", 1, 1, 1, 3), 3, 3),
                        P(G("g7", "vest", "vest", "Chaleco", "stat:armor", 2, 2), 8, 4),
                        P(G("g14", "supplies", null, "Suministros", "§", 1, 1, 100, 100), 6, 5),
                        P(G("g15", "supplies", null, "Suministros", "§", 1, 1, 35, 100), 7, 5),
                        P(G("g16", "weapon", "crowbar", "Palanca", "weapon:crowbar", 3, 1), 0, 5),
                    },
                },
                Soldiers =
                {
                    new KitTabView { Id = "s6", Name = "Rafael", On = true, Mine = true },
                    new KitTabView { Id = "s2", Name = "Inés", Mine = true },
                    new KitTabView { Id = "s4", Name = "Marta", Mine = false, Theirs = true, Color = "#c58cff", Tip = "De Ana" },
                    new KitTabView { Id = "s5", Name = "Teo", Mine = false, Theirs = true, Color = "#c58cff", Tip = "De Ana" },
                },
                Kit = new KitView
                {
                    Soldier = "s6", Name = "Rafael Costa", Line = "9 de 12 de salud", Mine = true,
                    Slots =
                    {
                        S("primary", 4, 2, G("g18", "weapon", "rifle", "Fusil de asalto", "weapon:rifle", 4, 1)),
                        S("melee", 3, 1, G("g17", "weapon", "knife", "Cuchillo", "overwatchMelee", 2, 1)),
                        S("pistol", 2, 1),
                        S("vest", 2, 2),
                        S("belt0", 1, 1, G("g19", "item", "firstAid", "Botiquín", "firstAid", 1, 1, 1, 5)),
                        S("belt1", 1, 1),
                    },
                    Pack = new GearGridView
                    {
                        Owner = "s6", Slot = "pack", W = 4, H = 3,
                        Items =
                        {
                            P(G("g20", "mags", null, "Cargadores", "stat:ammo", 1, 1, 2, 5), 0, 0),
                            P(G("g21", "supplies", null, "Suministros", "§", 1, 1, 20, 100), 1, 0),
                            P(G("g22", "weapon", "pistol", "Pistola", "weapon:pistol", 2, 1), 2, 0),
                        },
                    },
                },
            };
        }

        /// <summary>«menu-invitacion»: the menu of a host whose partner isn't in, the router's port open (as RoomText.InviteOf gives it).</summary>
        static InviteView Invite() => new InviteView
        {
            Line = Bastion.UI.Start.RoomText.Invite("Arturo"),
            Addresses = { (Bastion.UI.Start.RoomText.Lan, "192.168.1.20:41391"), (Bastion.UI.Start.RoomText.Internet, "83.45.12.7:41391 · KXRT5") },
            Key = Bastion.UI.Start.RoomText.KeyLine("KXRT5"),
            Copy = Bastion.UI.Start.RoomText.Copy,
            Copied = Bastion.UI.Start.RoomText.Copied,
        };

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

        static SoldierTileView Tile(string id, string name, bool theirs, int rank = 0, bool points = false, string tag = null, string tone = null, bool selected = false) =>
            new SoldierTileView { Id = id, Name = name, Theirs = theirs, Rank = rank, Points = points, PointsTip = "Puntos por gastar en el tablero", Tag = tag, TagTone = tone, Selected = selected };

        static SoldiersView Soldiers()
        {
            var v = new SoldiersView();
            v.Mine.Add(Tile("s1", "Yuki", false, 1, true));
            v.Mine.Add(Tile("s3", "Dmitri", false, 1, true));
            v.Mine.Add(Tile("s5", "Hiro", false, 0, true, "2 misiones", "warn"));
            v.Mine.Add(Tile("s7", "Lucía", false, 0, true));
            v.Partner.Add(Tile("s0", "Anja", true));
            v.Partner.Add(Tile("s2", "Arjun", true, 0, false, "2 misiones", "warn"));
            v.Partner.Add(Tile("s4", "Chiara", true));
            v.Partner.Add(Tile("s6", "Rafael", true, 0, false, null, null, true));
            v.Recruit = new RecruitView
            {
                Label = "Reclutar",
                Cost = "30 §",
                Tip = "Un recluta nuevo: sale con cuchillo y pistola y con algo de práctica en una o dos habilidades. Llega al momento.\nTus soldados: 4 de 6. Camas libres: 0. Cada soldado necesita una cama: la autocaravana tiene 8, y cada Barracón suma más.",
                Enabled = false,
                Why = "No quedan camas: para reclutar más, construid un Barracón.",
            };
            var sheet = new SoldierSheetView
            {
                Name = "Rafael Costa", NameTip = "Misiones: 3 · Bajas: 2\nPuntería al disparar 68 · cuerpo a cuerpo 63\nVoluntad 40 · Esquiva 10",
                Hp = 10, HpTip = "Salud: 10", Ap = 2, ApTip = "2 acciones por turno", Moves = 8, MovesTip = "Movimiento: 8 casillas por turno",
                Note = "Ana no está: lo preparas tú hasta que vuelva.", Customizable = true,
            };
            var hand = new PickView { Id = "hand", Label = "Mano", Value = "Fusil de asalto", Icon = "weapon:rifle", Lines = { "Daño 3–5 · Alcance medio", "y la pistola al costado" }, Condition = 100, ConditionText = "Estado 100", Tags = { "Mira" }, Editable = true };
            hand.Options.Add(new PickOptionView { Id = "none", Name = "Sin arma: la pistola", Note = "Fusil de asalto vuelve a la Armería" });
            hand.Options.Add(new PickOptionView { Id = "w103", Name = "Fusil de precisión", Note = "En la Armería" });
            hand.Options.Add(new PickOptionView { Id = "w104", Name = "Escopeta", Note = "La lleva Hiro Sato, de Arturo", Enabled = false });
            sheet.Arms.Add(hand);
            sheet.Arms.Add(new PickView { Id = "belt", Label = "Cinto", Value = "Cuchillo", Icon = "overwatchMelee", Lines = { "Daño 2–4" }, Editable = true });
            sheet.Armor = new ArmorView { Tier = 3, Name = "Equipo antidisturbios", Figures = "10 de salud · 1 de blindaje" };
            sheet.Belt.Add(new PickView { Id = "slot0", Label = "Hueco 1", Value = "Nada", Editable = true });
            sheet.Belt.Add(new PickView { Id = "slot1", Label = "Hueco 2", Value = "Nada", Editable = true });
            var next = EngineLore.RankReward(0, "m");
            var xp = new SkillsView
            {
                Head = "Experiencia", RankLabel = "Rango", Rank = "Novato", RankFilled = 4, RankTotal = 5,
                RankTip = $"Sin rango: menos de 5 puntos gastados en el tablero.\nVoluntad {Bastion.Engine.Bars.VeteranWill(0)} · 2 acciones por turno\nSiguiente rango: {next?.Long}",
                Board = "Tablero de habilidades", BoardPoints = "3 puntos por gastar",
            };
            var filled = new[] { 0, 18, 10, 3, 2, 0 };
            var growth = new[] { "+8 %", null, null, null, null, "+10 %" };
            for (var i = 0; i < 6; i++)
            {
                var skill = Bastion.Engine.SkillIds.All[i];
                var reward = EngineLore.BarReward(skill);
                var def = Bastion.Engine.Bars.SkillDefs[skill];
                xp.Rows.Add(new SkillRowView { Name = def.Name, Tip = $"{def.Line}\n{reward.Long}", Growth = growth[i], GrowthTip = def.Stat, Value = filled[i], Share = filled[i] / 20f });
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
            // The engine's heads and names (15 since the 0.12.15; «Gorra» became «Gorra con linterna», Lore).
            c.Groups.Add(Group("head", "Cabeza", "A cara descubierta", Bastion.Engine.HeadIds.All.Select(id => Bastion.Engine.Appearances.Heads[id].Name).ToArray()));
            c.Groups.Add(Group("build", "Complexión", "Normal", "Delgada", "Normal", "Robusta"));
            var swatches = new List<string> { "#5d5a7a", "#6b6b4a", "#7a6a4a", "#4a4f4a", "#4f5f4a", "#6f6f6f", "#2f3236", "#5a3a3a", "#3f4f3a", "#4a5a7a", "#59606a" };
            void Row(string id, string label, string note, bool locked = false) => c.Colors.Add(new ColorRowView { Id = id, Label = label, Note = note, Color = "#5d5a7a", Swatches = new List<string>(swatches), Locked = locked });
            Row("all", "Toda la armadura", "todas las piezas");
            Row("helmet", "Casco", "A cara descubierta: nada que pintar", true);
            Row("torso", "Torso", "como la armadura");
            Row("arms", "Brazos", "como la armadura");
            Row("legs", "Piernas", "como la armadura");
            Row("trim", "Detalles", "gorras, gorros, capuchas, boina y pañuelo");
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

        static PickRowView Pick(string id, string name, string accent, int rank, string chip = null, string tone = null, bool inSquad = false, bool enabled = true) =>
            new PickRowView
            {
                Id = id, Name = name, Accent = accent, Skills = new float[] { 30, 20, 10, 10, 5, 10 }, Rank = rank, Chip = chip, ChipTone = tone, In = inSquad, Enabled = enabled,
                ChipTip = chip == "✓" ? StrategyText.Squad.InTip : chip != null ? "Heridas" : null,
                Peek = new SoldierPeekView
                {
                    Name = name, Kicker = (rank > 0 ? "Fogueado" : "Novato") + (accent == Arturo ? " · tuyo" : " · de Ana"),
                    Stats = { new PeekStatView { Name = "Salud", Value = "7" }, new PeekStatView { Name = "Puntería", Value = rank > 0 ? "72" : "65" }, new PeekStatView { Name = "Movimiento", Value = "8" } },
                    Weapon = "Fusil de asalto · Machete", Belt = "Botiquín · Granada de humo",
                    Abilities = rank > 0 ? new List<string> { "Pulso firme", "Tajo" } : new List<string>(),
                    Health = tone == "warn" ? "Heridas · " + chip : "Sin heridas",
                    HealthTone = tone == "warn" ? "warn" : null,
                },
            };

        /// <summary>Who stands in the lab's line-up, in order: the toggles of «escuadra» take them out and put them in (Arturo, 1/11 #1).</summary>
        static readonly List<(string id, string name, string rank)> labSquad = new List<(string, string, string)> { ("s1", "Yuki Tanaka", "Fogueada"), ("s7", "Lucía Ortega", "Novata"), ("s3", "Dmitri Volkov", "Fogueado") };
        static readonly Dictionary<string, (string name, string rank)> labRoster = new Dictionary<string, (string, string)> { ["s1"] = ("Yuki Tanaka", "Fogueada"), ["s7"] = ("Lucía Ortega", "Novata"), ["s3"] = ("Dmitri Volkov", "Fogueado"), ["s5"] = ("Hiro Sato", "Novato") };

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
            v.Mine.Add(Pick("s5", "Hiro Sato", Arturo, 0, "2 misiones", "warn", false, false));
            v.Mine.Add(Pick("s3", "Dmitri Volkov", Arturo, 1, "✓", "good", true));
            v.Partner.Add(Pick("s6", "Rafael Costa", Ana, 0, null, null, false, false));
            v.Partner.Add(Pick("s2", "Arjun Mehta", Ana, 0, "2 misiones", "warn", false, false));
            v.Partner.Add(Pick("s4", "Chiara Rossi", Ana, 0, null, null, false, false));
            v.Partner.Add(Pick("s0", "Anja Kowalska", Ana, 0, null, null, false, false));
            foreach (var (id, name, rank) in labSquad) v.Cards.Add(new LineupCardView { Id = id, Name = name, Accent = Arturo, Lines = { rank }, Removable = true });
            while (v.Cards.Count < 4) v.Cards.Add(new LineupCardView { Tip = "Elige un soldado a la izquierda" });
            foreach (var row in v.Mine)
            {
                row.In = labSquad.Any(e => e.id == row.Id);
                if (row.Enabled) (row.Chip, row.ChipTone) = row.In ? ("✓", "good") : (null, null);
            }
            v.MineHead = $"Tus soldados · {labSquad.Count}/4";
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
            // Forma física's bar in points of dodge (0.12.1, Lore's BarFillFloat): the longest of the bars' tags.
            v.Cards.Add(Card("s7", "Lucía Ortega", Ana, "0 bajas", Skill("+7 Disparo"), new LearnedView { Kind = "bar", Line = 5, Text = "▲ FORMA FÍSICA +3 DE ESQUIVA" }, new LearnedView { Kind = "point", Count = 1 }));
            v.Cards.Add(Card("s6", "Rafael Costa", Ana, "2 bajas", Skill("+10 Disparo"), new LearnedView { Kind = "bar", Line = 0, Text = "▲ DISPARO +8 %" }, new LearnedView { Kind = "point", Count = 1 }));
            return v;
        }

        const string Story = "#4a7dff";
        const string Op = "#ffb030";
        const string Nest = "#ff4a5a";

        /// <summary>
        /// «muestra-…»: the presenter's views from the sample campaign itself (SampleCampaign), not made-up ones: as Arturo
        /// (slot 1) with Ana away, as the 0.10.1's references. For now the strip, the tabs and the Asentamiento
        /// (muestra-asentamiento; -bastion with the caravan picked); with «-radio», the recap over it.
        /// </summary>
        /// <summary>A frame of BASTION_ESTRATEGIA_ARRASTRE: down on the pad's middle, then 10 points on, then up at the end.</summary>
        void Drag()
        {
            var pad = UnityEngine.UIElements.UQueryExtensions.Q(root.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement, className: "soldiers-turn");
            if (pad == null)
            {
                Debug.Log("[estrategia] arrastre: no hay dónde girar");
                drag = 0;
                return;
            }
            var at = pad.worldBound.center + new Vector2(dragged, 0);
            if (dragged == 0) Send(pad, EventType.MouseDown, at);
            var step = Mathf.Sign(drag) * Mathf.Min(10, Mathf.Abs(drag) - Mathf.Abs(dragged));
            dragged += step;
            Send(pad, EventType.MouseDrag, at + new Vector2(step, 0));
            if (Mathf.Abs(dragged) < Mathf.Abs(drag)) return;
            Send(pad, EventType.MouseUp, at + new Vector2(step, 0));
            Debug.Log($"[estrategia] arrastre de {dragged} puntos sobre el claro (frame {Time.frameCount})");
            drag = 0;
        }

        /// <summary>BASTION_ESTRATEGIA_FIGURA: the pointer onto the figure, or a click on it.</summary>
        void Figure()
        {
            var pad = UnityEngine.UIElements.UQueryExtensions.Q(root.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement, className: "soldiers-turn");
            var what = figure;
            figure = null;
            if (pad == null) return;
            var at = pad.worldBound.center;
            Send(pad, EventType.MouseMove, at);
            if (what == "clic")
            {
                Send(pad, EventType.MouseDown, at);
                Send(pad, EventType.MouseUp, at);
            }
            Debug.Log($"[estrategia] figura: {what} en {at}");
        }

        /// <summary>
        /// BASTION_ESTRATEGIA_LUGARES=1 on the Mapa: the six places' drawings in a strip low on the screen (LineIcon
        /// place:…; Arturo 31/10 #9), at the card's 16 px and at 48, each with its name, to see them all in one shot;
        /// BASTION_ESTRATEGIA_TIEMPOS=1, the five weathers' (weather:…; Arturo, 01/11), with the first offer that has one open.
        /// </summary>
        void PlaceDrawings()
        {
            Drawings("place:", new[] { Bastion.Engine.Places.City, Bastion.Engine.Places.Town, Bastion.Engine.Places.Estate, Bastion.Engine.Places.Factory, Bastion.Engine.Places.Roadside, Bastion.Engine.Places.Country, Bastion.Engine.Places.Hospital, Bastion.Engine.Places.Offices, Bastion.Engine.Places.Shed, Bastion.Engine.Places.Police, Bastion.Engine.Places.Plaza, Bastion.Engine.Places.Camping }.Select(id => (id, Bastion.Engine.Places.Names.TryGetValue(id, out var name) ? name : id)));
        }

        void WeatherDrawings()
        {
            Drawings("weather:", Bastion.Engine.Weathers.All.Select(id => (id, HudText.Weather.Name(id))));
        }

        void Drawings(string prefix, System.Collections.Generic.IEnumerable<(string id, string name)> items)
        {
            var panel = GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
            var strip = new UnityEngine.UIElements.VisualElement();
            strip.style.position = UnityEngine.UIElements.Position.Absolute;
            strip.style.left = 400;
            strip.style.bottom = 90;
            strip.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
            strip.style.backgroundColor = new Color(0.08f, 0.09f, 0.1f, 0.95f);
            strip.style.paddingLeft = strip.style.paddingRight = strip.style.paddingTop = strip.style.paddingBottom = 12;
            foreach (var (id, label) in items)
            {
                var cell = new UnityEngine.UIElements.VisualElement();
                cell.style.alignItems = UnityEngine.UIElements.Align.Center;
                cell.style.marginLeft = cell.style.marginRight = 10;
                foreach (var size in new[] { 16f, 48f })
                {
                    var box = new UnityEngine.UIElements.VisualElement();
                    box.style.width = box.style.height = size;
                    box.style.marginBottom = 6;
                    box.style.unityBackgroundImageTintColor = Color.white;
                    box.Add(new LineIcon(prefix + id));
                    cell.Add(box);
                }
                var name = new UnityEngine.UIElements.Label(label);
                name.style.color = Color.white;
                cell.Add(name);
                strip.Add(cell);
            }
            panel.Add(strip);
        }

        static void Send(UnityEngine.UIElements.VisualElement to, EventType type, Vector2 at)
        {
            var e = new Event { type = type, mousePosition = at, button = 0, clickCount = 1 };
            using (var pe = type == EventType.MouseDown ? (UnityEngine.UIElements.EventBase)UnityEngine.UIElements.PointerDownEvent.GetPooled(e)
                : type == EventType.MouseUp ? UnityEngine.UIElements.PointerUpEvent.GetPooled(e) : UnityEngine.UIElements.PointerMoveEvent.GetPooled(e))
            {
                pe.target = to;
                to.SendEvent(pe);
            }
        }

        /// <summary>
        /// «runs-…»: one of Gameplay's samples (.dev-snapshots/gameplay/runs/muestras/<name>.json: base-vacia, base-llena,
        /// mapa-fila3, trueque, suceso; or genrescate's, by its path: .dev-snapshots/ui/unity/runs/n7/lab) through the presenter, as player 0; the grids with the engine's own
        /// rules, its moves applied here (RunsGear). BASTION_RUNS_ESCENA: almacen, else the map (with its stop) in a run.
        /// </summary>
        void RunsSample(string which)
        {
            // An absolute path (a dump of the pass), or one of Gameplay's by name.
            var path = which.StartsWith("/") ? which : System.IO.Path.Combine(Application.dataPath, "../../../.dev-snapshots/gameplay/runs/muestras", which + ".json");
            // Brought up to today's version as the room does a save it loads (Campaign.MigrateRuns: the 18/11 samples
            // still had a Superviviente stop, gone with N3, and 4 seats).
            var state = Bastion.Engine.Campaign.MigrateRuns(Bastion.Net.Json.Read<Bastion.Engine.CampaignState>(System.IO.File.ReadAllText(path)));
            var p = new StrategyPresenter { State = state, Slot = 0, Seats = SampleCampaign.Seats(state.Players.Count > 1) };
            // Their busts, as BaseController asks for them with each campaign (Almacén's and the map's tabs, T5).
            Bastion.Client.Portraits.Know(state);
            var gear = new RunsGear(p, () => Show(p, null));
            root.BindGear(gear);
            runsShow = id => Show(p, id);
            Show(p, null);
            // BASTION_ESTRATEGIA_LUGARES=1: the places' drawings, the special maps' too, in a strip (Arturo, 17/11).
            if (System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_LUGARES") == "1") PlaceDrawings();
        }

        string seriesHelp;
        int seriesHelpAt;

        /// <summary>«class[:n|:off]»: that element's help open (the n-th with the class, or the first whose key is off), scrolled into sight.</summary>
        void PinHelp(string help)
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

        System.Action<string> runsShow;
        readonly string[] series = System.Environment.GetEnvironmentVariable("BASTION_RUNS_SERIE")?.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
        int seriesNext;
        float seriesAt = 11;
        string seriesScene;

        void Show(StrategyPresenter p, string chosen)
        {
            if (chosen != null) runsChosen = chosen;
            var scene = series != null ? seriesScene : System.Environment.GetEnvironmentVariable("BASTION_RUNS_ESCENA");
            // «respondido»: the sample's event answered with its first option, by Anja, as the engine leaves it (H4).
            if (scene == "respondido" && p.State.Run?.Stop is Bastion.Engine.StopState stop && stop.Event != null)
            {
                stop.Result = new Bastion.Engine.EventResult { Event = stop.Event, Option = 0, Soldier = "c1", SoldierName = "Anja" };
                stop.Outcome = stop.Event + ".0";
            }
            // «parte»: a stage's report made up over the sample: things found, a reward of which some didn't fit, one fallen.
            if (scene == "parte" && p.State.LastReport == null)
            {
                var c = p.State;
                var squad = c.Soldiers.Where(x => x.Alive).Take(4).ToList();
                var gone = squad.Last();
                var found = c.Stash.Items.Take(4).Select(x => x.Gear).ToList();
                c.LastReport = new Bastion.Engine.MissionReport
                {
                    MissionName = "Limpieza · Pueblo", Victory = true,
                    Squad = squad.Select(x => new Bastion.Engine.ReportSoldier { Id = x.Id, Name = x.Name, Owner = x.Owner, Status = x == gone ? "dead" : "ok", Kills = 2 }).ToList(),
                    Run = new Bastion.Engine.RunReport
                    {
                        Node = "n3-0", NodeKind = "elimination", Found = found, Reward = 60, RewardLost = 20,
                        Fallen = { new Bastion.Engine.RunFallen { Id = gone.Id, Name = gone.Name, Lost = gone.Kit?.Primary != null ? new List<Bastion.Engine.Gear> { gone.Kit.Primary } : new List<Bastion.Engine.Gear>() } },
                        Hp = squad.Where(x => x != gone).ToDictionary(x => x.Id, x => 3),
                    },
                };
            }
            var view = p.State.Run != null && (scene == null || scene == "respondido") ? "runmap" : scene == "parte" ? "debrief" : scene == "mejoras" ? "upgrades" : scene == "salir" ? "squad" : scene == "soldados" || scene == "personalizar" ? "barracks" : "stash";
            root.SetTop(p.Top());
            root.SetNav(p.Nav(view));
            root.SetScreen(view);
            // «personalizar»: Personalizar open on the player's first soldier, the draft as they are (T1).
            var pick = new SoldiersPick { Soldier = runsChosen ?? p.State.Soldiers.FirstOrDefault(s => s.Owner == 0 && s.Alive)?.Id };
            if (scene == "personalizar" && p.State.Soldiers.Find(s => s.Id == pick.Soldier) is Bastion.Engine.CampaignSoldier own)
                pick.Draft = new CustomDraft { Soldier = own.Id, Name = own.Name, Nickname = own.Nickname ?? "", Look = CustomDraft.Copy(own.Appearance) ?? Bastion.Engine.Appearances.DefaultAppearance(own.Owner, null, null, own.Id) };
            if (view == "runmap") root.SetRunMap(p.Expedition(runsChosen));
            // The stage map's proposals for the cuadro (BASTION_MAPA_PROPUESTA: carretera, filas, valle), over it, once laid out.
            if (view == "runmap" && MapProposals.Kind != null)
            {
                var doc = root.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
                void Draw() => MapProposals.Draw(doc, p, p.Expedition(runsChosen), root.IconFor, id => root.PortraitOf?.Invoke(id));
                // Drawn again as the busts come (they are drawn after the screen).
                if (!proposalBusts)
                {
                    proposalBusts = true;
                    Bastion.Client.Portraits.Changed += _ => Draw();
                }
                // (A capture's scheduler doesn't run: drawn now if laid out, else at the first layout.)
                if (!float.IsNaN(doc.layout.width) && doc.layout.width > 0) Draw();
                else
                {
                    UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.GeometryChangedEvent> once = null;
                    once = _ => { doc.UnregisterCallback(once); Draw(); };
                    doc.RegisterCallback(once);
                }
            }
            // The loading window's proposals (M2, BASTION_CARGA: a, b, c) over the map on paper, once it is laid out (it reads
            // the marks' places from it), and again as the busts come.
            else if (view == "runmap" && LoadProposals.Kind != null)
            {
                var doc = root.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
                void Draw() => LoadProposals.Draw(doc, p.Expedition(runsChosen), root.IconFor, id => root.PortraitOf?.Invoke(id));
                if (!proposalBusts)
                {
                    proposalBusts = true;
                    Bastion.Client.Portraits.Changed += _ => Draw();
                }
                UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.GeometryChangedEvent> laid = null;
                laid = _ => Draw();
                UnityEngine.UIElements.UQueryExtensions.Q(doc, className: "runmap-field")?.RegisterCallback(laid);
                if (!float.IsNaN(doc.layout.width) && doc.layout.width > 0) Draw();
            }
            else if (view == "upgrades") root.SetUpgrades(p.Upgrades());
            else if (view == "squad") root.SetSquad(p.RunSquad());
            else if (view == "barracks") root.SetSoldiers(p.Soldiers(pick));
            else if (view == "debrief") root.SetDebrief(p.Debrief());
            else root.SetStash(p.StashScreen(runsChosen));
            // In Arte's scene «Base»: the runs' clearing as BaseController shows it, the tab's shot, and Soldados' soldier (with
            // the draft's look while customizing).
            if (Bastion.Client.Settlement.Instance is Bastion.Client.Settlement clearing)
            {
                clearing.SetVehicle(Bastion.Engine.Runs.VehicleOf(p.State));
                clearing.ShowFixed();
                clearing.SetView(view == "stash" ? "barracks" : view == "runmap" ? "geoscape" : view == "upgrades" ? "facilities" : view, true);
                clearing.Customizing = view == "barracks" && pick.Draft != null;
                if (view == "barracks") clearing.Showcase(p.State, pick.Soldier, pick.Draft?.Look);
                // Almacén's chosen soldier, and the squad by La Lata (the debrief's), as BaseController.Dress does (Arte, 18/11:
                // «Partir» showed the cards over nobody).
                if (view == "stash") clearing.Showcase(p.State, p.StashScreen(runsChosen)?.Kit?.Soldier, null);
                var report = view == "debrief";
                var (mirror, halves) = p.LineupSides(report);
                clearing.SetLineup(p.State, p.Lineup(report), mirror, halves);
            }
        }

        string runsChosen;
        bool proposalBusts;

        /// <summary>The grids of a runs sample with the engine's rules: each move asked (Gears.*Refusal) and applied as the server would.</summary>
        sealed class RunsGear : IGearHandlers
        {
            readonly StrategyPresenter p;
            readonly System.Action changed;

            public RunsGear(StrategyPresenter p, System.Action changed)
            {
                this.p = p;
                this.changed = changed;
            }

            System.Collections.Generic.List<int> Connected => p.Seats.Where(s => s.Connected).Select(s => s.Slot).ToList();

            public string GearRefusal(GearMove m) => p.GearRefusal(m);

            public void GearMoved(GearMove m)
            {
                var c = p.State;
                var cmd = p.GearCommand(m);
                Debug.Log($"[estrategia] {cmd.Type}: {m}");
                switch (cmd)
                {
                    case Bastion.Engine.MountCommand mc: Bastion.Engine.Gears.Mount(c, mc); break;
                    case Bastion.Engine.SplitGearCommand sc: Bastion.Engine.Gears.SplitGear(c, p.Slot, Connected, sc); break;
                    case Bastion.Engine.MoveGearCommand mv: Bastion.Engine.Gears.Move(c, p.Slot, Connected, mv); break;
                }
                changed();
            }

            public void GearDropped(string gear)
            {
                Bastion.Engine.Gears.Drop(p.State, new Bastion.Engine.DropGearCommand { Gear = gear });
                changed();
            }

            public System.Collections.Generic.IReadOnlyList<GearActionView> GearActions(string gear) => p.GearActions(gear);

            public void GearAction(string gear, string action)
            {
                var c = p.State;
                switch (p.GearActionCommand(gear, action))
                {
                    case Bastion.Engine.UnmountCommand u: Bastion.Engine.Gears.Unmount(c, u); break;
                    case Bastion.Engine.UseGearCommand g: Bastion.Engine.Gears.Use(c, g); break;
                }
                changed();
            }
        }

        void Sample(string which)
        {
            // Ana here only for the proposals (70- and 71-propuesta), as the 0.10.1's steps set her connected.
            var p = new StrategyPresenter { State = SampleCampaign.Load(), Slot = 1, Seats = SampleCampaign.Seats(which.StartsWith("propuesta") || which == "esperar-pedido" || which == "escuadra-listo" || which == "armeria-arma-ana") };
            // «escuadra-listo»: Ana here and Arturo ready (his cards' «LISTO»); «parte-caido»: the debrief's first soldier fallen («CAÍDO»).
            if (which == "escuadra-listo") p.State.LaunchReady[1] = true;
            if (which == "parte-caido" && p.State.LastReport?.Squad.Count > 0) p.State.LastReport.Squad[0].Status = "dead";
            // «parte-mezcla», «parte-mezcla-ana»: the debrief of both commanders' soldiers, mixed in the report (every other
            // one Arturo's), as Arturo sees it and as Ana does (A6 of 2/11: each their own on their left).
            if (which.StartsWith("parte-mezcla") && p.State.LastReport != null)
            {
                var squad = p.State.LastReport.Squad;
                for (var i = 1; i < squad.Count; i += 2)
                {
                    squad[i].Owner = 1;
                    var s = p.State.Soldiers.Find(x => x.Id == squad[i].Id);
                    if (s != null) s.Owner = 1;
                }
                if (which.EndsWith("-ana")) p.Slot = 0;
            }
            // «armeria-nuevas», «soldados-nuevas»: the weapons found in the field (Ana's notes, Arturo 29/10) on the rack too.
            if (which.EndsWith("-nuevas"))
            {
                p.State.FoundWeapons ??= new List<Bastion.Engine.FoundWeapon>();
                var kinds = new[] { Bastion.Engine.FoundWeaponIds.Pan, Bastion.Engine.FoundWeaponIds.Bat, Bastion.Engine.FoundWeaponIds.Crowbar, Bastion.Engine.FoundWeaponIds.Chainsaw };
                for (var i = 0; i < kinds.Length; i++) p.State.FoundWeapons.Add(new Bastion.Engine.FoundWeapon { Id = "wn" + i, Kind = kinds[i] });
            }
            // «…-pistolas»: the pistols counted (Arturo, 01/11 #10: CampaignState.Pistols): one at Lucía's side by her shotgun,
            // one on the Armería's rack, and Yuki with nothing in the hand nor at the side (the knife alone). «…-nivel1»: the
            // Bastión at its first level (the shop's grenades wait for the third).
            if (which.Contains("-pistolas"))
            {
                p.State.Pistols = true;
                p.State.FoundWeapons.RemoveAll(w => w.Holder == "c4");
                p.State.FoundWeapons.Add(new Bastion.Engine.FoundWeapon { Id = "wp1", Kind = Bastion.Engine.FoundWeaponIds.Pistol, Holder = "c2" });
                p.State.FoundWeapons.Add(new Bastion.Engine.FoundWeapon { Id = "wp2", Kind = Bastion.Engine.FoundWeaponIds.Pistol });
            }
            if (which.Contains("-nivel1")) p.State.Level = 1;
            // «parte-capitulo»: the debrief of the second chapter won, with its page open (Ana's #15, Arturo 29/10).
            if (which == "parte-capitulo" && p.State.LastReport != null)
            {
                p.State.LastReport.Story = 2;
                p.State.LastReport.Victory = true;
            }
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
            var screen = which.StartsWith("asentamiento") ? "facilities" : which == "escuadra" || which == "escuadra-listo" || which == "lanzar" ? "squad" : which.StartsWith("parte") ? "debrief"
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
            // The 0.10.1's 10-soldados: the first of Ana's soldiers picked, as its steps did.
            // 72-acontecimiento was shot after 50-escuadra (its mission chosen), on Arturo's first soldier.
            if (which.StartsWith("acontecimiento"))
            {
                p.State.ActiveMission = p.State.Offers[0].Id;
                root.SetNav(p.Nav(screen));
            }
            var soldiersPick = new SoldiersPick { Soldier = which.StartsWith("acontecimiento") ? null : p.State.Soldiers.Find(s => s.Owner == 0 && s.Alive)?.Id };
            // BASTION_ESTRATEGIA_SOLDADO=c4: that soldier's sheet instead.
            var sheetOf = System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_SOLDADO");
            if (!string.IsNullOrEmpty(sheetOf)) soldiersPick.Soldier = sheetOf;
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
                // The runs as BaseController shows them: the fixed clearing and the seats' La Lata.
                if (p.State.Stash != null)
                {
                    clearing.SetVehicle(Bastion.Engine.Runs.VehicleOf(p.State));
                    clearing.ShowFixed();
                }
                else clearing.Show(p.State);
                var (mirror, halves) = p.LineupSides(screen == "debrief");
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
                // The vote inside the window, not the plate behind it too (BaseController).
                var shownEvent = p.Event(screen);
                root.SetProposal(shownEvent?.Vote != null ? null : p.Proposal(), p.Waiting(screen));
                root.SetModal(shownEvent);
            }
            // 12-tablero: Rafael's board, as Arturo sees a soldier of Ana's.
            if (which == "tablero") root.SetSkillBoard(p.SkillBoard(p.State.Soldiers.Find(s => s.Owner == 0 && s.Alive)?.Id));
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
        }

        /// <summary>The Bastion's level in the sample's strip: 3, or BASTION_ESTRATEGIA_NIVEL.</summary>
        static readonly int Level = int.TryParse(System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA_NIVEL"), out var n) ? n : 3;

        async void LaunchSoon(Bastion.Client.Settlement clearing)
        {
            // Three seconds in: the busts drawn and the row standing, for a strip that starts just before.
            await Awaitable.WaitForSecondsAsync(3f, destroyCancellationToken);
            root.Launching(true);
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
        public void OnSoldier(string id)
        {
            Debug.Log("[estrategia] soldier " + id);
            runsShow?.Invoke(id);
        }
        public void OnRecruit() => Debug.Log("[estrategia] recruit");
        public void OnCustomize() => Debug.Log("[estrategia] customize");
        public void OnCare(int index) => Debug.Log("[estrategia] care " + index);
        public void OnPick(string pick, string option) => Debug.Log($"[estrategia] pick {pick} {option}");
        public void OnBoard() => Debug.Log("[estrategia] board");
        public void OnBoardFor(string soldier) => Debug.Log($"[estrategia] board {soldier}");
        public void OnCustomName(string name, string nickname) => Debug.Log($"[estrategia] name {name} «{nickname}»");
        public void OnCustomChoice(string group, string id, bool preview) => Debug.Log($"[estrategia] choice {group} {id} {preview}");
        public void OnCustomColor(string part, string color, bool preview) => Debug.Log($"[estrategia] color {part} {color} {preview}");
        public void OnCustomAction(string action) => Debug.Log("[estrategia] custom " + action);
        public void OnUnlock(string cell) => Debug.Log("[estrategia] unlock " + cell);
        public void OnBoardClose() => root.SetSkillBoard(null);
        public void OnSquadToggle(string id, bool on)
        {
            Debug.Log($"[estrategia] squad {id} {on}");
            // «escuadra»: the line-up as the toggle leaves it, drawn again as the game's presenter would (Arturo, 1/11 #1).
            if (Case != "escuadra" || !labRoster.TryGetValue(id, out var who)) return;
            labSquad.RemoveAll(e => e.id == id);
            if (on && labSquad.Count < 4) labSquad.Add((id, who.name, who.rank));
            root.SetSquad(Squad());
            Debug.Log($"[estrategia] escuadra: {string.Join(", ", labSquad.Select(e => e.name))}");
        }
        public void OnLaunch() => Debug.Log("[estrategia] launch");
        public void OnSquadBack() => Debug.Log("[estrategia] squad back");
        public void OnSquadRecruit() => Debug.Log("[estrategia] squad recruit");
        public void OnDebriefContinue() => Debug.Log("[estrategia] debrief continue");
        public void OnWait() => Debug.Log("[estrategia] wait");
        public void OnNode(string id) => Debug.Log("[estrategia] etapa " + id);
        public void OnEndRun() => Debug.Log("[estrategia] volver a la base");
        public void OnBuy(string ware) => Debug.Log("[estrategia] cambiar " + ware);
        public void OnSell(string gear) => Debug.Log("[estrategia] ofrecer " + gear);
        public void OnLeaveStop() => Debug.Log("[estrategia] seguir");
        public void OnUpgrade(string id) => Debug.Log("[estrategia] mejorar " + id);
    }
}
