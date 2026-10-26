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
    /// custom:&lt;action&gt;; on the clearing take:&lt;piece&gt;, fell, walk, turn, point:free[:&lt;n&gt;]|tree|post|&lt;x&gt;:&lt;y&gt;|+&lt;dx&gt;:+&lt;dy&gt;
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

        /// <summary>The tile the test's pointer was last put on.</summary>
        static Vector2Int pointed;

        /// <summary>The test's pointer onto a tile of the clearing (kept pressed if it was): one named, or the nearest fit for the tool.</summary>
        static bool Point(BaseController control, string[] part)
        {
            var clearing = Bastion.Client.Settlement.Instance;
            var b = clearing?.Base;
            if (b == null) return false;
            Vector2Int? tile = null;
            var relative = part.Length > 2 && (part[1].StartsWith("+") || part[1].StartsWith("-")) && ClearingInput.Fake != null;
            if (part.Length > 2 && int.TryParse(part[1], out var x) && int.TryParse(part[2], out var y)) tile = relative ? pointed + new Vector2Int(x, y) : new Vector2Int(x, y);
            else
            {
                // From the middle outwards: the first that fits.
                var mid = new Vector2Int(b.Width / 2, b.Height / 2);
                var input = control.Clearing;
                foreach (var t in Enumerable.Range(0, b.Width * b.Height).Select(i => new Vector2Int(i % b.Width, i / b.Width)).OrderBy(t => (t - mid).sqrMagnitude))
                {
                    // free:<n>: room for a line of n to the east from there.
                    var n = part.Length > 2 && int.TryParse(part[2], out var k) ? k : 1;
                    var fits = part.Length > 1 && part[1] == "tree"
                        ? Base.TreeAt(b, t.x, t.y) && Base.FellBlocker(b, t.x, t.y) == null
                        : part.Length > 1 && part[1] == "post"
                        ? input.Soldier != null && control.PostBlocker(input.Soldier, t.x, t.y) == null
                        : input.Piece != null && (n > 1
                            ? control.LineBlocker(input.Piece, Enumerable.Range(0, n).Select(i => new Vec2 { X = t.x + i, Y = t.y }).ToList(), input.Rot) == null
                            : ClearingInput.PlaceAt(input.Piece, input.Rot, t) is var at && control.Blocker(input.Piece, at.x, at.y, input.Rot) == null);
                    if (!fits) continue;
                    tile = t;
                    break;
                }
            }
            if (tile == null)
            {
                Debug.LogWarning($"[base-run] ninguna casilla para {string.Join(":", part)}");
                return false;
            }
            var down = ClearingInput.Fake?.down ?? false;
            pointed = tile.Value;
            var s = clearing.Camera.WorldToScreenPoint(clearing.TileWorld(tile.Value.x, tile.Value.y));
            ClearingInput.Fake = (new Vector2(s.x, s.y), down);
            Debug.Log($"[base-run] puntero en {tile.Value.x},{tile.Value.y}");
            return false;
        }

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
                case "take": control.OnPieceTake(S(1)); return false;
                case "fell": control.OnFell(); return false;
                case "walk": control.OnWalk(); return false;
                case "turn": control.Clearing.Turn(1); return false;
                case "point": return Point(control, part);
                case "press":
                case "release":
                    if (ClearingInput.Fake is var f && f != null) ClearingInput.Fake = (f.Value.at, part[0] == "press");
                    var hand = control.Clearing;
                    var seen = ClearingInput.Fake is var g && g != null ? Bastion.Client.Settlement.Instance?.TileAt(g.Value.at) : null;
                    var over = ClearingInput.Fake is var o && o != null && FindFirstObjectByType<StrategyRoot>().Over(o.Value.at);
                    Debug.Log($"[base-run] {part[0]} en {pointed.x},{pointed.y} (TileAt: {seen}, sobre un panel: {over}): {hand.Tool} {hand.Piece ?? hand.Soldier}{(hand.Soldier != null ? $", puesto: {control.PostBlocker(hand.Soldier, pointed.x, pointed.y) ?? "sí"}" : "")}");
                    return part[0] == "release";
                default:
                    Debug.LogWarning($"[base-run] paso desconocido: {step}");
                    return false;
            }
        }
    }
}
