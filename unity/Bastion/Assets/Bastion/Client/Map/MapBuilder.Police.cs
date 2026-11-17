using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The runs' police station (H9 §3, .dev-snapshots/gameplay/runs/H9-INTERIORES.md; PoliceDetailKinds): POLYGON Police
    /// Station's furniture on the engine's marks, as the offices' and the shed's (MapBuilder.Interiors.cs). On furniture
    /// (PoliceFurniture, from Furniture): desks with a monitor, a tower and a keyboard, filing cabinets, weapon lockers, gun
    /// racks with their rifles, gun and ammunition cases, evidence shelving with its boxes, the interrogation tables,
    /// the vending machine, the cells' steel toilets, Military's lockers. Over two tiles (PoliceThing, from
    /// Thing): the lobby's benches, the cells' bunks. On the floor (PoliceFitted, from Fitted): the chairs at the desks and at
    /// the interrogation tables, the boards (a corkboard with its papers hung on the wall; a whiteboard hung on an outer wall, Office's on its feet by a
    /// partition), the water cooler, evidence bags, the polygraph on
    /// its table, a dead officer (CorpseBake's, Sidekick's Modern Police). A counter's mark adds nothing: the wall with its
    /// window is the kit's (WallLayout.Counters); the kitchen and the washrooms are a house's (their decor), the blood the
    /// offices' (Fitted).
    /// </summary>
    public sealed partial class MapBuilder
    {
        /// <summary>The marks of the map's police stations, and the list they were read from.</summary>
        HashSet<InteriorDetail> policeMarks;
        List<InteriorSite> policeFrom;

        /// <summary>Whether mark `d` is a police station's.</summary>
        bool OfPolice(InteriorDetail d)
        {
            if (d == null || s.Interiors == null) return false;
            if (policeMarks == null || policeFrom != s.Interiors)
            {
                policeFrom = s.Interiors;
                policeMarks = new HashSet<InteriorDetail>(s.Interiors.Where(site => site?.Kind == InteriorKinds.Police && site.Dressing != null).SelectMany(site => site.Dressing));
            }
            return policeMarks.Contains(d);
        }

        /// <summary>
        /// How far a wall's face stands from the middle of the floor tile beside it (tiles): the wall is laid along its tile's
        /// middle, half its thickness (WallLayout's 0.1 · Thick · 0.8) towards the floor.
        /// </summary>
        const float WallFace = 1 - 0.1f * WallLayout.Thick * 0.8f;

        /// <summary>Whether tile (x, y) of storey k is a wall, a window, a door or bars.</summary>
        bool WallAt(int k, int x, int y)
        {
            if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return false;
            var tiles = TilesOf(k);
            int i = y * s.Width + x;
            return i < tiles.Count && BuildingPlan.IsWallLike(tiles[i]);
        }

        /// <summary>Whether tile (x, y) of storey k is a plain wall (no door, window nor bars).</summary>
        bool SolidAt(int k, int x, int y)
        {
            if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return false;
            var tiles = TilesOf(k);
            int i = y * s.Width + x;
            return i < tiles.Count && tiles[i] == TileKinds.Wall;
        }

        /// <summary>
        /// How far back from its tile's middle a piece `depth` tiles deep stands, away from the way `facing` it looks: its
        /// back against the wall there if there is one, else in the middle.
        /// </summary>
        float Back(int k, int x, int y, int facing, float depth)
        {
            var d = Bastion.Engine.Grid.Dirs4[(facing + 2) % 4];
            return WallAt(k, x + d.X, y + d.Y) ? Mathf.Max(0, WallFace - 0.03f - depth / 2) : 0;
        }

        /// <summary>
        /// A placed prop's insert in the pack (a rack's rifles, a shelf's boxes, a machine's goods, a board's papers): its
        /// model in the prop's own model's frame (they share their origin), so it is fitted and turned with it.
        /// </summary>
        void Insert(GameObject placed, string id)
        {
            var model = placed != null && placed.transform.childCount > 0 && placed.transform.GetChild(0).childCount > 0 ? placed.transform.GetChild(0).GetChild(0) : null;
            if (model == null) return;
            var art = MapArt.Prop(id);
            var prefab = art != null && !string.IsNullOrEmpty(art.fbx) ? prefabOf(MapArt.ModelKey(art)) : null;
            if (prefab == null)
            {
                Missing.Add(id);
                return;
            }
            var o = Object.Instantiate(prefab, model, false);
            o.name = id;
            Placed++;
        }

        /// <summary>
        /// The furniture on tile i of storey k as its police station's mark says (Furniture's); false to draw the tile as any
        /// other (what has no model of the pack's on low cover: a weapon locker, a rack or shelving knocked down).
        /// </summary>
        bool PoliceFurniture(int k, int i, InteriorDetail mark, List<InteriorDetail> marks, bool low, float floor)
        {
            int x = i % s.Width, y = i / s.Width;
            float cx = x + 0.5f, cz = y + 0.5f, T = MapSpace.Tile;
            // (0.10.1's frame in tiles to Unity's: z the other way.)
            Vector3 At(float ax, float ay, float az) => new Vector3(ax * T, floor + ay * T, -az * T);
            float R(int salt) => MapArt.Hash(x, y, 150 + salt + k * 7);
            var tile = new[] { i };
            GameObject Put(string id, Vector3 at, float yaw)
            {
                var o = Place(props[k], id, at, yaw);
                Own(o, k, tile);
                return o;
            }
            int f = mark.Facing;
            var front = Bastion.Engine.Grid.Dirs4[f];
            // `back` tiles back from the middle (away from the way it faces), `aside` across, `up` over the floor.
            GameObject Stand(string id, float back, float aside, float up, float turn = 0) =>
                Put(id, At(cx - front.X * back - front.Y * aside, up, cz - front.Y * back + front.X * aside), YawOf(f) + turn);
            float Deep(string id) => (float)Measure(id).z;
            switch (mark.Kind)
            {
                case PoliceDetailKinds.Desk:
                {
                    // Its sitter's side to the chair beside it that looks at it; without one, its mark's way.
                    for (int n = 0; n < 4; n++)
                    {
                        var d = Bastion.Engine.Grid.Dirs4[n];
                        if (MarkAt(k, x + d.X, y + d.Y, PoliceDetailKinds.DeskChair)?.Facing == (n + 2) % 4) f = n;
                    }
                    var to = Bastion.Engine.Grid.Dirs4[f];
                    var across = new Vector2(-to.Y, to.X);
                    string id = new[] { "poDesk", "poDesk2", "poDesk3" }[Mathf.FloorToInt(R(0) * 3) % 3];
                    Put(id, At(cx, 0, cz), YawOf(f) + (R(1) - 0.5f) * 6);
                    if (marks.Any(d => d.Kind == PoliceDetailKinds.Computer))
                    {
                        // The monitor at its back, the keyboard before it, the tower at one end, all to the chair.
                        float top = (float)Measure(id).top, end = R(2) < 0.5f ? -1 : 1;
                        Vector3 On(float back, float aside) => At(cx - to.X * back + across.x * aside, top, cz - to.Y * back + across.y * aside);
                        Put("poMonitor", On(0.14f, -end * 0.08f), YawOf(f) + (R(3) - 0.5f) * 20);
                        Put("poKeyboard", On(-0.1f, -end * 0.08f), YawOf(f) + (R(4) - 0.5f) * 14);
                        Put("poComputer", On(0.06f, end * 0.32f), YawOf(f) + (R(5) - 0.5f) * 8);
                    }
                    return true;
                }
                case PoliceDetailKinds.FilingCabinet:
                {
                    // Low, Office's short one; high, the pack's bank of four, or two of its tall ones side by side.
                    if (low) Stand("inFilingLow", Back(k, x, y, f, Deep("inFilingLow")), 0, 0);
                    else if (R(0) < 0.4f) Stand("poFiling2", Back(k, x, y, f, Deep("poFiling2")), 0, 0, (R(1) - 0.5f) * 3);
                    else
                        for (int n = -1; n <= 1; n += 2)
                            Stand("poFiling", Back(k, x, y, f, Deep("poFiling")), n * 0.2f, 0, (R(n + 2) - 0.5f) * 4);
                    return true;
                }
                case PoliceDetailKinds.WeaponLocker:
                {
                    if (low) return false;
                    string id = R(0) < 0.5f ? "poLocker" : "poLocker2";
                    Stand(id, Back(k, x, y, f, Deep(id)), 0, 0);
                    return true;
                }
                case PoliceDetailKinds.WeaponRack:
                {
                    // The rack along its row, its rifles in it (one in seven taken).
                    if (low) return false;
                    var rack = Stand("poRack", Back(k, x, y, f, Deep("poRack")), 0, 0);
                    if (R(0) >= 1 / 7f) Insert(rack, "poRackGuns");
                    return true;
                }
                case PoliceDetailKinds.EvidenceShelf:
                {
                    // The shelving along its row, one of its three loads of boxes on it (one in seven bare).
                    if (low) return false;
                    var shelf = Stand("poShelf", Back(k, x, y, f, Deep("poShelf")), 0, 0);
                    if (R(0) >= 1 / 7f) Insert(shelf, new[] { "poShelfIn", "poShelfIn2", "poShelfIn3" }[Mathf.FloorToInt(R(1) * 3) % 3]);
                    return true;
                }
                case PoliceDetailKinds.GunCase:
                {
                    // A long case on the floor and a shorter one on it, askew; on high cover a third.
                    string[] ids = { "poGunCase", "poGunCase2", "poGunCase3" };
                    string first = ids[R(0) < 0.5f ? 2 : 1];
                    Stand(first, 0, (R(1) - 0.5f) * 0.1f, 0, (R(2) - 0.5f) * 8);
                    float up = (float)Measure(first).top;
                    string second = ids[R(3) < 0.6f ? 0 : 1];
                    Stand(second, (R(4) - 0.5f) * 0.15f, (R(5) - 0.5f) * 0.2f, up, (R(6) - 0.5f) * 24);
                    if (!low) Stand(ids[0], (R(7) - 0.5f) * 0.12f, (R(8) - 0.5f) * 0.2f, up + (float)Measure(second).top, 90 + (R(9) - 0.5f) * 30);
                    return true;
                }
                case PoliceDetailKinds.AmmoCase:
                {
                    // Ammunition boxes stacked two by two, three high (six on high cover), askew, a few gone from the top.
                    float high = (float)Measure("poAmmo").top;
                    int layers = low ? 3 : 6;
                    for (int h = 0; h < layers; h++)
                        for (int n = 0; n < 4; n++)
                        {
                            if (h == layers - 1 && R(10 + n) < 0.4f) continue;
                            float aside = (n % 2 - 0.5f) * 0.32f + (R(20 + h * 4 + n) - 0.5f) * 0.04f, back = (n / 2 - 0.5f) * 0.18f;
                            Stand("poAmmo", back, aside, h * high, (R(40 + h * 4 + n) - 0.5f) * 10);
                        }
                    return true;
                }
                case PoliceDetailKinds.InterrogationTable:
                    // The table alone: its chairs are marks of their own, one either side (interrogationChair, PoliceFitted).
                    Stand("poTable", 0, 0, 0, (R(0) - 0.5f) * 4);
                    return true;
                case PoliceDetailKinds.VendingMachine:
                {
                    if (low) return false;
                    var machine = Stand("poVending", Back(k, x, y, f, Deep("poVending")), 0, 0);
                    Insert(machine, "poVendingIn");
                    return true;
                }
                case PoliceDetailKinds.CellToilet:
                {
                    // The steel toilet with its basin, its back to a solid wall (the side walls between the cells, not the bars).
                    int g = f;
                    for (int n = 0; n < 4 && !SolidAt(k, x - Bastion.Engine.Grid.Dirs4[g].X, y - Bastion.Engine.Grid.Dirs4[g].Y); n++) g = (mark.Facing + n + 1) % 4;
                    var to = Bastion.Engine.Grid.Dirs4[g];
                    float back = Back(k, x, y, g, Deep("poCellToilet"));
                    Put("poCellToilet", At(cx - to.X * back, 0, cz - to.Y * back), YawOf(g));
                    return true;
                }
                case PoliceDetailKinds.LockerRow:
                    Stand("inLockers", Back(k, x, y, f, Deep("inLockers")), 0, 0);
                    return true;
            }
            return false;
        }

        /// <summary>
        /// An object over two tiles that is a police station's (Thing's): a lobby's bench, the pack's seats or benches, or a
        /// cell's bunk, hung on the wall along it; null for any other (Thing draws it).
        /// </summary>
        GameObject PoliceThing(Transform parent, DecorObject thing, float floor)
        {
            if (thing.Kind != "bench" && thing.Kind != "bed") return null;
            int k = Mathf.RoundToInt(floor / MapSpace.Storey), ax = thing.Anchor % s.Width, ay = thing.Anchor / s.Width;
            var mark = MarkAt(k, ax, ay, thing.Kind == "bench" ? PoliceDetailKinds.Bench : PoliceDetailKinds.CellBed);
            if (mark == null || !OfPolice(mark)) return null;
            int x0 = thing.Tiles.Min(t => t % s.Width), x1 = thing.Tiles.Max(t => t % s.Width);
            int y0 = thing.Tiles.Min(t => t / s.Width), y1 = thing.Tiles.Max(t => t / s.Width);
            float cx = (x0 + x1 + 1) / 2f, cz = (y0 + y1 + 1) / 2f, T = MapSpace.Tile;
            var front = Bastion.Engine.Grid.Dirs4[thing.Facing];
            if (thing.Kind == "bench")
            {
                // Its seat the way it faces, its back to the wall behind it.
                string id = new[] { "poSeating", "poBench3", "poBench2" }[Mathf.FloorToInt(MapArt.Hash(x0, y0, 160) * 3) % 3];
                bool wall = thing.Tiles.Any(t => WallAt(k, t % s.Width - front.X, t / s.Width - front.Y));
                float back = wall ? Mathf.Max(0, WallFace - 0.03f - (float)Measure(id).z / 2) : 0;
                return Place(parent, id, new Vector3((cx - front.X * back) * T, floor, -(cz - front.Y * back) * T), YawOf(thing.Facing));
            }
            // The bunk along its two tiles, against the wall on one side of it (its chains' side), off the floor as the pack has it.
            var across = new Vector2Int(-front.Y, front.X);
            int towards = thing.Tiles.Any(t => WallAt(k, t % s.Width + across.x, t / s.Width + across.y)) ? 1 : -1;
            float off = Mathf.Max(0, WallFace - 0.03f - (float)Measure("poBunk").x / 2);
            var bunk = Place(parent, "poBunk", new Vector3((cx + across.x * towards * off) * T, floor + BunkUp, -(cz + across.y * towards * off) * T), YawOf(thing.Facing) + (towards > 0 ? 0 : 180));
            return bunk;
        }

        /// <summary>How high the pack's bunk hangs off the floor (m): its frame's underside, as Synty has it.</summary>
        const float BunkUp = 0.25f;

        /// <summary>
        /// A police station's mark on the floor (Fitted's), each where its tile is floor still: an interrogation table's chair
        /// pulled in to it; the chairs at the desks
        /// pushed in and askew (one in eight knocked over), a corkboard hung on the wall behind it (going with that wall in the
        /// cutaway, as the clinics' signs), a whiteboard as well on an outer wall and on its feet by a partition, the water
        /// cooler against it, evidence bags by a box, the polygraph
        /// on a table against the wall with its chair, a dead officer. False for those Fitted draws as the offices' (blood).
        /// </summary>
        bool PoliceFitted(InteriorDetail d, int i, int k, int b)
        {
            if (!OfPolice(d)) return false;
            float R(int salt) => MapArt.Hash(d.X, d.Y, 170 + salt + k * 7);
            var front = Bastion.Engine.Grid.Dirs4[d.Facing];
            float cx = d.X + 0.5f, cz = d.Y + 0.5f, floor = k * MapSpace.Storey, T = MapSpace.Tile;
            // `back` tiles towards the wall behind it, `aside` across, `up` over the floor.
            GameObject Stand(string id, float back, float aside, float up, float turn)
            {
                var at = new Vector3((cx - front.X * back - front.Y * aside) * T, floor + up * T, -(cz - front.Y * back + front.X * aside) * T);
                var o = Place(props[k], id, at, YawOf(d.Facing) + turn);
                if (o != null) fitted.Add(Tracked(o, i, k));
                return o;
            }
            // Whether the wall behind it is one of the building's outer walls (a partition drops whenever the building is cut).
            bool Outer()
            {
                var site = s.Interiors.FirstOrDefault(p => p?.Kind == InteriorKinds.Police && p.Dressing != null && p.Dressing.Contains(d));
                int wx = d.X - front.X, wy = d.Y - front.Y;
                return site != null && (wx <= site.X || wy <= site.Y || wx >= site.X + site.W - 1 || wy >= site.Y + site.H - 1);
            }
            // Hung on the wall behind it, its bottom `up` tiles over the floor: it goes with that wall in the cutaway.
            GameObject Hang(string id, float up)
            {
                float back = WallFace - 0.01f - (float)Measure(id).z / 2;
                var at = new Vector3((cx - front.X * back) * T, floor + up * T, -(cz - front.Y * back) * T);
                var o = Place(props[k], id, at, YawOf(d.Facing));
                if (o == null) return null;
                if (b >= 0) Cutaway.Current?.TrackFixture(o, b, k, Outer() ? new Vector2(-front.X, -front.Y) : Vector2.zero);
                fitted.Add(o);
                return o;
            }
            switch (d.Kind)
            {
                case PoliceDetailKinds.DeskChair:
                {
                    // (Its back to the room, pushed towards the desk it looks at.)
                    string id = new[] { "poChair", "poChair2", "poChair3", "poChair4" }[Mathf.FloorToInt(R(0) * 4) % 4];
                    var chair = Stand(id, -0.16f, (R(1) - 0.5f) * 0.2f, 0, (R(2) - 0.5f) * 50);
                    if (chair != null && R(3) < 0.125f)
                    {
                        // Knocked over on its back.
                        chair.transform.rotation *= Quaternion.AngleAxis(-80, Vector3.right);
                        chair.transform.position += Vector3.up * 0.12f;
                    }
                    return true;
                }
                case PoliceDetailKinds.Whiteboard:
                    // On an outer wall the pack's, hung; on a partition, which drops whenever the building is cut and took
                    // the board with it, Office's on its feet before it, as the offices' (Producto, 17/11).
                    if (WallAt(k, d.X - front.X, d.Y - front.Y) && Outer()) Hang("poWhiteboard", 0.62f);
                    else Stand("inWhiteboard", 0.3f, (R(0) - 0.5f) * 0.1f, 0, (R(1) - 0.5f) * 6);
                    return true;
                case PoliceDetailKinds.Corkboard:
                    if (WallAt(k, d.X - front.X, d.Y - front.Y))
                    {
                        var board = Hang(R(0) < 0.5f ? "poCork" : "poCork2", 0.62f);
                        if (R(1) < 0.85f) Insert(board, new[] { "poCorkIn", "poCorkIn2", "poCorkIn3" }[Mathf.FloorToInt(R(2) * 3) % 3]);
                    }
                    return true;
                case PoliceDetailKinds.InterrogationChair:
                    // The pack's plain chair, as the table's, pulled in to it, face to face with the other.
                    Stand("poChair3", -0.32f, (R(0) - 0.5f) * 0.12f, 0, (R(1) - 0.5f) * 16);
                    return true;
                case PoliceDetailKinds.WaterCooler:
                    Stand("poCooler", Back(k, d.X, d.Y, d.Facing, (float)Measure("poCooler").z), (R(0) - 0.5f) * 0.3f, 0, (R(1) - 0.5f) * 10);
                    return true;
                case PoliceDetailKinds.EvidenceBag:
                {
                    // A box of them, and bags about it on the floor, lying.
                    Stand("poEvidenceBox", (R(0) - 0.5f) * 0.3f, (R(1) - 0.5f) * 0.3f, 0, R(2) * 360);
                    string[] bags = { "poBag", "poBag2", "poBag3" };
                    for (int n = 0, of = 2 + Mathf.FloorToInt(R(3) * 2); n < of; n++)
                    {
                        float turn = R(10 + n) * 360, a = n * 2.4f + R(4) * 6;
                        string id = bags[Mathf.FloorToInt(R(20 + n) * 3) % 3];
                        var bag = Stand(id, Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, 0, turn);
                        if (bag == null) continue;
                        // (On its back: its thickness, half of it under the floor as it turns, brought up.)
                        bag.transform.rotation *= Quaternion.AngleAxis(-90, Vector3.right);
                        bag.transform.position += Vector3.up * ((float)Measure(id).z * T / 2 + 0.005f);
                        foreach (var r in bag.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    return true;
                }
                case PoliceDetailKinds.Polygraph:
                {
                    // On a table against the wall, the recorder by it, a chair before it.
                    float back = Back(k, d.X, d.Y, d.Facing, (float)Measure("poSideTable").z);
                    Stand("poSideTable", back, 0, 0, 0);
                    float top = (float)Measure("poSideTable").top;
                    Stand("poPolygraph", back, -0.12f, top, (R(0) - 0.5f) * 16);
                    Stand("poRecorder", back, 0.25f, top, (R(1) - 0.5f) * 30);
                    Stand("poChair3", back - 0.55f, (R(2) - 0.5f) * 0.2f, 0, 180 + (R(3) - 0.5f) * 30);
                    return true;
                }
                case PoliceDetailKinds.CorpseOfficer:
                {
                    var at = Matrix4x4.TRS(new Vector3(cx, floor / T, cz), Quaternion.AngleAxis(R(0) * 360, Vector3.up), Vector3.one);
                    var body = PlaceGame(props[k], R(1) < 0.5f ? "corpseOfficer" : "corpseOfficer2", at);
                    if (body != null) fitted.Add(Tracked(body, i, k));
                    return true;
                }
            }
            return false;
        }
    }
}
