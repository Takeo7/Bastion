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
    /// BASTION_DATOS): once in the lobby it opens a new campaign; on the base, the base's screens (StrategyRoot and
    /// BaseSession, as AppFlow mounts them) and, one at a time, the clicks of BASTION_PASOS (comma-separated), each
    /// after the host's answer to the one before. The steps: tab:&lt;view&gt;, soldier:&lt;n&gt; (one's own, in order),
    /// offer:&lt;n&gt;, zone:&lt;n&gt;, map:&lt;action&gt;, recruit, launch, wait, squad:&lt;n&gt;:on|off, armory-tab:&lt;tab&gt;,
    /// armory-pick:&lt;id&gt;, armory:&lt;action&gt;[:&lt;arg&gt;], board, unlock:&lt;cell&gt;, vote:yes|no, withdraw, option:&lt;n&gt;,
    /// modal:&lt;n&gt;, continue, customize, custom-choice:&lt;group&gt;:&lt;id&gt;, custom-color:&lt;part&gt;:&lt;#hex&gt;,
    /// custom:&lt;action&gt;; and in the mission, abandon and tobase. In the mission the base's screens go, as with AppFlow's
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
            var net = NetLink.Instance;
            var phase = net.LastRoom?.Phase;
            if (phase == RoomPhases.Lobby && !asked && net.Slot >= 0)
            {
                asked = true;
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
            var step = steps.Dequeue();
            Debug.Log($"[base-run] {step}");
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

        /// <summary>The click; true when it is an order, whose answer the next step waits for.</summary>
        static bool Do(BaseController control, string step)
        {
            var c = control.State;
            var part = step.Split(':');
            int N(int i) => part.Length > i && int.TryParse(part[i], out var n) ? n : 0;
            string S(int i) => part.Length > i ? part[i] : null;
            var mine = c.Soldiers.Where(s => s.Owner == NetLink.Instance.Slot && s.Alive).ToList();
            switch (part[0])
            {
                case "tab": control.OnTab(S(1)); return false;
                case "soldier": control.OnSoldier(mine.ElementAtOrDefault(N(1))?.Id); return false;
                case "offer": control.OnOffer(c.Offers.ElementAtOrDefault(N(1))?.Id); return false;
                case "zone": control.OnZone(c.Regions.ElementAtOrDefault(N(1))?.Id, false); return false;
                case "map": control.OnMapAction(S(1)); return S(1) != "squad";
                case "recruit": control.OnRecruit(); return true;
                case "launch": control.OnLaunch(); return true;
                case "wait": control.OnWait(); return true;
                case "squad": control.OnSquadToggle(mine.ElementAtOrDefault(N(1))?.Id, S(2) != "off"); return true;
                case "armory-tab": control.OnArmoryTab(S(1)); return false;
                case "armory-pick": control.OnArmoryPick(string.Join(":", part.Skip(1))); return false;
                case "armory": control.OnArmoryAction(S(1), S(2)); return true;
                case "board": control.OnBoard(); return false;
                case "unlock": control.OnUnlock(S(1)); return true;
                case "vote": control.OnVote(S(1) == "yes"); return true;
                case "withdraw": control.OnWithdraw(); return true;
                case "option": control.OnOption(N(1)); return true;
                case "modal": control.OnModalButton(N(1)); return false;
                case "continue": control.OnDebriefContinue(); return false;
                case "customize": control.OnCustomize(); return false;
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
