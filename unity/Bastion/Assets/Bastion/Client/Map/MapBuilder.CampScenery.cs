using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The camping's marks out of doors (runs, H9 §5: GameState.Interiors' camping, CampDetailKinds), Apocalypse's as the
    /// base's clearing has them (CaravanArt: the packs' prefabs whole, their fronts +z). Each tent over its block (the
    /// engine's `low` tiles, a military one's `high`, no decor) as big as the block, no taller than the pack made it, its
    /// door the way its mark faces (the long one's and the quarantine one's, `tentMilitary` of variant 1, on a short end:
    /// the engine lays their blocks long that way, Gameplay 17/11). The chairs, the fire
    /// pit's stones (CampFire's flames in them), a rotary washing line, a cool box, a sleeping bag laid out or rolled up,
    /// on their tiles; Construction's portaloos (MapArt's: the pack's camping toilet is a seat, not a cabin); the dead,
    /// baked lying as the houses' (in a caravan, on its bed's mattress: Caravans.BedOf). Made again whole when the map
    /// changes under them (CampKey).
    /// </summary>
    public sealed partial class MapBuilder
    {
        readonly List<GameObject> campScenery = new List<GameObject>();
        long campKey = long.MinValue;

        /// <summary>How big the fire pit's stones are, round CampFire's flames (the pack's: under a metre across).</summary>
        const float FireStones = 1.2f;

        /// <summary>The map's camping, or null.</summary>
        InteriorSite CampSite => s.Interiors?.FirstOrDefault(site => site?.Kind == InteriorKinds.Camping && site.Dressing != null);

        /// <summary>
        /// The blocks a tent's mark may have (tiles, x × y; the bigger first), as the engine lays them (Mapgen.CampingMap): a
        /// long one and the quarantine one long the way the mark faces; null for no tent.
        /// </summary>
        static (int w, int h)[] BlocksOf(InteriorDetail d)
        {
            bool northSouth = d.Facing == 0 || d.Facing == 2;
            switch (d.Kind)
            {
                case CampDetailKinds.TentDome: return new[] { (2, 2) };
                case CampDetailKinds.TentLong: return new[] { northSouth ? (2, 3) : (3, 2) };
                case CampDetailKinds.TentMilitary: return d.Variant == 1 ? new[] { northSouth ? (4, 5) : (5, 4) } : new[] { (6, 6), (4, 4) };
                default: return null;
            }
        }

        /// <summary>
        /// Tent d's block on state s: of those it may have with its mark on a corner (the engine's first tile, wherever the
        /// map's turn took it), the one most of whose tiles still stand as the tent's (its cover, no decor); null if none does.
        /// </summary>
        static RectInt? BlockOf(GameState s, InteriorDetail d)
        {
            var sizes = BlocksOf(d);
            if (sizes == null || s.Tiles == null) return null;
            var tiles = TilesOf(s, 0);
            var decor = DecorOf(s, 0);
            string kind = d.Kind == CampDetailKinds.TentMilitary ? TileKinds.High : TileKinds.Low;
            RectInt? best = null;
            float most = 0;
            foreach (var (w, h) in sizes)
                foreach (int x0 in new[] { d.X, d.X - w + 1 })
                    foreach (int y0 in new[] { d.Y, d.Y - h + 1 })
                    {
                        int n = 0;
                        for (int y = y0; y < y0 + h; y++)
                            for (int x = x0; x < x0 + w; x++)
                            {
                                int i = y * s.Width + x;
                                if (x >= 0 && y >= 0 && x < s.Width && y < s.Height && i < tiles.Count && tiles[i] == kind && (i >= decor.Count || decor[i] == 0)) n++;
                            }
                        // (Of the share of it standing: a block of 4 is all of one, a third of one of 6.)
                        float share = (float)n / (w * h);
                        if (share <= most) continue;
                        most = share;
                        best = new RectInt(x0, y0, w, h);
                    }
            return best;
        }

        RectInt? BlockOf(InteriorDetail d) => BlockOf(s, d);

        /// <summary>
        /// The tiles of state s's tents' blocks (Fog: a big one is high cover all through, its middle never in sight; each is
        /// seen whole when any of it is); none off a camping.
        /// </summary>
        public static List<int[]> TentBlocks(GameState s)
        {
            var blocks = new List<int[]>();
            var site = s?.Interiors?.FirstOrDefault(i => i?.Kind == InteriorKinds.Camping && i.Dressing != null);
            if (site == null) return blocks;
            foreach (var d in site.Dressing)
            {
                var r = d.Z == 0 && d.X >= 0 && d.Y >= 0 && d.X < s.Width && d.Y < s.Height ? BlockOf(s, d) : null;
                if (r == null) continue;
                var tiles = new List<int>();
                foreach (var p in r.Value.allPositionsWithin)
                    if (p.x >= 0 && p.y >= 0 && p.x < s.Width && p.y < s.Height) tiles.Add(p.y * s.Width + p.x);
                blocks.Add(tiles.ToArray());
            }
            return blocks;
        }

        /// <summary>What CampScenery draws from, hashed: each mark, and what its tiles (a tent's, its block's) are now.</summary>
        long CampKey(InteriorSite site)
        {
            long h = 37;
            var tiles = TilesOf(0);
            foreach (var d in site.Dressing)
            {
                if (d.Z != 0 || d.X < 0 || d.Y < 0 || d.X >= s.Width || d.Y >= s.Height) continue;
                var r = BlockOf(d);
                unchecked
                {
                    h = h * 31 + (d.Y * s.Width + d.X) * 17 + d.Kind.GetHashCode();
                    if (r == null) h = h * 31 + (tiles[d.Y * s.Width + d.X]?.GetHashCode() ?? 1);
                    else
                        foreach (var p in r.Value.allPositionsWithin)
                            h = h * 31 + (p.x >= 0 && p.y >= 0 && p.x < s.Width && p.y < s.Height ? tiles[p.y * s.Width + p.x]?.GetHashCode() ?? 1 : 0);
                }
            }
            return h;
        }

        /// <summary>
        /// CaravanArt's prefab `name` whole on the ground under the map's props, its middle at (x, z) (0.10.1's tiles), `up`
        /// metres over the ground, its front (+z) turned `yaw` (Place's); with its size as the pack made it (metres). Null
        /// without it (in Missing).
        /// </summary>
        (GameObject o, Vector3 size) Whole(string name, float x, float z, float yaw, float up = 0)
        {
            var prefab = CaravanArt.Load()?.Model(name);
            if (prefab == null)
            {
                Missing.Add($"camping ({name}: CaravanArt)");
                return (null, Vector3.zero);
            }
            var holder = new GameObject(name).transform;
            holder.SetParent(props[0], false);
            var model = UnityEngine.Object.Instantiate(prefab, holder, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var box = BoundsIn(model, holder);
            model.transform.localPosition = new Vector3(-box.center.x, -box.min.y, -box.center.z);
            holder.SetPositionAndRotation(new Vector3(x * MapSpace.Tile, up, -z * MapSpace.Tile), Quaternion.Euler(0, yaw, 0));
            campScenery.Add(holder.gameObject);
            Placed++;
            return (holder.gameObject, box.size);
        }

        /// <summary>Tent `model` over block r, its door to `front`: as long and as wide as the block (a little in), stretched no more than a quarter one way than the other, no taller than the pack made it.</summary>
        void Tent(string model, RectInt r, int front)
        {
            var f = Bastion.Engine.Grid.Dirs4[front];
            float along = (f.Y != 0 ? r.height : r.width) * 0.95f * MapSpace.Tile, across = (f.Y != 0 ? r.width : r.height) * 0.95f * MapSpace.Tile;
            var (o, size) = Whole(model, r.x + r.width / 2f, r.y + r.height / 2f, YawOf(front));
            if (o == null) return;
            float sx = across / Mathf.Max(size.x, 0.01f), sz = along / Mathf.Max(size.z, 0.01f);
            sx = Mathf.Min(sx, 1.25f * sz);
            sz = Mathf.Min(sz, 1.25f * sx);
            o.transform.localScale = new Vector3(sx, Mathf.Min(1, Mathf.Min(sx, sz)), sz);
        }

        /// <summary>
        /// The camping's scenery (the marks of its site out of the caravans', which Caravans draws), each where its tiles still
        /// are what they were laid as: a tent while any of its block stands, a chair, a sleeping bag, a washing line, a cool
        /// box or a body on the floor, a portaloo on its cover; the fire pit's stones always. Made again whole when the map
        /// changes (CampKey).
        /// </summary>
        void CampScenery()
        {
            var site = CampSite;
            if (site == null && campScenery.Count == 0) return;
            long drawnFrom = site == null ? 0 : CampKey(site);
            if (drawnFrom == campKey && campScenery.All(o => o != null)) return;
            campKey = drawnFrom;
            foreach (var o in campScenery) DestroyNow(o);
            campScenery.Clear();
            if (site == null || props.Length == 0) return;
            var tiles = TilesOf(0);
            float T = MapSpace.Tile;
            foreach (var d in site.Dressing)
            {
                if (d.Z != 0 || d.X < 0 || d.Y < 0 || d.X >= s.Width || d.Y >= s.Height) continue;
                int i = d.Y * s.Width + d.X;
                string tile = i < tiles.Count ? tiles[i] : null;
                bool floor = tile == TileKinds.Floor;
                float R(int salt) => MapArt.Hash(d.X, d.Y, 190 + salt);
                // A little off the tile's middle, and askew.
                float cx = d.X + 0.5f + (R(1) - 0.5f) * 0.16f, cz = d.Y + 0.5f + (R(2) - 0.5f) * 0.16f;
                float yaw = YawOf(d.Facing) + (R(3) - 0.5f) * 30;
                switch (d.Kind)
                {
                    case CampDetailKinds.TentDome:
                    case CampDetailKinds.TentLong:
                    case CampDetailKinds.TentMilitary:
                    {
                        var r = BlockOf(d);
                        if (r == null) break;
                        bool quarantine = d.Kind == CampDetailKinds.TentMilitary && d.Variant == 1;
                        string model = d.Kind == CampDetailKinds.TentDome ? "SM_Prop_Tent_Dome_01" : d.Kind == CampDetailKinds.TentLong ? "SM_Prop_Tent_Long_01"
                            : quarantine ? "SM_Bld_Quarantine_Tent_01" : "SM_Bld_Military_Tent_01";
                        Tent(model, r.Value, d.Facing);
                        break;
                    }
                    case CampDetailKinds.FirePit:
                    {
                        var (o, _) = Whole("SM_Prop_FirePit_01", d.X + 0.5f, d.Y + 0.5f, R(0) * 360);
                        if (o != null) o.transform.localScale = Vector3.one * FireStones;
                        break;
                    }
                    case CampDetailKinds.CampChair:
                        if (floor) Whole("SM_Prop_Camp_Chair_01", cx, cz, yaw);
                        break;
                    case CampDetailKinds.SleepingBag:
                    {
                        if (!floor) break;
                        // Rolled up one in three; else laid out, a little shorter than the tile.
                        if (R(0) < 1 / 3f)
                        {
                            Whole("SM_Prop_Sleeping_Bag_Rolled_01", cx, cz, yaw + R(4) * 90);
                            break;
                        }
                        var (o, _) = Whole("SM_Prop_Sleeping_Bag_01", d.X + 0.5f, d.Y + 0.5f, YawOf(d.Facing) + (R(3) - 0.5f) * 16);
                        if (o != null) o.transform.localScale = Vector3.one * 0.8f;
                        break;
                    }
                    case CampDetailKinds.Clothesline:
                    {
                        if (!floor) break;
                        var (o, _) = Whole("SM_Prop_WashingLine_01", d.X + 0.5f, d.Y + 0.5f, R(0) * 360);
                        if (o != null) o.transform.localScale = Vector3.one * 0.75f;
                        break;
                    }
                    case CampDetailKinds.Cooler:
                        if (floor) Whole("SM_Prop_Cooler_01", cx, cz, yaw);
                        break;
                    case CampDetailKinds.ToiletPortable:
                    {
                        if (tile != TileKinds.High) break;
                        // (MapArt's has its door at its back: a town's stands any way round.)
                        var o = Place(props[0], "portaloo", new Vector3((d.X + 0.5f) * T, 0, -(d.Y + 0.5f) * T), YawOf(d.Facing) + 180 + (R(0) - 0.5f) * 6);
                        if (o != null) campScenery.Add(o);
                        break;
                    }
                    case CampDetailKinds.CorpseCamper:
                    {
                        string id = R(0) < 0.5f ? "corpseCivilian" : "corpseStaff";
                        int b = plan != null && i < plan.Owner.Length ? plan.Owner[i] : -1;
                        var caravan = b >= 0 && b < plan.Buildings.Count && plan.Buildings[b].trailer != null ? plan.Buildings[b] : null;
                        GameObject body = null;
                        if (caravan != null)
                        {
                            // On its bed, along it, its head to the pillows (the bed's mark faces away from them, as this one).
                            var bed = Bastion.Client.Map.Caravans.BedOf(terrain, caravan);
                            if (bed != null) body = PlaceGame(props[0], id, Matrix4x4.TRS(bed.Value, Quaternion.AngleAxis((4 - d.Facing) % 4 * 90f, Vector3.up), Vector3.one));
                        }
                        else if (floor) body = PlaceGame(props[0], id, Matrix4x4.TRS(new Vector3(cx, 0, cz), Quaternion.AngleAxis((4 - d.Facing) % 4 * 90f + (R(4) - 0.5f) * 40, Vector3.up), Vector3.one));
                        if (body != null) campScenery.Add(body);
                        break;
                    }
                }
            }
        }
    }
}
