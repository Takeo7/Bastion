namespace Bastion.UI.Hud
{
    /// <summary>
    /// What the HUD asks of the game (hud.ts HudHandlers): the combat controller answers, sending the engine's
    /// commands or the network's messages (docs/UNITY-UI.md §3.4), or doing what is the client's own (camera, picking).
    /// </summary>
    public interface IHudHandlers
    {
        /// <summary>A key of the bar, or a choice of one of its menus.</summary>
        void OnAbility(string id);
        void OnEndTurn();
        void OnSelectUnit(string id);
        void OnConfirmTarget();
        void OnCancel();
        void OnCycleTarget(int dir);
        void OnPassCommand();
        /// <summary>The camera to what the partner holding the command is doing.</summary>
        void OnWatchPartner();
        /// <summary>The camera to the soldier whose turn it is, picked.</summary>
        void OnGoToSoldier();
        /// <summary>The camera switch («Cámara al soldado») flipped.</summary>
        void OnFollowTurn(bool on);
        /// <summary>The partner's call answered: evacAnswer, or the abandonAnswer message.</summary>
        void OnCallAnswer(bool accept);
        /// <summary>The arrival card closed: the mission plays.</summary>
        void OnIntroClosed();
        /// <summary>A held banner let go («TU TURNO», Ana 22/10 #50): the turn clock may run (the idle message).</summary>
        void OnBannerReleased();
        /// <summary>After the outcome: the same again on a new map, or any at random (skirmish).</summary>
        void OnNewMission(bool random);
        void OnReturnToBase();
        void OnBackToMenu();
        /// <summary>The soldier's sheet, to look at (H).</summary>
        void OnSheet(string unit);
        /// <summary>One floor up (1) or down (-1).</summary>
        void OnFloor(int dir);
        /// <summary>«Menú» (Esc): the game's menu, open or closed.</summary>
        void OnMenu();
        /// <summary>An order on the shown soldier's backpack or tile (hud.ts onItem): the engine's `item` command.</summary>
        void OnItem(PackOrder order);
        /// <summary>A fallen soldier's rucksack taken (onTakeAll): one thing after another while they fit.</summary>
        void OnTakeAll(System.Collections.Generic.List<string> ids);
        /// <summary>The backpack full: slot `drop` left on the floor, then `take` picked up (onSwap).</summary>
        void OnSwap(string take, int drop);
    }

    /// <summary>Handlers that do nothing: the HUD lab's, and the HUD's before the game binds its own.</summary>
    public sealed class NoHandlers : IHudHandlers
    {
        public static readonly NoHandlers Instance = new NoHandlers();

        public void OnAbility(string id) { }
        public void OnEndTurn() { }
        public void OnSelectUnit(string id) { }
        public void OnConfirmTarget() { }
        public void OnCancel() { }
        public void OnCycleTarget(int dir) { }
        public void OnPassCommand() { }
        public void OnWatchPartner() { }
        public void OnGoToSoldier() { }
        public void OnFollowTurn(bool on) { }
        public void OnCallAnswer(bool accept) { }
        public void OnIntroClosed() { }
        public void OnBannerReleased() { }
        public void OnNewMission(bool random) { }
        public void OnReturnToBase() { }
        public void OnBackToMenu() { }
        public void OnSheet(string unit) { }
        public void OnFloor(int dir) { }
        public void OnMenu() { }
        public void OnItem(PackOrder order) { }
        public void OnTakeAll(System.Collections.Generic.List<string> ids) { }
        public void OnSwap(string take, int drop) { }
    }
}

namespace Bastion.UI.Hud
{
    /// <summary>What the pieces hold: it passes everything on to whoever is bound now (the game, the lab, nobody).</summary>
    public sealed class HandlerRelay : IHudHandlers
    {
        public IHudHandlers Target = NoHandlers.Instance;

        public void OnAbility(string id) => Target.OnAbility(id);
        public void OnEndTurn() => Target.OnEndTurn();
        public void OnSelectUnit(string id) => Target.OnSelectUnit(id);
        public void OnConfirmTarget() => Target.OnConfirmTarget();
        public void OnCancel() => Target.OnCancel();
        public void OnCycleTarget(int dir) => Target.OnCycleTarget(dir);
        public void OnPassCommand() => Target.OnPassCommand();
        public void OnWatchPartner() => Target.OnWatchPartner();
        public void OnGoToSoldier() => Target.OnGoToSoldier();
        public void OnFollowTurn(bool on) => Target.OnFollowTurn(on);
        public void OnCallAnswer(bool accept) => Target.OnCallAnswer(accept);
        public void OnIntroClosed() => Target.OnIntroClosed();
        public void OnBannerReleased() => Target.OnBannerReleased();
        public void OnNewMission(bool random) => Target.OnNewMission(random);
        public void OnReturnToBase() => Target.OnReturnToBase();
        public void OnBackToMenu() => Target.OnBackToMenu();
        public void OnSheet(string unit) => Target.OnSheet(unit);
        public void OnFloor(int dir) => Target.OnFloor(dir);
        public void OnMenu() => Target.OnMenu();
        public void OnItem(PackOrder order) => Target.OnItem(order);
        public void OnTakeAll(System.Collections.Generic.List<string> ids) => Target.OnTakeAll(ids);
        public void OnSwap(string take, int drop) => Target.OnSwap(take, drop);
    }
}
