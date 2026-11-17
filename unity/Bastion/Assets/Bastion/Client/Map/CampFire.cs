using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Effects;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The camping's fires lit (runs, H9 §5: each `firePit` mark of GameState.Interiors' camping, the stones its marks draw):
    /// the clearing's flames (Settlement.Flames: a few licks of Particle FX's flame, each flickering on its own, turning
    /// slowly), the same licks and colours. Its light by night is NightLights' (CampFires). Stepped in the labs' strips
    /// too (IShotStep).
    /// </summary>
    public sealed class CampFire : MonoBehaviour, IShotStep
    {
        sealed class Lick
        {
            public Transform t;
            public Vector3 at;
            public float size, phase, rate;
        }

        readonly List<Lick> licks = new List<Lick>();
        float time;

        /// <summary>Where state `s`'s camp fires burn: each `firePit` mark's tile middle, on its floor (Unity's metres).</summary>
        public static List<Vector3> Of(GameState s)
        {
            var list = new List<Vector3>();
            if (s?.Interiors == null) return list;
            foreach (var site in s.Interiors.Where(i => i?.Kind == InteriorKinds.Camping && i.Dressing != null))
                foreach (var d in site.Dressing)
                    if (d.Kind == CampDetailKinds.FirePit) list.Add(MapSpace.TileToWorld(s, new Pos { X = d.X, Y = d.Y, Z = d.Z }));
            return list;
        }

        /// <summary>The fires of state `s` lit under `parent`; null if it has none, or no flame to light them with.</summary>
        public static CampFire Make(Transform parent, GameState s)
        {
            var fires = Of(s);
            if (fires.Count == 0) return null;
            var art = Resources.Load<FxArt>("FxArt");
            var mesh = art != null ? art.flame : null;
            if (mesh == null)
            {
                Debug.LogWarning("[map] no flame in Resources/FxArt.asset (FxArtMake.Run): the camping's fire stays cold");
                return null;
            }
            var fire = new GameObject("Hoguera").AddComponent<CampFire>();
            fire.transform.SetParent(parent, false);
            var materials = Settlement.FlameMaterials(art);
            var bounds = mesh.bounds;
            float across = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 1e-4f);
            for (int n = 0; n < fires.Count; n++)
                for (int k = 0; k < Settlement.FlameLicks; k++)
                {
                    float a = (float)k / Settlement.FlameLicks * Mathf.PI * 2 + n;
                    float out_ = k == 0 ? 0 : Settlement.FlameOut * (0.6f + 0.4f * ((k * 37) % 5) / 4f);
                    var lick = new GameObject("llama").transform;
                    lick.SetParent(fire.transform, false);
                    // (Particle FX's shape at its own size and pivot: a unit across round its middle, as the clearing's.)
                    var shape = new GameObject("forma").transform;
                    shape.SetParent(lick, false);
                    shape.localScale = Vector3.one / across;
                    shape.localPosition = -bounds.center / across;
                    shape.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = shape.gameObject.AddComponent<MeshRenderer>();
                    r.sharedMaterial = materials[k % materials.Length];
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    fire.licks.Add(new Lick
                    {
                        t = lick,
                        at = fires[n] + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * out_ * MapSpace.Tile,
                        size = Settlement.FlameTall * (k == 0 ? 1 : 0.55f + 0.1f * (k % 3)),
                        phase = k * 1.7f + n * 3.1f,
                        rate = 5 + (k % 4) * 1.3f,
                    });
                }
            fire.Step(0);
            return fire;
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        public void ShotStep(float dt) => Step(dt);

        /// <summary>The flames `dt` seconds on: each lick's flicker, height and turn (Settlement.StepFlames).</summary>
        void Step(float dt)
        {
            time += dt;
            foreach (var l in licks)
            {
                if (l.t == null) continue;
                float t = time * l.rate + l.phase;
                float flick = 0.75f + 0.25f * Mathf.Sin(t) * Mathf.Sin(t * 0.37f + 1.1f);
                float s = l.size * flick;
                l.t.localScale = new Vector3(s * 0.55f, s, s * 0.55f) * MapSpace.Tile;
                l.t.position = l.at + Vector3.up * (0.08f + s * 0.38f) * MapSpace.Tile;
                l.t.rotation = Quaternion.Euler(0, -(time * 0.6f + l.phase) * Mathf.Rad2Deg, 0);
            }
        }
    }
}
