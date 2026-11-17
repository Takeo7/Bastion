using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The runs' maps that are one building (H9, .dev-snapshots/gameplay/runs/H9-INTERIORES.md; GameState.Interiors: an office
    /// block, a factory's shed): a model on each of the engine's marks, as the clinics' dressing (Dressing). What blocks or
    /// covers is a tile's, its decor; a mark on it says which model that furniture is (Furniture, from Decor: a desk with its
    /// computer, a meeting table, filing cabinets, shelving stocked, pallets, a forklift, a workbench, lockers; a container is
    /// its block's, DrawBlock). A mark on the floor is scenery and never in the way (Fitted: a chair at its desk, a
    /// whiteboard, a water cooler, a copier or a plant against the wall, a body baked lying, papers and blood as
    /// MedicalDetails' shapes). A crate's and a drum's are drawn as any factory's (Stacked, the sets). A police station's
    /// marks are POLYGON Police Station's (MapBuilder.Police.cs).
    /// </summary>
    public sealed partial class MapBuilder
    {
        /// <summary>The marks on furniture (H9 §1–2): the rest lie on the floor.</summary>
        static readonly HashSet<string> OnFurniture = new HashSet<string>
        {
            OfficeDetailKinds.Desk, OfficeDetailKinds.Computer, OfficeDetailKinds.MeetingTable, OfficeDetailKinds.FilingCabinet,
            ShedDetailKinds.Shelving, ShedDetailKinds.Pallet, ShedDetailKinds.Crate, ShedDetailKinds.Forklift, ShedDetailKinds.Workbench,
            ShedDetailKinds.Drum, ShedDetailKinds.Container, ShedDetailKinds.LockerRow,
            // (A police station's, H9 §3: MapBuilder.Police.cs. A counter's is the kit's wall with its window.)
            PoliceDetailKinds.CellBed, PoliceDetailKinds.CellToilet, PoliceDetailKinds.Counter, PoliceDetailKinds.Bench, PoliceDetailKinds.WeaponLocker,
            PoliceDetailKinds.WeaponRack, PoliceDetailKinds.GunCase, PoliceDetailKinds.AmmoCase, PoliceDetailKinds.EvidenceShelf,
            PoliceDetailKinds.InterrogationTable, PoliceDetailKinds.VendingMachine,
        };

        /// <summary>Those Furniture draws itself (a computer rides on its desk; a crate and a drum are a factory's; a container its block's).</summary>
        static readonly HashSet<string> DrawnHere = new HashSet<string>
        {
            OfficeDetailKinds.Desk, OfficeDetailKinds.MeetingTable, OfficeDetailKinds.FilingCabinet, ShedDetailKinds.Shelving, ShedDetailKinds.Pallet,
            ShedDetailKinds.Forklift, ShedDetailKinds.Workbench, ShedDetailKinds.LockerRow,
            // (A police station's bed and bench are objects over two tiles: Thing's, PoliceThing.)
            PoliceDetailKinds.CellToilet, PoliceDetailKinds.WeaponLocker, PoliceDetailKinds.WeaponRack, PoliceDetailKinds.GunCase, PoliceDetailKinds.AmmoCase,
            PoliceDetailKinds.EvidenceShelf, PoliceDetailKinds.InterrogationTable, PoliceDetailKinds.VendingMachine,
        };

        /// <summary>The map's marks by storey and tile (Key), and the list they were read from.</summary>
        Dictionary<long, List<InteriorDetail>> marksOn;
        List<InteriorSite> marksFrom;

        /// <summary>The marks on tile i of storey k; null for none.</summary>
        List<InteriorDetail> MarksOn(int k, int i)
        {
            if (s.Interiors == null || s.Interiors.Count == 0) return null;
            if (marksOn == null || marksFrom != s.Interiors)
            {
                marksFrom = s.Interiors;
                marksOn = new Dictionary<long, List<InteriorDetail>>();
                foreach (var d in s.Interiors.Where(site => site?.Dressing != null).SelectMany(site => site.Dressing))
                {
                    if (d.X < 0 || d.Y < 0 || d.X >= s.Width || d.Y >= s.Height) continue;
                    // (A mark over several tiles, InteriorDetail.Length: an office's meeting table, on each of them.)
                    foreach (var (x, y) in TilesOf(d))
                    {
                        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) continue;
                        long key = Key(d.Z, y * s.Width + x);
                        if (!marksOn.TryGetValue(key, out var list)) marksOn[key] = list = new List<InteriorDetail>();
                        list.Add(d);
                    }
                }
            }
            return marksOn.TryGetValue(Key(k, i), out var on) ? on : null;
        }

        /// <summary>
        /// The tiles a mark covers (InteriorDetail.Length, Gameplay 17/11): its own (X, Y) and on along the line across the
        /// way it faces, x facing north or south, y facing east or west; null, its own alone.
        /// </summary>
        static IEnumerable<(int x, int y)> TilesOf(InteriorDetail d)
        {
            bool alongX = d.Facing == 0 || d.Facing == 2;
            for (int n = 0; n < System.Math.Max(1, d.Length ?? 1); n++) yield return alongX ? (d.X + n, d.Y) : (d.X, d.Y + n);
        }

        /// <summary>Whether tile i of storey k has a mark that Furniture draws.</summary>
        bool Marked(int k, int i) => MarksOn(k, i)?.Any(d => DrawnHere.Contains(d.Kind)) == true || PlazaMarked(k, i);

        /// <summary>A mark of `kind` on tile (x, y) of storey k.</summary>
        InteriorDetail MarkAt(int k, int x, int y, string kind) =>
            x < 0 || y < 0 || x >= s.Width || y >= s.Height ? null : MarksOn(k, y * s.Width + x)?.FirstOrDefault(d => d.Kind == kind);

        /// <summary>A facing as Place's yaw (0.10.1's turn, counter-clockwise from above in tiles with y south; Unity's the other way round).</summary>
        static float YawOf(int facing) => -((4 - facing) % 4) * 90;

        /// <summary>
        /// The furniture on tile i of storey k as its mark says (Decor, past what a search leaves before it); false if no mark
        /// of its own is there, and the tile is drawn as any other.
        /// </summary>
        bool Furniture(int k, int i, bool low, float floor)
        {
            var marks = MarksOn(k, i);
            // (A shopping plaza's, its own: MapBuilder.Plaza.)
            if (PlazaFurniture(k, i, low, floor, marks)) return true;
            var mark = marks?.FirstOrDefault(d => DrawnHere.Contains(d.Kind));
            if (mark == null) return false;
            if (OfPolice(mark)) return PoliceFurniture(k, i, mark, marks, low, floor);
            int x = i % s.Width, y = i / s.Width;
            float cx = x + 0.5f, cz = y + 0.5f, T = MapSpace.Tile;
            // (0.10.1's frame in tiles to Unity's: z the other way.)
            Vector3 At(float ax, float ay, float az) => new Vector3(ax * T, floor + ay * T, -az * T);
            float R(int salt) => MapArt.Hash(x, y, 90 + salt + k * 7);
            var tile = new[] { i };
            GameObject Put(string id, float ax, float ay, float az, float yaw)
            {
                var o = Place(props[k], id, At(ax, ay, az), yaw);
                Own(o, k, tile);
                return o;
            }
            var front = Bastion.Engine.Grid.Dirs4[mark.Facing];
            // Across the way it faces (0.10.1's frame: x east, y south).
            var side = new Vector2(-front.Y, front.X);
            switch (mark.Kind)
            {
                case OfficeDetailKinds.Desk:
                {
                    // Its drawers to the chair beside it that looks at it (the engine's, any side free); without one, its mark's way.
                    int f = mark.Facing;
                    for (int n = 0; n < 4; n++)
                    {
                        var d = Bastion.Engine.Grid.Dirs4[n];
                        if (MarkAt(k, x + d.X, y + d.Y, OfficeDetailKinds.DeskChair)?.Facing == (n + 2) % 4) f = n;
                    }
                    var to = Bastion.Engine.Grid.Dirs4[f];
                    string id = R(0) < 0.5f ? "officeDesk" : "officeDesk2";
                    Put(id, cx, 0, cz, YawOf(f) + (R(1) - 0.5f) * 6);
                    // The computer on it, at the back, its screen to the chair.
                    if (marks.Any(d => d.Kind == OfficeDetailKinds.Computer))
                    {
                        float top = (float)Measure(id).top;
                        float along = (R(2) - 0.5f) * 0.3f;
                        Put(R(3) < 0.5f ? "inComputer" : "inComputer2", cx - to.X * 0.1f - to.Y * along, top, cz - to.Y * 0.1f + to.X * along, YawOf(f) + (R(4) - 0.5f) * 24);
                    }
                    return true;
                }
                case OfficeDetailKinds.MeetingTable when (mark.Length ?? 1) > 1:
                {
                    // Over its tiles (InteriorDetail.Length: two desks side by side, Gameplay 17/11), drawn once from its own,
                    // its length across the way it faces: Office's conference tables (4 m) brought to the tiles' length at
                    // their own height, no deeper than a tile; a chair at each of its tiles on either side, where the floor
                    // beside it is free.
                    if (x != mark.X || y != mark.Y) return true;
                    var covered = TilesOf(mark).Select(p => p.y * s.Width + p.x).ToList();
                    int length = covered.Count;
                    float mx = cx + (mark.Facing == 0 || mark.Facing == 2 ? (length - 1) / 2f : 0), mz = cz + (mark.Facing == 0 || mark.Facing == 2 ? 0 : (length - 1) / 2f);
                    string id = R(0) < 0.5f ? "inMeeting2" : R(0) < 0.75f ? "inMeeting" : "inMeeting3";
                    var table = Place(props[k], id, At(mx, 0, mz), YawOf(mark.Facing));
                    Own(table, k, covered);
                    var size = Measure(id);
                    float deep = Mathf.Min((float)size.z, 0.92f);
                    if (table != null && size.x > 0 && size.z > 0) table.transform.localScale = new Vector3((length - 0.1f) / (float)size.x, 1, deep / (float)size.z);
                    var tiles = TilesOf(k);
                    var decor = DecorOf(k);
                    bool Free(int tx, int ty)
                    {
                        int j = ty * s.Width + tx;
                        return tx >= 0 && ty >= 0 && tx < s.Width && ty < s.Height && j < tiles.Count && tiles[j] == TileKinds.Floor && (j >= decor.Count || decor[j] == 0);
                    }
                    int salt = 5;
                    foreach (var (tx, ty) in TilesOf(mark))
                        for (int n = -1; n <= 1; n += 2)
                        {
                            float r = R(salt++), u = (R(salt++) - 0.5f) * 0.2f;
                            if (!Free(tx + front.X * n, ty + front.Y * n) || r > 0.85f) continue;
                            float out_ = deep / 2 + 0.14f;
                            var chair = Place(props[k], R(salt++) < 0.5f ? "inChair" : "inChair2", At(tx + 0.5f + front.X * n * out_ + side.x * u, 0, ty + 0.5f + front.Y * n * out_ + side.y * u),
                                YawOf((mark.Facing + (n > 0 ? 2 : 0)) % 4) + (R(salt++) - 0.5f) * 40);
                            Own(chair, k, covered);
                        }
                    return true;
                }
                case OfficeDetailKinds.MeetingTable:
                {
                    // Office's conference tables are four metres long: brought into the tile, its length across the way it
                    // faces, a chair either side tucked in.
                    string id = R(0) < 0.34f ? "inMeeting" : R(0) < 0.67f ? "inMeeting2" : "inMeeting3";
                    var table = Put(id, cx, 0, cz, YawOf(mark.Facing));
                    var size = Measure(id);
                    if (table != null && size.x > 0 && size.z > 0) table.transform.localScale = new Vector3(0.94f / (float)size.x, 1, 0.6f / (float)size.z);
                    for (int n = -1; n <= 1; n += 2)
                    {
                        float u = (R(5 + n) - 0.5f) * 0.3f;
                        Put(R(7 + n) < 0.5f ? "inChair" : "inChair2", cx + front.X * n * 0.42f + side.x * u, 0, cz + front.Y * n * 0.42f + side.y * u, YawOf((mark.Facing + (n > 0 ? 2 : 0)) % 4) + (R(9 + n) - 0.5f) * 40);
                    }
                    return true;
                }
                case OfficeDetailKinds.FilingCabinet:
                    // Back to the wall behind it: low, a short cabinet; high, two of the offices' tall ones side by side (MapArt's officeCabinet).
                    if (low) Put("inFilingLow", cx - front.X * 0.12f, 0, cz - front.Y * 0.12f, YawOf(mark.Facing));
                    else
                        for (int n = -1; n <= 1; n += 2)
                            Put("officeCabinet", cx - front.X * 0.12f + side.x * n * 0.18f, 0, cz - front.Y * 0.12f + side.y * n * 0.18f, YawOf(mark.Facing) + (R(n + 2) - 0.5f) * 4);
                    return true;
                case ShedDetailKinds.Shelving:
                {
                    // Military's racking, along its row, and what is left on it.
                    var rack = Put(low ? "inShelvesLow" : "inShelves", cx, 0, cz, YawOf(mark.Facing));
                    if (rack != null) Stock(k, i, cx, cz, floor, mark.Facing, low);
                    return true;
                }
                case ShedDetailKinds.Pallet:
                {
                    // Stacked as a factory's (Stacked), the pallets of Apocalypse's three; on a tall stack now and then a box.
                    var ids = new[] { "pallet", "inPallet2", "inPallet3" };
                    int of = low ? 3 : 6;
                    for (int n = 0; n < of; n++)
                    {
                        float P(int salt) => MapArt.Hash(x, y, 40 + n * 3 + salt) - 0.5f;
                        Put(ids[Mathf.FloorToInt(R(20 + n) * 3) % 3], cx + P(0) * 0.06f, n * 0.19f, cz + P(1) * 0.06f, YawOf(mark.Facing) - P(2) * 0.12f * Mathf.Rad2Deg);
                    }
                    if (!low && R(30) < 0.5f) Put("inShelfBox", cx + (R(31) - 0.5f) * 0.3f, of * 0.19f, cz + (R(32) - 0.5f) * 0.3f, R(33) * 360);
                    return true;
                }
                case ShedDetailKinds.Forklift:
                    Put("inForklift", cx, 0, cz, YawOf(mark.Facing) + (R(0) - 0.5f) * 14);
                    return true;
                case ShedDetailKinds.Workbench:
                {
                    // Along its run (the generator's machines stand in rows a tile wide): its back panel down the row, the
                    // whole row the same way round, as benches along a wall.
                    int f = mark.Facing;
                    bool northSouth = MarkAt(k, x, y - 1, ShedDetailKinds.Workbench) != null || MarkAt(k, x, y + 1, ShedDetailKinds.Workbench) != null;
                    bool eastWest = MarkAt(k, x - 1, y, ShedDetailKinds.Workbench) != null || MarkAt(k, x + 1, y, ShedDetailKinds.Workbench) != null;
                    if (northSouth && !eastWest) f = MapArt.Hash(x, 0, 96) < 0.5f ? 1 : 3;
                    else if (eastWest && !northSouth) f = MapArt.Hash(0, y, 97) < 0.5f ? 0 : 2;
                    Put("inWorkbench", cx, 0, cz, YawOf(f));
                    // What makes it a bench to work at (bare, from above it read as a pew): a tool board on its back
                    // panel, a vice at one end of its top (0.58 tiles up), a toolbox or a power tool at the other.
                    var to = Bastion.Engine.Grid.Dirs4[f];
                    var across = new Vector2(-to.Y, to.X);
                    float end = R(1) < 0.5f ? -1 : 1;
                    Vector3 OnIt(float back, float aside, float up) => At(cx - to.X * back + across.x * aside, up, cz - to.Y * back + across.y * aside);
                    GameObject PutAt(string id, Vector3 at, float yaw)
                    {
                        var o = Place(props[k], id, at, yaw);
                        Own(o, k, tile);
                        return o;
                    }
                    if (R(2) < 0.75f) PutAt("inToolboard", OnIt(0.16f, 0, 0.68f), YawOf(f) + 180);
                    if (R(3) < 0.7f) PutAt("inVice", OnIt(-0.12f, end * 0.32f, 0.58f), YawOf(f) + (R(4) - 0.5f) * 20);
                    float pick = R(5);
                    if (pick < 0.35f) PutAt("inToolbox", OnIt(0.05f, -end * 0.25f, 0.58f), YawOf(f) + 90 + (R(6) - 0.5f) * 30);
                    else if (pick < 0.55f) PutAt("inDrill", OnIt(0, -end * 0.2f, 0.58f), R(7) * 360);
                    else if (pick < 0.7f) PutAt("inGrinder", OnIt(0, -end * 0.2f, 0.58f), R(7) * 360);
                    return true;
                }
                case ShedDetailKinds.LockerRow:
                    Put("inLockers", cx - front.X * 0.12f, 0, cz - front.Y * 0.12f, YawOf(mark.Facing));
                    return true;
            }
            return false;
        }

        /// <summary>
        /// What is left on a rack (Furniture's shelving, `facing` its row's across): on the floor under it and on its boards,
        /// by the tile, a box or two or nothing, a little askew. Military's racking has its boards at 0.67 and 1.21 m and its
        /// top at 1.74 (InterioresLab.Superficies): stood 1.35 tiles high, 0.52 and 0.94 tiles up; the low one, as it is,
        /// 0.45.
        /// </summary>
        void Stock(int k, int i, float cx, float cz, float floor, int facing, bool low)
        {
            int x = i % s.Width, y = i / s.Width;
            float T = MapSpace.Tile;
            float R(int salt) => MapArt.Hash(x, y, 120 + salt + k * 7);
            var front = Bastion.Engine.Grid.Dirs4[facing];
            var along = new Vector2(-front.Y, front.X);
            var boards = low ? new[] { 0f, 0.45f } : new[] { 0f, 0.52f, 0.94f };
            var ids = new[] { "inShelfBox", "inShelfBox2", "inShelfBox3" };
            // Some racks stripped bare, the rest as full as they were left.
            float full = R(0) < 0.15f ? 0 : 0.45f + R(1) * 0.45f;
            int salt = 2;
            foreach (float up in boards)
                for (int slot = -1; slot <= 1; slot += 2)
                {
                    if (R(salt++) >= full) continue;
                    float u = slot * 0.24f + (R(salt++) - 0.5f) * 0.1f, v = (R(salt++) - 0.5f) * 0.16f;
                    var at = new Vector3((cx + along.x * u + front.X * v) * T, floor + (up + 0.02f) * T, -(cz + along.y * u + front.Y * v) * T);
                    Own(Place(props[k], ids[Mathf.FloorToInt(R(salt++) * 3) % 3], at, YawOf(facing) + (R(salt++) - 0.5f) * 30), k, new[] { i });
                }
        }

        /// <summary>A shed's container (H9, ShedDetailKinds.Container: its mark on its block's first tile), Apocalypse's; null if none is marked there.</summary>
        string ContainerMarked(float floor, int x0, int y0)
        {
            int k = Mathf.RoundToInt(floor / MapSpace.Storey);
            if (MarkAt(k, x0, y0, ShedDetailKinds.Container) == null) return null;
            return MapArt.Hash(x0, y0, 98) < 0.5f ? "inContainer" : "inContainer2";
        }

        /// <summary>The floor's scenery as drawn (Fitted), and what it was drawn from: made again when that changes.</summary>
        readonly List<GameObject> fitted = new List<GameObject>();
        long fittedKey = long.MinValue;

        /// <summary>What Fitted draws from, hashed: each mark on the floor, its tile and what that tile is now.</summary>
        long FittedKey()
        {
            long h = 23;
            foreach (var d in s.Interiors.Where(site => site?.Dressing != null).SelectMany(site => site.Dressing))
            {
                if (OnFurniture.Contains(d.Kind) || d.X < 0 || d.Y < 0 || d.X >= s.Width || d.Y >= s.Height || d.Z < 0 || d.Z >= Storeys.Length) continue;
                int i = d.Y * s.Width + d.X;
                var tiles = TilesOf(d.Z);
                unchecked
                {
                    h = h * 31 + d.Z * 7919 + i;
                    h = h * 31 + (i < tiles.Count ? tiles[i]?.GetHashCode() ?? 1 : 0);
                }
            }
            return h;
        }

        /// <summary>
        /// The floor's scenery of a map that is one building (H9: OfficeDetailKinds' and ShedDetailKinds' marks not on
        /// furniture), each where its tile is floor still: what stands against the wall at its back (the way it faces is
        /// away from it), the desk's chair pushed in a little and askew (one in eight knocked over), a body lying, the papers
        /// and the blood flat (MedicalDetails' shapes, merged by storey and building, as the clinics'). Made again whole when
        /// the map changes under it.
        /// </summary>
        void Fitted()
        {
            // (A shopping plaza's scenery, its own: MapBuilder.Plaza.)
            PlazaScenery();
            if ((s.Interiors == null || s.Interiors.Count == 0) && fitted.Count == 0) return;
            long drawnFrom = s.Interiors == null ? 0 : FittedKey();
            if (drawnFrom == fittedKey && fitted.All(o => o != null)) return;
            fittedKey = drawnFrom;
            foreach (var o in fitted) DestroyNow(o);
            fitted.Clear();
            if (s.Interiors == null) return;
            var faces = new Dictionary<(int k, int b), List<(MedicalDetails.Face face, Matrix4x4 at)>>();
            float storey = MapSpace.Storey / MapSpace.Tile, T = MapSpace.Tile;
            foreach (var d in s.Interiors.Where(site => site?.Dressing != null).SelectMany(site => site.Dressing))
            {
                if (OnFurniture.Contains(d.Kind) || d.X < 0 || d.Y < 0 || d.X >= s.Width || d.Y >= s.Height || d.Z < 0 || d.Z >= Storeys.Length) continue;
                int i = d.Y * s.Width + d.X, k = d.Z;
                var tiles = TilesOf(k);
                if (i >= tiles.Count || tiles[i] != TileKinds.Floor) continue;
                int b = i < plan.Owner.Length ? plan.Owner[i] : -1;
                if (PoliceFitted(d, i, k, b)) continue;
                float R(int salt) => MapArt.Hash(d.X, d.Y, 110 + salt + k * 7);
                var front = Bastion.Engine.Grid.Dirs4[d.Facing];
                float cx = d.X + 0.5f, cz = d.Y + 0.5f, floor = k * MapSpace.Storey;
                // Back `back` tiles towards the wall behind it, `aside` across.
                GameObject Stand(string id, float back, float aside, float turn)
                {
                    var at = new Vector3((cx - front.X * back - front.Y * aside) * T, floor, -(cz - front.Y * back + front.X * aside) * T);
                    var o = Place(props[k], id, at, YawOf(d.Facing) + turn);
                    if (o != null) fitted.Add(Tracked(o, i, k));
                    return o;
                }
                switch (d.Kind)
                {
                    case OfficeDetailKinds.DeskChair:
                    {
                        // (Its back to the room, pushed towards the desk it looks at.)
                        var chair = Stand(R(0) < 0.5f ? "inChair" : "inChair2", -0.16f, (R(1) - 0.5f) * 0.2f, (R(2) - 0.5f) * 50);
                        if (chair != null && R(3) < 0.125f)
                        {
                            // Knocked over on its back.
                            chair.transform.rotation *= Quaternion.AngleAxis(-80, Vector3.right);
                            chair.transform.position += Vector3.up * 0.12f;
                        }
                        break;
                    }
                    case OfficeDetailKinds.Whiteboard:
                        Stand("inWhiteboard", 0.3f, (R(0) - 0.5f) * 0.1f, (R(1) - 0.5f) * 6);
                        break;
                    case OfficeDetailKinds.WaterCooler:
                        Stand("inCooler", 0.3f, (R(0) - 0.5f) * 0.3f, (R(1) - 0.5f) * 10);
                        break;
                    case OfficeDetailKinds.Copier:
                        Stand(R(0) < 0.5f ? "inCopier" : "inCopier2", 0.24f, (R(1) - 0.5f) * 0.2f, (R(2) - 0.5f) * 6);
                        break;
                    case OfficeDetailKinds.Plant:
                        Stand(new[] { "inPlant", "inPlant2", "inPlant3", "inPlant4" }[Mathf.FloorToInt(R(0) * 4) % 4], 0.22f, (R(1) - 0.5f) * 0.3f, R(2) * 360);
                        break;
                    case OfficeDetailKinds.CorpseWorker:
                    {
                        var at = Matrix4x4.TRS(new Vector3(cx, k * storey, cz), Quaternion.AngleAxis(R(0) * 360, Vector3.up), Vector3.one);
                        var body = PlaceGame(props[k], R(1) < 0.5f ? "corpseWorker" : "corpseWorker2", at);
                        if (body != null) fitted.Add(Tracked(body, i, k));
                        break;
                    }
                    case OfficeDetailKinds.ScatteredPapers:
                    case OfficeDetailKinds.BloodPool:
                    {
                        var at = Matrix4x4.TRS(new Vector3(cx, k * storey, cz), Quaternion.AngleAxis((4 - d.Facing) % 4 * 90f, Vector3.up), Vector3.one);
                        if (!faces.TryGetValue((k, b), out var list)) faces[(k, b)] = list = new List<(MedicalDetails.Face, Matrix4x4)>();
                        foreach (var f in MedicalDetails.Of(d.Kind, d.Variant ?? Mathf.FloorToInt(R(4) * 8))) list.Add((f, at));
                        break;
                    }
                }
            }
            var material = Resources.Load<Material>("FrameShape");
            foreach (var group in faces)
            {
                var o = Shapes($"Vestido de dentro {group.Key.b}", props[group.Key.k], group.Value, material, false);
                if (o == null) continue;
                if (group.Key.k > 0 && group.Key.b >= 0) Cutaway.Current?.Track(o, group.Key.b, group.Key.k);
                fitted.Add(o);
            }
        }
    }
}
