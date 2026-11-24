using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base's orders through a real room, in one app (HudShots.BaseRed, with BASTION_ANFITRION hosting it on
    /// BASTION_DATOS): once in the lobby it opens a new campaign (BASTION_CONTINUAR=1: goes on with the one saved there); on the base, the base's screens (StrategyRoot and
    /// BaseSession, as AppFlow mounts them) and, one at a time, the clicks of BASTION_PASOS (comma-separated), each
    /// after the host's answer to the one before. The steps: tab:&lt;view&gt;, soldier:&lt;n&gt; (one's own, in order),
    /// offer:&lt;n&gt;, zone:&lt;n&gt;, map:&lt;action&gt;, recruit, launch, wait, squad:&lt;n&gt;:on|off, armory-tab:&lt;tab&gt;,
    /// armory-pick:&lt;id&gt;, armory:&lt;action&gt;[:&lt;arg&gt;], board, tablero:&lt;soldier&gt;, unlock:&lt;cell&gt;, vote:yes|no, withdraw, option:&lt;n&gt;,
    /// modal:&lt;n&gt;, continue, customize, custom-choice:&lt;group&gt;:&lt;id&gt;, custom-color:&lt;part&gt;:&lt;#hex&gt;,
    /// custom:&lt;action&gt;, choice:&lt;hand|belt|…&gt; (a choice of the sheet opened), esc and b (the keys), menu[:settings] (the base's menu, on a page), see-tile:&lt;x&gt;:&lt;y&gt; and see-piece:&lt;n&gt; (a proposal's «Ver»); on the clearing take:&lt;piece&gt;, fell, walk, turn, point:free[:&lt;n&gt;]|tree|post|&lt;x&gt;:&lt;y&gt;|+&lt;dx&gt;:+&lt;dy&gt;
    /// (the pointer there: free, the nearest tile where the piece in hand fits, or a line of n of it to the east; post,
    /// where the siege's defender in hand can start; tree, the nearest that can be felled; signed,
    /// from where it is), press, release;
    /// and in the mission, abandon and tobase. In the mission the base's screens go, as with AppFlow's
    /// change of scene, and a stand-in for the battle listens to NetLink as CombatSession does (the snapshot first, and
    /// off once the room leaves the mission), so the way back to the base goes as in the app. What happens goes to the
    /// log («[base-run]», and the controller's «[base]»).
    /// </summary>
    public sealed class BaseRun : MonoBehaviour
    {
        /// <summary>Frames an order waits for the host's answer before the next step goes anyway.</summary>
        const int AnswerFrames = 90;

        readonly Queue<string> steps = new Queue<string>();
        bool asked;
        float pauseUntil;
        int pauseFrames;
        GameObject screens;
        BaseSession session;
        object waitingFor;
        int waited;
        bool battle;
        bool over;
        CampaignState tookOff;

        void Start()
        {
            foreach (var s in (System.Environment.GetEnvironmentVariable("BASTION_PASOS") ?? "").Split(',').Select(x => x.Trim()).Where(x => x != "")) steps.Enqueue(s);
        }

        void Update()
        {
            // The panels' scale as the app's (AppFlow does it each frame): BASTION_DENSIDAD and BASTION_INTERFAZ count here
            // too (the 2560 × 1440 proposal, 24/11: the base came out at the project's own scale).
            Bastion.UI.App.GameSettings.WatchScreen();
            var net = NetLink.Instance;
            var phase = net.LastRoom?.Phase;
            if (phase == RoomPhases.Lobby && !asked && net.Slot >= 0)
            {
                asked = true;
                // BASTION_CONTINUAR=1: the campaign saved in BASTION_DATOS (save.json), as «Continuar partida» (U8, 17/11:
                // a save of Red's two-app pass, played on from where it was).
                if (System.Environment.GetEnvironmentVariable("BASTION_CONTINUAR") == "1")
                {
                    Debug.Log("[base-run] continuar la partida guardada");
                    net.Send(new ContinueCampaignMessage());
                    return;
                }
                Debug.Log("[base-run] nueva campaña");
                net.Send(new NewCampaignMessage());
                return;
            }
            if (phase == RoomPhases.Mission)
            {
                Mission(net);
                return;
            }
            if (phase != RoomPhases.Base) return;
            if (screens == null)
            {
                screens = new GameObject("Base");
                screens.AddComponent<UIDocument>().panelSettings = App.AppFlow.PanelOf(GetComponent<UIDocument>());
                screens.AddComponent<StrategyRoot>();
                session = screens.AddComponent<BaseSession>();
                waitingFor = null;
                waited = 0;
                return;
            }
            var control = session.Control;
            if (control?.State == null) return;
            // The answer to the last order: a new campaign from the host, or enough frames gone (refused, or nothing to do).
            if (waitingFor != null && waitingFor == (object)control.State && ++waited < AnswerFrames) return;
            if (waitingFor != null || waited == 0) Debug.Log($"[base-run] en la base: {control.View}, día {control.State.Day}");
            waitingFor = null;
            waited = -1;
            if (steps.Count == 0 || steps.Peek() == "abandon" || steps.Peek() == "tobase") return;
            // «pausa:20»: twenty seconds of the wall's clock before the next step (the partner's console client, U10).
            if (Time.realtimeSinceStartup < pauseUntil || Time.frameCount < pauseFrames) return;
            // «cuadros:3»: three frames before the next step (a capture's: at 1 fps, three seconds of its clock; revisión de la UI, 22/11).
            if (steps.Peek().StartsWith("cuadros:"))
            {
                var wait = steps.Dequeue();
                pauseFrames = Time.frameCount + int.Parse(wait.Substring(8));
                Debug.Log($"[base-run] {wait} desde el cuadro {Time.frameCount}, a {Time.time:0.0} s");
                return;
            }
            if (steps.Peek().StartsWith("pausa:"))
            {
                var pause = steps.Dequeue();
                pauseUntil = Time.realtimeSinceStartup + float.Parse(pause.Substring(6), System.Globalization.CultureInfo.InvariantCulture);
                Debug.Log($"[base-run] {pause}");
                return;
            }
            var step = steps.Dequeue();
            // (And the real clock: the plates' graces count by it, AskGrace.)
            Debug.Log($"[base-run] {step} a {Time.time:0.0} s (reloj real {Time.realtimeSinceStartup:0.00} s)");
            if (Do(control, step))
            {
                waitingFor = control.State;
                waited = 0;
            }
            if (steps.Count == 0) Debug.Log($"[base-run] hecho: {control.View}");
        }

        /// <summary>The mission: the base's screens gone, the stand-in listening, and the mission's steps.</summary>
        void Mission(NetLink net)
        {
            if (screens != null)
            {
                Destroy(screens);
                screens = null;
                session = null;
            }
            if (!battle)
            {
                Debug.Log("[base-run] en la misión: las pantallas de la base, fuera");
                battle = true;
                over = false;
                tookOff = null;
                net.Message += OnBattle;
            }
            if (steps.Count == 0) return;
            if (steps.Peek() == "abandon" && !over)
            {
                steps.Dequeue();
                Debug.Log("[base-run] abandon");
                net.Send(new AbandonMessage());
            }
            else if (steps.Peek() == "tobase" && over)
            {
                steps.Dequeue();
                Debug.Log("[base-run] tobase");
                net.Send(new ReturnToBaseMessage());
            }
        }

        /// <summary>The battle's stand-in (CombatSession's handover): what it heard, and off once the room leaves the mission.</summary>
        void OnBattle(ServerMessage m)
        {
            var net = NetLink.Instance;
            switch (m)
            {
                case SnapshotMessage s:
                    Debug.Log($"[base-run] misión: la foto, {s.State?.CampaignMission?.Name}");
                    break;
                case CampaignMessage c when tookOff == null:
                    tookOff = c.State;
                    break;
                case EventsMessage e when e.Events.Any(x => x is MissionEndedEvent):
                    over = true;
                    Debug.Log("[base-run] misión acabada");
                    break;
                case RoomMessage r when r.Phase != RoomPhases.Mission && r == net.LastRoom:
                    net.Message -= OnBattle;
                    battle = false;
                    BaseSession.TookOff = tookOff;
                    Debug.Log($"[base-run] fuera de la misión: {r.Phase}");
                    break;
            }
        }

        /// <summary>The tile the test's pointer was last put on.</summary>
        /// <summary>The click; true when it is an order, whose answer the next step waits for.</summary>
        internal static bool Do(BaseController control, string step)
        {
            var c = control.State;
            var part = step.Split(':');
            int N(int i) => part.Length > i && int.TryParse(part[i], out var n) ? n : 0;
            string S(int i) => part.Length > i ? part[i] : null;
            var mine = c.Soldiers.Where(s => s.Owner == NetLink.Instance.Slot && s.Alive).ToList();
            switch (part[0])
            {
                case "tab": control.OnTab(S(1)); return false;
                // («soldier:c9»: by its id.)
                case "soldier": control.OnSoldier(char.IsDigit((S(1) ?? "0")[0]) ? mine.ElementAtOrDefault(N(1))?.Id : S(1)); return false;
                case "recruit": control.OnRecruit(); return true;
                case "launch": control.OnLaunch(); return true;
                case "wait": control.OnWait(); return true;
                case "squad": control.OnSquadToggle(mine.ElementAtOrDefault(N(1))?.Id, S(2) != "off"); return true;
                case "board": control.OnBoard(); return false;
                // «tablero:c1»: that one's board from the expedition's map, a stop or the report (U10's keys).
                case "tablero": control.OnBoardFor(S(1)); return false;
                // «celda:steadyHand» (N6): that cell of the open board shown, as the pointer over it.
                case "celda": FindFirstObjectByType<StrategyRoot>()?.LabBoardCell(S(1)); return false;
                // «ayuda:rnode-n4-0»: the help of the element by that name pinned open (the expedition map's node card).
                case "ayuda":
                {
                    var strategy = FindFirstObjectByType<StrategyRoot>();
                    // (A path as «pasa» takes it: «board-stat#7».)
                    VisualElement found = strategy?.GetComponent<UIDocument>()?.rootVisualElement;
                    foreach (var piece in S(1).Split('/'))
                    {
                        var nth = piece.Split('#');
                        found = nth.Length > 1 ? found?.Query(className: nth[0]).AtIndex(int.Parse(nth[1])) : found?.Q(piece) ?? found?.Q(className: piece);
                    }
                    if (found != null) strategy.PinTip(found);
                    Debug.Log($"[base-run] ayuda de {S(1)}: {(found != null ? "abierta" : "no está")}");
                    return false;
                }
                // «pasa:rnode-n4-0»: the pointer over that element (the map on paper: its card); «pulsa:rnode-n4-0», a click on it
                // (a stage picked), as its own button's (cuadro 132). «pulsa:runmap-side/kit-fold»: by a path of names, the first
                // inside the one before (Almacén has a «kit-fold» of its own).
                case "pasa":
                case "pulsa":
                {
                    var strategy = FindFirstObjectByType<StrategyRoot>();
                    VisualElement found = strategy?.GetComponent<UIDocument>()?.rootVisualElement;
                    // («squad-row#3»: the fourth of them.)
                    foreach (var piece in S(1).Split('/'))
                    {
                        var nth = piece.Split('#');
                        found = nth.Length > 1 ? found?.Query(className: nth[0]).AtIndex(int.Parse(nth[1])) : found?.Q(piece) ?? found?.Q(className: piece);
                    }
                    if (found != null && part[0] == "pasa")
                        using (var e = PointerEnterEvent.GetPooled())
                        {
                            e.target = found;
                            found.SendEvent(e);
                        }
                    else if (found is Button button)
                        using (var e = NavigationSubmitEvent.GetPooled())
                        {
                            e.target = button;
                            button.SendEvent(e);
                        }
                    Debug.Log($"[base-run] {part[0]} {S(1)}: {(found != null ? "hecho" : "no está")}");
                    return false;
                }
                // The grids (M3, M9, M12, M13): «equipo», every place drawn and what lies in it, in the log; «mayus:g44», Mayús and a
                // click on it; «arrastra:g44>s2@0;1» dragged over a place (owner/slot: stash, s1/pack, s1/primary…, s2 for a
                // bust) and held there (a «!» at the end: let go; «~»: turned).
                case "equipo":
                    Debug.Log($"[base-run] equipo (retratos {StrategyRoot.PictureStamp}, cuadro {Time.frameCount}): {FindFirstObjectByType<StrategyRoot>()?.Gear?.TestList()}");
                    return false;
                case "mayus":
                {
                    var done = FindFirstObjectByType<StrategyRoot>()?.Gear?.TestSend(S(1)) == true;
                    Debug.Log($"[base-run] mayus {S(1)}: {(done ? "va" : "no va (o no está)")}");
                    return done;
                }
                case "arrastra":
                {
                    var board = FindFirstObjectByType<StrategyRoot>()?.Gear;
                    var parts = S(1).Split('>');
                    var done = false;
                    if (board != null && parts.Length == 2)
                    {
                        var spot = parts[1].TrimEnd('!', '~').Split('@');
                        var place = spot[0].Split('/');
                        var xy = spot.Length > 1 ? spot[1].Split(';') : new[] { "0", "0" };
                        done = board.TestDragTo(parts[0], place[0], place.Length > 1 ? place[1] : null, int.Parse(xy[0]), int.Parse(xy[1]), parts[1].Contains("~"), parts[1].EndsWith("!"));
                    }
                    Debug.Log($"[base-run] arrastra {S(1)}: {(done ? "hecho" : "no está")}");
                    return done && parts[1].EndsWith("!");
                }
                // «estado»: what is open now, in the log (U10's test with two).
                // «baja:runstop__offers»: that scroll list to its bottom, as the wheel would; its offset in the log (the 0.14.4's
                // review, 1: the Trueque's list kept where it was when a question opens). «mira:runstop__offers»: just the log.
                case "baja":
                case "mira":
                {
                    var list = FindFirstObjectByType<StrategyRoot>()?.GetComponent<UIDocument>()?.rootVisualElement?.Q<ScrollView>(className: S(1));
                    if (list != null && part[0] == "baja") list.scrollOffset = new Vector2(0, list.contentContainer.layout.height);
                    Debug.Log($"[base-run] {part[0]} {S(1)}: {(list != null ? $"desplazada {list.scrollOffset.y:0} de {list.contentContainer.layout.height - list.contentViewport.layout.height:0}" : "no está")}");
                    return false;
                }
                case "estado": Debug.Log($"[base-run] estado: {control.OpenNow()}"); return false;
                // «ocultar»: every panel of the screen hidden, the clearing and its row as they are (A8: Arte's shots of the
                // squad's feet and shadows on the halt); «mostrar», back.
                case "ocultar":
                case "mostrar":
                    foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                        if (doc.rootVisualElement != null) doc.rootVisualElement.style.display = part[0] == "ocultar" ? DisplayStyle.None : DisplayStyle.Flex;
                    return false;
                case "unlock": control.OnUnlock(S(1)); return true;
                case "vote": control.OnVote(S(1) == "yes"); return true;
                case "withdraw": control.OnWithdraw(); return true;
                case "option": control.OnOption(N(1)); return true;
                case "modal": control.OnModalButton(N(1)); return false;
                case "continue": control.OnDebriefContinue(); return false;
                case "customize": control.OnCustomize(); return false;
                // The runs (16/11): a stage, back to the base, an upgrade, a ware, on from a stop.
                case "node": control.OnNode(S(1)); return true;
                case "endrun": control.OnEndRun(); return true;
                case "upgrade": control.OnUpgrade(S(1)); return true;
                case "buy": control.OnBuy(S(1)); return true;
                // «ofrece:g36»: that thing of the chosen soldier's offered at the Trueque (M10).
                case "ofrece": control.OnSell(S(1)); return true;
                case "leave": control.OnLeaveStop(); return true;
                case "choice":
                    control.OpenChoice(S(1));
                    Debug.Log($"[base-run] choice:{S(1)} → {control.OpenNow()}");
                    return false;
                // The base's menu (BaseController.OnMenu), on a page of it (settings, controls), as its key and an entry.
                case "menu":
                    control.OnMenu();
                    if (!string.IsNullOrEmpty(S(1))) FindFirstObjectByType<StrategyRoot>().MenuPage(S(1));
                    return false;
                // The keys, as BaseSession takes them: Esc (BaseController.Escape).
                case "esc":
                    control.Escape();
                    Debug.Log($"[base-run] {part[0]} → {control.OpenNow()}");
                    return false;
                case "custom-choice": control.OnCustomChoice(S(1), S(2), false); return false;
                case "custom-color": control.OnCustomColor(S(1), S(2), false); return false;
                case "custom": control.OnCustomAction(S(1)); return S(1) == "save" || S(1) == "copy";
                default:
                    Debug.LogWarning($"[base-run] paso desconocido: {step}");
                    return false;
            }
        }
    }
}
