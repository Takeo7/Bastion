using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A canopy over each filling station's pumps (mapView.ts placeCanopies and fadeCanopies; UI's, Gameplay 26/10): the
    /// pumps (decor tank, low, on the ground) within three tiles of each other are a station, and its canopy stands over
    /// their middle, along the longer way, as long as the row and a lane at each end (never more than three quarters of the
    /// model); burnt now and then. While what the player looks at (the selected soldier, the target, the tile under the
    /// cursor: the cutaway's focus) is under one, it fades to 0.15 in 0.3 s, as a building's roof does: the camera never
    /// hides the fight. Each its own material (Bastion/Canopy, the cutaway's dither), the atlas of its model.
    /// </summary>
    public sealed class PumpCanopies : MonoBehaviour
    {
        /// <summary>The model's size (tiles at scale 1), the scale's bounds, how faded one gets and how fast.</summary>
        const float ModelLong = 8.9f, ModelWide = 4.7f, Least = 0.55f, Most = 0.75f, Faded = 0.15f, FadeTime = 0.3f;

        sealed class Canopy
        {
            public Material Material;
            /// <summary>Its roof's bounds, tiles (x east, y south).</summary>
            public float x0, y0, x1, y1;
            public float Shown = 1;
        }

        readonly List<Canopy> canopies = new List<Canopy>();
        static readonly int ShownId = Shader.PropertyToID("_Shown");

        /// <summary>The canopies of `state`'s stations under `parent`, as a whole that fades them; null with no station.</summary>
        public static PumpCanopies Build(GameState state, PropParts props, Transform parent)
        {
            var decor = state.Decor;
            var pumps = new List<int>();
            for (int i = 0; i < state.Tiles.Count; i++)
                if (state.Tiles[i] == TileKinds.Low && decor != null && i < decor.Count && Bastion.Engine.Decor.DecorOf(decor[i])?.Kind == DecorKinds.Tank && (i < state.Elev.Count ? state.Elev[i] : 0) == 0)
                    pumps.Add(i);
            if (pumps.Count == 0) return null;
            var shader = Resources.Load<Shader>("Canopy");
            if (shader == null)
            {
                Debug.LogError("PumpCanopies: Resources/Canopy.shader missing");
                return null;
            }
            var holder = new GameObject("Techos de surtidores");
            holder.transform.SetParent(parent, false);
            var self = holder.AddComponent<PumpCanopies>();
            int w = state.Width;
            var left = new HashSet<int>(pumps);
            while (left.Count > 0)
            {
                // A station: the pumps within three tiles (either way) of one already in it.
                int first = left.First();
                var station = new List<int> { first };
                left.Remove(first);
                for (int n = 0; n < station.Count; n++)
                {
                    int ax = station[n] % w, ay = station[n] / w;
                    foreach (int j in left.ToList())
                    {
                        if (Mathf.Max(Mathf.Abs(j % w - ax), Mathf.Abs(j / w - ay)) > 3) continue;
                        left.Remove(j);
                        station.Add(j);
                    }
                }
                int x0 = station.Min(i => i % w), x1 = station.Max(i => i % w) + 1;
                int z0 = station.Min(i => i / w), z1 = station.Max(i => i / w) + 1;
                bool alongX = x1 - x0 > z1 - z0;
                float scale = Mathf.Clamp((Mathf.Max(x1 - x0, z1 - z0) + 2) / ModelLong, Least, Most);
                string id = MapArt.Hash(x0, z0, 77) < 0.3f ? "pumpCanopyBurnt" : "pumpCanopy";
                var parts = props.Of(id);
                if (parts.Count == 0) continue;
                float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f;
                // 0.10.1's quarter turn counter-clockwise (y south), Unity's the other way round. Its parts merged with
                // their own fitting (PropParts), then given the canopy's material, the atlas of its model.
                var at = Matrix4x4.TRS(new Vector3(cx * MapSpace.Tile, 0, -cz * MapSpace.Tile), Quaternion.Euler(0, alongX ? -90 : 0, 0), Vector3.one * scale);
                var canopy = props.Merge(holder.transform, id, new[] { (id, at, true) });
                var material = new Material(shader) { name = "Techo de surtidores" };
                var atlas = parts.Select(p => p.Material.HasProperty("_Albedo_Map") ? p.Material.GetTexture("_Albedo_Map") : p.Material.mainTexture).FirstOrDefault(t => t != null);
                if (atlas != null) material.SetTexture("_BaseMap", atlas);
                foreach (var renderer in canopy.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
                float halfW = ModelWide * scale / 2, halfL = ModelLong * scale / 2;
                float hx = alongX ? halfL : halfW, hz = alongX ? halfW : halfL;
                self.canopies.Add(new Canopy { Material = material, x0 = cx - hx, y0 = cz - hz, x1 = cx + hx, y1 = cz + hz });
            }
            return self;
        }

        void Update()
        {
            if (canopies.Count == 0) return;
            // What the player looks at, as the cutaway has it (its focus, the units as UnitViews shows them).
            var points = new List<Vector2>(3);
            var cutaway = Cutaway.Current;
            var units = UnitViews.Current?.State?.Units;
            if (cutaway != null)
            {
                var focus = cutaway.Focus;
                foreach (var id in new[] { focus.selected, focus.target })
                {
                    var u = id != null ? units?.FirstOrDefault(x => x.Id == id) : null;
                    if (u?.Pos != null) points.Add(new Vector2(u.Pos.X + 0.5f, u.Pos.Y + 0.5f));
                }
                if (focus.hover != null) points.Add(new Vector2(focus.hover.X + 0.5f, focus.hover.Y + 0.5f));
            }
            float dt = Time.unscaledDeltaTime;
            foreach (var c in canopies)
            {
                bool under = points.Any(q => q.x > c.x0 - 0.5f && q.x < c.x1 + 0.5f && q.y > c.y0 - 0.5f && q.y < c.y1 + 0.5f);
                float to = under ? Faded : 1;
                if (c.Shown == to) continue;
                c.Shown = to > c.Shown ? Mathf.Min(to, c.Shown + dt / FadeTime) : Mathf.Max(to, c.Shown - dt / FadeTime);
                c.Material.SetFloat(ShownId, c.Shown);
            }
        }

        void OnDestroy()
        {
            foreach (var c in canopies) if (c.Material != null) Destroy(c.Material);
        }
    }
}
