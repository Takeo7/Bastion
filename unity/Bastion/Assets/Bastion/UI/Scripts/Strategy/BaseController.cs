using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base over the network (screen.ts StrategyScreen: its state, update and handlers; UI's). The campaign the host
    /// sends is drawn through the presenter; what is open stays here (the tab, the soldier, the Armería's row, the Mapa's
    /// pick, the piece on the clearing, the board, the windows waiting); every button that changes the campaign goes to
    /// the host as its CampaignCommand, and the host answers with the whole campaign (or `rejected` and why, a note).
    /// Arte's clearing is not here yet: placing, felling, the siege's posts and walking only pick, and nothing is sent.
    /// </summary>
    public sealed class BaseController : IStrategyHandlers
    {
        readonly StrategyRoot root;
        readonly Action<ClientMessage> send;
        readonly StrategyPresenter p;
        Radio radio;
        string view = "barracks";
        readonly SoldiersPick soldiers = new SoldiersPick();
        readonly ArmoryPick armory = new ArmoryPick();
        readonly MapPick map = new MapPick();
        readonly SettlementPick settlement = new SettlementPick();
        /// <summary>The soldier whose skill board is open; null, closed.</summary>
        string board;
        /// <summary>The windows of what happened (collectAlerts, the premise), one at a time, the first on screen.</summary>
        readonly List<StrategyModalView> alerts = new List<StrategyModalView>();
        StrategyModalView shown;
        bool leaving;
        bool quitting;
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
            root.ClockOf = Clock;
            root.MenuChose += MenuChose;
            root.SettingChanged += (id, value) => App.GameSettings.Change(id, value);
        }

        /// <summary>The campaign as the squad took off, when the base is made anew after a mission (main.ts's `before`).</summary>
        public CampaignState Before;

        public CampaignState State => p.State;
        public string View => view;

        // ------------------------------------------------------------ the host

        /// <summary>
        /// The campaign as the host has it now (screen.ts update): the windows for what changed since the last one, the
        /// premise once, the hangar when a mission is chosen and the debrief when one is back, the radio; then drawn.
        /// </summary>
        public void Campaign(CampaignState c)
        {
            if (c == null) return;
            // A base made anew after a mission compares with it as it took off: the mission's news still pop up.
            var prev = p.State ?? Before;
            Before = null;
            p.State = c;
            p.SeeNews(view);
            radio = radio ?? new Radio(root.RadioCard, p.Slot, Vars);
            // One's own proposal that costs: said at once that nothing is paid until the yes (playtest 16/10 #5).
            if (c.Proposal != null && c.Proposal.By == p.Slot && prev?.Proposal?.Text != c.Proposal.Text && p.Proposal()?.Cost != null)
                root.Toast($"Propuesto a {PartnerName}: se paga cuando diga que sí.");
            if (c.Proposal == null) dueAt = null;
            if (settlement.Piece != null && !c.Bastion.Pieces.Any(x => x.Id == settlement.Piece)) settlement.Piece = null;
            if (prev != null) alerts.AddRange(p.Alerts(prev));
            var premise = p.Premise();
            if (premise != null) alerts.Insert(0, premise);
            // A mission chosen by either player sends both to the hangar; a finished one to the debrief.
            if (prev?.ActiveMission == null && c.ActiveMission != null) SetView("squad");
            else if (prev?.ActiveMission != null && c.ActiveMission == null && view == "squad") SetView("geoscape");
            if (ReportPending(c) && view != "debrief") SetView("debrief");
            if (!viewSent) SetView(view);
            radio.Update(c, view == "debrief" || ReportPending(c));
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

        /// <summary>The radio's placeholders: {yo}, this player's name; {saludo}, the commanders'.</summary>
        Dictionary<string, string> Vars()
        {
            var mine = p.Seats.FirstOrDefault(s => s.Slot == p.Slot)?.Name ?? "Comandante";
            var all = p.Seats.OrderBy(s => s.Slot).Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
            return new Dictionary<string, string> { ["yo"] = StrategyText.Radio.Greeting(new[] { mine }), ["saludo"] = StrategyText.Radio.Greeting(all.Count > 0 ? all : new List<string> { mine }) };
        }

        // ------------------------------------------------------------ drawing

        /// <summary>Everything from the state and what is open (screen.ts render).</summary>
        public void Render()
        {
            if (p.State == null || !root.Ready) return;
            root.SetTop(p.Top());
            root.SetNav(p.Nav(view));
            root.SetScreen(view);
            switch (view)
            {
                case "barracks":
                    root.SetSoldiers(p.Soldiers(soldiers));
                    break;
                case "engineering":
                    root.SetArmory(p.Armory(armory));
                    break;
                case "squad":
                    root.SetSquad(p.Squad());
                    break;
                case "debrief":
                    root.SetDebrief(p.Debrief());
                    break;
                case "geoscape":
                    if (root.ValleyCamera == null) root.ValleyCamera = Camera.main;
                    root.SetMap(p.Map(map));
                    break;
                case "facilities":
                    root.SetSettlement(p.Settlement(settlement));
                    break;
            }
            root.SetSkillBoard(board != null ? p.SkillBoard(board) : null);
            root.SetProposal(p.Proposal(), p.Waiting(view));
            shown = Modal();
            root.SetModal(shown);
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
            return p.End() ?? p.Event(view) ?? (alerts.Count > 0 && view != "debrief" ? alerts[0] : null);
        }

        /// <summary>
        /// The debrief is shown to whoever took part, once (reportPending): who joins later lands in the base. Also with a
        /// mission already chosen (Gameplay, 24/10: the 0.10.1 asked for none, and a siege the engine picks on the way back
        /// hid the debrief for good; docs/UNITY-ESTRATEGIA.md §7).
        /// </summary>
        bool ReportPending(CampaignState c)
        {
            var r = c.LastReport;
            if (r == null || !r.Squad.Any(s => s.Owner == p.Slot)) return false;
            var read = LocalMarks.Read(LocalMarks.Key("seenReport", c));
            return read == null || !read.Contains(ReportKey(c));
        }

        static string ReportKey(CampaignState c) => c.LastReport is MissionReport r ? $"{r.Day}:{r.MissionName}:{r.Victory}" : "";

        /// <summary>To a tab (screen.ts go): its news seen, the board and what was in hand put away, the partner told.</summary>
        void SetView(string to)
        {
            view = to;
            p.SeeNews(view);
            board = null;
            if (view != "facilities")
            {
                settlement.InHand = null;
                settlement.Felling = false;
                settlement.Piece = null;
                settlement.Dismantling = false;
            }
            viewSent = true;
            send(new BaseViewMessage { View = view });
        }

        void Go(string to)
        {
            SetView(to);
            Render();
        }

        void Order(CampaignCommand cmd)
        {
            if (cmd == null) return;
            Debug.Log($"[base] orden «{cmd.Type}»");
            send(new CampaignCmdMessage { Cmd = cmd });
        }

        // ------------------------------------------------------------ the menu

        /// <summary>The base's menu (screen.ts renderModal's menu): Continuar, Ajustes (the graphics; sound is Arte's), the main menu, Salir.</summary>
        public void OnMenu() => root.ToggleMenu(new GameMenuView { Abandon = false, QuitLabel = Lore.QuitText.Button, Settings = App.GameSettings.Views() });

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
                    Leave();
                    break;
                default:
                    if (alerts.Count > 0 && shown == alerts[0]) alerts.RemoveAt(0);
                    break;
            }
            Render();
        }

        // ------------------------------------------------------------ handlers

        public void OnTab(string tab) => Go(tab);
        public void OnVote(bool accept) => Order(new VoteCommand { Accept = accept });
        public void OnWithdraw() => Order(new WithdrawCommand());
        public void OnWaitingGo() => Go("squad");
        public void OnOption(int index) => Order(new AnswerEventCommand { Option = index });

        /// <summary>«Ver» (proposalDetail's see): to where the proposal is, its piece, mission or zone picked.</summary>
        public void OnSeeProposal()
        {
            var c = p.State;
            switch (c?.Proposal?.Action)
            {
                case PlaceAction _:
                case PlaceLineAction _:
                case FellAction _:
                case LevelUpAction _:
                    Go("facilities");
                    break;
                case RaiseAction a:
                    SeePiece(a.Piece);
                    break;
                case MendAction a:
                    SeePiece(a.Piece);
                    break;
                case RearmAction a:
                    SeePiece(a.Piece);
                    break;
                case DismantleAction a:
                    SeePiece(a.Piece);
                    break;
                case ChooseMissionAction a:
                    map.Offer = a.Id;
                    map.Region = c.Offers.FirstOrDefault(o => o.Id == a.Id)?.Region;
                    Go("geoscape");
                    break;
                case ContactAction a:
                    map.Offer = null;
                    map.Region = a.Region;
                    Go("geoscape");
                    break;
                case InvestigateAction a:
                    map.Offer = null;
                    map.Region = c.Rumors.FirstOrDefault(r => r.Id == a.Id)?.Region ?? map.Region;
                    Go("geoscape");
                    break;
                case RecruitAction _:
                    Go("barracks");
                    break;
                case BuyAction _:
                    Go("engineering");
                    break;
            }
        }

        void SeePiece(string piece)
        {
            if (view != "facilities") SetView("facilities");
            settlement.Piece = piece;
            settlement.Dismantling = false;
            Render();
        }

        // Soldados.
        public void OnSoldier(string id)
        {
            soldiers.Soldier = id;
            Render();
        }

        public void OnRecruit() => Order(new RecruitCommand());

        /// <summary>Personalizar waits for Arte's wardrobe (the figure that changes as one chooses).</summary>
        public void OnCustomize() => Debug.Log("[base] Personalizar: espera al armario de Arte");

        public void OnCare(int index) => Order(p.CareOrder(soldiers.Soldier, index));

        /// <summary>A choice on the sheet (arms.ts weaponPick, screen.ts loadout): the hand's or the belt's weapon, or a belt's place.</summary>
        public void OnPick(string pick, string option)
        {
            var c = p.State;
            var s = c?.Soldiers.FirstOrDefault(x => x.Id == soldiers.Soldier);
            if (s == null) return;
            if (pick == "hand" || pick == "belt")
            {
                var current = pick == "belt" ? Progression.MeleeWeaponOf(c, s) : Progression.FoundWeaponOf(c, s);
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

        // Personalizar (Arte's wardrobe first).
        public void OnCustomName(string name, string nickname) { }
        public void OnCustomChoice(string group, string id, bool preview) { }
        public void OnCustomColor(string part, string color, bool preview) { }
        public void OnCustomAction(string action) { }

        // The skill board.
        public void OnUnlock(string cell) => Order(board != null ? new UnlockCommand { Soldier = board, Cell = cell } : null);

        public void OnBoardClose()
        {
            board = null;
            Render();
        }

        // The Armería.
        public void OnArmoryTab(string tab)
        {
            armory.Tab = tab;
            armory.Pick = null;
            Render();
        }

        public void OnArmoryPick(string id)
        {
            armory.Pick = id;
            Render();
        }

        /// <summary>
        /// A button of the Armería's sheet (arms.ts, screen.ts renderEngineering): buy the belt's item, have the Taller
        /// make it, mend the weapon, an improvement on or off, the weapon taken back or given; on a loose improvement,
        /// «Ponérsela» to a weapon.
        /// </summary>
        public void OnArmoryAction(string action, string arg)
        {
            var picked = armory.Pick ?? "";
            var weapon = picked.StartsWith("f:") ? picked.Substring(2) : null;
            switch (action)
            {
                case "buy":
                    Order(new BuyCommand { Item = picked });
                    break;
                case "craft":
                    var project = StrategyPresenter.CraftOf(picked);
                    Order(project != null ? new CraftCommand { Project = project } : null);
                    break;
                case "repair":
                    Order(weapon != null ? new RepairCommand { Weapon = weapon } : null);
                    break;
                case "mod":
                case "unmod":
                    Order(weapon != null ? new ModCommand { Weapon = weapon, Mod = arg, On = action == "mod" } : null);
                    break;
                case "take":
                    Order(weapon != null ? new AssignWeaponCommand { Weapon = weapon, Soldier = null } : null);
                    break;
                case "give":
                    if (picked.StartsWith("x:")) Order(new ModCommand { Weapon = arg, Mod = picked.Substring(2), On = true });
                    else Order(weapon != null ? new AssignWeaponCommand { Weapon = weapon, Soldier = arg } : null);
                    break;
            }
        }

        // Escuadra and the debrief.
        public void OnSquadToggle(string id, bool on) => Order(new SquadCommand { Soldier = id, On = on });
        public void OnLaunch() => Order(new LaunchCommand { Ready = !p.State.LaunchReady[p.Slot] });
        public void OnSquadBack() => Go("barracks");
        public void OnSquadRecruit() => Order(new RecruitCommand { Squad = true });
        public void OnGoMap() => Go("geoscape");

        /// <summary>
        /// «Continuar»: the debrief read (once per mission on this computer), to the Escuadra of the mission chosen, or to
        /// the Mapa without one (Gameplay, 24/10; the 0.10.1 went to Soldados).
        /// </summary>
        public void OnDebriefContinue()
        {
            var c = p.State;
            if (c?.LastReport != null) LocalMarks.Write(LocalMarks.Key("seenReport", c), new[] { ReportKey(c) });
            Go(c?.ActiveMission != null ? "squad" : "geoscape");
        }

        /// <summary>«Construir» on the siege's damage: to the Asentamiento.</summary>
        public void OnDebriefLine(int index) => Go("facilities");

        // The Mapa.
        /// <summary>
        /// A zone: its marker picks its first mission on the list too (GeoscapeSet's click, docs/UNITY-ESTRATEGIA.md §7:
        /// as the 0.10.1), a Nido's or an errand's row only the zone (focusRegion).
        /// </summary>
        public void OnZone(string id, bool mark)
        {
            map.Region = id;
            map.Offer = mark && p.State != null ? StrategyPresenter.ShownOffers(p.State).FirstOrDefault(o => o.Region == id)?.Id : null;
            Render();
        }

        public void OnOffer(string id)
        {
            map.Offer = id;
            map.Region = p.State?.Offers.FirstOrDefault(o => o.Id == id)?.Region ?? map.Region;
            Render();
        }

        public void OnWait() => Order(new AdvanceCommand { Ready = !p.State.AdvanceReady[p.Slot] });
        public void OnListen(string rumor) => Order(new InvestigateCommand { Id = rumor });

        /// <summary>The sheet's buttons (screen.ts renderGeoscape): Desplegar, to the Escuadra, Cancelar misión, Contactar.</summary>
        public void OnMapAction(string action)
        {
            switch (action)
            {
                case "deploy":
                    Order(map.Offer != null ? new ChooseMissionCommand { Id = map.Offer } : null);
                    break;
                case "squad":
                    Go("squad");
                    break;
                case "cancel":
                    Order(new CancelMissionCommand());
                    break;
                case "contact":
                    Order(map.Region != null ? new ContactCommand { Region = map.Region } : null);
                    break;
            }
        }

        // The Asentamiento: what goes on the clearing waits for Arte's (where it points, the drag, the axe on the trees).
        public void OnPieceTake(string piece)
        {
            settlement.InHand = settlement.InHand == piece ? null : piece;
            settlement.Felling = false;
            Render();
        }

        public void OnFell()
        {
            settlement.Felling = !settlement.Felling;
            settlement.InHand = null;
            Render();
        }

        public void OnBuildDrop()
        {
            settlement.InHand = null;
            settlement.Felling = false;
            Render();
        }

        /// <summary>The piece's card (buildMode.ts pieceCard): mend, raise, the Bastion's level, rearm, dismantle (asked first).</summary>
        public void OnPieceAction(string action)
        {
            var piece = settlement.Piece;
            switch (action)
            {
                case "mend":
                    Order(piece != null ? new MendCommand { Piece = piece } : null);
                    break;
                case "raise":
                    Order(piece != null ? new RaiseCommand { Piece = piece } : null);
                    break;
                case "levelUp":
                    Order(new LevelUpCommand());
                    break;
                case "rearm":
                    Order(piece != null ? new RearmCommand { Piece = piece } : null);
                    break;
                case "dismantle":
                    settlement.Dismantling = true;
                    Render();
                    break;
                case "dismantleYes":
                    settlement.Dismantling = false;
                    Order(piece != null ? new DismantleCommand { Piece = piece } : null);
                    break;
                case "dismantleNo":
                    settlement.Dismantling = false;
                    Render();
                    break;
            }
        }

        public void OnPieceClose()
        {
            settlement.Piece = null;
            settlement.Dismantling = false;
            Render();
        }

        public void OnWalk() => Debug.Log("[base] Andar por la base: es de Arte");

        public void OnSeen(string key)
        {
            if (p.Fresh.See(key)) Render();
        }

        /// <summary>Esc (the base's): the radio first, then the menu's page, the board, the window asked, and the menu.</summary>
        public void Escape()
        {
            if (root.RadioKey("escape") || root.MenuBack()) return;
            if (board != null) OnBoardClose();
            else if (quitting || leaving)
            {
                quitting = leaving = false;
                Render();
            }
            else OnMenu();
        }
    }
}
