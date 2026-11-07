using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The dead La Lata goes over as it pulls away (Arturo's #5 of 7/11: «molaría que cuando el coche salga, si hay algún
    /// zombi, lo atropelle y salga volando»; MapView.LataDeparts): only what is seen, the mission being over as it goes and
    /// the rules done with it.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>
        /// Thrown: how fast along the ground at most (m/s: what MapView gives it, on ahead and off to the side), how fast up
        /// (m/s, and so much more for each m/s along, at most), and the world's pull (m/s²).
        /// </summary>
        const float ThrownMost = 10, ThrownUp = 3, ThrownUpPer = 0.25f, ThrownUpMost = 5.5f, Pull = 9.8f;
        /// <summary>Down again: how long it slides on, slowing (s), and how much of its speed it keeps as it hits the ground.</summary>
        const float ThrownSlide = 0.35f, ThrownKept = 0.35f;
        /// <summary>How high its middle is (m, a figure of the dead's size): what it turns head over heels round.</summary>
        const float Middle = 0.9f;

        /// <summary>The dead standing and seen, where their figures are: what La Lata may go over.</summary>
        public List<(string id, Vector3 at)> DeadStanding() =>
            views.Values.Where(v => v.deadKind && !v.dead && !v.runOver && v.root != null && v.root.gameObject.activeInHierarchy).Select(v => (v.id, v.root.position)).ToList();

        /// <summary>
        /// `id` hit by La Lata, thrown `push` (m/s, along the ground: MapView's, ahead and off to the side): its death the way
        /// it goes as it flies on and up, head over heels round its middle once (twice if thrown hard), to come down where its
        /// death lies, and a slide; it stays so whatever the state says after (Sync leaves it: runOver).
        /// </summary>
        public async Awaitable RunOver(string id, Vector3 push)
        {
            if (!views.TryGetValue(id, out var v) || v.dead || v.runOver || v.root == null) return;
            v.runOver = true;
            var way = new Vector3(push.x, 0, push.z);
            float speed = way.magnitude;
            way = speed > 1e-3f ? way / speed : v.root.forward;
            // (Die goes by its last blow: from behind it, the bus, it falls the way it is thrown.)
            v.blow = new Blow { from = v.root.position - way * 2, melee = true, at = Time.time };
            _ = Die(id);
            float on = Mathf.Min(speed, ThrownMost), up = Mathf.Min(ThrownUpMost, ThrownUp + on * ThrownUpPer);
            float flight = 2 * up / Pull;
            int turns = on > 7 ? 2 : 1;
            var axis = Vector3.Cross(Vector3.up, way);
            var start = v.root.position;
            var stood = v.root.rotation;
            var middle = Vector3.up * Middle * v.size * Figures.CharacterScale;
            for (float t = 0; t < flight; t += Time.deltaTime)
            {
                if (v.root == null) return;
                var turn = Quaternion.AngleAxis(360f * turns * t / flight, axis);
                var through = start + way * on * t + Vector3.up * (up * t - Pull * t * t / 2);
                v.root.SetPositionAndRotation(through + middle - turn * middle, turn * stood);
                await Awaitable.NextFrameAsync();
            }
            if (v.root == null) return;
            var down = start + way * on * flight;
            v.root.SetPositionAndRotation(down, stood);
            for (float t = 0; t < ThrownSlide; t += Time.deltaTime)
            {
                if (v.root == null) return;
                float k = t / ThrownSlide;
                v.root.position = down + way * on * ThrownKept * ThrownSlide * (k - k * k / 2);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
