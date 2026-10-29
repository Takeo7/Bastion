using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The snow on a winter map (0.10.1's snow.ts SnowCover; Mood.Snowy): on what faces up, out of doors, a cap of Alpine's
    /// snow (from straight up to about 37° full, gone past 68°: snow.ts's smoothstep of the normal's height), over the
    /// map's models, its trees and the outline, not the ground (it has its own) nor the figures; and small banks at the
    /// foot of the cover (drifts). The cap is URP's decals («Nieve», SnowFeatureMake: DBuffer, the albedo alone, before
    /// the light, as 0.10.1 mixed it into the diffuse colour), one projector over the whole map, on what carries rendering
    /// layer Layer (Mark): what stands under a building's roof doesn't. The decals are on while a snowy map is shown.
    /// </summary>
    [ExecuteAlways]
    public sealed class SnowCover : MonoBehaviour
    {
        /// <summary>The rendering layer what gets snow is on (the project's Light Layer 7, unused).</summary>
        public const int Layer = 7;
        public const uint Mask = 1u << Layer;
        /// <summary>The banks' model (snow.ts 'drift': Alpine's mound), for the game's catalog.</summary>
        public const string Mound = "alpine:Environment/SM_Env_Snow_Mound_01";
        const float Tile = MapSpace.Tile;

        static int shown;
        bool counted;

        /// <summary>The snow of `s` under `root` (its map's), the buildings as `plan` reads them; the banks from `prefabOf`.</summary>
        public static SnowCover Make(Transform root, GameState s, BuildingPlan plan, Func<string, GameObject> prefabOf)
        {
            var o = new GameObject("Nieve");
            o.transform.SetParent(root, false);
            var snow = o.AddComponent<SnowCover>();
            snow.Build(s, plan, prefabOf);
            return snow;
        }

        void Build(GameState s, BuildingPlan plan, Func<string, GameObject> prefabOf)
        {
            float w = s.Width * Tile, h = s.Height * Tile;
            // Out past the edge as far as the pinar and the outline go.
            const float margin = 80;
            var projector = new GameObject("Proyector").AddComponent<DecalProjector>();
            projector.transform.SetParent(transform, false);
            // Projecting down from above: URP fades a decal by how far a surface's normal turns from facing it (as
            // ((1 - cos) / 2)², on 0 to 180), so what faces up is at 0.
            projector.transform.localPosition = new Vector3(w / 2, 40, -h / 2);
            projector.transform.localRotation = Quaternion.Euler(90, 0, 0);
            projector.size = new Vector3(w + margin, h + margin, 80);
            projector.pivot = new Vector3(0, 0, 40);
            projector.material = Resources.Load<Material>("SnowDecal");
            // snow.ts: the texture every 3 tiles.
            projector.uvScale = new Vector2((w + margin) / (3 * Tile), (h + margin) / (3 * Tile));
            projector.fadeFactor = 0.96f;
            // snow.ts smoothstep(0.38, 0.8, normal.y): whole up to 37° from straight up, none past 68°; on URP's scale,
            // ((1 - cos θ) / 2)² × 180.
            projector.startAngleFade = Angle(0.8f);
            projector.endAngleFade = Angle(0.38f);
            projector.renderingLayerMask = Mask;
            projector.drawDistance = 1000;
            Banks(s, plan, prefabOf);
        }

        /// <summary>URP's angle fade for a surface whose normal is `up` high (the cosine of its angle from straight up).</summary>
        static float Angle(float up) => (1 - up) / 2 * ((1 - up) / 2) * 180;

        /// <summary>
        /// Off as the game starts: the app comes with the decals on, as it was built (SnowBuild: else URP strips their
        /// shaders), and on a map without snow they would cost for nothing.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Starting()
        {
            shown = 0;
            Decals(false);
        }

        void OnEnable()
        {
            if (counted) return;
            counted = true;
            if (shown++ == 0) Decals(true);
        }

        void OnDisable()
        {
            if (!counted) return;
            counted = false;
            if (--shown == 0) Decals(false);
        }

        /// <summary>The decals of every quality level's renderers on or off («Nieve»: SnowFeatureMake).</summary>
        internal static void Decals(bool on)
        {
            for (int q = 0; q < QualitySettings.count; q++)
            {
                if (!(QualitySettings.GetRenderPipelineAssetAt(q) is UniversalRenderPipelineAsset urp)) continue;
                foreach (var data in urp.rendererDataList)
                    if (data != null)
                        foreach (var f in data.rendererFeatures)
                            if (f != null && f.name == "Nieve") f.SetActive(on);
            }
        }

        /// <summary>
        /// What under `map` (the map's objects) takes the snow: on Layer, all of it but the ground itself (its cover takes it)
        /// and what stands inside a building that still has its roof (snow.ts shelter; the building's own pieces are
        /// Bastion/Building's, which takes no decals).
        /// </summary>
        public static void Mark(Transform map, BuildingPlan plan, GameState s, Transform ground)
        {
            foreach (var r in map.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.transform == ground) continue;
                var p = r.bounds.center;
                int x = Mathf.FloorToInt(p.x / Tile), y = Mathf.FloorToInt(-p.z / Tile);
                bool sheltered = false;
                if (plan != null && x >= 0 && y >= 0 && x < s.Width && y < s.Height)
                {
                    int i = y * s.Width + x;
                    int b = plan.Owner[i];
                    sheltered = b >= 0 && plan.Inside[i] != 0 && b < plan.Buildings.Count && plan.Buildings[b].kind != BuildingKinds.Ruin
                        && p.y < (plan.Buildings[b].@base + plan.Buildings[b].storeys * BuildingPlan.Storey) * Tile;
                }
                r.renderingLayerMask = sheltered ? r.renderingLayerMask & ~Mask : r.renderingLayerMask | Mask;
            }
        }

        /// <summary>
        /// Small banks within the cover out of doors (snow.ts drifts): on every third tile of `high` cover, off any building,
        /// on the ground; a mound 0.92 tiles across and 0.23 high, each turned its own way; no shadow.
        /// </summary>
        void Banks(GameState s, BuildingPlan plan, Func<string, GameObject> prefabOf)
        {
            var prefab = prefabOf(Mound);
            var material = Resources.Load<Material>("Snowbank");
            if (prefab == null) return;
            var probe = Instantiate(prefab);
            var b = Bounds(probe.transform);
            DestroyNow(probe);
            float across = Mathf.Max(b.size.x, b.size.z, 1e-3f);
            var scale = new Vector3(0.92f * Tile / across, 0.23f * Tile / Mathf.Max(b.size.y, 1e-3f), 0.92f * Tile / across);
            var root = new GameObject("Montones").transform;
            root.SetParent(transform, false);
            int made = 0;
            for (int i = 0; i < s.Tiles.Count; i++)
            {
                if (s.Tiles[i] != TileKinds.High || (plan != null && i < plan.Owner.Length && plan.Owner[i] >= 0) || (i < s.Elev.Count ? s.Elev[i] : 0) != 0 || i % 3 != 0) continue;
                int x = i % s.Width, z = i / s.Width;
                var holder = new GameObject("Montón").transform;
                holder.SetParent(root, false);
                holder.localPosition = new Vector3((x + 0.5f) * Tile, 0.006f * Tile, -(z + 0.5f) * Tile);
                // three's counter-clockwise turn, the other way round here.
                holder.localRotation = Quaternion.Euler(0, -(float)((i * 2.39996) % (2 * Math.PI)) * Mathf.Rad2Deg, 0);
                var mound = Instantiate(prefab, holder, false);
                mound.transform.localScale = Vector3.Scale(mound.transform.localScale, scale);
                mound.transform.localPosition = new Vector3(-b.center.x * scale.x, -b.min.y * scale.y, -b.center.z * scale.z);
                foreach (var r in mound.GetComponentsInChildren<Renderer>())
                {
                    if (material != null) r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                made++;
            }
            Debug.Log($"[snow] {made} banks");
        }

        static Bounds Bounds(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            b.center -= root.position;
            return b;
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
