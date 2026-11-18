using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The mission's marks on the map (overlays.ts showEvacZone, setBeacons, setItem, setFlares; UI's): the evacuation zone
    /// washed green and beating; a beam of light standing over what is to be reached (the equipment to fix, the one to
    /// rescue, the alarm), with a ring at its foot; the item lying loose, a glowing core under its beam; and where more
    /// are coming, a red flare's smoke and a ring that keeps spreading. Made again only when what they mark changes.
    /// None of them while La Lata leaves (Cinematic: Arturo, 31/10 #1).
    /// </summary>
    public sealed class MissionMarks : MonoBehaviour
    {
        const float T = Board.Tile;
        /// <summary>How high a beam goes (overlays.ts GUIDE_HEIGHT), from just over a figure's head.</summary>
        const float BeamTop = 40 * T;
        const float BeamFoot = 1.6f * T;

        static readonly Color32 Evac = MoveOverlay.Hex(0x4dff9a);
        static readonly Color32 Flare = MoveOverlay.Hex(0xff4a3a);
        /// <summary>
        /// A run's Rescate's zone to look in (N2): white, apart from the evacuation's green (Mixta: no amber, the orange is a
        /// player's; Ana, 7/11 #6).
        /// </summary>
        static readonly Color32 Search = MoveOverlay.Hex(0xffffff);

        readonly MeshBuilder build = new MeshBuilder();
        MeshFilter zone;
        Material zoneMaterial;
        MeshFilter search;
        /// <summary>
        /// The rooms still to search, their floor washed and their border (U18, Mayús held; CombatController.RoomsToSearch):
        /// tested against what is in front in the game's view, over everything from above.
        /// </summary>
        MeshFilter roomsFill, roomsShade, roomsLine, roomsFillOver, roomsShadeOver, roomsLineOver;
        string roomsKey = "";
        MeshFilter beams;
        MeshFilter flares;
        Material flareRing;
        MeshFilter flareRings;
        Transform core;
        /// <summary>What holds them all: off while the event player films La Lata leaving.</summary>
        Transform shown;
        string key;

        void Awake()
        {
            shown = new GameObject("Marcas").transform;
            shown.SetParent(transform, false);
            zone = Layer("Evacuación", 3097, false);
            zoneMaterial = zone.GetComponent<MeshRenderer>().sharedMaterial;
            search = Layer("Búsqueda", 3096, false);
            roomsFill = Layer("Habitaciones por registrar", 3093, false);
            roomsShade = Layer("Habitaciones por registrar (sombra)", 3094, false, true);
            roomsLine = Layer("Habitaciones por registrar (borde)", 3095, false);
            roomsFillOver = Layer("Habitaciones por registrar, desde arriba", 3093, true);
            roomsShadeOver = Layer("Habitaciones por registrar, desde arriba (sombra)", 3094, true, true);
            roomsLineOver = Layer("Habitaciones por registrar, desde arriba (borde)", 3095, true);
            beams = Layer("Balizas", 3108, false);
            flares = Layer("Bengalas", 3098, false);
            flareRings = Layer("Bengalas (anillos)", 3099, false);
            flareRing = flareRings.GetComponent<MeshRenderer>().sharedMaterial;
            // The item: a small core glowing, turning and bobbing (overlays.ts item).
            var cube = Layer("Objeto", 3100, false);
            build.Clear();
            var h = 0.17f * T;
            var c = new Color32(0x4d, 0xff, 0x9a, 0xe6);
            var faces = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            foreach (var n in faces)
            {
                var u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up) * h;
                var v = Vector3.Cross(n, u.normalized) * h;
                var o = n * h;
                build.Quad(o - u - v, o + u - v, o + u + v, o - u + v, c);
            }
            cube.sharedMesh = build.Build(null);
            core = cube.transform;
            core.gameObject.SetActive(false);
        }

        MeshFilter Layer(string name, int queue, bool overAll, bool shade = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(shown, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = MeshBuilder.Material(queue, overAll, shade);
            return go.AddComponent<MeshFilter>();
        }

        /// <summary>As a cutscene (La Lata leaving: Arturo, 31/10 #1), none of the mission's marks; back as it ends.</summary>
        public void Cinematic(bool on)
        {
            if (shown != null && shown.gameObject.activeSelf == on) shown.gameObject.SetActive(!on);
        }

        void Update()
        {
            var time = Time.time;
            var c = zoneMaterial.color;
            c.a = (0.18f + Mathf.Sin(time * 2.2f) * 0.06f) / 0.22f;
            zoneMaterial.color = c;
            // A flare's ring spreads over and over, fading.
            var k = time * 0.9f % 1;
            flareRings.transform.localScale = Vector3.one;
            var f = flareRing.color;
            f.a = 0.8f * (1 - k);
            flareRing.color = f;
            flareSpread = 0.6f + k * 1.6f;
            if (flareAt.Count > 0) DrawFlareRings();
            if (core.gameObject.activeSelf)
            {
                core.localRotation = Quaternion.Euler(0, time * 1.2f * Mathf.Rad2Deg, 0);
                core.localPosition = coreAt + Vector3.up * ((0.45f + Mathf.Sin(time * 2.4f) * 0.06f) * T);
            }
        }

        /// <summary>
        /// A run's Rescate's zone (N2, 17/11): its ground tiles washed faintly, those whose middle is within the circle, and
        /// the circle itself drawn round them, on the ground floor.
        /// </summary>
        void SearchZone(GameState s, SearchArea area)
        {
            build.Clear();
            if (area != null)
            {
                var r = (float)area.Radius;
                var inset = 0.02f * T;
                var reach = Mathf.CeilToInt(r);
                for (var y = area.Center.Y - reach; y <= area.Center.Y + reach; y++)
                    for (var x = area.Center.X - reach; x <= area.Center.X + reach; x++)
                    {
                        if (!Bastion.Engine.Grid.InBounds(s, x, y) || (x - area.Center.X) * (x - area.Center.X) + (y - area.Center.Y) * (y - area.Center.Y) > r * r) continue;
                        var floor = Board.TileToWorld(s, new Pos { X = x, Y = y }).y + 0.012f * T;
                        var a = Board.Corner(x, y, floor);
                        build.Flat(a.x + inset, a.z - inset, a.x + T - inset, a.z - T + inset, floor, new Color32(Search.r, Search.g, Search.b, 0x14));
                    }
                var middle = Board.TileToWorld(s, new Pos { X = area.Center.X, Y = area.Center.Y });
                build.Arc(middle + Vector3.up * (0.03f * T), (r + 0.4f) * T, (r + 0.5f) * T, 0, Mathf.PI * 2, 96, new Color32(Search.r, Search.g, Search.b, 0xc8));
            }
            search.sharedMesh = build.Build(search.sharedMesh);
        }

        float flareSpread = 1;
        readonly List<Vector3> flareAt = new List<Vector3>();
        Vector3 coreAt;

        void DrawFlareRings()
        {
            build.Clear();
            foreach (var p in flareAt) build.Arc(p + Vector3.up * (0.06f * T), 0.8f * T * flareSpread, T * flareSpread, 0, Mathf.PI * 2, 40, MoveOverlay.Hex(0xffffff));
            flareRings.sharedMesh = build.Build(flareRings.sharedMesh);
        }

        /// <summary>
        /// The rooms still to search with Mayús held (U18, Arturo 18/11: «que dándole al shift veas las habitaciones que
        /// faltan por lootear también»; cuadro 73, the floor washed): each room's floor washed white, a little short of its
        /// tiles' edges, and its border drawn round it, white over a dark line (it reads on white tiles and on dark ones,
        /// apart from the walking range's blue border and from the light through a window, which has no edges). The one the
        /// picked soldier stands in and can search now (`lit`) brighter and its border thicker. From above (`over`) over
        /// everything, roofs too: straight down, a room's own place on the roof over it. Made again only when they change.
        /// </summary>
        public void ShowRooms(GameState s, IList<(List<Pos> tiles, bool lit)> list, bool over)
        {
            var now = list == null ? "" : over + "|" + string.Join("|", list.Select(r => $"{r.tiles[0].X},{r.tiles[0].Y},{r.tiles[0].Z}:{r.tiles.Count}:{r.lit}"));
            if (now == roomsKey) return;
            roomsKey = now;
            var (fill, shade, line) = over ? (roomsFillOver, roomsShadeOver, roomsLineOver) : (roomsFill, roomsShade, roomsLine);
            var (offFill, offShade, offLine) = over ? (roomsFill, roomsShade, roomsLine) : (roomsFillOver, roomsShadeOver, roomsLineOver);
            foreach (var f in new[] { offFill, offShade, offLine }) f.sharedMesh = null;
            // (Inside the room's edge, apart from the walking range's line and the foot of the walls, which hide its edge
            // from the game's camera: the hospital's corridor, 18/11.)
            var inset = 0.14f * T;
            // The fill.
            build.Clear();
            foreach (var (tiles, lit) in list ?? new List<(List<Pos>, bool)>())
            {
                var colour = new Color32(Search.r, Search.g, Search.b, (byte)(lit ? 0x58 : 0x2c));
                foreach (var p in tiles)
                {
                    var floor = Board.TileToWorld(s, p).y + 0.014f * T;
                    var a = Board.Corner(p.X, p.Y, floor);
                    build.Flat(a.x, a.z, a.x + T, a.z - T, floor, colour);
                }
            }
            fill.sharedMesh = build.Build(fill.sharedMesh);
            // The border: each tile's sides with no tile of the room beyond, a little inside it; the dark line under, then
            // the white one.
            var edges = new List<(Vector3 a, Vector3 b, bool lit)>();
            foreach (var (tiles, lit) in list ?? new List<(List<Pos>, bool)>())
            {
                var mine = new HashSet<(int, int)>(tiles.Select(p => (p.X, p.Y)));
                foreach (var p in tiles)
                {
                    var floor = Board.TileToWorld(s, p).y + 0.02f * T;
                    float x0 = p.X * T, x1 = (p.X + 1) * T, z0 = -p.Y * T, z1 = -(p.Y + 1) * T;
                    if (!mine.Contains((p.X, p.Y - 1))) edges.Add((new Vector3(x0, floor, z0 - inset), new Vector3(x1, floor, z0 - inset), lit));
                    if (!mine.Contains((p.X, p.Y + 1))) edges.Add((new Vector3(x0, floor, z1 + inset), new Vector3(x1, floor, z1 + inset), lit));
                    if (!mine.Contains((p.X - 1, p.Y))) edges.Add((new Vector3(x0 + inset, floor, z0), new Vector3(x0 + inset, floor, z1), lit));
                    if (!mine.Contains((p.X + 1, p.Y))) edges.Add((new Vector3(x1 - inset, floor, z0), new Vector3(x1 - inset, floor, z1), lit));
                }
            }
            build.Clear();
            foreach (var (a, b, lit) in edges) build.Line(a, b, lit ? RoomLitPx + 2.6f : RoomPx + 2f, RoomShade);
            shade.sharedMesh = build.Build(shade.sharedMesh);
            build.Clear();
            foreach (var (a, b, lit) in edges) build.Line(a, b, lit ? RoomLitPx : RoomPx, new Color32(Search.r, Search.g, Search.b, (byte)(lit ? 0xff : 0xb0)));
            line.sharedMesh = build.Build(line.sharedMesh);
        }

        /// <summary>
        /// The rooms' borders, in pixels: the one the picked soldier can search, and the rest. The border does the work on a
        /// light floor, where the white wash is lost (Arturo, cuadro 76: the wash and the border, no stripes).
        /// </summary>
        const float RoomLitPx = 3.2f, RoomPx = 1.6f;
        /// <summary>The dark line under a room's border (as the walking range's: MoveOverlay.Shade).</summary>
        static readonly Color32 RoomShade = MoveOverlay.Hex(0x3a4048);

        /// <summary>
        /// The marks for `s`: the evacuation zone's tiles, beams (where and in what colour), the loose item's tile, the
        /// flares where more are coming, and a run's Rescate's zone to look in (`area`, N2). Nothing is made again unless one
        /// of them changed.
        /// </summary>
        public void Show(GameState s, IList<Vec2> evac, IList<(Pos at, int color)> beacons, Pos item, IList<Pos> flaresAt, SearchArea area = null, IList<Pos> exits = null)
        {
            var now = string.Join(";", evac.Select(p => $"{p.X},{p.Y}")) + "|" + (exits != null ? string.Join(";", exits.Select(p => $"{p.X},{p.Y}")) : "") + "|" + string.Join(";", beacons.Select(b => $"{b.at.X},{b.at.Y},{b.at.Z},{b.color}")) + "|" + (item != null ? $"{item.X},{item.Y},{item.Z}" : "") + "|" + string.Join(";", flaresAt.Select(p => $"{p.X},{p.Y}"))
                + "|" + (area != null ? $"{area.Center.X},{area.Center.Y},{area.Radius}" : "");
            if (now == key) return;
            key = now;
            SearchZone(s, area);
            // The zone: a square on each tile, a little short of its edges, and its edges drawn: one tile, one seat in La
            // Lata, by its doors (T21: as many tiles as seats; a loose wash read as an area, not as places).
            build.Clear();
            var inset = 0.02f * T;
            var seat = new Color32(Evac.r, Evac.g, Evac.b, 0xe0);
            foreach (var t in evac)
            {
                var floor = Board.TileToWorld(s, new Pos { X = t.X, Y = t.Y }).y + 0.015f * T;
                var a = Board.Corner(t.X, t.Y, floor);
                build.Flat(a.x + inset, a.z - inset, a.x + T - inset, a.z - T + inset, floor, new Color32(Evac.r, Evac.g, Evac.b, 0x38));
                var edge = 0.08f * T;
                var c0 = new Vector3(a.x + edge, floor + 0.005f * T, a.z - edge);
                var c1 = new Vector3(a.x + T - edge, c0.y, a.z - edge);
                var c2 = new Vector3(a.x + T - edge, c0.y, a.z - T + edge);
                var c3 = new Vector3(a.x + edge, c0.y, a.z - T + edge);
                build.Line(c0, c1, 2.5f, seat);
                build.Line(c1, c2, 2.5f, seat);
                build.Line(c2, c3, 2.5f, seat);
                build.Line(c3, c0, 2.5f, seat);
            }
            // Bajar armed (T24): the tiles one inside can get down to, an edge only, thinner and with no fill: not seats.
            if (exits != null)
                foreach (var t in exits)
                {
                    var floor = Board.TileToWorld(s, new Pos { X = t.X, Y = t.Y }).y + 0.02f * T;
                    var a = Board.Corner(t.X, t.Y, floor);
                    var edge = 0.14f * T;
                    var c0 = new Vector3(a.x + edge, floor, a.z - edge);
                    var c1 = new Vector3(a.x + T - edge, floor, a.z - edge);
                    var c2 = new Vector3(a.x + T - edge, floor, a.z - T + edge);
                    var c3 = new Vector3(a.x + edge, floor, a.z - T + edge);
                    var white = new Color32(0xff, 0xff, 0xff, 0xc0);
                    build.Line(c0, c1, 1.5f, white);
                    build.Line(c1, c2, 1.5f, white);
                    build.Line(c2, c3, 1.5f, white);
                    build.Line(c3, c0, 1.5f, white);
                }
            zone.sharedMesh = build.Build(zone.sharedMesh);
            // The beams: a dark casing and a bright core going up, a ring at the foot.
            build.Clear();
            var all = beacons.Select(b => (b.at, MoveOverlay.Hex(b.color))).ToList();
            if (item != null) all.Add((item, Evac));
            foreach (var (at, colour) in all)
            {
                var foot = Board.TileToWorld(s, at);
                build.Line(foot + Vector3.up * BeamFoot, foot + Vector3.up * BeamTop, 7, new Color32(0x0b, 0x1a, 0x12, 0x80));
                build.Line(foot + Vector3.up * BeamFoot, foot + Vector3.up * BeamTop, 3.5f, new Color32(colour.r, colour.g, colour.b, 0xf2));
                build.Arc(foot + Vector3.up * (0.05f * T), 0.48f * T, 0.6f * T, 0, Mathf.PI * 2, 36, new Color32(colour.r, colour.g, colour.b, 0xbf));
            }
            beams.sharedMesh = build.Build(beams.sharedMesh);
            core.gameObject.SetActive(item != null);
            if (item != null) coreAt = Board.TileToWorld(s, item);
            // The flares: a red smoke going up from the tile (a cone, wider at the top), its ring spreading (Update).
            build.Clear();
            flareAt.Clear();
            foreach (var p in flaresAt)
            {
                var foot = Board.TileToWorld(s, p);
                flareAt.Add(foot);
                for (var i = 0; i < 12; i++)
                {
                    var a0 = i * Mathf.PI * 2 / 12;
                    var a1 = (i + 1) * Mathf.PI * 2 / 12;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    var top = Vector3.up * (5 * T);
                    var low = new Color32(Flare.r, Flare.g, Flare.b, 0x52);
                    var high = new Color32(Flare.r, Flare.g, Flare.b, 0);
                    build.Quad(foot + d0 * (0.35f * T), foot + d1 * (0.35f * T), foot + top + d1 * (0.9f * T), foot + top + d0 * (0.9f * T), low, low, high, high);
                }
            }
            flares.sharedMesh = build.Build(flares.sharedMesh);
            flareRing.color = Flare;
            if (flareAt.Count == 0) flareRings.sharedMesh = build.Build(flareRings.sharedMesh);
        }
    }
}
