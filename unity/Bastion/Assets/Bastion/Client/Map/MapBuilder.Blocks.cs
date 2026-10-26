using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The objects of before over several tiles (mapView.ts BLOCKS, blockOf, drawBlock; UI's, Gameplay 26/10): a decor kind
    /// on each tile of a block, drawn as one model over its middle while all of it stands as it was made: a lorry (in a
    /// barn, its old tractor) with its nose and a container with its doors the way the block faces, a water tower, a silo as
    /// wide as its block, a bench, a log pile along its length. Broken, or with no model in the project yet (Woodland's
    /// containers and lorries), its tiles go on as any other: the set of their kind.
    /// </summary>
    public sealed partial class MapBuilder
    {
        /// <summary>What each kind stands on as made (mapView.ts BLOCKS).</summary>
        static readonly Dictionary<string, string> BlockParts = new Dictionary<string, string>
        {
            ["truck"] = "high", ["container"] = "high", ["waterTower"] = "high", ["silo"] = "high", ["logPile"] = "low", ["bench"] = "low",
        };

        /// <summary>
        /// A block of before, not an object (mapView.ts thingOf: only an anchor and its parts are one; the blocks of a kind,
        /// which Decor.ObjectAt gives as one too, are drawn their own way, Block).
        /// </summary>
        static bool OldBlock(DecorObject thing, IList<int> decor) =>
            Bastion.Engine.Decor.BlockKinds.Contains(thing.Kind) && (thing.Tiles.Count < 2 || thing.Tiles[1] >= decor.Count || Bastion.Engine.Decor.DecorOf(decor[thing.Tiles[1]])?.Kind != "part");

        /// <summary>Tile i of storey k's block, drawn once from its anchor (the tile under its middle); false when it isn't one, or can't be.</summary>
        bool Block(int k, int i, DecorLook look, IList<string> tiles, IList<int> decor, HashSet<int> done)
        {
            if (!BlockParts.ContainsKey(look.Kind)) return false;
            int w = s.Width, h = s.Height;
            // The tiles of its kind joined to it (blockOf).
            var block = new List<int> { i };
            var seen = new HashSet<int> { i };
            for (int n = 0; n < block.Count; n++)
            {
                int tx = block[n] % w, ty = block[n] / w;
                foreach (var d in Bastion.Engine.Grid.Dirs4)
                {
                    int nx = tx + d.X, ny = ty + d.Y, j = ny * w + nx;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen.Contains(j) || j >= decor.Count) continue;
                    if (Bastion.Engine.Decor.DecorOf(decor[j])?.Kind != look.Kind) continue;
                    seen.Add(j);
                    block.Add(j);
                }
            }
            bool whole = block.All(t => t < tiles.Count && tiles[t] == BlockParts[look.Kind]);
            if (!whole) return false;
            int x0 = block.Min(t => t % w), y0 = block.Min(t => t / w);
            int bw = block.Max(t => t % w) - x0 + 1, bh = block.Max(t => t / w) - y0 + 1;
            int anchor = (y0 + bh / 2) * w + x0 + bw / 2;
            if (done.Contains(anchor)) return true;
            var drawn = DrawBlock(props[k], look.Kind, Bastion.Engine.Decor.DecorOf(decor[anchor])?.Facing ?? look.Facing, x0, y0, bw, bh, anchor, k * MapSpace.Storey);
            if (drawn == null) return false;
            done.Add(anchor);
            Own(drawn, k, block);
            return true;
        }

        /// <summary>A block's model over its middle (drawBlock); null if its model isn't in the project (it is noted missing).</summary>
        GameObject DrawBlock(Transform parent, string kind, int facing, int x0, int y0, int bw, int bh, int anchor, float floor)
        {
            float cx = x0 + bw / 2f, cz = y0 + bh / 2f;
            float r = MapArt.Hash(x0, y0, 31);
            // 0.10.1's turn, counter-clockwise from above in tiles with y south; Unity's is the other way round.
            float yaw = -((4 - facing) % 4) * 90;
            int length = Mathf.Max(bw, bh);
            string id;
            float quarter = Mathf.Floor(r * 4) * 90;
            var scale = Vector3.one;
            MapArt.Footprint fit = null;
            switch (kind)
            {
                case "truck":
                    // In a barn, its old tractor (playtest 18/10, #53); elsewhere a lorry's wreck. At its own length (a lorry
                    // of 2 × 6, a tractor of 2 × 3: docs/MAPAS.md, phase 1), the model fitted to it; one of before, as it was.
                    int owner = plan != null && anchor < plan.Owner.Length ? plan.Owner[anchor] : -1;
                    id = owner >= 0 && owner < plan.Buildings.Count && plan.Buildings[owner].kind == BuildingKinds.Barn ? "tractor" : Pick("truck", r);
                    if (length == (id == "tractor" ? 3 : 6)) fit = MapArt.FootprintOf(id);
                    break;
                case "container":
                    id = Pick("container", r);
                    break;
                case "waterTower":
                    id = "waterTower";
                    yaw = -quarter;
                    break;
                case "silo":
                    // Its ladder any way round; as wide as its block (the model fits 2: a silo of before).
                    id = Pick("silo", r);
                    scale = Vector3.one * (Mathf.Min(bw, bh) / 2f);
                    yaw = -Mathf.Floor(MapArt.Hash(x0, y0, 32) * 4) * 90;
                    break;
                case "bench":
                    // Along its two tiles, its seat the way it faces; a broken one now and then.
                    id = r < 0.3f ? "parkBenchBroken" : "parkBench";
                    break;
                case "logPile":
                    // Its logs (the model's z) along a long pile, narrowed to its tile; a square one whole. Half a survivor high or so.
                    id = "logPile";
                    if (bw != bh)
                    {
                        scale = new Vector3(0.62f, 0.75f, 1);
                        yaw = bw > bh ? -90 : 0;
                    }
                    else
                    {
                        scale = new Vector3(1, 0.75f, 1);
                        yaw = -quarter;
                    }
                    break;
                default:
                    return null;
            }
            if (!Modelled(id)) return null;
            var placed = Place(parent, id, new Vector3(cx * MapSpace.Tile, floor, -cz * MapSpace.Tile), yaw, fit);
            if (placed != null && scale != Vector3.one) placed.transform.localScale = scale;
            return placed;
        }

        /// <summary>One of a set's props by a pick in [0, 1) (mapView.ts pickOf).</summary>
        static string Pick(string set, float pick)
        {
            var ids = MapArt.Set(set);
            return ids[Mathf.Min(ids.Length - 1, Mathf.FloorToInt(pick * ids.Length))];
        }

        /// <summary>Whether prop `id` has its model in the project; not, it is noted missing (MapCatalogMake.Check lists it).</summary>
        bool Modelled(string id)
        {
            var art = MapArt.Prop(id);
            if (art != null && !string.IsNullOrEmpty(art.fbx) && prefabOf(MapArt.ModelKey(art)) != null) return true;
            Missing.Add(art == null || string.IsNullOrEmpty(art.fbx) ? id : $"{id} ({art.pack}/{art.fbx})");
            return false;
        }
    }
}
