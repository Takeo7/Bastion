using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The units' figures in the scene by unit id: what UI's marks and rings follow (HudRoot.HeadOf, the selected one's
    /// ring). Each figure registers as it is made and leaves as it goes.
    /// </summary>
    public static class Figures
    {
        /// <summary>
        /// Characters stand this much taller than true scale (0.10.1's syntyManifest.ts CHARACTER_SCALE): 1.35 tiles
        /// instead of 1.2, so they read from the tactical camera. Weapons and hats grow with them; props keep their size.
        /// </summary>
        public const float CharacterScale = 1.35f / 1.2f;

        sealed class Figure
        {
            public Transform feet;
            public Transform head;
        }

        static readonly Dictionary<string, Figure> figures = new Dictionary<string, Figure>();

        /// <summary>A figure: its root (on its feet, moving as it walks) and its head (an Animator's head bone, or above the root).</summary>
        public static void Register(string id, Transform feet, Transform head = null)
        {
            if (head == null)
            {
                var animator = feet.GetComponentInChildren<Animator>();
                head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            }
            figures[id] = new Figure { feet = feet, head = head != null ? head : feet };
        }

        public static void Unregister(string id) => figures.Remove(id);

        /// <summary>Its head, or null while it has no figure in the scene.</summary>
        public static Transform HeadOf(string id) => figures.TryGetValue(id, out var f) && f.head != null ? f.head : null;

        /// <summary>Its feet (its root: where it stands, step by step as it walks), or null.</summary>
        public static Transform FeetOf(string id) => figures.TryGetValue(id, out var f) && f.feet != null ? f.feet : null;
    }
}
