using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Effects;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// What burns on a map all the mission long (GameState.fires, Arturo 1/11; the rules' Burning): on what burns big
    /// flames, embers and a column of black smoke (over a car's body, a bin's or drums' top, a heap); on each tile of the
    /// ground on fire small flames, each its own size and turn; the ground under them scorched. Particle FX's (FxArt).
    /// By night its light is NightLights' (the fire's own in GameState.lights). Made with the map: the fires are there from
    /// the start and burn to the end. A molotov's (Arturo, 1/11: Fire.Turns) lit and put out as the battle goes (Light,
    /// Out: EventPlayer's FireLitEvent and FireOutEvent), a pool of petrol burning: low flames all over its tiles, the
    /// ground under them aglow, the ground scorched (left when it goes out), nothing over what burns.
    /// </summary>
    public sealed class FireView : MonoBehaviour
    {
        /// <summary>The map's in view (the last made).</summary>
        public static FireView Current { get; private set; }

        /// <summary>A molotov's fire growing to its size, and dying down once out (s).</summary>
        const float Grows = 0.6f, DiesDown = 3;
        /// <summary>
        /// A pool's flames (FxArt.fireLick, Particle FX's small fire of flame shapes) against the middling fire's scale they
        /// had, so they stand as high (the 3D review's L-05, 10/11: FX_Fire_01's balls rising read from above as yellow
        /// cylinders with white tops).
        /// </summary>
        const float LickScale = 18;
        /// <summary>
        /// And of their flames a second: they stand on one spot each, and its own (8–10 a second, a second each) summed up
        /// white, light added to light.
        /// </summary>
        const float LickRate = 0.4f;
        /// <summary>How high the big flames stand over what burns (m): a car's body, bins and drums, a heap on the ground.</summary>
        const float OverCar = 0.55f, OverBins = 0.45f, OverHeap = 0.1f;
        /// <summary>The soot under the fire: its colour and how dark.</summary>
        static readonly Color Soot = new Color(0.04f, 0.035f, 0.03f, 0.6f);

        static Material soot;
        static Mesh quad;
        static Texture2D embers;
        /// <summary>
        /// A molotov's burning ground under its flames (Arturo's #8 of 2/11: the fire over the whole tile, not a little of
        /// it): its colour (over 1, for the bloom) and how much it flickers.
        /// </summary>
        static readonly Color Ablaze = new Color(2.2f, 0.75f, 0.18f, 0.5f);
        const float Flicker = 0.35f;
        /// <summary>
        /// The burning ground of each pool, by its fire: its own flicker, since when it dies down (Out; null, burning), and
        /// when it was lit (it comes up over Grows, L-05).
        /// </summary>
        readonly Dictionary<string, (Material material, float seed, float? since, float born)> glows = new Dictionary<string, (Material, float, float?, float)>();

        void Update()
        {
            foreach (var id in glows.Keys.ToList())
            {
                var (material, seed, since, born) = glows[id];
                if (material == null)
                {
                    glows.Remove(id);
                    continue;
                }
                float n = Mathf.PerlinNoise(Time.time * 2.6f, seed);
                float left = since is float t ? Mathf.Clamp01(1 - (Time.time - t) / DiesDown) : 1;
                // (Lit up over Grows, as its flames grow: grown with them from a tenth, its blots came together in the middle,
                // their light summed, as white discs spreading out: L-05.)
                left *= Mathf.Clamp01((Time.time - born) / Grows);
                material.SetColor("_BaseColor", new Color(Ablaze.r, Ablaze.g, Ablaze.b, Ablaze.a * (1 - Flicker + Flicker * 2 * n) * left));
            }
        }

        public static FireView Make(Transform parent, GameState s)
        {
            var view = new GameObject("Fuego").AddComponent<FireView>();
            view.transform.SetParent(parent, false);
            Current = view;
            var art = Resources.Load<FxArt>("FxArt");
            if (art == null || s.Fires == null || s.Fires.Count == 0) return view;
            foreach (var fire in s.Fires)
            {
                if (fire.Turns != null) view.Pool(s, fire, art, false);
                else view.Burn(s, fire, art);
            }
            Debug.Log($"[fuego] {s.Fires.Count} fires, {s.Fires.Sum(f => f.Tiles.Count)} tiles burning");
            return view;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>A fire lit now (a molotov's, FireLitEvent): growing quickly; nothing if it burns already.</summary>
        public void Light(GameState s, Fire fire)
        {
            var art = Resources.Load<FxArt>("FxArt");
            if (art == null || fire == null || transform.Find(fire.Id) != null) return;
            if (fire.Turns != null) Pool(s, fire, art, Application.isPlaying);
            else Burn(s, fire, art);
        }

        /// <summary>A fire put out (FireOutEvent): its flames and smoke no longer coming, gone once they die down; its scorch stays.</summary>
        public void Out(string id)
        {
            var holder = id != null ? transform.Find(id) : null;
            if (holder == null) return;
            holder.name = id + " (apagado)";
            if (glows.TryGetValue(id, out var glow)) glows[id] = (glow.material, glow.seed, Time.time, glow.born);
            foreach (Transform part in holder.Cast<Transform>().ToList())
            {
                if (part.name == "hollín") continue;
                foreach (var ps in part.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (Application.isPlaying) Destroy(part.gameObject, DiesDown);
                else DestroyImmediate(part.gameObject);
            }
        }

        /// <summary>
        /// A molotov's fire: a pool of petrol burning over its tiles (the 3×3 one can step on), low flames on each, embers
        /// over the middle, the ground scorched under it all; `grow`, its flames from where the bottle broke out to their
        /// size, and the burning ground coming up with them (Grows). No smoke of its own (the 3D review's L-05, with Arturo's
        /// C for the smoke, 10/11: Particle FX's black smoke was a column of opaque dark hexagons; the bottle breaking puffs
        /// its own, see-through).
        /// </summary>
        void Pool(GameState s, Fire fire, FxArt art, bool grow)
        {
            int w = s.Width;
            int sx = fire.Source % w, sy = fire.Source / w;
            var at = MapSpace.World(sx, sy);
            var holder = new GameObject(fire.Id).transform;
            holder.SetParent(transform, false);
            holder.position = at;
            // What grows (Grow): the flames and embers alone, not the ground under them (L-05).
            var flames = new GameObject("llamas").transform;
            flames.SetParent(holder, false);
            Effect(art.embers, flames, at + Vector3.up * 0.1f, 0, 0.7f);
            Scorch(holder, at, 3.4f, MapArt.Hash(sx, sy, 440) * 360);
            var glow = Glow(holder, fire.Id, grow);
            var licks = art.fireLick != null ? art.fireLick : art.fire;
            float lick = art.fireLick != null ? LickScale : 1;
            foreach (int i in fire.Tiles)
            {
                int x = i % w, y = i / w;
                float r(int salt) => MapArt.Hash(x, y, 450 + salt);
                // The ground of the tile aglow, a soft blot over it and into its neighbours', so the pool reads whole.
                if (glow != null) Patch(holder, glow, MapSpace.World(x, y), 1.6f, r(9) * 360);
                // (Four flames a tile, low, one in each quarter, so they cover it: a pool's, not a car's; the middle's a
                // little bigger. The middling fire made small: the small one's flames are a few centimetres, nothing from the
                // game's camera.)
                for (int k = 0; k < 4; k++)
                {
                    var quarter = new Vector3((k % 2) - 0.5f, 0, (k / 2) - 0.5f) * 0.5f + new Vector3(r(k * 3) - 0.5f, 0, r(k * 3 + 1) - 0.5f) * 0.25f;
                    var spot = MapSpace.World(x, y) + quarter * MapSpace.Tile;
                    Effect(licks, flames, spot, r(k * 3 + 2) * 360, ((i == fire.Source && k == 0 ? 0.62f : 0.42f) + 0.16f * r(13 + k)) * lick, art.fireLick != null ? LickRate : 1);
                }
            }
            if (grow) StartCoroutine(Grow(flames));
        }

        /// <summary>From a tenth of its size to its own over Grows, eased out.</summary>
        static System.Collections.IEnumerator Grow(Transform holder)
        {
            for (float t = 0; t < Grows && holder != null; t += Time.deltaTime)
            {
                float k = 1 - (1 - t / Grows) * (1 - t / Grows);
                holder.localScale = Vector3.one * Mathf.Lerp(0.1f, 1, k);
                yield return null;
            }
            if (holder != null) holder.localScale = Vector3.one;
        }

        void Burn(GameState s, Fire fire, FxArt art)
        {
            int w = s.Width;
            int sx = fire.Source % w, sy = fire.Source / w;
            float R(int salt) => MapArt.Hash(sx, sy, 400 + salt);
            var at = MapSpace.World(sx, sy);
            // What burns: a car (its body, half a tile back from its nose), bins or drums, or a heap on the ground.
            var thing = s.Decor != null && fire.Source < s.Decor.Count ? Bastion.Engine.Decor.DecorOf(s.Decor[fire.Source]) : null;
            float over = OverHeap;
            if (thing?.Kind == "carFront")
            {
                var d = Bastion.Engine.Grid.Dirs4[thing.Facing];
                at -= new Vector3(d.X, 0, -d.Y) * 0.5f * MapSpace.Tile;
                over = OverCar;
            }
            else if (thing != null) over = OverBins;
            var holder = new GameObject(fire.Id).transform;
            holder.SetParent(transform, false);
            // (A car's fire the biggest: the whole body burning.)
            Effect(art.fireBig, holder, at + Vector3.up * over, R(0) * 360, (over == OverCar ? 1.3f : 0.9f) + 0.2f * R(1));
            Effect(art.embers, holder, at + Vector3.up * over, 0, 1);
            Effect(art.smoke, holder, at + Vector3.up * (over + 0.8f), 0, 1.2f);
            Scorch(holder, at, 1.6f + 0.4f * R(2), R(3) * 360);
            foreach (int i in fire.Tiles)
            {
                int x = i % w, y = i / w;
                if (i == fire.Source) continue;
                float r(int salt) => MapArt.Hash(x, y, 420 + salt);
                var spot = MapSpace.World(x, y) + new Vector3(r(0) - 0.5f, 0, r(1) - 0.5f) * 0.5f * MapSpace.Tile;
                Effect(r(2) < 0.3f ? art.fire : art.fireSmall, holder, spot, r(3) * 360, 0.7f + 0.5f * r(4));
                Scorch(holder, MapSpace.World(x, y), 1.1f + 0.4f * r(5), r(6) * 360);
            }
        }

        /// <summary>One of Particle FX's effects at `at`, turned and scaled (its particles with it), playing; `rate`, of its particles a second.</summary>
        static void Effect(GameObject prefab, Transform parent, Vector3 at, float yaw, float scale, float rate = 1)
        {
            if (prefab == null) return;
            var made = Instantiate(prefab, parent, false);
            made.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            made.transform.localScale = Vector3.one * scale;
            foreach (var ps in made.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = true;
                if (rate != 1)
                {
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier *= rate;
                }
                // (Burning already as the map is shown: some seconds of it run before.)
                if (!Application.isPlaying) ps.Simulate(3, false, true);
                else ps.Play();
            }
        }

        /// <summary>The ground scorched round `at`: a dark blot `size` tiles across, lying on the ground.</summary>
        /// <summary>A pool's burning ground (Ablaze), its own copy for its flicker (Update), gone with the pool; null without the kit.</summary>
        Material Glow(Transform holder, string id, bool grow)
        {
            var material = KitMaterials.New(KitMaterials.SeeThrough, "Fuego en el suelo " + id);
            if (material == null) return null;
            // (Growing, unlit till Update brings it up: else whole for its first frame.)
            material.SetColor("_BaseColor", grow ? new Color(Ablaze.r, Ablaze.g, Ablaze.b, 0) : Ablaze);
            material.mainTexture = embers ??= Embers();
            material.renderQueue = 2991;
            glows[id] = (material, MapArt.Hash(id.Length, id.GetHashCode() & 1023, 460) * 100, null, grow ? Time.time : Time.time - Grows);
            holder.gameObject.AddComponent<Owned>().material = material;
            return material;
        }

        /// <summary>A pool's copy of its material, destroyed with it.</summary>
        sealed class Owned : MonoBehaviour
        {
            public Material material;

            void OnDestroy()
            {
                if (material == null) return;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }

        /// <summary>A flat blot of `material` on the ground at `at`, `size` tiles across, turned by `yaw`.</summary>
        static void Patch(Transform parent, Material material, Vector3 at, float size, float yaw)
        {
            quad ??= Quad();
            var blot = new GameObject("brasas");
            blot.transform.SetParent(parent, false);
            blot.transform.SetPositionAndRotation(at + Vector3.up * 0.03f, Quaternion.Euler(0, yaw, 0));
            blot.transform.localScale = Vector3.one * size * MapSpace.Tile;
            blot.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = blot.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Scorch(Transform parent, Vector3 at, float size, float yaw)
        {
            if (soot == null)
            {
                soot = KitMaterials.New(KitMaterials.SeeThrough, "Hollín");
                if (soot == null) return;
                soot.SetColor("_BaseColor", Soot);
                soot.mainTexture = Blot();
                soot.renderQueue = 2990;
            }
            quad ??= Quad();
            var blot = new GameObject("hollín");
            blot.transform.SetParent(parent, false);
            blot.transform.SetPositionAndRotation(at + Vector3.up * 0.02f, Quaternion.Euler(0, yaw, 0));
            blot.transform.localScale = Vector3.one * size * MapSpace.Tile;
            blot.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = blot.AddComponent<MeshRenderer>();
            r.sharedMaterial = soot;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A soft blot, darkest in the middle and ragged at its edge (alpha only): soot, or a wall's smudge round a car's hole.</summary>
        internal static Texture2D Blot()
        {
            const int size = 64;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Mancha" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f) / (size / 2f);
                    float ragged = 0.75f + 0.25f * Mathf.Sin(Mathf.Atan2(p.y, p.x) * 5 + 1.3f) * Mathf.Sin(Mathf.Atan2(p.y, p.x) * 3);
                    float a = Mathf.Clamp01(1 - p.magnitude / ragged);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
                }
            t.SetPixels32(pixels);
            t.Apply(true);
            return t;
        }

        /// <summary>A round, soft blot, even over most of it and a little uneven (alpha only): a pool's burning ground.</summary>
        static Texture2D Embers()
        {
            const int size = 64;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Brasas" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f) / (size / 2f);
                    float a = (1 - Mathf.SmoothStep(0.3f, 1, p.magnitude)) * (0.75f + 0.25f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            t.SetPixels32(pixels);
            t.Apply(true);
            return t;
        }

        /// <summary>A unit square lying flat (facing up), its middle at the origin.</summary>
        static Mesh Quad()
        {
            var m = new Mesh { name = "Mancha" };
            m.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
