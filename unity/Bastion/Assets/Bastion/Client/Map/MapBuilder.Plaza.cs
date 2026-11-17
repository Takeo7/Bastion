using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A shopping plaza (runs, H9 §4: GameState.Interiors' site of kind «plaza», its rooms the shops, each with what it
    /// sells, ShopUses): a model on each of the engine's marks (PlazaDetailKinds). On furniture (PlazaFurniture, from
    /// Decor), what that tile is: a counter (Apocalypse's, as today's shops) with its till (Shops' cash registers),
    /// Shopping Plaza's display cases (a jewellery's rings, necklaces and watches on them, an electronics' laptops and
    /// cameras), Shops' clothes racks and shelving (stocked by what the shop sells: televisions and consoles; balls, helmets
    /// and weights; toys), the square's planters as Shopping Plaza's beds with a bush. Scenery (PlazaScenery): mannequins
    /// (Apocalypse's; a sports shop's Shops'), the cinema's lobby (a big popcorn bucket, its films' posters on the wall, the
    /// projector by the ceiling, sofas), broken glass on the floor (Apocalypse's shards); out of doors an awning on its
    /// poles before each door, each shop's sign over its front (Shops' big letters by what it sells) and two billboards by
    /// the street. Its fronts are WallLayout's («plaza» kit); its café's stools and coffee machine HomeFurnishing's.
    /// </summary>
    public sealed partial class MapBuilder
    {
        /// <summary>A plaza's marks on furniture (a till rides on its counter): the rest are scenery.</summary>
        static readonly HashSet<string> PlazaOnFurniture = new HashSet<string>
        {
            PlazaDetailKinds.DisplayCase, PlazaDetailKinds.RingDisplay, PlazaDetailKinds.WatchDisplay, PlazaDetailKinds.ClothesRack, PlazaDetailKinds.Shelf,
            PlazaDetailKinds.Counter, PlazaDetailKinds.Planter,
        };

        /// <summary>
        /// Each shop's sign by what it sells (Shops' big letters; the pharmacy's Military's cross); the cinema has none of
        /// theirs, and the empty one is to let: theirs in letters (PlazaWords).
        /// </summary>
        static readonly Dictionary<string, string> PlazaSigns = new Dictionary<string, string>
        {
            [ShopUses.Jewellery] = "pzSignStyle", [ShopUses.Clothes] = "pzSignClothing", [ShopUses.Electronics] = "pzSignTech", [ShopUses.Pharmacy] = "pzSignMedical",
            [ShopUses.Sports] = "pzSignGym", [ShopUses.Cafe] = "pzSignCafe", [ShopUses.Toys] = "pzSignGame",
        };

        /// <summary>The same in Spanish, of Shops' letters one by one (a lab's choice: BASTION_PLAZA_LETRAS=1, to show Arturo); the cinema's always.</summary>
        static readonly Dictionary<string, string> PlazaWords = new Dictionary<string, string>
        {
            [ShopUses.Jewellery] = "JOYERIA", [ShopUses.Clothes] = "ROPA", [ShopUses.Electronics] = "ELECTRONICA", [ShopUses.Pharmacy] = "FARMACIA",
            [ShopUses.Sports] = "DEPORTES", [ShopUses.Cafe] = "CAFE", [ShopUses.Toys] = "JUGUETES", [ShopUses.Cinema] = "CINE", [ShopUses.Empty] = "SE ALQUILA",
        };

#if UNITY_EDITOR
        static readonly bool PlazaLetters = System.Environment.GetEnvironmentVariable("BASTION_PLAZA_LETRAS") == "1";
#else
        const bool PlazaLetters = false;
#endif

        /// <summary>Shops' letters: how wide each is (m; 0.714 tall), the rest 0.4.</summary>
        static readonly Dictionary<char, float> LetterWide = new Dictionary<char, float> { ['A'] = 0.478f, ['I'] = 0.158f, ['M'] = 0.522f, ['J'] = 0.36f };

        /// <summary>What stands on a shop's shelves, by what it sells (on its three boards); a shop of none, cardboard boxes.</summary>
        static readonly Dictionary<string, string[]> PlazaGoods = new Dictionary<string, string[]>
        {
            [ShopUses.Electronics] = new[] { "pzTv", "pzLaptop", "pzLaptop2", "pzConsole", "pzCamera" },
            [ShopUses.Sports] = new[] { "pzBasketball", "pzSoccer", "pzHelmet", "pzDumbbell", "pzKettlebell", "pzSkateboard" },
            [ShopUses.Toys] = new[] { "pzBear", "pzBlocks", "pzBlocks2", "pzTrain", "pzSamurai", "pzBear" },
        };

        /// <summary>Shops' clothes racks hung with clothes (its 03, 07 and 08; the rest bare rails, or a table).</summary>
        static readonly string[] PlazaRacks = { "pzRack3", "pzRack7", "pzRack8" };
        /// <summary>Shops' sets of clothes on hangers, to hang on them.</summary>
        static readonly string[] PlazaClothes = { "pzClothesShirt", "pzClothesJacket", "pzClothesBlouse", "pzClothesSweater" };

        /// <summary>Shops' shelving's boards (Basic_01 and _03, m: 0.40, 1.09, 1.76 and 0.43, 1.04, 1.71), in tiles.</summary>
        static readonly float[] PlazaBoards = { 0.02f, 0.71f, 1.16f };

        /// <summary>The map's shopping plaza, or null.</summary>
        InteriorSite PlazaSite => s.Interiors?.FirstOrDefault(i => i != null && i.Kind == InteriorKinds.Plaza);

        /// <summary>What the plaza's shop on tile i sells (Building.plaza), or null.</summary>
        string PlazaUseAt(int i)
        {
            int b = plan != null && i >= 0 && i < plan.Owner.Length ? plan.Owner[i] : -1;
            return b >= 0 && b < plan.Buildings.Count ? plan.Buildings[b].plaza : null;
        }

        /// <summary>Whether tile i of storey k has a plaza's mark on furniture (Marked).</summary>
        bool PlazaMarked(int k, int i) => k == 0 && PlazaSite != null && MarksOn(k, i)?.Any(d => PlazaOnFurniture.Contains(d.Kind)) == true;

        /// <summary>
        /// The plaza's furniture on tile i (storey 0) as its mark says; false if none of its marks is there. Each turned the
        /// way its mark faces, a little askew; what stands on it on its top or its boards (Measure, PlazaBoards).
        /// </summary>
        bool PlazaFurniture(int k, int i, bool low, float floor, List<InteriorDetail> marks)
        {
            if (k != 0 || marks == null || PlazaSite == null) return false;
            var mark = marks.FirstOrDefault(d => PlazaOnFurniture.Contains(d.Kind));
            if (mark == null) return false;
            int x = i % s.Width, y = i / s.Width;
            float cx = x + 0.5f, cz = y + 0.5f, T = MapSpace.Tile;
            // (0.10.1's frame in tiles to Unity's: z the other way.)
            Vector3 At(float ax, float ay, float az) => new Vector3(ax * T, floor + ay * T, -az * T);
            float R(int salt) => MapArt.Hash(x, y, 140 + salt);
            var tile = new[] { i };
            GameObject Put(string id, float ax, float ay, float az, float yaw)
            {
                var o = Place(props[k], id, At(ax, ay, az), yaw);
                Own(o, k, tile);
                return o;
            }
            string Pick(string[] ids, int salt) => ids[Mathf.FloorToInt(R(salt) * ids.Length) % ids.Length];
            var front = Bastion.Engine.Grid.Dirs4[mark.Facing];
            // Across the way it faces (0.10.1's frame: x east, y south).
            var side = new Vector2(-front.Y, front.X);
            float yaw = YawOf(mark.Facing);
            string use = PlazaUseAt(i);
            // Things on a top `up` tiles high, spread along it (`spread` of the tile), each a little turned.
            void OnTop(string[] ids, int count, float up, float back, float spread, int salt)
            {
                for (int n = 0; n < count; n++)
                {
                    float u = count == 1 ? (R(salt + n * 3) - 0.5f) * 0.2f : (n / (count - 1f) - 0.5f) * spread + (R(salt + n * 3) - 0.5f) * 0.06f;
                    Put(Pick(ids, salt + 1 + n * 3), cx - front.X * back + side.x * u, up, cz - front.Y * back + side.y * u, yaw + (R(salt + 2 + n * 3) - 0.5f) * 40);
                }
            }
            switch (mark.Kind)
            {
                case PlazaDetailKinds.Counter:
                {
                    // Today's shops' counter (Furnished's), the till on the first (on the clerk's side, turned to them).
                    Put("shopCounter", cx, 0, cz, yaw);
                    float top = (float)Measure("shopCounter").top;
                    if (marks.Any(d => d.Kind == PlazaDetailKinds.Till))
                        Put($"pzTill{1 + Mathf.FloorToInt(R(1) * 6) % 6}", cx - front.X * 0.12f + side.x * 0.15f, top, cz - front.Y * 0.12f + side.y * 0.15f, yaw + 180 + (R(2) - 0.5f) * 20);
                    // (A cinema's: popcorn by the till, one counter in three.)
                    else if (use == ShopUses.Cinema && R(3) < 0.4f) OnTop(new[] { "pzPopcorn" }, 2, top, 0, 0.4f, 10);
                    return true;
                }
                case PlazaDetailKinds.DisplayCase:
                case PlazaDetailKinds.RingDisplay:
                case PlazaDetailKinds.WatchDisplay:
                {
                    if (!low)
                    {
                        // A tall one (on a jewellery's high tile): the small case on its plinth, stood taller, a necklace in it.
                        var tall = Put("pzCaseTall", cx - front.X * 0.1f, 0, cz - front.Y * 0.1f, yaw + (R(1) - 0.5f) * 8);
                        if (tall != null) tall.transform.localScale = Vector3.one * 1.3f;
                        Put(R(2) < 0.5f ? "pzNecklace" : "pzNecklace2", cx - front.X * 0.1f, 0.65f * 1.3f / T, cz - front.Y * 0.1f, yaw);
                        return true;
                    }
                    Put("pzCase", cx, 0, cz, yaw + (R(1) - 0.5f) * 4);
                    float top = (float)Measure("pzCase").top;
                    if (mark.Kind == PlazaDetailKinds.RingDisplay) OnTop(new[] { "pzRing", "pzRing2", "pzRing3", "pzNecklace" }, 3, top, 0, 0.6f, 20);
                    else if (mark.Kind == PlazaDetailKinds.WatchDisplay) OnTop(new[] { "pzWatch" }, 3, top, 0, 0.55f, 30);
                    else if (use == ShopUses.Electronics) OnTop(new[] { "pzLaptop", "pzLaptop2", "pzCamera", "pzConsole" }, 2, top, 0, 0.45f, 40);
                    else OnTop(new[] { "pzNecklace", "pzNecklace2", "pzRing2" }, 2, top, 0, 0.45f, 50);
                    return true;
                }
                case PlazaDetailKinds.ClothesRack:
                {
                    // Shops' rack along its wall, and on its rail one of Shops' sets of clothes on their hangers (their hooks at
                    // their top: hung from the rail, the rack's top), brought to the rack's length.
                    string rack = PlazaRacks[Mathf.FloorToInt(R(1) * PlazaRacks.Length) % PlazaRacks.Length];
                    float turn = yaw + (R(2) - 0.5f) * 6;
                    Put(rack, cx - front.X * 0.12f, 0, cz - front.Y * 0.12f, turn);
                    string set = PlazaClothes[Mathf.FloorToInt(R(3) * PlazaClothes.Length) % PlazaClothes.Length];
                    var hung = Put(set, cx - front.X * 0.12f, (float)(Measure(rack).top - Measure(set).top * 0.85) - 0.02f, cz - front.Y * 0.12f, turn);
                    if (hung != null) hung.transform.localScale = Vector3.one * 0.85f;
                    return true;
                }
                case PlazaDetailKinds.Shelf:
                {
                    // Shops' shelving against its wall, and on its boards what the shop sells, some taken (a box where it sells nothing).
                    Put(R(1) < 0.5f ? "pzShelf" : "pzShelf2", cx - front.X * 0.12f, 0, cz - front.Y * 0.12f, yaw);
                    var goods = use != null && PlazaGoods.TryGetValue(use, out var g) ? g : new[] { "inShelfBox", "inShelfBox2", "inShelfBox3" };
                    float full = R(2) < 0.15f ? 0.1f : 0.5f + R(3) * 0.4f;
                    for (int n = 0; n < PlazaBoards.Length; n++)
                        if (R(4 + n) < full) OnTop(goods, R(8 + n) < 0.5f ? 1 : 2, PlazaBoards[n] + 0.01f, 0.14f, 0.5f, 60 + n * 10);
                    return true;
                }
                case PlazaDetailKinds.Planter:
                {
                    // Shopping Plaza's bed, a bush in it (the square's planters are low cover).
                    Put("pzPlanter", cx, 0, cz, R(1) < 0.5f ? 0 : 90);
                    var bush = Put("bush", cx + (R(2) - 0.5f) * 0.1f, 0.05f, cz + (R(3) - 0.5f) * 0.1f, R(4) * 360);
                    if (bush != null) bush.transform.localScale = Vector3.one * (0.75f + R(5) * 0.2f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>The plaza's scenery as drawn (PlazaScenery), and what it was drawn from: made again when that changes.</summary>
        readonly List<GameObject> plazaScenery = new List<GameObject>();
        long plazaKey = long.MinValue;

        /// <summary>What PlazaScenery draws from, hashed: each mark of scenery, its tile and what that tile is now.</summary>
        long PlazaKey(InteriorSite site)
        {
            long h = 29;
            foreach (var d in site.Dressing)
            {
                if (PlazaOnFurniture.Contains(d.Kind) || d.X < 0 || d.Y < 0 || d.X >= s.Width || d.Y >= s.Height) continue;
                int i = d.Y * s.Width + d.X;
                unchecked
                {
                    h = h * 31 + i * 17 + d.Kind.GetHashCode();
                    h = h * 31 + (i < s.Tiles.Count ? s.Tiles[i]?.GetHashCode() ?? 1 : 0);
                }
            }
            return h;
        }

        /// <summary>
        /// The plaza's scenery (its marks not on furniture), on the ground floor, each where its tile still is what it was
        /// laid on: a mannequin standing (one in six knocked over), the cinema's big popcorn bucket, posters on the wall
        /// behind, the projector up under the ceiling, a sofa (its back to the wall by one), broken glass flat on the floor; out
        /// of doors the awning over the double door, the sign over the front, a billboard on a wall by the street. What hangs
        /// on a building goes with it in the cutaway (Cutaway.Track, TrackFixture). Made again whole when the map changes.
        /// </summary>
        void PlazaScenery()
        {
            var site = PlazaSite;
            if (site == null && plazaScenery.Count == 0) return;
            long drawnFrom = site == null ? 0 : PlazaKey(site);
            if (drawnFrom == plazaKey && plazaScenery.All(o => o != null)) return;
            plazaKey = drawnFrom;
            foreach (var o in plazaScenery) DestroyNow(o);
            plazaScenery.Clear();
            if (site == null || props.Length == 0) return;
            float T = MapSpace.Tile;
            foreach (var d in site.Dressing)
            {
                if (PlazaOnFurniture.Contains(d.Kind) || d.Kind == PlazaDetailKinds.Till || d.Z != 0 || d.X < 0 || d.Y < 0 || d.X >= s.Width || d.Y >= s.Height) continue;
                int i = d.Y * s.Width + d.X;
                string tile = i < s.Tiles.Count ? s.Tiles[i] : null;
                bool onWall = d.Kind == PlazaDetailKinds.ShopSign || d.Kind == PlazaDetailKinds.Billboard;
                if (onWall ? !BuildingPlan.IsWallLike(tile) : tile != TileKinds.Floor) continue;
                float R(int salt) => MapArt.Hash(d.X, d.Y, 160 + salt);
                var front = Bastion.Engine.Grid.Dirs4[d.Facing];
                var side = new Vector2(-front.Y, front.X);
                float cx = d.X + 0.5f, cz = d.Y + 0.5f;
                int b = plan != null && i < plan.Owner.Length ? plan.Owner[i] : -1;
                string use = PlazaUseAt(i);
                // At (ax, ay, az) tiles, turned `yaw` from the mark's way.
                GameObject Stand(string id, float ax, float ay, float az, float turn)
                {
                    var o = Place(props[0], id, new Vector3(ax * T, ay * T, -az * T), YawOf(d.Facing) + turn);
                    if (o != null) plazaScenery.Add(o);
                    return o;
                }
                // Back `back` tiles from the tile's middle against the way it faces, `aside` across.
                GameObject Back(string id, float back, float aside, float up, float turn) =>
                    Stand(id, cx - front.X * back + side.x * aside, up, cz - front.Y * back + side.y * aside, turn);
                switch (d.Kind)
                {
                    case PlazaDetailKinds.Mannequin:
                    {
                        string id = use == ShopUses.Sports ? (R(0) < 0.5f ? "pzMannequinSport" : "pzMannequinSport2")
                            : (R(0) < 0.5f ? "pzMannequin" : "pzMannequinF") + (1 + Mathf.FloorToInt(R(1) * 6) % 6);
                        var o = Back(id, (R(2) - 0.5f) * 0.2f, (R(3) - 0.5f) * 0.2f, 0, (R(4) - 0.5f) * 50);
                        if (o != null && R(5) < 1 / 6f)
                        {
                            // Knocked over, on its back.
                            o.transform.rotation *= Quaternion.AngleAxis(-84, Vector3.right);
                            o.transform.position += Vector3.up * 0.18f;
                        }
                        break;
                    }
                    case PlazaDetailKinds.Popcorn:
                    {
                        // The lobby's: Shopping Plaza's bucket stood a metre tall by the wall (City's big one is the same).
                        var o = Back("pzPopcorn", 0.18f, 0, 0, (R(0) - 0.5f) * 30);
                        if (o != null) o.transform.localScale = Vector3.one * 4.6f;
                        break;
                    }
                    case PlazaDetailKinds.MoviePoster:
                    {
                        // On the wall behind it (its middle 0.33 tiles back from the tile's, the wall's face at 0.37), its foot 0.6 m up.
                        var o = Back(new[] { "pzPoster", "pzPoster2", "pzPoster3", "pzPoster4" }[Mathf.FloorToInt(R(0) * 4) % 4], 0.33f, 0, 0.41f, 0);
                        if (b >= 0) Cutaway.Current?.TrackFixture(o, b, 0, new Vector2(-front.X, -front.Y));
                        break;
                    }
                    case PlazaDetailKinds.Projector:
                        Back("pzProjector", 0.3f, 0, 2.02f, 0);
                        break;
                    case PlazaDetailKinds.Sofa:
                        Back("pzCouch", ByWall(d.X, d.Y, d.Facing) ? 0.12f : 0, (R(0) - 0.5f) * 0.1f, 0, (R(1) - 0.5f) * 8);
                        break;
                    case PlazaDetailKinds.BrokenGlass:
                    {
                        // A few of Apocalypse's shards, flat, small, no shadow.
                        for (int n = 0, count = 4 + Mathf.FloorToInt(R(0) * 3); n < count; n++)
                        {
                            var o = Stand($"pzShard{1 + Mathf.FloorToInt(R(1 + n * 4) * 11) % 11}", cx + (R(2 + n * 4) - 0.5f) * 0.8f, 0.004f, cz + (R(3 + n * 4) - 0.5f) * 0.8f, R(4 + n * 4) * 360);
                            if (o == null) continue;
                            o.transform.localScale = Vector3.one * (0.16f + R(5 + n * 4) * 0.16f);
                            foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        }
                        break;
                    }
                    case PlazaDetailKinds.Awning:
                        PlazaAwning(d, front, side, R);
                        break;
                    case PlazaDetailKinds.ShopSign:
                        PlazaSign(d, front, b, use);
                        break;
                    case PlazaDetailKinds.Billboard:
                    {
                        // Shopping Plaza's billboard on its struts out from the wall (its model's origin on the wall's face), its
                        // face one of three ads, three quarters its size (5.1 by 2.6 m), its top a little over the roof's edge.
                        float scale = 0.75f, foot = 1.05f / T;
                        string face = new[] { "pzBillboardFace", "pzBillboardFace2", "pzBillboardFace3" }[Mathf.FloorToInt(R(0) * 3) % 3];
                        foreach (var (id, up) in new[] { ("pzBillboard", 0f), (face, 0.053f * scale / T) })
                        {
                            var o = Stand(id, cx + front.X * PlazaWallFace, foot + up, cz + front.Y * PlazaWallFace, 0);
                            if (o == null) continue;
                            o.transform.localScale = Vector3.one * scale;
                            if (b >= 0) Cutaway.Current?.TrackFixture(o, b, 0, new Vector2(front.X, front.Y));
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>How far a wall's face stands from its middle (tiles): Shops' brick thickened (0.39 m), half of it.</summary>
        const float PlazaWallFace = 0.13f;

        /// <summary>Whether the tile behind (x, y), against the way `facing`, is a wall (the engine's ByWall: what stands by one faces away).</summary>
        bool ByWall(int x, int y, int facing)
        {
            var d = Bastion.Engine.Grid.Dirs4[(facing + 2) % 4];
            int qx = x + d.X, qy = y + d.Y;
            return qx >= 0 && qy >= 0 && qx < s.Width && qy < s.Height && BuildingPlan.IsWallLike(s.Tiles[qy * s.Width + qx]);
        }

        /// <summary>
        /// The awning before a shop's door (its mark on the tile out from the pair's first door, facing out): Shopping Plaza's
        /// sail (a triangle: its base 6.9 m, its point 8.9 m from it) brought down to the double door, its point on the front
        /// over the middle of the pair and its base out, 3.3 m along and 2 m from the wall, a pole under each of its two
        /// corners there; its underside 2.8 m up, under the top of the glass. It goes with its shop in the cutaway, as a sign.
        /// </summary>
        void PlazaAwning(InteriorDetail d, Vec2 front, Vector2 side, System.Func<int, float> R)
        {
            float T = MapSpace.Tile;
            // The door behind it and its partner along the wall: the middle of the two.
            int dx = d.X - front.X, dy = d.Y - front.Y;
            float mx = dx + 0.5f, mz = dy + 0.5f;
            foreach (int way in new[] { -1, 1 })
            {
                int px = dx + (int)side.x * way, py = dy + (int)side.y * way;
                string t = px >= 0 && py >= 0 && px < s.Width && py < s.Height ? s.Tiles[py * s.Width + px] : null;
                if (t != TileKinds.Door && t != TileKinds.DoorOpen) continue;
                mx += side.x * way * 0.5f;
                mz += side.y * way * 0.5f;
                break;
            }
            int b = plan != null && dy * s.Width + dx < plan.Owner.Length ? plan.Owner[dy * s.Width + dx] : -1;
            float yaw = YawOf(d.Facing);
            const float Along = 3.3f, Out = 2f, Under = 2.8f, Thick = 0.28f;
            float wall = 0.5f + PlazaWallFace;
            // (A quarter turn: its point, along its x as placed, to the wall; its base, along its z, along the wall.)
            var cover = Place(props[0], "pzAwning", new Vector3((mx + front.X * (wall + Out / T / 2)) * T, Under, -(mz + front.Y * (wall + Out / T / 2)) * T), yaw + 90);
            if (cover != null)
            {
                var box = BoundsIn(cover, cover.transform);
                cover.transform.localScale = new Vector3(Out / Mathf.Max(box.size.x, 0.01f), Thick / Mathf.Max(box.size.y, 0.01f), Along / Mathf.Max(box.size.z, 0.01f));
                plazaScenery.Add(cover);
                if (b >= 0) Cutaway.Current?.Track(cover, b, 0, true, new Vector2(front.X, front.Y));
            }
            foreach (int way in new[] { -1, 1 })
            {
                float reach = wall + Out / T - 0.08f, aside = (Along / T / 2 - 0.08f) * way;
                var pole = Place(props[0], "pzAwningPole", new Vector3((mx + front.X * reach + side.x * aside) * T, 0, -(mz + front.Y * reach + side.y * aside) * T), yaw);
                if (pole == null) continue;
                var box = BoundsIn(pole, pole.transform);
                pole.transform.localScale = new Vector3(1, (Under + Thick * 0.5f) / Mathf.Max(box.size.y, 0.01f), 1);
                plazaScenery.Add(pole);
                if (b >= 0) Cutaway.Current?.Track(pole, b, 0, true, new Vector2(front.X, front.Y));
            }
        }

        /// <summary>
        /// A shop's sign (its mark on a tile of its front, facing out): over the middle of its double door, on the front's
        /// face at the roof's edge, no wider than 3 tiles nor taller than 1.1 m: Shops' big letters by what it sells (PlazaSigns), or its name in
        /// Shops' letters one by one (the cinema's, which has none of theirs; all of them with BASTION_PLAZA_LETRAS=1). It goes
        /// with its shop in the cutaway (a sign's, turned from the camera too).
        /// </summary>
        void PlazaSign(InteriorDetail d, Vec2 front, int b, string use)
        {
            if (use == null) return;
            float T = MapSpace.Tile;
            var side = new Vector2(-front.Y, front.X);
            // The front's door pair: the middle of its doors along this wall.
            float sum = 0;
            int count = 0;
            for (int u = -8; u <= 8; u++)
            {
                int px = d.X + (int)side.x * u, py = d.Y + (int)side.y * u;
                if (px < 0 || py < 0 || px >= s.Width || py >= s.Height) continue;
                string t = s.Tiles[py * s.Width + px];
                if ((t != TileKinds.Door && t != TileKinds.DoorOpen) || plan.Owner[py * s.Width + px] != b) continue;
                sum += u;
                count++;
            }
            float at = count > 0 ? sum / count : 0;
            float mx = d.X + 0.5f + side.x * at, mz = d.Y + 0.5f + side.y * at;
            float yaw = YawOf(d.Facing), up = BuildingPlan.Storey + 0.02f, outFrom = PlazaWallFace + 0.06f;
            var outward = new Vector2(front.X, front.Y);
            if (PlazaLetters || !PlazaSigns.ContainsKey(use))
            {
                if (!PlazaWords.TryGetValue(use, out var word)) return;
                // Letter by letter, 0.08 m apart, the word's middle over the doors.
                const float Gap = 0.08f;
                float Wide(char c) => c == ' ' ? 0.25f : LetterWide.TryGetValue(c, out var lw) ? lw : 0.4f;
                float width = word.Sum(Wide) + Gap * (word.Length - 1);
                float k = Mathf.Min(1, 4 * T / width), from = -width * k / 2;
                foreach (char c in word)
                {
                    float lw = Wide(c) * k;
                    float u = (from + lw / 2) / T;
                    if (c == ' ')
                    {
                        from += lw + Gap * k;
                        continue;
                    }
                    // (A piece's x along the wall is (o.y, −o.x): read from the left facing it.)
                    var o = Place(props[0], $"pzL{c}", new Vector3((mx + front.X * outFrom + front.Y * u) * T, up * T, -(mz + front.Y * outFrom - front.X * u) * T), yaw);
                    from += lw + Gap * k;
                    if (o == null) continue;
                    o.transform.localScale = Vector3.one * k;
                    plazaScenery.Add(o);
                    if (b >= 0) Cutaway.Current?.Track(o, b, 0, true, outward);
                }
                return;
            }
            var sign = Place(props[0], PlazaSigns[use], new Vector3((mx + front.X * outFrom) * T, up * T, -(mz + front.Y * outFrom) * T), yaw);
            if (sign == null) return;
            var box = BoundsIn(sign, sign.transform);
            float scale = Mathf.Min(1, 3 * T / Mathf.Max(box.size.x, 0.01f), 1.1f / Mathf.Max(box.size.y, 0.01f));
            sign.transform.localScale = Vector3.one * scale;
            plazaScenery.Add(sign);
            if (b >= 0) Cutaway.Current?.Track(sign, b, 0, true, outward);
        }
    }
}
