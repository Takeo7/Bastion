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

        // The Armería.
        /// <summary>items or gear.</summary>
        void OnArmoryTab(string tab);
        /// <summary>A row of a list (its id: «i:grenade», «f:w100»…).</summary>
        void OnArmoryPick(string id);
        /// <summary>buy, craft, repair, mod (its id), unmod (its id), take, give (the soldier).</summary>
        void OnArmoryAction(string action, string arg);

        // Escuadra and the debrief.
        /// <summary>A soldier in or out of the squad.</summary>
        void OnSquadToggle(string id, bool on);
        /// <summary>LANZAR or A PUNTO: this player's key.</summary>
        void OnLaunch();
        void OnSquadBack();
        void OnSquadRecruit();
        void OnGoMap();
        void OnDebriefContinue();
        /// <summary>A debrief line's button (Construir), by the line's place.</summary>
        void OnDebriefLine(int index);

        // The Mapa.
        /// <summary>A zone: its marker on the valley (`mark`, its first mission picked too) or a Nido's or an errand's row.</summary>
        void OnZone(string id, bool mark);
        /// <summary>A mission of the list.</summary>
        void OnOffer(string id);
        /// <summary>«Esperar un día»: this player's key.</summary>
        void OnWait();
        /// <summary>«Escuchar» a rumour.</summary>
        void OnListen(string rumor);
        /// <summary>The sheet's buttons: deploy, squad, cancel, contact.</summary>
        void OnMapAction(string action);

        // The Asentamiento.
        /// <summary>A piece of the palette taken up, to put on the clearing.</summary>
        void OnPieceTake(string piece);
        /// <summary>Talar taken up.</summary>
        void OnFell();
        /// <summary>The piece in hand (or the axe) let go.</summary>
        void OnBuildDrop();
        /// <summary>The card's buttons: mend, raise, levelUp, rearm, dismantle (asks), dismantleYes, dismantleNo.</summary>
        void OnPieceAction(string action);
        /// <summary>The card's ✕: the piece let go.</summary>
        void OnPieceClose();
        /// <summary>«Andar por la base».</summary>
        void OnWalk();

        /// <summary>The pointer went over something marked «Nuevo» (its key, fresh.ts): its mark goes.</summary>
        void OnSeen(string key);
    }

    /// <summary>Nobody answers: everything is ignored (before Bind).</summary>
    public sealed class NoStrategyHandlers : IStrategyHandlers
    {
        public static readonly NoStrategyHandlers Instance = new NoStrategyHandlers();
        public void OnTab(string view) { }
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
        public void OnCustomName(string name, string nickname) { }
        public void OnCustomChoice(string group, string id, bool preview) { }
        public void OnCustomColor(string part, string color, bool preview) { }
        public void OnCustomAction(string action) { }
        public void OnUnlock(string cell) { }
        public void OnBoardClose() { }
        public void OnArmoryTab(string tab) { }
        public void OnArmoryPick(string id) { }
        public void OnArmoryAction(string action, string arg) { }
        public void OnSquadToggle(string id, bool on) { }
        public void OnLaunch() { }
        public void OnSquadBack() { }
        public void OnSquadRecruit() { }
        public void OnGoMap() { }
        public void OnDebriefContinue() { }
        public void OnDebriefLine(int index) { }
        public void OnZone(string id, bool mark) { }
        public void OnOffer(string id) { }
        public void OnWait() { }
        public void OnListen(string rumor) { }
        public void OnMapAction(string action) { }
        public void OnPieceTake(string piece) { }
        public void OnFell() { }
        public void OnBuildDrop() { }
        public void OnPieceAction(string action) { }
        public void OnPieceClose() { }
        public void OnWalk() { }
        public void OnSeen(string key) { }
    }

    /// <summary>The handlers the pieces hold, so Bind can change who answers without making them again.</summary>
    public sealed class StrategyRelay : IStrategyHandlers
    {
        public IStrategyHandlers Target = NoStrategyHandlers.Instance;
        public void OnTab(string view) => Target.OnTab(view);
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
        public void OnCustomName(string name, string nickname) => Target.OnCustomName(name, nickname);
        public void OnCustomChoice(string group, string id, bool preview) => Target.OnCustomChoice(group, id, preview);
        public void OnCustomColor(string part, string color, bool preview) => Target.OnCustomColor(part, color, preview);
        public void OnCustomAction(string action) => Target.OnCustomAction(action);
        public void OnUnlock(string cell) => Target.OnUnlock(cell);
        public void OnBoardClose() => Target.OnBoardClose();
        public void OnArmoryTab(string tab) => Target.OnArmoryTab(tab);
        public void OnArmoryPick(string id) => Target.OnArmoryPick(id);
        public void OnArmoryAction(string action, string arg) => Target.OnArmoryAction(action, arg);
        public void OnSquadToggle(string id, bool on) => Target.OnSquadToggle(id, on);
        public void OnLaunch() => Target.OnLaunch();
        public void OnSquadBack() => Target.OnSquadBack();
        public void OnSquadRecruit() => Target.OnSquadRecruit();
        public void OnGoMap() => Target.OnGoMap();
        public void OnDebriefContinue() => Target.OnDebriefContinue();
        public void OnDebriefLine(int index) => Target.OnDebriefLine(index);
        public void OnZone(string id, bool mark) => Target.OnZone(id, mark);
        public void OnOffer(string id) => Target.OnOffer(id);
        public void OnWait() => Target.OnWait();
        public void OnListen(string rumor) => Target.OnListen(rumor);
        public void OnMapAction(string action) => Target.OnMapAction(action);
        public void OnPieceTake(string piece) => Target.OnPieceTake(piece);
        public void OnFell() => Target.OnFell();
        public void OnBuildDrop() => Target.OnBuildDrop();
        public void OnPieceAction(string action) => Target.OnPieceAction(action);
        public void OnPieceClose() => Target.OnPieceClose();
        public void OnWalk() => Target.OnWalk();
        public void OnSeen(string key) => Target.OnSeen(key);
    }
}
