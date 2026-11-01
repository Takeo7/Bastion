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
    /// the start and burn to the end.
    /// </summary>
    public sealed class FireView : MonoBehaviour
    {
        /// <summary>How high the big flames stand over what burns (m): a car's body, bins and drums, a heap on the ground.</summary>
        const float OverCar = 0.55f, OverBins = 0.45f, OverHeap = 0.1f;
        /// <summary>The soot under the fire: its colour and how dark.</summary>
        static readonly Color Soot = new Color(0.04f, 0.035f, 0.03f, 0.6f);

        static Material soot;
        static Mesh quad;

        public static FireView Make(Transform parent, GameState s)
        {
            var view = new GameObject("Fuego").AddComponent<FireView>();
            view.transform.SetParent(parent, false);
            var art = Resources.Load<FxArt>("FxArt");
            if (art == null || s.Fires == null) return view;
            foreach (var fire in s.Fires) view.Burn(s, fire, art);
            Debug.Log($"[fuego] {s.Fires.Count} fires, {s.Fires.Sum(f => f.Tiles.Count)} tiles burning");
            return view;
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

        /// <summary>One of Particle FX's effects at `at`, turned and scaled (its particles with it), playing.</summary>
        static void Effect(GameObject prefab, Transform parent, Vector3 at, float yaw, float scale)
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
                // (Burning already as the map is shown: some seconds of it run before.)
                if (!Application.isPlaying) ps.Simulate(3, false, true);
                else ps.Play();
            }
        }

        /// <summary>The ground scorched round `at`: a dark blot `size` tiles across, lying on the ground.</summary>
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
