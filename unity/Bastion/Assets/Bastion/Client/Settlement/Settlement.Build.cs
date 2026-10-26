using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client
{
    /// <summary>
    /// What building shows over the clearing (0.10.1's clearing.ts ClearingSet: the grid, the piece about to go down in
    /// green or red, a line of them, the tiles marked, the cursor, the piece picked, a proposal, the partner's piece in
    /// hand, the siege's posts), and which tile is under a point of the screen. UI drives it from its input and its rules
    /// (placeBlocker, fellBlocker, postBlocker) and writes the hints and the names that float; Arte draws (Gameplay, 26/10).
    /// Tiles as the engine's (x east, y south); 0.10.1's heights over the ground, in tiles.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>A tile marked (clearing.ts mark): fit to build on, not, or amber (the trees to fell, one's own post).</summary>
        public enum Mark { Ok, Bad, Amber }

        /// <summary>A piece where it would go (BaseCursor): which, its corner tile as the engine's BasePiece x/y, its quarter turns.</summary>
        public struct PieceCursor
        {
            public string piece;
            public int x, y, rot;
        }

        /// <summary>A siege post (#58): its soldier's id and name, its commander's colour, its tile, whether it is this player's.</summary>
        public struct SiegePost
        {
            public string id, name;
            public Color colour;
            public int x, y;
            public bool mine;
        }

        /// <summary>The clearing walked about in first person (walkAbout.ts: Arte's, its input and camera too); UI's input keeps off while it is.</summary>
        public bool Walking { get; private set; }
        public event Action<bool> WalkingChanged;

        static readonly Color Green = Hex(0x5fd28a), Red = Hex(0xff5a4a), Amber = Hex(0xf0b040), Picked = Hex(0xffe08a), Proposed = Hex(0x7ac8ff);
        /// <summary>How see-through each is (clearing.ts): the ghost, a proposal, the partner's, the marks, the grid, the edge band, a post's ring.</summary>
        const float GhostAlpha = 0.45f, ProposalAlpha = 0.32f, PartnerAlpha = 0.38f, MarkAlpha = 0.4f, GridAlpha = 0.12f, BandAlpha = 0.35f, RingAlpha = 0.9f;
        /// <summary>The most tiles marked at a time (clearing.ts cells: FELL_MAX twice).</summary>
        const int MarksMax = 800;

        /// <summary>The map's builder as last shown (Show), for a piece's models alone.</summary>
        MapBuilder built;
        Transform buildRoot;
        GameObject grid, band, marks, cursorLoop, pickedLoop, proposalLoop, postRings;
        readonly List<Material> buildMaterials = new List<Material>();
        Material ghostMaterial, proposalMaterial, partnerMaterial;
        GameObject ghost, proposal, partner;
        string ghostKey, proposalKey, partnerKey;
        readonly List<GameObject> lineGhosts = new List<GameObject>();
        string lineKey;
        string picked;
        List<SiegePost> posts = new List<SiegePost>();
        string heldPost;
        Vector3? partnerTag;

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>0.10.1's point in tiles (x east, up, z south) in the map's metres.</summary>
        static Vector3 World(float x, float up, float z) => new Vector3(x * MapSpace.Tile, up * MapSpace.Tile, -z * MapSpace.Tile);

        Transform BuildRoot
        {
            get
            {
                if (buildRoot != null) return buildRoot;
                buildRoot = new GameObject("Construir").transform;
                buildRoot.SetParent(Map != null ? Map.transform : transform, false);
                return buildRoot;
            }
        }

        /// <summary>A see-through, unlit material of `colour` at `alpha` (KitMaterials), drawn over the ground (and over all, `onTop`).</summary>
        Material See(Color colour, float alpha, bool onTop = false, string name = "Construir")
        {
            var m = KitMaterials.New(KitMaterials.SeeThrough, name);
            if (m == null) return null;
            m.SetColor("_BaseColor", new Color(colour.r, colour.g, colour.b, alpha));
            if (onTop) m.SetFloat("_ZTest", (float)CompareFunction.Always);
            m.renderQueue = (int)RenderQueue.Transparent + (onTop ? 10 : 5);
            buildMaterials.Add(m);
            return m;
        }

        static GameObject Drawn(string name, Transform parent, Mesh mesh, Material material)
        {
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return o;
        }

        /// <summary>Construir: the tile grid over what can be built on and the edge band where the dead come in, darker (drawGrid).</summary>
        public void SetBuilding(bool on)
        {
            if (on && grid == null && Base != null) DrawGrid(Base);
            if (grid != null) grid.SetActive(on);
            if (band != null) band.SetActive(on);
        }

        void DrawGrid(BaseState @base)
        {
            int edge = Bastion.Engine.Base.BaseEdge;
            int a = edge, b = @base.Width - edge, c = edge, d = @base.Height - edge;
            var points = new List<Vector3>();
            for (int x = a; x <= b; x++) points.AddRange(new[] { World(x, 0.03f, c), World(x, 0.03f, d) });
            for (int y = c; y <= d; y++) points.AddRange(new[] { World(a, 0.03f, y), World(b, 0.03f, y) });
            var lines = new Mesh { name = "Rejilla", indexFormat = IndexFormat.UInt32 };
            lines.SetVertices(points);
            lines.SetIndices(Enumerable.Range(0, points.Count).ToArray(), MeshTopology.Lines, 0);
            grid = Drawn("Rejilla", BuildRoot, lines, See(Color.white, GridAlpha));
            var strips = new List<(float x, float y, float w, float h)>
            {
                (0, 0, @base.Width, edge), (0, @base.Height - edge, @base.Width, edge),
                (0, edge, edge, @base.Height - edge * 2), (@base.Width - edge, edge, edge, @base.Height - edge * 2),
            };
            band = Drawn("Franja del borde", BuildRoot, Quads(strips.Select(s => (s.x, s.y, s.w, s.h, 0.025f))), See(Hex(0x2a0c08), BandAlpha));
            grid.SetActive(false);
            band.SetActive(false);
        }

        /// <summary>Flat rectangles on the ground (tiles: corner x, y, across, deep, height), facing up.</summary>
        static Mesh Quads(IEnumerable<(float x, float y, float w, float h, float up)> rects)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var (x, y, w, h, up) in rects)
            {
                int k = v.Count;
                v.AddRange(new[] { World(x, up, y), World(x + w, up, y), World(x + w, up, y + h), World(x, up, y + h) });
                // (Wound clockwise seen from above, Unity's front: its z is 0.10.1's the other way.)
                t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            var mesh = new Mesh { name = "Casillas", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A rectangle of tiles as a closed line `up` over the ground (setLoop).</summary>
        static Mesh Loop(float x, float y, float w, float h, float up)
        {
            var mesh = new Mesh { name = "Contorno" };
            mesh.SetVertices(new List<Vector3> { World(x, up, y), World(x + w, up, y), World(x + w, up, y + h), World(x, up, y + h), World(x, up, y) });
            mesh.SetIndices(new[] { 0, 1, 2, 3, 4 }, MeshTopology.LineStrip, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        GameObject SetLoop(ref GameObject loop, string name, Color colour, float alpha, RectInt? tiles, float up)
        {
            if (tiles == null)
            {
                if (loop != null) loop.SetActive(false);
                return loop;
            }
            var r = tiles.Value;
            var mesh = Loop(r.x, r.y, r.width, r.height, up);
            if (loop == null) loop = Drawn(name, BuildRoot, mesh, See(colour, alpha, true, name));
            else
            {
                var filter = loop.GetComponent<MeshFilter>();
                DestroyNowObject(filter.sharedMesh);
                filter.sharedMesh = mesh;
            }
            loop.SetActive(true);
            return loop;
        }

        /// <summary>Tiles marked, each in its colour (mark: green, red, amber); empty or null, none.</summary>
        public void Marks(IReadOnlyList<(Vector2Int tile, Mark mark)> tiles)
        {
            if (marks != null) DestroyNowObject(marks);
            marks = null;
            if (tiles == null || tiles.Count == 0) return;
            marks = new GameObject("Casillas marcadas");
            marks.transform.SetParent(BuildRoot, false);
            foreach (Mark kind in Enum.GetValues(typeof(Mark)))
            {
                var these = tiles.Take(MarksMax).Where(t => t.mark == kind).Select(t => (t.tile.x + 0.04f, t.tile.y + 0.04f, 0.92f, 0.92f, 0.04f)).ToList();
                if (these.Count == 0) continue;
                var colour = kind == Mark.Ok ? Green : kind == Mark.Bad ? Red : Amber;
                Drawn(kind.ToString(), marks.transform, Quads(these), See(colour, MarkAlpha, false, "Marca"));
            }
        }

        /// <summary>The cursor: a closed line round a rectangle of tiles (the tile under the pointer, a piece's plot, the fell box); null, none.</summary>
        public void Outline(RectInt? tiles) => SetLoop(ref cursorLoop, "Cursor", Color.white, 0.8f, tiles, 0.06f);

        /// <summary>The piece in hand where it would go down, its ghost in green or red; a null piece, none. Those about the clearing keep off it.</summary>
        public void Ghost(string piece, int rot, Vector2Int at, bool ok)
        {
            if (ghostMaterial == null) ghostMaterial = See(Green, GhostAlpha, false, "Fantasma");
            if (ghostMaterial != null) ghostMaterial.SetColor("_BaseColor", WithAlpha(ok ? Green : Red, GhostAlpha));
            string key = piece == null ? null : $"{piece}:{rot}";
            if (key != ghostKey)
            {
                DestroyNowObject(ghost);
                ghost = key == null ? null : Model(piece, rot, ghostMaterial);
                ghostKey = key;
            }
            if (ghost != null) PlaceModel(ghost, piece, rot, at.x, at.y);
            KeepClear(piece == null ? null : Bastion.Engine.Base.PieceCells(piece, at.x, at.y, rot).Select(c => new Vector2Int(c.X, c.Y)).ToList());
        }

        /// <summary>A line of pieces being dragged (#35): one ghost a tile, all green or all red; null or empty, none.</summary>
        public void GhostLine(string piece, int rot, IReadOnlyList<Vector2Int> cells, bool ok)
        {
            if (ghostMaterial == null) ghostMaterial = See(Green, GhostAlpha, false, "Fantasma");
            if (ghostMaterial != null) ghostMaterial.SetColor("_BaseColor", WithAlpha(ok ? Green : Red, GhostAlpha));
            int n = piece == null || cells == null ? 0 : cells.Count;
            string key = n == 0 ? null : $"{piece}:{rot}:{n}";
            if (key != lineKey)
            {
                foreach (var o in lineGhosts) DestroyNowObject(o);
                lineGhosts.Clear();
                for (int k = 0; k < n; k++) lineGhosts.Add(Model(piece, rot, ghostMaterial));
                lineKey = key;
            }
            for (int k = 0; k < n; k++) if (lineGhosts[k] != null) PlaceModel(lineGhosts[k], piece, rot, cells[k].x, cells[k].y);
            KeepClear(n == 0 ? null : cells.SelectMany(c => Bastion.Engine.Base.PieceCells(piece, c.x, c.y, rot)).Select(c => new Vector2Int(c.X, c.Y)).ToList());
        }

        /// <summary>The piece picked (by its id), its plot's outline beating in 0xffe08a; null, none.</summary>
        public void Select(string pieceId)
        {
            picked = pieceId;
            RefreshPicked();
        }

        /// <summary>A proposal waiting: the piece in the proposal's blue and its plot's outline; null, none.</summary>
        public void SetProposal(PieceCursor? p)
        {
            if (proposalMaterial == null) proposalMaterial = See(Proposed, ProposalAlpha, false, "Propuesta");
            string key = p == null ? null : $"{p.Value.piece}:{p.Value.rot}";
            if (key != proposalKey)
            {
                DestroyNowObject(proposal);
                proposal = key == null ? null : Model(p.Value.piece, p.Value.rot, proposalMaterial);
                proposalKey = key;
            }
            if (p == null)
            {
                SetLoop(ref proposalLoop, "Propuesta", Proposed, 0.9f, null, 0.07f);
                return;
            }
            var c = p.Value;
            if (proposal != null) PlaceModel(proposal, c.piece, c.rot, c.x, c.y);
            var (w, h) = Footprint(c.piece, c.rot);
            SetLoop(ref proposalLoop, "Propuesta", Proposed, 0.9f, new RectInt(c.x, c.y, w, h), 0.07f);
        }

        /// <summary>The partner's piece in hand, in their colour (Construir); null, none. Where its name goes: PartnerTagWorld.</summary>
        public void PartnerCursor(PieceCursor? p, Color colour)
        {
            if (partnerMaterial == null) partnerMaterial = See(colour, PartnerAlpha, false, "Compañero");
            if (partnerMaterial != null) partnerMaterial.SetColor("_BaseColor", WithAlpha(colour, PartnerAlpha));
            string key = p == null ? null : $"{p.Value.piece}:{p.Value.rot}";
            if (key != partnerKey)
            {
                DestroyNowObject(partner);
                partner = key == null ? null : Model(p.Value.piece, p.Value.rot, partnerMaterial);
                partnerKey = key;
            }
            if (p == null)
            {
                partnerTag = null;
                return;
            }
            var c = p.Value;
            if (partner != null) PlaceModel(partner, c.piece, c.rot, c.x, c.y);
            var (w, h) = Footprint(c.piece, c.rot);
            partnerTag = BuildRoot.TransformPoint(World(c.x + w / 2f, 0.4f, c.y + h / 2f));
        }

        /// <summary>Where the partner's name goes over their piece in hand (world, metres); null with none.</summary>
        public Vector3? PartnerTagWorld => partnerTag;

        /// <summary>The siege's posts (#58): a ring on each tile in its commander's colour; the one in hand (`held`, a post's id) outlined, beating.</summary>
        public void SetPosts(IReadOnlyList<SiegePost> list, string held)
        {
            posts = list?.ToList() ?? new List<SiegePost>();
            heldPost = held;
            if (postRings != null) DestroyNowObject(postRings);
            postRings = null;
            if (posts.Count > 0)
            {
                postRings = new GameObject("Puestos");
                postRings.transform.SetParent(BuildRoot, false);
                // On its tile's floor (0.10.1 left them on the ground, under a watchtower's deck).
                foreach (var p in posts) Drawn(p.id, postRings.transform, Ring(p.x + 0.5f, p.y + 0.5f, 0.3f, 0.4f, PostFloor(p.x, p.y) + 0.045f, 28), See(p.colour, RingAlpha, false, "Puesto"));
            }
            PostFigures(posts);
            RefreshPicked();
        }

        /// <summary>Where post `id`'s name goes (world, metres): over its tile, over the head of the figure standing there (on its floor: 0.10.1 left it at a head's height off the ground, inside one on a watchtower); null if there is no such post.</summary>
        public Vector3? PostWorld(string id)
        {
            foreach (var p in posts)
                if (p.id == id) return BuildRoot.TransformPoint(World(p.x + 0.5f, PostFloor(p.x, p.y) + PostTagHeight, p.y + 0.5f));
            return null;
        }

        /// <summary>The name over a post: over a head (clearing.ts lift, with figures there: 2.05 tiles; 0.45, on the ring, without).</summary>
        const float PostTagHeight = 2.05f;

        /// <summary>A point of the clearing in the world (metres): tile (x, y)'s middle, in fractions too (TileAt's way back; clearing.ts focusTile).</summary>
        public Vector3 TileWorld(float x, float y) => BuildRoot.TransformPoint(World(x + 0.5f, 0, y + 0.5f));

        /// <summary>The tile under a point of the screen (pixels from the bottom left, as Input.mousePosition), on the ground; null off the clearing.</summary>
        public Vector2Int? TileAt(Vector2 screen)
        {
            var camera = Camera;
            if (camera == null || Base == null) return null;
            var ray = camera.ScreenPointToRay(screen);
            var ground = new Plane(BuildRoot.up, BuildRoot.position);
            if (!ground.Raycast(ray, out float t)) return null;
            var p = BuildRoot.InverseTransformPoint(ray.GetPoint(t));
            int x = Mathf.FloorToInt(p.x / MapSpace.Tile), y = Mathf.FloorToInt(-p.z / MapSpace.Tile);
            if (x < 0 || y < 0 || x >= Base.Width || y >= Base.Height) return null;
            return new Vector2Int(x, y);
        }

        /// <summary>The outline of the piece picked, or of the post in hand (clearing.ts outline), and its beat (update: 0.7 + 0.3 sin 4t).</summary>
        void RefreshPicked()
        {
            RectInt? r = null;
            var piece = picked != null ? Base?.Pieces.FirstOrDefault(q => q.Id == picked) : null;
            if (piece != null)
            {
                var (w, h) = Footprint(piece.Piece, piece.Rot);
                r = new RectInt(piece.X, piece.Y, w, h);
            }
            else if (heldPost != null && posts.FirstOrDefault(q => q.id == heldPost) is SiegePost held && held.id != null) r = new RectInt(held.x, held.y, 1, 1);
            SetLoop(ref pickedLoop, "Elegida", Picked, 1, r, 0.08f);
        }

        void StepBuild(float time)
        {
            if (pickedLoop == null || !pickedLoop.activeSelf) return;
            var m = pickedLoop.GetComponent<MeshRenderer>().sharedMaterial;
            if (m != null) m.SetColor("_BaseColor", WithAlpha(Picked, 0.7f + Mathf.Sin(time * 4) * 0.3f));
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>A piece's tiles across and deep once turned.</summary>
        static (int w, int h) Footprint(string piece, int rot)
        {
            if (!Bastion.Engine.Base.Pieces.TryGetValue(piece, out var def)) return (1, 1);
            int across = def.Size.Count > 0 ? def.Size[0] : 1, deep = def.Size.Count > 1 ? def.Size[1] : 1;
            return rot % 2 != 0 ? (deep, across) : (across, deep);
        }

        /// <summary>A piece's models alone (MapBuilder.PieceAlone) with `material` on every mesh.</summary>
        GameObject Model(string piece, int rot, Material material)
        {
            if (built == null) return null;
            var o = built.PieceAlone(piece, rot, 1, BuildRoot);
            if (o == null || material == null) return o;
            foreach (var r in o.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = material;
                r.sharedMaterials = mats;
            }
            return o;
        }

        /// <summary>A model alone at a piece's plot: its middle there, a little off the ground (clearing.ts model: 0.02).</summary>
        static void PlaceModel(GameObject o, string piece, int rot, int x, int y)
        {
            var (w, h) = Footprint(piece, rot);
            o.transform.localPosition = World(x + w / 2f, 0.02f, y + h / 2f);
        }

        /// <summary>A flat ring round (x, z) in tiles, from `inner` to `outer`, `up` over the ground, facing up.</summary>
        static Mesh Ring(float x, float z, float inner, float outer, float up, int segments)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int k = 0; k <= segments; k++)
            {
                float a = k * Mathf.PI * 2 / segments;
                v.Add(World(x + Mathf.Cos(a) * inner, up, z + Mathf.Sin(a) * inner));
                v.Add(World(x + Mathf.Cos(a) * outer, up, z + Mathf.Sin(a) * outer));
            }
            for (int k = 0; k < segments; k++)
            {
                int b = k * 2;
                t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = "Aro del puesto" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The tiles those about the clearing keep off (what is being placed: clearing.ts keepClear); null, none.</summary>
        public IReadOnlyList<Vector2Int> KeptClear { get; private set; }

        void KeepClear(IReadOnlyList<Vector2Int> cells)
        {
            KeptClear = cells;
            life?.KeepClear(cells?.Select(c => new Vec2 { X = c.x, Y = c.y }));
        }

        static void DestroyNowObject(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
