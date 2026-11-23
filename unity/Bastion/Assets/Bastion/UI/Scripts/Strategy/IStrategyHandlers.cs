namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Who answers the base's buttons (screen.ts's handlers and its own clicks): the campaign's controller, which sends
    /// the commands over the network (Red's), or the lab.
    /// </summary>
    public interface IStrategyHandlers
    {
        /// <summary>A tab (geoscape, engineering, barracks, facilities, squad).</summary>
        void OnTab(string view);
        void OnMenu();
        /// <summary>The partner's proposal: yes or no.</summary>
        void OnVote(bool accept);
        /// <summary>One's own proposal, taken back.</summary>
        void OnWithdraw();
        /// <summary>«Ver»: where the proposal is (the clearing, the map…).</summary>
        void OnSeeProposal();
        /// <summary>«Ir a la Escuadra» from «Te esperan».</summary>
        void OnWaitingGo();
        /// <summary>An event's option.</summary>
        void OnOption(int index);
        /// <summary>A window's button, by its place (0 the main one).</summary>
        void OnModalButton(int index);

        // Soldados.
        /// <summary>A soldier's tile: their sheet.</summary>
        void OnSoldier(string id);
        void OnRecruit();
        void OnCustomize();
        /// <summary>A care button (treat, confirm, withdraw, reject, heal), by its place.</summary>
        void OnCare(int index);
        /// <summary>A choice on the sheet: the weapon in a hand, an item in a belt's slot.</summary>
        void OnPick(string pick, string option);
        /// <summary>«Tablero de habilidades»: the board.</summary>
        void OnBoard();
        /// <summary>That soldier's board, from the expedition's map, its stops and the stage's report (U10).</summary>
        void OnBoardFor(string soldier);

        // Personalizar.
        void OnCustomName(string name, string nickname);
        /// <summary>A choice of Ropa, Cabeza or Complexión; `preview` while the pointer is only over it.</summary>
        void OnCustomChoice(string group, string id, bool preview);
        /// <summary>A part's colour (#rrggbb), or null to follow again; `preview` while dragging in the picker.</summary>
        void OnCustomColor(string part, string color, bool preview);
        /// <summary>random, reset, copy, save, cancel.</summary>
        void OnCustomAction(string action);

        // The skill board.
        /// <summary>A cell bought (held a second).</summary>
        void OnUnlock(string cell);
        void OnBoardClose();

        // Escuadra and the debrief.
        /// <summary>A soldier in or out of the squad.</summary>
        void OnSquadToggle(string id, bool on);
        /// <summary>LANZAR or A PUNTO: this player's key.</summary>
        void OnLaunch();
        void OnSquadBack();
        void OnSquadRecruit();
        void OnDebriefContinue();

        // Soldados' «esperar un día» (the campaign of before).
        /// <summary>«Esperar un día»: this player's key.</summary>
        void OnWait();



        // The runs (H1): a stage of the map to go to (a vote with two), and back to the base, which ends the expedition.
        void OnNode(string id);
        void OnEndRun();
        /// <summary>The Trueque: a ware, into the chosen soldier's pack; «Seguir» from a stop (with two, once both ask).</summary>
        void OnBuy(string ware);
        /// <summary>The Trueque's other side (M10, cuadro 141): that thing of the chosen soldier's, worn or in the pack, for §.</summary>
        void OnSell(string gear);
        void OnLeaveStop();
        /// <summary>Mejoras: the next level of one (a vote with two).</summary>
        void OnUpgrade(string id);
    }

    /// <summary>Nobody answers: everything is ignored (before Bind).</summary>
    public sealed class NoStrategyHandlers : IStrategyHandlers
    {
        public static readonly NoStrategyHandlers Instance = new NoStrategyHandlers();
        public void OnTab(string view) { }
        public void OnNode(string id) { }
        public void OnEndRun() { }
        public void OnBuy(string ware) { }
        public void OnSell(string gear) { }
        public void OnLeaveStop() { }
        public void OnUpgrade(string id) { }
        public void OnMenu() { }
        public void OnVote(bool accept) { }
        public void OnWithdraw() { }
        public void OnSeeProposal() { }
        public void OnWaitingGo() { }
        public void OnOption(int index) { }
        public void OnModalButton(int index) { }
        public void OnSoldier(string id) { }
        public void OnRecruit() { }
        public void OnCustomize() { }
        public void OnCare(int index) { }
        public void OnPick(string pick, string option) { }
        public void OnBoard() { }
        public void OnBoardFor(string soldier) { }
        public void OnCustomName(string name, string nickname) { }
        public void OnCustomChoice(string group, string id, bool preview) { }
        public void OnCustomColor(string part, string color, bool preview) { }
        public void OnCustomAction(string action) { }
        public void OnUnlock(string cell) { }
        public void OnBoardClose() { }
        public void OnSquadToggle(string id, bool on) { }
        public void OnLaunch() { }
        public void OnSquadBack() { }
        public void OnSquadRecruit() { }
        public void OnDebriefContinue() { }
        public void OnWait() { }
    }

    /// <summary>The handlers the pieces hold, so Bind can change who answers without making them again.</summary>
    public sealed class StrategyRelay : IStrategyHandlers
    {
        public IStrategyHandlers Target = NoStrategyHandlers.Instance;
        public void OnTab(string view) => Target.OnTab(view);
        public void OnNode(string id) => Target.OnNode(id);
        public void OnEndRun() => Target.OnEndRun();
        public void OnBuy(string ware) => Target.OnBuy(ware);
        public void OnSell(string gear) => Target.OnSell(gear);
        public void OnLeaveStop() => Target.OnLeaveStop();
        public void OnUpgrade(string id) => Target.OnUpgrade(id);
        public void OnMenu() => Target.OnMenu();
        public void OnVote(bool accept) => Target.OnVote(accept);
        public void OnWithdraw() => Target.OnWithdraw();
        public void OnSeeProposal() => Target.OnSeeProposal();
        public void OnWaitingGo() => Target.OnWaitingGo();
        public void OnOption(int index) => Target.OnOption(index);
        public void OnModalButton(int index) => Target.OnModalButton(index);
        public void OnSoldier(string id) => Target.OnSoldier(id);
        public void OnRecruit() => Target.OnRecruit();
        public void OnCustomize() => Target.OnCustomize();
        public void OnCare(int index) => Target.OnCare(index);
        public void OnPick(string pick, string option) => Target.OnPick(pick, option);
        public void OnBoard() => Target.OnBoard();
        public void OnBoardFor(string soldier) => Target.OnBoardFor(soldier);
        public void OnCustomName(string name, string nickname) => Target.OnCustomName(name, nickname);
        public void OnCustomChoice(string group, string id, bool preview) => Target.OnCustomChoice(group, id, preview);
        public void OnCustomColor(string part, string color, bool preview) => Target.OnCustomColor(part, color, preview);
        public void OnCustomAction(string action) => Target.OnCustomAction(action);
        public void OnUnlock(string cell) => Target.OnUnlock(cell);
        public void OnBoardClose() => Target.OnBoardClose();
        public void OnSquadToggle(string id, bool on) => Target.OnSquadToggle(id, on);
        public void OnLaunch() => Target.OnLaunch();
        public void OnSquadBack() => Target.OnSquadBack();
        public void OnSquadRecruit() => Target.OnSquadRecruit();
        public void OnDebriefContinue() => Target.OnDebriefContinue();
        public void OnWait() => Target.OnWait();
    }
}
