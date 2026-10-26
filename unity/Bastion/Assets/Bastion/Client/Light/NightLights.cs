using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using UnityEngine;
using UnityEngine.Rendering;
using Light = UnityEngine.Light;

namespace Bastion.Client
{
    /// <summary>
    /// The map by night (0.10.1's night.ts): the warm light of what still works. Every street light's head glows (UI's
    /// GroundView.Lamps), a few wrecks' headlights are left on (MapBuilder), and a fixed pool of real lights goes where it
    /// counts: the Bengalas and fires burning first (the engine's lights that last a few turns), then the street lights
    /// nearest the point the camera looks at. The lit windows are the panes' (Bastion/Building, _Night). Hidden by day.
    /// three's point lights in tiles and over π become Unity's in metres.
    /// </summary>
    public sealed class NightLights : MonoBehaviour, IShotStep
    {
        public static NightLights Current { get; private set; }

        /// <summary>Where a lamp shines from (a street light's head) or a car's front, which way it faces, how much stronger it is (m).</summary>
        public struct Glow
        {
            public Vector3 at;
            public Vector3 facing;
            public float power;
        }

        /// <summary>Real lights, wherever they're most needed (night.ts REAL_LIGHTS).</summary>
        const int RealLights = 6;
        /// <summary>three's colours (linear).</summary>
        static readonly Color Lamp = new Color(1.0f, 0.62f, 0.3f), LampHead = new Color(2.2f, 1.5f, 0.9f), Headlight = new Color(2.0f, 1.9f, 1.6f);
        static readonly Color Flare = new Color(1.0f, 0.1f, 0.08f), Fire = new Color(1.0f, 0.5f, 0.15f);
        /// <summary>How strong each light is (Arturo 17/10: «la luz deslumbra mucho de noche»), and how far a street light reaches (tiles).</summary>
        const float LampLight = 9, BurningLight = 16, LampReach = 7;
        /// <summary>Seconds a Bengala's or a fire's light takes to come up or die down.</summary>
        const float Fade = 0.5f;
        /// <summary>three's point light (candela, 1/d² in tiles, over π) as URP's (in metres): times 1.5² over π.</summary>
        static readonly float Unity = MapTile * MapTile / Mathf.PI;
        const float MapTile = Map.MapSpace.Tile;

        readonly List<Glow> lamps = new List<Glow>();
        readonly List<GameObject> glows = new List<GameObject>();
        Light[] pool;
        readonly Dictionary<string, (Vector3 at, Color colour, float radius, float level, bool flare)> burning = new Dictionary<string, (Vector3, Color, float, float, bool)>();
        float time;
        Material glowMaterial;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (glowMaterial != null) DestroyImmediate(glowMaterial);
        }

        /// <summary>The map's street lights and headlights left on (Unity's metres), made again.</summary>
        public void Show(IReadOnlyList<(Vector3 at, Vector3 facing)> streetLights, IReadOnlyList<Glow> headlights)
        {
            Current = this;
            lamps.Clear();
            foreach (var (at, facing) in streetLights ?? new List<(Vector3, Vector3)>()) lamps.Add(new Glow { at = at, facing = facing, power = 1 });
            foreach (var o in glows) DestroyNow(o);
            glows.Clear();
            // The lamps' heads and the headlights (HDR, for the bloom), boxes in one mesh each.
            var heads = new List<(Vector3 centre, Vector3 size)>();
            foreach (var l in lamps) heads.Add((l.at, new Vector3(0.32f, 0.08f, 0.2f) * MapTile));
            var fronts = new List<(Vector3 centre, Vector3 size)>();
            foreach (var h in headlights ?? new List<Glow>())
            {
                // Across the car's nose, either side (night.ts: (-dz, dx) a quarter of a tile out).
                var side = new Vector3(h.facing.z, 0, -h.facing.x) * (0.24f * MapTile);
                foreach (int s in new[] { -1, 1 }) fronts.Add((h.at + side * s, new Vector3(0.1f, 0.07f, 0.1f) * MapTile));
            }
            glows.Add(Boxes("Farolas", heads, LampHead));
            glows.Add(Boxes("Faros", fronts, Headlight));
            glows.RemoveAll(o => o == null);
            // The map made again under the same root takes its children with it (MapView.Build): the pool too.
            if (pool != null && pool.Any(l => l == null)) pool = null;
            if (pool == null)
            {
                pool = new Light[RealLights];
                for (int k = 0; k < RealLights; k++)
                {
                    pool[k] = new GameObject($"Luz {k}").AddComponent<Light>();
                    pool[k].type = LightType.Point;
                    pool[k].shadows = LightShadows.None;
                    pool[k].transform.SetParent(transform, false);
                }
            }
            Step(0);
            Debug.Log($"[night] {lamps.Count} street lights, {headlights?.Count ?? 0} headlights; lights on: {string.Join(", ", pool.Where(l => l.enabled).Select(l => $"{l.transform.position} {l.intensity:0.##} {l.range:0.#}m"))}");
        }

        GameObject Boxes(string name, List<(Vector3 centre, Vector3 size)> boxes, Color colour)
        {
            if (boxes.Count == 0) return null;
            if (glowMaterial == null)
            {
                var shader = Resources.Load<Effects.FxArt>("FxArt")?.unlit ?? Shader.Find("Bastion/FxUnlit");
                glowMaterial = new Material(shader) { name = "Brillo de noche", renderQueue = (int)RenderQueue.AlphaTest };
                glowMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
                glowMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
                glowMaterial.SetFloat("_ZWrite", 1);
                glowMaterial.SetFloat("_Cull", (float)CullMode.Back);
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var unit = cube.GetComponent<MeshFilter>().sharedMesh;
            DestroyNow(cube);
            var parts = boxes.Select(b => new CombineInstance { mesh = unit, transform = Matrix4x4.TRS(b.centre, Quaternion.identity, b.size) }).ToArray();
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts, true, true);
            var o = new GameObject(name);
            o.transform.SetParent(transform, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            // A material's colour is taken as sRGB: three's linear HDR values given as such.
            block.SetColor("_BaseColor", colour.gamma);
            r.SetPropertyBlock(block);
            return o;
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        public void ShotStep(float dt) => Step(dt);

        /// <summary>Fades and flicker, and the real lights put where they count.</summary>
        void Step(float dt)
        {
            bool night = Mood.Current != null && Mood.Current.Now == Mood.Night;
            foreach (var o in glows) if (o != null && o.activeSelf != night) o.SetActive(night);
            // (Gone with the map made again, till Show makes them anew.)
            if (pool == null || pool.Any(l => l == null)) return;
            if (!night)
            {
                foreach (var l in pool) l.enabled = false;
                return;
            }
            time += dt;
            // The Bengalas and fires burning now (the engine's lights with turns): new ones come up, gone ones die down.
            var s = UnitViews.Current?.State;
            var now = new HashSet<string>();
            if (s?.Lights != null)
                foreach (var l in s.Lights)
                {
                    if (l.Turns == null) continue;
                    now.Add(l.Id);
                    if (burning.ContainsKey(l.Id)) continue;
                    bool flare = l.Radius >= 3;
                    var at = Map.MapSpace.TileToWorld(s, l.Tile) + Vector3.up * (flare ? 1.4f : 0.35f) * MapTile;
                    burning[l.Id] = (at, flare ? Flare : Fire, (float)l.Radius, 0, flare);
                }
            foreach (string id in burning.Keys.ToList())
            {
                var b = burning[id];
                b.level = Mathf.Clamp01(b.level + (now.Contains(id) ? dt : -dt) / Fade);
                if (!now.Contains(id) && b.level <= 0) burning.Remove(id);
                else burning[id] = b;
            }
            int n = 0;
            foreach (var b in burning.Values)
            {
                if (n >= pool.Length) break;
                var light = pool[n++];
                float flicker = b.flare ? 0.9f + 0.1f * Mathf.Sin(time * 23) : 0.75f + 0.25f * Mathf.Sin(time * 11) * Mathf.Sin(time * 7.3f);
                light.enabled = true;
                light.color = b.colour.gamma;
                light.intensity = BurningLight * b.level * flicker * Unity;
                light.range = b.radius * 2.6f * MapTile;
                // A fire's light over its flames, not in them.
                light.transform.position = b.flare ? b.at : b.at + Vector3.up * 0.55f * MapTile;
            }
            // Then the street lights nearest the point the camera looks at.
            var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            var focus = Vector3.zero;
            if (camera != null)
            {
                var ray = camera.transform.forward;
                float t = ray.y < -0.01f ? -camera.transform.position.y / ray.y : 0;
                focus = camera.transform.position + ray * t;
            }
            var nearest = Enumerable.Range(0, lamps.Count).OrderBy(i => (lamps[i].at.x - focus.x) * (lamps[i].at.x - focus.x) + (lamps[i].at.z - focus.z) * (lamps[i].at.z - focus.z)).Take(pool.Length - n).ToList();
            foreach (int i in nearest)
            {
                var light = pool[n++];
                var l = lamps[i];
                light.enabled = true;
                light.color = Lamp.gamma;
                light.intensity = LampLight * l.power * Unity;
                light.range = LampReach * Mathf.Sqrt(l.power) * MapTile;
                // Well under the lamp's own head: a point light right at a surface burnt it white.
                light.transform.position = l.at - Vector3.up * 0.6f * MapTile;
            }
            for (; n < pool.Length; n++) pool[n].enabled = false;
        }

        static void DestroyNow(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
