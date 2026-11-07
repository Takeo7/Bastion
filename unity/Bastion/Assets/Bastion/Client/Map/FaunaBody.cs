using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// On an animal's model, by its animator (Fauna, its root motion on): what its clips walk it by is handed here and
    /// left, for Fauna moves the animal itself. Without root motion the animator wrote its clips' root onto the model,
    /// which went on ahead within each loop and back as it started again: a metre every 0.8 s, walking (Arturo, 7/11: «como
    /// que vuelve»). How fast its clips would walk it now (m/s at the animator's speed 1), for Fauna to match its legs to
    /// its pace.
    /// </summary>
    public sealed class FaunaBody : MonoBehaviour
    {
        Animator animator;

        /// <summary>How fast its clips walk it this frame, at speed 1 (m/s, along the ground; 0 with clips that walk it nowhere).</summary>
        public float ClipPace { get; private set; }

        void Awake() => animator = GetComponent<Animator>();

        void OnAnimatorMove()
        {
            if (animator == null) return;
            float dt = Time.deltaTime;
            var step = animator.deltaPosition;
            step.y = 0;
            ClipPace = dt > 0 && animator.speed > 1e-3f ? step.magnitude / dt / animator.speed : 0;
        }
    }
}
