using System.Collections.Generic;
using Bastion.Client.Effects;
using Bastion.Client.Map;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The clearing's fires lit (0.10.1's clearingArt.ts CampFlames: basePieces' fire pits are cold stones): over each a
    /// few licks of Particle FX's flame, each flickering on its own, turning slowly; no light of their own (a light more is
    /// a shader more).
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>
        /// A fire's flames: how many licks, how tall at most and how far out from its middle (tiles), their colours
        /// (yellower than fire looks: past white the tone mapping turns a saturated orange pink), and how bright (to bloom).
        /// </summary>
        const int FlameLicks = 6;
        const float FlameTall = 0.42f, FlameOut = 0.13f, FlameGlow = 1.3f;
        static readonly int[] FlameColours = { 0xffb030, 0xffd050, 0xff9a20, 0xffe080 };

        sealed class Lick
        {
            public Transform t;
            public Vector3 at;
            public float size, phase, rate;
        }

        readonly List<Lick> licks = new List<Lick>();
        Transform flames;
        string flamesKey = "";
        float flameTime;
        static Material[] flameMaterials;

        /// <summary>The fires at `fires` (0.10.1's tiles: their middles), lit; made again only if they moved.</summary>
        void SetFlames(List<Vector3> fires)
        {
            string key = string.Join(" ", fires.ConvertAll(f => $"{f.x:0.00},{f.z:0.00}"));
            if (key == flamesKey && flames != null) return;
            flamesKey = key;
            if (flames != null) DestroyNow(flames.gameObject);
            licks.Clear();
            flames = new GameObject("Llamas").transform;
            flames.SetParent(transform, false);
            var art = Resources.Load<FxArt>("FxArt");
            var mesh = art != null ? art.flame : null;
            if (mesh == null)
            {
                Debug.LogWarning("[settlement] no flame in Resources/FxArt.asset (FxArtMake.Run): the fires stay cold");
                return;
            }
            var materials = FlameMaterials(art);
            var bounds = mesh.bounds;
            float across = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 1e-4f);
            for (int n = 0; n < fires.Count; n++)
                for (int k = 0; k < FlameLicks; k++)
                {
                    float a = (float)k / FlameLicks * Mathf.PI * 2 + n;
                    float out_ = k == 0 ? 0 : FlameOut * (0.6f + 0.4f * ((k * 37) % 5) / 4f);
                    var lick = new GameObject("llama").transform;
                    lick.SetParent(flames, false);
                    // (Particle FX's shape at its own size and pivot: a unit across round its middle, as fx.ts unitShape.)
                    var shape = new GameObject("forma").transform;
                    shape.SetParent(lick, false);
                    shape.localScale = Vector3.one / across;
                    shape.localPosition = -bounds.center / across;
                    shape.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = shape.gameObject.AddComponent<MeshRenderer>();
                    r.sharedMaterial = materials[k % materials.Length];
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    licks.Add(new Lick
                    {
                        t = lick,
                        at = new Vector3(fires[n].x + Mathf.Cos(a) * out_, 0, fires[n].z + Mathf.Sin(a) * out_),
                        size = FlameTall * (k == 0 ? 1 : 0.55f + 0.1f * (k % 3)),
                        phase = k * 1.7f + n * 3.1f,
                        rate = 5 + (k % 4) * 1.3f,
                    });
                }
            StepFlames(0);
            Debug.Log($"[settlement] {fires.Count} fires lit: {key}");
        }

        /// <summary>Bastion/FxUnlit, opaque, in each colour: three's basic material, its colour in linear times the glow.</summary>
        static Material[] FlameMaterials(FxArt art)
        {
            if (flameMaterials != null && flameMaterials[0] != null) return flameMaterials;
            var shader = art.unlit != null ? art.unlit : Shader.Find("Bastion/FxUnlit");
            flameMaterials = new Material[FlameColours.Length];
            for (int i = 0; i < FlameColours.Length; i++)
            {
                int c = FlameColours[i];
                var colour = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, 255);
                var m = new Material(shader) { name = "Llama", renderQueue = 2000, hideFlags = HideFlags.DontSave };
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                m.SetFloat("_ZWrite", 1);
                m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                m.SetColor("_BaseColor", UnderWhite(((Color)colour).linear * FlameGlow).gamma);
                flameMaterials[i] = m;
            }
            return flameMaterials;
        }

        /// <summary>
        /// A colour brighter than white (linear) as the one no brighter than white whose tone mapping comes nearest its own:
        /// Unity's grade (Grade.shader) gets the picture after URP's post, already clipped at 1, and a flame at 1.3 came out
        /// lemon where three's tone mapping, which takes it whole, made it orange (25/10: green 25 levels over at the same
        /// lick).
        /// </summary>
        static Color UnderWhite(Color hdr)
        {
            var target = Neutral(new Vector3(hdr.r, hdr.g, hdr.b));
            var x = Vector3.Min(target, Vector3.one);
            for (int k = 0; k < 60; k++)
            {
                x += target - Neutral(x);
                x = new Vector3(Mathf.Clamp01(x.x), Mathf.Clamp01(x.y), Mathf.Clamp01(x.z));
            }
            return new Color(x.x, x.y, x.z, hdr.a);
        }

        /// <summary>Khronos' PBR neutral tone mapping (three's NeutralToneMapping, Grade.shader's Neutral).</summary>
        static Vector3 Neutral(Vector3 c)
        {
            const float start = 0.8f - 0.04f, desaturation = 0.15f;
            float x = Mathf.Min(c.x, Mathf.Min(c.y, c.z));
            float offset = x < 0.08f ? x - 6.25f * x * x : 0.04f;
            c -= Vector3.one * offset;
            float peak = Mathf.Max(c.x, Mathf.Max(c.y, c.z));
            if (peak < start) return c;
            float d = 1 - start;
            float top = 1 - d * d / (peak + d - start);
            c *= top / peak;
            float g = 1 - 1 / (desaturation * (peak - top) + 1);
            return Vector3.Lerp(c, Vector3.one * top, g);
        }

        /// <summary>The flames `dt` seconds on: each lick's flicker, height and turn.</summary>
        void StepFlames(float dt)
        {
            flameTime += dt;
            foreach (var l in licks)
            {
                if (l.t == null) continue;
                float t = flameTime * l.rate + l.phase;
                float flick = 0.75f + 0.25f * Mathf.Sin(t) * Mathf.Sin(t * 0.37f + 1.1f);
                float s = l.size * flick;
                l.t.localScale = new Vector3(s * 0.55f, s, s * 0.55f) * MapSpace.Tile;
                l.t.localPosition = World(new Vector3(l.at.x, 0.08f + s * 0.38f, l.at.z));
                l.t.localRotation = Quaternion.Euler(0, -(flameTime * 0.6f + l.phase) * Mathf.Rad2Deg, 0);
            }
        }
    }
}
