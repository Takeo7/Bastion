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
    /// </summary>
    public sealed class MissionMarks : MonoBehaviour
    {
        const float T = Board.Tile;
        /// <summary>How high a beam goes (overlays.ts GUIDE_HEIGHT), from just over a figure's head.</summary>
        const float BeamTop = 40 * T;
        const float BeamFoot = 1.6f * T;

        static readonly Color32 Evac = MoveOverlay.Hex(0x4dff9a);
        static readonly Color32 Flare = MoveOverlay.Hex(0xff4a3a);

        readonly MeshBuilder build = new MeshBuilder();
        MeshFilter zone;
        Material zoneMaterial;
        MeshFilter beams;
        MeshFilter flares;
        Material flareRing;
        MeshFilter flareRings;
        Transform core;
        string key;

        void Awake()
        {
            zone = Layer("Evacuación", 3097, false);
            zoneMaterial = zone.GetComponent<MeshRenderer>().sharedMaterial;
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

        MeshFilter Layer(string name, int queue, bool overAll)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = MeshBuilder.Material(queue, overAll);
            return go.AddComponent<MeshFilter>();
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
        /// The marks for `s`: the evacuation zone's tiles, beams (where and in what colour), the loose item's tile, and the
        /// flares where more are coming. Nothing is made again unless one of them changed.
        /// </summary>
        public void Show(GameState s, IList<Vec2> evac, IList<(Pos at, int color)> beacons, Pos item, IList<Pos> flaresAt)
        {
            var now = string.Join(";", evac.Select(p => $"{p.X},{p.Y}")) + "|" + string.Join(";", beacons.Select(b => $"{b.at.X},{b.at.Y},{b.at.Z},{b.color}")) + "|" + (item != null ? $"{item.X},{item.Y},{item.Z}" : "") + "|" + string.Join(";", flaresAt.Select(p => $"{p.X},{p.Y}"));
            if (now == key) return;
            key = now;
            // The zone: a square on each tile, a little short of its edges.
            build.Clear();
            var inset = 0.02f * T;
            foreach (var t in evac)
            {
                var floor = Board.TileToWorld(s, new Pos { X = t.X, Y = t.Y }).y + 0.015f * T;
                var a = Board.Corner(t.X, t.Y, floor);
                build.Flat(a.x + inset, a.z - inset, a.x + T - inset, a.z - T + inset, floor, new Color32(Evac.r, Evac.g, Evac.b, 0x38));
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
