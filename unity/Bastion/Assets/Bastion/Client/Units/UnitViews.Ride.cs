using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The motorbike (N5, Arturo 17/11: La Lata with two seats, those aboard seen on it). One boarding it (Board) runs to
    /// its side by its seat (LaLata.MountSpot), then gets on over MountSeconds: turned to its nose, its hips up over the
    /// seat and onto it (LaLata.Seat), sat as Mixamo's «Sitting Idle 3» has one on a chair (no riding clip yet; Producto,
    /// 17/11: what there is, for now). There it stays as La Lata stands and drives off, and is gone with it. Its view stays
    /// though the state has it evacuated (Sync), and the rules move it no more.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>Seconds getting on takes once beside it, and how high its hips go over the way meanwhile (metres).</summary>
        public const float MountSeconds = 0.6f;
        const float MountHop = 0.15f;
        /// <summary>The way to its side, run: metres a second, as the run's clip has its feet go on our figures (Gaits).</summary>
        const float MountRun = 2.4f * Figures.CharacterScale;
        /// <summary>How fast it turns on the way (degrees a second).</summary>
        const float MountTurnRate = 540;

        /// <summary>Whether anyone is still on the way to the motorbike or getting on it (MapView.LataDeparts waits for them).</summary>
        public bool Mounting => views.Values.Any(v => v.seat >= 0 && v.mounted < 1);

        /// <summary>Onto the motorbike, if La Lata is one and has a seat free: true if so (else Board fades it out by it).</summary>
        bool Mount(View v)
        {
            var lata = MapView.Current?.Lata;
            // (Only onto one built: without its model, Seat is null and nobody could ever sit; it fades out by it as into the others.)
            if (lata == null || !lata.IsBike || lata.Seat(0) == null || v.root == null || v.anim == null) return false;
            int seat = -1;
            for (int k = 0; k < lata.Seats && seat < 0; k++)
                if (!views.Values.Any(o => o != v && o.seat == k)) seat = k;
            if (seat < 0) return false;
            v.seat = seat;
            v.mounted = -1;
            v.yawTo = null;
            v.anim.Motion("run");
            // The rider's gun on the back, the hands free for the handlebar (Arturo, 18/11); the one behind keeps theirs.
            if (seat == 0)
            {
                Draw(v, EmptyHands);
                v.anim.Rest(null);
                v.anim.ArmRest(null, 0, 0.2f);
            }
            return true;
        }

        // ------------------------------------------------------------ in by the door and down out of it (T24)

        /// <summary>Seconds one takes to come down out of La Lata onto its tile (Alight), and how near a doorway has to be to go in or come out by it (metres).</summary>
        public const float AlightSeconds = 0.5f;
        const float DoorNear = 3f;

        /// <summary>Who is aboard La Lata by the state last seen (Unit.Aboard), and who came down in this Sync (from where; off the motorbike, with a hop).</summary>
        readonly HashSet<string> aboard = new HashSet<string>();
        readonly List<(string id, Vector3 from, bool hop)> alighting = new List<(string, Vector3, bool)>();
        /// <summary>BASTION_LATA_LOG=1: who the state has aboard, who comes down and from where, and when it is down ([lata] lines).</summary>
        static readonly bool LataLog = System.Environment.GetEnvironmentVariable("BASTION_LATA_LOG") == "1";

        /// <summary>La Lata's doorway nearest `near`, in the world, if within DoorNear; null on the motorbike, or without doors.</summary>
        static Vector3? Doorway(Vector3 near)
        {
            var lata = MapView.Current?.Lata;
            if (lata == null || lata.IsBike || !lata.Shown) return null;
            Vector3? best = null;
            float least = DoorNear * DoorNear;
            // (Its doorways in tiles, 0.10.1's frame, z south: in Unity's world.)
            foreach (var t in lata.Doorways())
            {
                var p = new Vector3(t.x, t.y, -t.z) * MapSpace.Tile;
                float d = (new Vector3(p.x - near.x, 0, p.z - near.z)).sqrMagnitude;
                if (d < least) (best, least) = (p, d);
            }
            return best;
        }

        /// <summary>
        /// One aboard on the motorbike seated in sight (a game loaded or joined with it aboard: UI's capture 13 of 18/11, the
        /// motorbike empty with someone aboard since the turn began): its view made if need be and put beside its seat, to
        /// get on (Ride). True if its view is kept.
        /// </summary>
        bool Seated(Unit u)
        {
            var lata = MapView.Current?.Lata;
            if (u.Aboard != true || lata == null || !lata.IsBike || !lata.Shown || lata.Seat(0) == null) return false;
            if (!views.TryGetValue(u.Id, out var v)) views[u.Id] = v = Make(u);
            if (v.seat >= 0) return true;
            v.boarded = true;
            if (!Mount(v)) return false;
            if (lata.MountSpot(v.seat, v.root.position) is Vector3 spot) v.root.position = new Vector3(spot.x, v.root.position.y, spot.z);
            return true;
        }

        /// <summary>Where `u`, aboard till now, comes down from: off its seat on the motorbike (`hop`), or La Lata's doorway nearest its tile; null, none (it is just there).</summary>
        Vector3? AlightFrom(Unit u, out bool hop)
        {
            hop = views.TryGetValue(u.Id, out var v) && v.seat >= 0 && v.root != null;
            if (hop) return v.root.position;
            return state != null ? Doorway(MapSpace.TileToWorld(state, u.Pos)) : null;
        }

        /// <summary>Down out of La Lata (AlightedEvent, by the state: Sync): from `from` to where it stands, faded in as it comes (off the motorbike, in sight, with a hop).</summary>
        void Alight(View v, Vector3 from, bool hop)
        {
            if (v.root == null) return;
            (v.alight, v.alightHop, v.alightTo) = (0, hop, v.root.position);
            v.alightFrom = new Vector3(from.x, hop ? from.y : v.alightTo.y, from.z);
            v.root.position = v.alightFrom;
            var way = v.alightTo - v.alightFrom;
            way.y = 0;
            if (way.sqrMagnitude > 1e-4f) v.root.rotation = Quaternion.LookRotation(way);
            v.yawTo = null;
            if (!hop)
            {
                v.fade = 0.001f;
                v.fadeGoal = 1;
                v.fadeRate = 1 / AlightSeconds;
                // (Now: made this Sync, it was drawn whole by the door for a frame before its fade began.)
                StepFade(v, 0);
            }
            v.anim?.Motion("run");
            if (LataLog) Debug.Log($"[lata] {v.id} comes down from {v.alightFrom} to {v.alightTo}, frame {Time.frameCount}");
        }

        /// <summary>One going in by the door or coming down, a step.</summary>
        void Door(View v, float dt)
        {
            if (v.root == null) return;
            if (v.boarded && v.seat < 0 && v.boardFrom is Vector3 a && v.boardTo is Vector3 b) v.root.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, 1 - v.fade));
            if (v.alight < 0) return;
            v.alight = Mathf.Min(1, v.alight + dt / AlightSeconds);
            float k = Mathf.SmoothStep(0, 1, v.alight);
            v.root.position = Vector3.Lerp(v.alightFrom, v.alightTo, k) + (v.alightHop ? Vector3.up * (Mathf.Sin(Mathf.PI * k) * MountHop) : Vector3.zero);
            if (v.alight < 1) return;
            v.alight = -1;
            v.anim?.Motion(IdleOf(v));
            if (LataLog) Debug.Log($"[lata] {v.id} is down at {v.root.position}, frame {Time.frameCount}");
        }

        /// <summary>One on its way to the motorbike, getting on it or on it, a step (after its pose: its hips' place).</summary>
        void Ride(View v, float dt)
        {
            Door(v, dt);
            if (v.seat < 0 || v.root == null) return;
            var lata = MapView.Current?.Lata;
            var seatNow = lata != null ? lata.Seat(v.seat) : null;
            if (seatNow == null) return;
            var (at, facing) = seatNow.Value;
            // (Gone with it: once it has driven off it is back on its bay out of sight, and they with it.)
            if (v.mounted >= 1 && !lata.Shown)
            {
                if (v.root.gameObject.activeSelf) v.root.gameObject.SetActive(false);
                return;
            }
            if (v.mounted < 0)
            {
                var spot = lata.MountSpot(v.seat, v.root.position) ?? v.root.position;
                var to = spot - v.root.position;
                to.y = 0;
                float left = to.magnitude, go = MountRun * dt;
                if (left > go)
                {
                    v.root.position += to / left * go;
                    v.root.rotation = Quaternion.RotateTowards(v.root.rotation, Quaternion.LookRotation(to), MountTurnRate * dt);
                    return;
                }
                v.root.position = new Vector3(spot.x, v.root.position.y, spot.z);
                v.anim.Motion(IdleOf(v));
                v.anim.CampLoop(FigureCatalog.Load()?.Clip("sitChair"));
                v.mounted = 0;
                v.mountFrom = v.root.position;
                v.mountTurn = v.root.rotation;
            }
            v.mounted = Mathf.Min(1, v.mounted + dt / MountSeconds);
            float k = Mathf.SmoothStep(0, 1, v.mounted);
            v.root.rotation = Quaternion.Slerp(v.mountTurn, facing, k);
            // (Where its hips are under the pose now, the root then put so that they come over the seat.)
            var hips = v.animator != null ? v.animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            var off = hips != null ? hips.position - v.root.position : Vector3.up * 0.65f;
            v.root.position = Vector3.Lerp(v.mountFrom, at - off, k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * MountHop);
            if (v.seat == 0 && lata.Grips() is var (gripL, gripR)) RiderHands(v.animator, gripL, gripR, k);
        }

        /// <summary>
        /// The rider's hands onto the handlebar's grips (Arturo, 18/11: «manos en los puños»), `k` of the way from where the
        /// pose has them (getting on, as it goes): each arm by two-bone IK (Reach), its elbow out to its side and down. The
        /// base's riders too (Settlement.Ride).
        /// </summary>
        public static void RiderHands(Animator a, Vector3 left, Vector3 right, float k)
        {
            if (a == null || !a.isHuman || k <= 0) return;
            var body = a.transform;
            foreach (var (upper, lower, handBone, knuckleBone, grip, side) in new[]
            {
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, left, -1f),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, right, 1f),
            })
            {
                var hand = a.GetBoneTransform(handBone);
                var knuckle = a.GetBoneTransform(knuckleBone);
                if (hand == null) continue;
                // (The palm on the grip, not the wrist: the palm is ahead of the wrist, and turns with the forearm. Twice, as Support.)
                for (int pass = 0; pass < 2; pass++)
                {
                    var palm = knuckle != null ? Vector3.Lerp(hand.position, knuckle.position, 0.6f) : hand.position;
                    var goal = Vector3.Lerp(palm, grip, k);
                    Reach(a, upper, lower, handBone, hand.position + (goal - palm), body.right * side - body.up * 0.6f, 0.005f);
                }
            }
        }
    }
}
