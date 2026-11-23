using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Gear;
using Bastion.UI.Hud;
using UnityEngine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base over the network (screen.ts StrategyScreen: its state, update and handlers; UI's). The campaign the host
    /// sends is drawn through the presenter; what is open stays here (the tab, the soldier, the board, the windows
    /// waiting); every button that changes the campaign goes to the host as its CampaignCommand, and the host answers
    /// with the whole campaign (or `rejected` and why, a note). Arte's clearing (the scene «Base», Settlement), fixed in
    /// the runs, follows: the tab, the plates on screen, Soldados' and Almacén's soldier and Personalizar's draft; and the
    /// busts (Portraits) know every look. The grids are the runs' (IGearHandlers).
    /// </summary>
    public sealed class BaseController : IStrategyHandlers, IGearHandlers
    {
        readonly StrategyRoot root;
        readonly Action<ClientMessage> send;
        readonly StrategyPresenter p;
        string view = BaseViews.Barracks;
        readonly SoldiersPick soldiers = new SoldiersPick();
        /// <summary>The soldier whose skill board is open; null, closed.</summary>
        string board;
        /// <summary>The windows of what happened (collectAlerts, the premise), one at a time, the first on screen.</summary>
        readonly List<StrategyModalView> alerts = new List<StrategyModalView>();
        StrategyModalView shown;
        bool leaving;
        bool quitting;
        /// <summary>«Volver a la base» asked first (it ends the expedition at once when alone).</summary>
        bool endingRun;
        /// <summary>When the proposal goes ahead on its own (the room's deadline) on this machine's clock, and how long it was.</summary>
        float? dueAt;
        float dueTotal;
        bool viewSent;

        /// <summary>This machine's clock, in seconds.</summary>
        public Func<float> Now = () => Time.realtimeSinceStartup;

        /// <summary>«Salir de Bastión» said yes to (AppFlow.Quit).</summary>
        public Action Quit;

        /// <summary>This app hosts the game: leaving it disconnects the partner (Lore.QuitHostWarning).</summary>
        public Func<bool> Hosting = () => false;

        /// <summary>`slot`: this player's seat; `send`: a message to the host.</summary>
        public BaseController(StrategyRoot root, int slot, Action<ClientMessage> send)
        {
            this.root = root;
            this.send = send;
            p = new StrategyPresenter { Slot = slot };
            root.Bind(this);
            root.BindGear(this);
            root.ClockOf = Clock;
            root.MenuChose += MenuChose;
            // Ajustes: kept and applied; drawn again (Personalizar's colours wait on «Ropa con el color del jugador»).
            root.SettingChanged += (id, value) =>
            {
                App.GameSettings.Change(id, value);
                Render();
            };
        }

        /// <summary>The campaign as the squad took off, when the base is made anew after a mission (main.ts's `before`).</summary>
        public CampaignState Before;

        /// <summary>Back from a mission, La Lata still to come in (screen.ts arriving).</summary>
        bool arriving;

        public CampaignState State => p.State;
        /// <summary>The stage being gone to on paper (cuadro 136), for the loading window; null, no stage of an expedition.</summary>
        public Bastion.UI.App.LoadView StageSheet() => p.StageSheet(chosenNode);

        public string View => view;

        /// <summary>For tests (BaseRun «esc», «b»): what is open, in a line for the log.</summary>
        public string OpenNow() => $"vista {view}, desplegable {root.SoldierChoice ?? "ninguno"}, Personalizar {(soldiers.Draft != null ? "abierto" : "cerrado")}, menú {(root.MenuOpen ? "abierto" : "cerrado")}, ventana {(root.ModalOpen ? "abierta" : "no")}, tablero {(board != null ? "abierto" : "cerrado")}";

        /// <summary>For tests (BaseRun «choice:hand»): the sheet's choice opened, as its head's click.</summary>
        public void OpenChoice(string id) => root.OpenSoldierChoice(id);

        // ------------------------------------------------------------ the host

        /// <summary>
        /// The campaign as the host has it now (screen.ts update): the windows for what changed since the last one, the
        /// premise once, the hangar when a mission is chosen and the debrief when one is back; then drawn.
        /// </summary>
        public void Campaign(CampaignState c)
        {
            if (c == null) return;
            // A base made anew after a mission compares with it as it took off: the mission's news still pop up; and La
            // Lata comes back the first time the clearing is in view.
            if (p.State == null && Before != null) arriving = true;
            var prev = p.State ?? Before;
            Before = null;
            p.State = c;
            // The runs: the same fixed clearing every time, nothing built on it (Arte, 16/11); La Lata the one the seats give,
            // as in the fight (Runs.VehicleOf; N6, 17/11: else the clearing's own, the bus), before it is drawn.
            if (c.Stash != null)
            {
                Settlement.Instance?.SetVehicle(Runs.VehicleOf(c));
                Settlement.Halt = HaltNow(c);
                Settlement.Instance?.ShowFixed();
            }
            else Settlement.Instance?.Show(c);
            Portraits.Know(c);
            // The draft's own bust, drawn again over what Know just put back.
            if (soldiers.Draft != null) Portraits.Know(soldiers.Draft.Soldier, soldiers.Draft.Look, NameOf(soldiers.Draft.Soldier));
            // One's own proposal that costs: said at once that nothing is paid until the yes (playtest 16/10 #5).
            if (c.Proposal != null && c.Proposal.By == p.Slot && prev?.Proposal?.Text != c.Proposal.Text && p.Proposal()?.Cost != null)
                root.Toast($"Propuesto a {PartnerName}: se paga cuando diga que sí.");
            if (c.Proposal == null) dueAt = null;
            // The partner's proposal waits on its plate: it blinks, never taking this player away (screen.ts update); and so
            // does the plate when the partner just said «a punto» for the mission or the siege (renderWaiting).
            var partnerSlot = 1 - p.Slot;
            if (c.Proposal != null && c.Proposal.By != p.Slot && prev?.Proposal?.Text != c.Proposal.Text) root.Blink("strat-proposal");
            if (prev != null && c.Players.Count > 1 && c.LaunchReady[partnerSlot] && !prev.LaunchReady[partnerSlot] && !c.LaunchReady[p.Slot]) root.Blink("strat-proposal");
            // The partner's «a punto», heard even with a proposal blinking (screen.ts).
            if (prev != null && c.Players.Count > 1 && c.LaunchReady[partnerSlot] && !prev.LaunchReady[partnerSlot] && !c.LaunchReady[p.Slot]) Bastion.Client.Audio.UiSounds.Play("bell");
            if (prev != null) alerts.AddRange(p.Alerts(prev));
            // A commander left with nobody alive gets a recruit (Campaign.NobodyLeft; Arturo, 2/11): its line in the debrief
            // back from the mission, or a note when it came with a day gone by (the base shows no log of its own).
            var joined = prev != null ? p.FreeRecruit(prev) : null;
            // Whoever came in after the stage has no campaign of before it to tell the recruit by, or one with them in it
            // already (Red's N9, 17/11: a seat taken after the squad fell): the recruits its report names.
            if (joined == null && p.Joined == null && ReportPending(c)) joined = p.ReportRecruits();
            if (joined != null)
            {
                if (ReportPending(c)) p.Joined = joined;
                else root.Toast(joined);
            }
            // The clock as the days pass.
            if (prev != null && c.Day > prev.Day) Bastion.Client.Audio.UiSounds.Play("tick");
            var premise = p.Premise();
            if (premise != null) alerts.Insert(0, premise);
            // A save of the 0.13.0 down to 2 seats, said once (N4).
            if (p.SeatsReset() is StrategyModalView seats) alerts.Add(seats);
            // A mission chosen by either player sends both to the hangar; a finished one to the debrief. A new campaign
            // opens there too, under its premise, with its first operation chosen (Ana, 7/11 #20: it came up on Soldados),
            // whatever state came before it.
            if (c.ActiveMission != null && (prev?.ActiveMission == null || premise != null)) SetView(BaseViews.Squad);
            else if (prev?.ActiveMission != null && c.ActiveMission == null && view == BaseViews.Squad) SetView(BaseViews.Geoscape);
            if (ReportPending(c) && view != BaseViews.Debrief) SetView(BaseViews.Debrief);
            // The runs: on an expedition, its map; back from one, Almacén.
            if (c.Stash != null)
            {
                if (c.Run != null && view != BaseViews.RunMap && view != BaseViews.Debrief) SetView(BaseViews.RunMap);
                else if (c.Run == null && (view == BaseViews.RunMap || view == BaseViews.Geoscape || view == BaseViews.Engineering || view == BaseViews.Facilities)) SetView(BaseViews.Stash);
            }
            // The first time, Salir alone (U16): whatever came before it, the partner coming in too.
            if (p.FirstTime && view != BaseViews.Squad && view != BaseViews.Debrief) SetView(BaseViews.Squad);
            if (!viewSent) SetView(view);
            Render();
        }

        /// <summary>Who is in the room (its `room`): names, colours, connected; the tab each is on stays.</summary>
        public void Players(IEnumerable<PlayerInfo> players)
        {
            var views = p.Seats.ToDictionary(s => s.Slot, s => s.View);
            p.Seats = players.Select(x => new SeatInfo { Slot = x.Slot, Name = x.Name, Color = x.Color, Connected = x.Connected, View = views.TryGetValue(x.Slot, out var v) ? v : null }).ToList();
            Render();
        }

        /// <summary>The tab the partner is on (their `baseView`).</summary>
        public void PartnerView(int slot, string tab)
        {
            var seat = p.Seats.FirstOrDefault(s => s.Slot == slot);
            if (seat == null) return;
            seat.View = tab;
            Render();
        }

        /// <summary>The proposal's deadline (`deadline`, kind proposal): goes ahead on its own `ms` from now; null, answered.</summary>
        public void Deadline(string kind, long? ms)
        {
            if (kind != DeadlineKinds.Proposal) return;
            if (ms == null) dueAt = null;
            else
            {
                dueAt = Now() + ms.Value / 1000f;
                dueTotal = Math.Max(1, ms.Value / 1000f);
            }
        }

        /// <summary>An order the host turned down, or its error: a short note.</summary>
        public void Note(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log($"[base] el anfitrión dice: {text}");
            root.Toast(text);
        }

        /// <summary>«Quedan 0:24…» on the proposal (proposalTimer): the text, what is left of it and whether it hurries.</summary>
        (string text, float left, bool hurry)? Clock()
        {
            var proposal = p.State?.Proposal;
            if (proposal == null || dueAt == null) return null;
            var left = Math.Max(0, dueAt.Value - Now());
            var s = (int)Math.Ceiling(left);
            return (Lore.ProposalClock($"{s / 60}:{s % 60:00}", proposal.By == p.Slot), left / dueTotal, left <= 10);
        }

        string PartnerName => p.Seats.FirstOrDefault(s => s.Slot != p.Slot)?.Name ?? "el otro jugador";

        bool PartnerHere => p.State != null && p.State.Players.Count > 1 && p.Seats.Any(s => s.Slot != p.Slot && s.Connected);


        // ------------------------------------------------------------ drawing

        /// <summary>Everything from the state and what is open (screen.ts render).</summary>
        public void Render()
        {
            if (p.State == null || !root.Ready) return;
            // The halt goes as the report is read (Continuar): the valley's clearing again under the route (A8).
            if (p.State.Stash != null && HaltNow(p.State) != Settlement.Halt)
            {
                Settlement.Halt = HaltNow(p.State);
                Settlement.Instance?.ShowFixed();
            }
            root.SetTop(p.Top());
            root.SetNav(p.Nav(view));
            root.SetScreen(view);
            switch (view)
            {
                case BaseViews.Barracks:
                    root.SetSoldiers(p.Soldiers(soldiers));
                    break;
                case BaseViews.Squad:
                    root.SetSquad(p.State.Stash != null ? p.RunSquad() : p.Squad());
                    break;
                case BaseViews.Upgrades:
                    root.SetUpgrades(p.Upgrades());
                    break;
                case BaseViews.Debrief:
                    root.SetDebrief(p.Debrief());
                    break;
                case BaseViews.Stash:
                    stash = p.StashScreen(soldiers.Soldier);
                    root.SetStash(stash);
                    break;
                case BaseViews.RunMap:
                    root.SetRunMap(p.Expedition(soldiers.Soldier));
                    break;
            }
            root.SetSkillBoard(board != null ? p.SkillBoard(board) : null);
            shown = Modal();
            // The event's window has the vote on its answer under its options: the plate isn't shown behind it too (Red's
            // capture of 2/11, «Un desertor»).
            root.SetProposal(shown?.Vote != null ? null : p.Proposal(), p.Waiting(view));
            root.SetModal(shown);
            var clearing = Settlement.Instance;
            if (clearing == null) return;
            // Soldados' soldier on the clearing, as Personalizar's draft has them while it is open; and while it is, the
            // whole figure fitted between its panels (Arte's Customizing, T1).
            clearing.Customizing = view == BaseViews.Barracks && soldiers.Draft != null;
            if (view == BaseViews.Barracks) clearing.Showcase(p.State, soldiers.Soldier, soldiers.Draft?.Look);
            // Almacén's chosen soldier between its plates, as in Soldados.
            if (view == BaseViews.Stash) clearing.Showcase(p.State, stash?.Kit?.Soldier, null);
            // The squad by La Lata (the debrief's while it is to be read): only if it changed.
            var report = ReportPending(p.State);
            var (mirror, halves) = p.LineupSides(report);
            clearing.SetLineup(p.State, p.Lineup(report), mirror, halves);
            // Back from a mission: La Lata up its track (screen.ts arrive), the first time the clearing is shown after the
            // debrief (screen.ts go: fixed shots too, as Soldados; in the debrief it stands parked behind the row; the Mapa
            // is the valley's).
            if (arriving && view != BaseViews.Debrief && view != BaseViews.Geoscape)
            {
                arriving = false;
                clearing.Arrive();
            }
        }

        /// <summary>One window at a time (renderModal): leaving the game, back to the menu, the campaign's end, the event, then the alerts.</summary>
        StrategyModalView Modal()
        {
            var c = p.State;
            if (quitting)
            {
                return new StrategyModalView
                {
                    Kind = "quit", Kicker = Lore.QuitText.Button, Title = Lore.QuitText.Title, Text = Lore.QuitText.Saved,
                    Note = PartnerHere && Hosting() ? Lore.QuitHostWarning(PartnerName) : null,
                    Buttons = { Lore.QuitText.Accept, Lore.QuitText.Refuse },
                };
            }
            if (leaving && c.Outcome == null) return p.Leave();
            if (endingRun && c.Run != null)
                return new StrategyModalView
                {
                    Kind = "endrun", Kicker = StrategyText.Runs.Head(c.Run.Chapter), Title = StrategyText.Runs.EndAsk, Text = StrategyText.Runs.BackTip,
                    Buttons = { StrategyText.Runs.Back, StrategyText.Runs.EndNo },
                };
            // A new game from the base of one lost (N8): its level first, the last one marked.
            if (choosingLevel && c.Outcome != null) return p.LevelChoice(levelPick);
            // The game lost (N8) after the squad's report, once it is read (R19 of Red, 23/11: the two at once, one over the other).
            return (view != BaseViews.Debrief ? p.End() : null) ?? p.Event(view) ?? (alerts.Count > 0 && view != BaseViews.Debrief ? alerts[0] : null);
        }

        /// <summary>
        /// The debrief is shown to whoever took part, once (reportPending): who joins later lands in the base. Also with a
        /// mission already chosen (Gameplay, 24/10: the 0.10.1 asked for none, and a siege the engine picks on the way back
        /// hid the debrief for good; docs/UNITY-ESTRATEGIA.md §7).
        /// </summary>
        /// <summary>BASTION_ALTO (Arte's, for the labs): the halt whatever the campaign.</summary>
        static readonly string HaltLab = Environment.GetEnvironmentVariable("BASTION_ALTO");

        /// <summary>
        /// A8 (Arturo, 18/11: «cuando acabas un nodo, al darle a seguir expedición, la vista parece que el soldado está en la
        /// base»; «Siempre una carretera»): the stage's report between two stages stands on Arte's halt by a road, not on the
        /// base's clearing; as Settlement.Halt takes it, «site/modifier/sky» of the stage just played: its modifier, or the
        /// one an event left on it (RunNode.Added: the fight had that night or storm too), and its day sky (RunMap.SkyOf;
        /// cuadro 83: the fight's own, none at night), each empty when none (hospital/night/, hospital//dusk). Null, the
        /// clearing: the route and the stops (the valley's, as before), and the base itself (no expedition under way, back
        /// after the boss or a Refugio).
        /// </summary>
        string HaltNow(CampaignState c)
        {
            if (HaltLab != null) return HaltLab;
            if (c?.Stash == null || c.Run == null || !ReportPending(c)) return null;
            var node = c.Run.Nodes.FirstOrDefault(n => n.Id == c.LastReport?.Run?.Node);
            var site = node?.Site ?? "roadside";
            var modifier = !string.IsNullOrEmpty(node?.Modifier) ? node.Modifier : node?.Added;
            var sky = node != null ? RunMap.SkyOf(node) : null;
            return $"{site}/{modifier}/{sky}";
        }

        bool ReportPending(CampaignState c)
        {
            var r = c.LastReport;
            if (r == null || !r.Squad.Any(s => s.Owner == p.Slot)) return false;
            var read = LocalMarks.Read(LocalMarks.Key("seenReport", c));
            return read == null || !read.Contains(ReportKey(c));
        }

        /// <summary>
        /// A report's key, read once: in the runs, by the stage it was (RunReport.Stage: the names and the nodes' ids repeat
        /// from one expedition to the next). Not the campaign's NodesPlayed now, which a stop chosen after it moves on: the
        /// same report came up again over the stop, to both (R4, 17/11). Reports of before Stage keep the old key.
        /// </summary>
        static string ReportKey(CampaignState c) => c.LastReport is MissionReport r ? r.Run != null ? $"run:{r.Run.Stage ?? c.NodesPlayed}:{r.Run.Node}:{r.MissionName}" : $"{r.Day}:{r.MissionName}:{r.Victory}" : "";

        /// <summary>To a tab (screen.ts go): its news seen, the board and what was in hand put away, the partner told.</summary>
        void SetView(string to)
        {
            view = to;
            board = null;
            CloseDraft();
            Settlement.Instance?.SetView(ClearingView(view));
            Bastion.Client.Audio.UiSounds.View(view);
            viewSent = true;
            // The room takes the engine's screens alone (Room.Valid: BaseViews.All); the lab never goes through it, so one
            // outside them is said here (R4, 17/11: Almacén, Mejoras and the map came back «Mensaje no válido.»). Every tab
            // is a BaseViews constant, none written by hand.
            if (!BaseViews.All.Contains(view)) Debug.LogWarning($"[base] vista fuera de BaseViews.All: {view}");
            send(new BaseViewMessage { View = view });
        }

        /// <summary>
        /// The clearing's shot for a tab (Arte, 16/11): Almacén's chosen soldier as Soldados', the map of an expedition the
        /// valley's (the clearing's camera off), the others as they are.
        /// </summary>
        static string ClearingView(string view) => view == BaseViews.Stash ? BaseViews.Barracks : view == BaseViews.RunMap ? BaseViews.Geoscape : view == BaseViews.Upgrades ? BaseViews.Facilities : view;

        void Go(string to)
        {
            // The first time the base's tabs are shut (U16): only Salir.
            if (p.FirstTime && to != BaseViews.Squad && to != BaseViews.Debrief)
            {
                Debug.Log($"[base] primera vez: «{to}» cerrada hasta volver de la primera expedición");
                return;
            }
            SetView(to);
            Render();
        }

        /// <summary>The first time (U16, StrategyPresenter.FirstTime): Salir alone.</summary>
        public bool FirstTime => p.FirstTime;

        // ------------------------------------------------------------ the grids (the runs, H2)

        /// <summary>Almacén as last drawn: its soldier on the clearing.</summary>
        StashScreenView stash;

        /// <summary>A stage of the map: there (with two, proposed).</summary>
        public void OnNode(string id)
        {
            chosenNode = id;
            Order(new ChooseNodeCommand { Node = id });
        }

        /// <summary>The stage this player last sent the squad to (the loading window's, before the campaign says so).</summary>
        string chosenNode;

        /// <summary>«Volver a la base»: asked first, then the expedition ends (with two, proposed).</summary>
        public void OnEndRun()
        {
            endingRun = true;
            Render();
        }

        /// <summary>The Trueque: the ware into the pack of the soldier chosen on the right (or the first of one's own).</summary>
        public void OnBuy(string ware) => Order(new BuyWareCommand { Ware = ware, Soldier = p.Expedition(soldiers.Soldier)?.Kit?.Soldier ?? soldiers.Soldier });

        /// <summary>«Seguir» from a stop; pressed again with two, taken back.</summary>
        public void OnLeaveStop() => Order(new LeaveStopCommand { Ready = !(p.State?.Run?.Stop?.Leaving is bool[] l && p.Slot >= 0 && p.Slot < l.Length && l[p.Slot]) });

        public string GearRefusal(GearMove m) => p.State == null ? "" : p.GearRefusal(m);
        public void GearMoved(GearMove m) => Order(p.GearCommand(m));
        public void GearDropped(string gear) => Order(new DropGearCommand { Gear = gear });
        public IReadOnlyList<GearActionView> GearActions(string gear) => p.State == null ? (IReadOnlyList<GearActionView>)new GearActionView[0] : p.GearActions(gear);
        public void GearAction(string gear, string action) => Order(p.GearActionCommand(gear, action));

        void Order(CampaignCommand cmd)
        {
            if (cmd == null) return;
            Debug.Log($"[base] orden «{cmd.Type}»");
            send(new CampaignCmdMessage { Cmd = cmd });
        }

        // ------------------------------------------------------------ the menu

        /// <summary>The base's menu (screen.ts renderModal's menu): Continuar, Ajustes (the graphics; sound is Arte's), the main menu, Salir.</summary>
        /// <summary>The base's menu (screen.ts renderModal): «Volver al menú principal» with its help, and Ajustes saying whose they are.</summary>
        public void OnMenu()
        {
            var partner = p.Seats.FirstOrDefault(s => s.Slot != p.Slot);
            root.ToggleMenu(new GameMenuView
            {
                Abandon = false, QuitLabel = Lore.QuitText.Button, Settings = App.GameSettings.Views(), LeaveTip = "La campaña queda guardada",
                SettingsNote = partner != null && partner.Connected ? $"Solo para ti: {partner.Name} sigue a lo suyo." : Bastion.UI.Start.StartText.SettingsHere,
                Invite = Bastion.UI.Start.RoomText.InviteOf(Bastion.Client.Net.NetLink.Instance),
            });
        }

        void MenuChose(string what)
        {
            if (what == GameMenu.Leave)
            {
                // Asked only with the other one here: it takes them back to the menu too.
                if (PartnerHere) leaving = true;
                else Leave();
            }
            else if (what == GameMenu.Quit) quitting = true;
            Render();
        }

        void Leave()
        {
            leaving = false;
            send(new BackToMenuMessage());
        }

        public void OnModalButton(int index)
        {
            switch (shown?.Kind)
            {
                case "quit":
                    quitting = false;
                    if (index == 0) Quit?.Invoke();
                    break;
                case "leave":
                    if (index == 0) Leave();
                    else leaving = false;
                    break;
                case "end":
                    // A game of expeditions lost (N8): «Nueva partida» asks the level first (LevelChoice), the last one marked;
                    // else back to the menu.
                    if (index == 0 && shown.Buttons.Count > 1)
                    {
                        choosingLevel = true;
                        levelPick = Levels.Of(p.State).Id;
                    }
                    else Leave();
                    break;
                case "level":
                    // «Empezar»: another game here at the level picked (the room takes it from the base of a game lost).
                    if (index == 0) send(new NewCampaignMessage { Level = levelPick });
                    choosingLevel = false;
                    break;
                case "endrun":
                    endingRun = false;
                    if (index == 0) Order(new EndRunCommand());
                    break;
                default:
                    if (alerts.Count > 0 && shown == alerts[0]) alerts.RemoveAt(0);
                    break;
            }
            Render();
        }

        // ------------------------------------------------------------ handlers

        public void OnTab(string tab) => Go(tab);

        /// <summary>The plate's keys: a vote, or a bite's treatment's (its own command: Arturo, 01/11 #8).</summary>
        public void OnVote(bool accept) => Order(new VoteCommand { Accept = accept });
        public void OnWithdraw() => Order(new WithdrawCommand());
        public void OnWaitingGo() => Go(BaseViews.Squad);
        /// <summary>An event's option; in the runs, a stop's answer (AnswerStopCommand: a vote with two).</summary>
        public void OnOption(int index)
        {
            // The level's cards (N8): one picked, lit; «Empezar» starts with it.
            if (shown?.Kind == "level")
            {
                if (index >= 0 && index < LevelIds.All.Count) levelPick = LevelIds.All[index];
                Render();
                return;
            }
            Order(p.State?.Run?.Stop != null ? (CampaignCommand)new AnswerStopCommand { Option = index } : new AnswerEventCommand { Option = index });
        }

        /// <summary>The level's cards open from a game lost (N8), and the one picked there.</summary>
        bool choosingLevel;
        string levelPick;

        /// <summary>«Ver» (proposalDetail's see): to the screen of what is proposed.</summary>
        public void OnSeeProposal()
        {
            switch (p.State?.Proposal?.Action)
            {
                case BuyUpgradeAction _:
                    Go(BaseViews.Upgrades);
                    break;
                case StartRunAction _:
                    Go(BaseViews.Squad);
                    break;
                case ChooseNodeAction _:
                case EndRunAction _:
                case AnswerStopAction _:
                    Go(BaseViews.RunMap);
                    break;
                case RecruitAction _:
                    Go(BaseViews.Barracks);
                    break;
            }
        }

        // Soldados.
        public void OnSoldier(string id)
        {
            if (soldiers.Draft != null && soldiers.Draft.Soldier != id) CloseDraft();
            soldiers.Soldier = id;
            Render();
        }

        public void OnRecruit() => Order(new RecruitCommand());

        /// <summary>Personalizar on the soldier shown: a draft of their name, nickname and looks (theirs, or as their id has them).</summary>
        public void OnCustomize()
        {
            var s = p.State?.Soldiers.FirstOrDefault(x => x.Id == soldiers.Soldier);
            if (s == null) return;
            soldiers.Draft = new CustomDraft
            {
                Soldier = s.Id, Name = s.Name, Nickname = s.Nickname ?? "",
                Look = CustomDraft.Copy(s.Appearance) ?? Appearances.DefaultAppearance(s.Owner, null, null, s.Id),
            };
            Prepare();
            Render();
        }

        /// <summary>The soldier, head and build the clearing last made the other outfits for (Settlement.Prepare).</summary>
        string prepared;

        /// <summary>
        /// The other outfits of the draft's sex made on the clearing in the background, in its head and build, so that the
        /// pointer over one shows it at once (Arte's Settlement.Prepare): on opening, and again when the head or build changes.
        /// </summary>
        void Prepare()
        {
            var d = soldiers.Draft;
            var k = d == null ? null : $"{d.Soldier}|{d.Look.Head}|{d.Look.Build}|{d.Look.Sex}";
            if (k == null || k == prepared) return;
            prepared = k;
            Settlement.Instance?.Prepare(p.State, d.Soldier, d.Look);
        }

        /// <summary>Personalizar closed without saving: the bust as the soldier is.</summary>
        void CloseDraft()
        {
            var d = soldiers.Draft;
            if (d == null) return;
            soldiers.Draft = null;
            prepared = null;
            var s = p.State?.Soldiers.FirstOrDefault(x => x.Id == d.Soldier);
            if (s != null) Portraits.Know(s.Id, s.Appearance, s.Name);
        }

        /// <summary>The draft changed for good (a click, a colour let go): drawn, on the clearing and in its bust.</summary>
        void DraftChanged()
        {
            var d = soldiers.Draft;
            d.CopyArmed = false;
            Portraits.Know(d.Soldier, d.Look, NameOf(d.Soldier));
            Prepare();
            Render();
        }

        /// <summary>A soldier's name as the campaign has it (who has no sex in their look gets a body by it).</summary>
        string NameOf(string soldier) => p.State?.Soldiers.FirstOrDefault(x => x.Id == soldier)?.Name;

        /// <summary>The draft on the clearing only (an outfit under the pointer, a colour being dragged).</summary>
        void Preview(Appearance look) => Settlement.Instance?.Showcase(p.State, soldiers.Draft?.Soldier, look);

        public void OnCare(int index) => Order(p.CareOrder(soldiers.Soldier, index));

        /// <summary>A choice on the sheet (arms.ts weaponPick, screen.ts loadout): the hand's or the belt's weapon, or a belt's place.</summary>
        public void OnPick(string pick, string option)
        {
            var c = p.State;
            var s = c?.Soldiers.FirstOrDefault(x => x.Id == soldiers.Soldier);
            if (s == null) return;
            // The hand, the belt, and the side's pistol where the Armería counts them (Arturo, 01/11 #10): one weapon each.
            if (pick == "hand" || pick == "belt" || pick == "side")
            {
                var current = pick == "belt" ? Progression.MeleeWeaponOf(c, s) : pick == "side" ? Progression.PistolOf(c, s) : Progression.FoundWeaponOf(c, s);
                if (option == "none") Order(current != null ? new AssignWeaponCommand { Weapon = current.Id, Soldier = null } : null);
                else if (option != current?.Id) Order(new AssignWeaponCommand { Weapon = option, Soldier = s.Id });
                return;
            }
            var slot = pick == "slot0" ? ItemSlots.Utility : pick == "slot1" ? ItemSlots.Ammo : null;
            if (slot == null) return;
            var item = option == "none" ? null : option;
            var now = slot == ItemSlots.Utility ? s.Loadout.Utility : s.Loadout.Ammo;
            if (item != now) Order(new EquipCommand { Soldier = s.Id, Slot = slot, Item = item });
        }

        public void OnBoard()
        {
            board = soldiers.Soldier;
            Render();
        }

        /// <summary>
        /// That soldier's board over the expedition's map, a stop or the stage's report (U10, Arturo 18/11: «mejorar las
        /// habilidades de los soldados entre etapas»): one's own to learn on, the partner's to look at (SkillBoard).
        /// </summary>
        public void OnBoardFor(string soldier)
        {
            board = soldier;
            Render();
        }

        // Personalizar (screen.ts customizer).
        public void OnCustomName(string name, string nickname)
        {
            if (soldiers.Draft == null) return;
            soldiers.Draft.Name = name;
            soldiers.Draft.Nickname = nickname;
            soldiers.Draft.CopyArmed = false;
            Render();
        }

        /// <summary>Ropa, Cabeza or Complexión; over an outfit (`preview`) the soldier wears it on the clearing, and back to the draft away.</summary>
        public void OnCustomChoice(string group, string id, bool preview)
        {
            var d = soldiers.Draft;
            if (d == null) return;
            if (preview)
            {
                if (group != "outfit") return;
                if (string.IsNullOrEmpty(id)) Preview(d.Look);
                else
                {
                    var look = CustomDraft.Copy(d.Look);
                    look.Character = id;
                    Preview(look);
                }
                return;
            }
            if (group == "outfit") d.Look.Character = string.IsNullOrEmpty(id) ? null : id;
            else if (group == "head") d.Look.Head = id;
            else if (group == "build") d.Look.Build = id;
            DraftChanged();
        }

        /// <summary>
        /// A colour: the whole armour (every piece that follows it, a beret, cap or hood keeping its own), the trim, or a
        /// piece (null: it follows again). While the picker is dragged (`preview`) only the clearing follows; the sheet
        /// draws again, and the bust, when it is let go.
        /// </summary>
        public void OnCustomColor(string part, string color, bool preview)
        {
            var d = soldiers.Draft;
            if (d == null) return;
            var look = d.Look;
            if (part == "all")
            {
                look.Primary = color ?? look.Primary;
                look.Torso = look.Arms = look.Legs = null;
                if (!(Appearances.Heads.TryGetValue(look.Head ?? "", out var head) && head.Paint == "trim")) look.Helmet = null;
            }
            else if (part == "trim") look.Secondary = color ?? look.Secondary;
            else StrategyPresenter.SetPart(look, part, color);
            if (preview) Preview(look);
            else DraftChanged();
        }

        /// <summary>Aleatorio, Restablecer, «Copiar a todos mis soldados» (asked twice), Guardar, Cancelar.</summary>
        public void OnCustomAction(string action)
        {
            var d = soldiers.Draft;
            var c = p.State;
            var s = c?.Soldiers.FirstOrDefault(x => x.Id == d?.Soldier);
            if (d == null || s == null) return;
            switch (action)
            {
                case "random":
                    d.Look = Appearances.RandomAppearance(new Rng(Environment.TickCount));
                    DraftChanged();
                    break;
                case "reset":
                    // Its own look by its id (the engine's): not everyone back in a helmet.
                    d.Look = Appearances.DefaultAppearance(s.Owner, null, s.Appearance?.Sex, s.Id, s.Appearance?.Wardrobe);
                    DraftChanged();
                    break;
                case "copy":
                    if (!d.CopyArmed)
                    {
                        d.CopyArmed = true;
                        Render();
                        break;
                    }
                    Save(d);
                    // The same colours on every soldier of this player, each keeping their name, head, build and wardrobe.
                    var hat = Appearances.Heads.TryGetValue(d.Look.Head ?? "", out var head) ? head.Paint : null;
                    foreach (var x in c.Soldiers.Where(x => x.Owner == s.Owner && x.Alive && x.Id != s.Id))
                    {
                        var a = new Appearance { Primary = d.Look.Primary, Secondary = d.Look.Secondary, Visor = d.Look.Visor, Head = x.Appearance?.Head ?? d.Look.Head, Build = x.Appearance?.Build ?? d.Look.Build, Wardrobe = x.Appearance?.Wardrobe };
                        foreach (var piece in ArmorParts.All)
                        {
                            var color = piece == ArmorParts.Helmet && hat != null ? Appearances.PartColor(d.Look, piece) : Appearances.PartOf(d.Look, piece);
                            if (color != null) StrategyPresenter.SetPart(a, piece, color);
                        }
                        Order(new CustomizeCommand { Soldier = x.Id, Name = x.Name, Nickname = x.Nickname ?? "", Appearance = a });
                    }
                    soldiers.Draft = null;
                    prepared = null;
                    Render();
                    break;
                case "save":
                    if (string.IsNullOrWhiteSpace(d.Name)) break;
                    Save(d);
                    soldiers.Draft = null;
                    prepared = null;
                    Render();
                    break;
                case "cancel":
                    CloseDraft();
                    Render();
                    break;
            }
        }

        void Save(CustomDraft d) => Order(new CustomizeCommand { Soldier = d.Soldier, Name = d.Name.Trim(), Nickname = (d.Nickname ?? "").Trim(), Appearance = d.Look });

        // The skill board.
        public void OnUnlock(string cell) => Order(board != null ? new UnlockCommand { Soldier = board, Cell = cell } : null);

        public void OnBoardClose()
        {
            board = null;
            Render();
        }

        // Escuadra and the debrief.
        public void OnSquadToggle(string id, bool on) => Order(new SquadCommand { Soldier = id, On = on });

        /// <summary>The Escuadra's key: the mission's launch, or in the runs «Salir» (StartRunCommand: a vote with two).</summary>
        public void OnLaunch()
        {
            if (p.State.Stash != null) Order(new StartRunCommand());
            else Order(new LaunchCommand { Ready = !p.State.LaunchReady[p.Slot] });
        }

        public void OnSquadBack() => Go(p.State?.Stash != null ? BaseViews.Stash : BaseViews.Barracks);

        /// <summary>Mejoras: the next level (with two, proposed).</summary>
        public void OnUpgrade(string id) => Order(new UpgradeCommand { Upgrade = id });
        public void OnSquadRecruit() => Order(new RecruitCommand { Squad = true });

        /// <summary>
        /// «Continuar»: the debrief read (once per mission on this computer), to the Escuadra of the mission chosen, or to
        /// the Mapa without one (Gameplay, 24/10; the 0.10.1 went to Soldados).
        /// </summary>
        public void OnDebriefContinue()
        {
            var c = p.State;
            if (c?.LastReport != null) LocalMarks.Write(LocalMarks.Key("seenReport", c), new[] { ReportKey(c) });
            p.Joined = null;
            Go(c?.Run != null ? BaseViews.RunMap : c?.Stash != null ? BaseViews.Stash : BaseViews.Squad);
        }

        /// <summary>Soldados' «Esperar un día» (the campaign of before the runs).</summary>
        public void OnWait() => Order(new AdvanceCommand { Ready = !p.State.AdvanceReady[p.Slot] });


        /// <summary>
        /// Esc (the base's; screen.ts back): the menu's page first, then the board, the window asked, the choice
        /// open on the sheet and Personalizar, what is in hand or picked, and the menu. A notice or an event is answered
        /// with its own buttons: Esc leaves it be.
        /// </summary>
        public void Escape()
        {
            if (root.MenuBack()) return;
            // A thing dragged on the grids goes back, the bin's question or a thing's orders close.
            if (root.CloseGear()) return;
            // On the board, a cell held to buy is let go first (hexBoard.ts key), the board after.
            if (board != null && root.CancelBoardHold()) return;
            if (board != null) OnBoardClose();
            else if (quitting || leaving || endingRun)
            {
                quitting = leaving = endingRun = false;
                Render();
            }
            else if (view == BaseViews.Barracks && root.CloseSoldierChoice()) return;
            else if (soldiers.Draft != null)
            {
                CloseDraft();
                Render();
            }
            else if (root.ModalOpen) return;
            else OnMenu();
        }
    }
}
