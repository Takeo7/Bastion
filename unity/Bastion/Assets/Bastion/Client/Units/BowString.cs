using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A bow's string (Caza, Arturo's cuadro 103: Synty's recurve bow, ANIMATION - Bow Combat's rig): its joint drawn to
    /// the fingers that pull it while `pull`, and back to rest at once when let go; the arrow nocked on it along the way
    /// to the bow's rest, shown while there is one (`nocked`). After the animator, each frame.
    /// </summary>
    public sealed class BowString : MonoBehaviour, IShotStep
    {
        public Transform joint, rest, fingers, arrow;
        public bool pull, nocked;
        Vector3 home;
        float drawn;

        void Start() => home = joint != null ? joint.localPosition : Vector3.zero;

        void LateUpdate() => Step(Time.deltaTime);
        public void ShotStep(float dt) => Step(dt);

        void Step(float dt)
        {
            if (joint == null) return;
            // (Let go, back in a twentieth of a second; drawn, with the hand.)
            drawn = Mathf.MoveTowards(drawn, pull && fingers != null ? 1 : 0, dt * (pull ? 6 : 20));
            var parent = joint.parent;
            var at = fingers != null ? parent.InverseTransformPoint(fingers.position) : home;
            joint.localPosition = Vector3.Lerp(home, at, drawn);
            if (arrow == null) return;
            arrow.gameObject.SetActive(nocked);
            if (!nocked || rest == null) return;
            var along = rest.position - joint.position;
            if (along.sqrMagnitude < 1e-6f) return;
            arrow.SetPositionAndRotation(joint.position, Quaternion.LookRotation(along.normalized, transform.up) * Quaternion.Euler(90, 0, 0));
        }
    }
}
