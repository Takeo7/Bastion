using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The sun's shadows over what the battle's camera sees (Gameplay 26/10: at its farthest, 42 tiles, 63 m, the middle of
    /// the screen was past the pipeline's 50 m and had none): every frame, the ground its view takes in, nearest and
    /// farthest (the depth of its corners' and its middle's rays down to the floor), the shadows reaching just past the
    /// farthest, and their cascades split over what is seen, not over the air between the camera and the ground (the
    /// nearest from the camera to a little past the nearest ground, so that close up they keep their sharpness). On the
    /// battle's map (MapView.FitShadows); the pipeline's own back when it goes.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class ShadowFit : MonoBehaviour
    {
        /// <summary>The camera fitted to; null, the scene's main one.</summary>
        public Camera Camera;

        /// <summary>The shadows this much past the farthest ground seen, never nearer than Least nor farther than Most (metres).</summary>
        const float Margin = 1.08f, Least = 20, Most = 140;
        /// <summary>A ray over the horizon, as if the ground were this far (metres, in depth).</summary>
        const float Horizon = 140;
        /// <summary>Where each cascade ends between the nearest ground seen and the farthest (2 cascades: the first; 4: the first three).</summary>
        static readonly float[] Splits2 = { 0.3f }, Splits4 = { 0.12f, 0.3f, 0.55f };

        /// <summary>The ground's depths last fitted to (metres), for the labs.</summary>
        public float Near { get; private set; }
        public float Far { get; private set; }

        void LateUpdate() => Fit();

        void OnDisable() => Mood.ShadowReach(null);

        /// <summary>Fits the shadows to the camera's view now (the labs call it in edit mode, where there are no frames).</summary>
        public void Fit()
        {
            var camera = Camera != null ? Camera : Camera.main;
            if (camera == null) return;
            float near = float.MaxValue, far = 0;
            var forward = camera.transform.forward;
            foreach (var corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0) })
            {
                var ray = camera.ViewportPointToRay(new Vector3(corner.x, corner.y, 0));
                float depth = Horizon;
                if (ray.direction.y < -1e-3f)
                {
                    var ground = ray.GetPoint(-ray.origin.y / ray.direction.y);
                    depth = Mathf.Min(Horizon, Vector3.Dot(ground - camera.transform.position, forward));
                }
                near = Mathf.Min(near, depth);
                far = Mathf.Max(far, depth);
            }
            Near = near;
            Far = Mathf.Clamp(far * Margin, Least, Most);
            Mood.FitShadows(Far, Mathf.Min(Near, Far), Splits2, Splits4);
        }
    }
}
