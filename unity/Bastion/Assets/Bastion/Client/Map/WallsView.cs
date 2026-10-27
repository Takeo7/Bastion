using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The buildings' walls drawn from their kit pieces (WallLayout; 0.10.1's buildings.ts batches): every piece and pane
    /// of a chunk of the map (Chunk tiles a side) and a storey joined into one mesh a material, so a city is a few hundred
    /// draws; a door's leaf a thing of its own, on its hinge, open a quarter turn or shut; the props on the walls as the
    /// map's. Laid again, only the chunks whose pieces changed are made again (a door opened, a wall blown down).
    /// </summary>
    public sealed class WallsView
    {
        /// <summary>Tiles a side of a chunk (0.10.1's batch chunk: a change remakes so much).</summary>
        public const int Chunk = 8;

        readonly Transform[] storeys;
        readonly int width;
        /// <summary>A catalog model set up under a parent, at its origin (MapBuilder.Place); null if it isn't there.</summary>
        readonly Func<Transform, string, GameObject> place;
        /// <summary>A building's colours on its walls (BuildingLooks: outer and inner, linear multipliers), by its index.</summary>
        readonly Func<int, (Vector3 outer, Vector3 inner)> colours;
        readonly Dictionary<(int level, int cx, int cz), GameObject> chunks = new Dictionary<(int, int, int), GameObject>();
        readonly Dictionary<(int level, int cx, int cz), int> signatures = new Dictionary<(int, int, int), int>();
        static readonly Dictionary<string, Mesh> panes = new Dictionary<string, Mesh>();

        /// <summary>How many pieces were laid, and those whose model is not in the catalog (once each).</summary>
        public int Laid { get; private set; }
        public readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>The buildings (a piece's storey, for the cutaway).</summary>
        readonly BuildingPlan plan;

        public WallsView(Transform[] storeys, int width, Func<Transform, string, GameObject> place, Func<int, (Vector3 outer, Vector3 inner)> colours = null, BuildingPlan plan = null)
        {
            this.storeys = storeys;
            this.width = width;
            this.plan = plan;
            this.place = place;
            this.colours = colours;
        }

        (int, int, int) KeyOf(int level, int tile) => (Mathf.Clamp(level, 0, storeys.Length - 1), tile % width / Chunk, tile / width / Chunk);

        /// <summary>The walls as `layout` has them: the chunks that changed made again, those left empty taken away.</summary>
        public void Lay(WallLayout layout)
        {
            var keys = new HashSet<(int, int, int)>();
            var pieces = layout.Pieces.ToLookup(p => KeyOf(p.level, p.tile));
            var glass = layout.Panes.ToLookup(p => KeyOf(p.level, p.tile));
            var props = layout.Props.ToLookup(p => KeyOf(p.level, p.tile));
            keys.UnionWith(pieces.Select(g => g.Key));
            keys.UnionWith(glass.Select(g => g.Key));
            keys.UnionWith(props.Select(g => g.Key));
            foreach (var key in chunks.Keys.Where(k => !keys.Contains(k)).ToList())
            {
                DestroyNow(chunks[key]);
                chunks.Remove(key);
                signatures.Remove(key);
            }
            Laid = layout.Pieces.Count;
            foreach (var key in keys)
            {
                List<WallPiece> a = pieces[key].ToList();
                List<WallPane> b = glass[key].ToList();
                List<WallProp> c = props[key].ToList();
                int signature = Signature(a, b, c);
                if (chunks.ContainsKey(key) && signatures[key] == signature) continue;
                if (chunks.TryGetValue(key, out var old)) DestroyNow(old);
                chunks[key] = Build(key, a, b, c);
                signatures[key] = signature;
            }
        }

        static int Signature(List<WallPiece> pieces, List<WallPane> glass, List<WallProp> props)
        {
            unchecked
            {
                int h = 17;
                foreach (var p in pieces)
                {
                    h = h * 31 + p.id.GetHashCode();
                    h = h * 31 + p.matrix.GetHashCode();
                    h = h * 31 + (p.open ? 1 : 0) + (p.otherHinge ? 2 : 0);
                }
                foreach (var p in glass) h = h * 31 + p.kit.GetHashCode() * 7 + p.matrix.GetHashCode();
                foreach (var p in props) h = h * 31 + p.id.GetHashCode() * 5 + p.matrix.GetHashCode();
                return h;
            }
        }

        GameObject Build((int level, int cx, int cz) key, List<WallPiece> list, List<WallPane> glass, List<WallProp> props)
        {
            var holder = new GameObject($"Muros {key.cx},{key.cz}");
            holder.transform.SetParent(storeys[key.level], false);
            var byMaterial = new Dictionary<Material, Batch>();
            Batch BatchOf(Material material)
            {
                if (!byMaterial.TryGetValue(material, out var batch)) byMaterial[material] = batch = new Batch();
                return batch;
            }
            foreach (var piece in list)
            {
                var parts = Pieces.Parts(piece.id);
                if (parts.Count == 0)
                {
                    Missing.Add(piece.id);
                    continue;
                }
                var at = InMetres(piece.matrix);
                var thicken = Matrix4x4.Scale(new Vector3(piece.square ? piece.thick : 1, 1, piece.thick));
                var (outer, inner) = ColoursOf(piece);
                var more = new Vector4(StoreyOf(piece.building, piece.floor), 0, 0, 0);
                var cut = CutOf(piece.building, piece.roof, piece.between, piece.outward, piece.floor);
                Material first = null;
                foreach (var part in parts)
                {
                    if (part.leaf)
                    {
                        // In the cutaway a leaf drops lower than the walls, so a door reads as the way through it is: its
                        // cut measured from below its floor (CUT_LOW - LEAF_CUT).
                        var leafCut = piece.roof ? cut : CutOf(piece.building, false, piece.between, piece.outward, piece.floor - (CutLow - LeafCut));
                        Leaf(holder.transform, piece, part, at, thicken, outer, inner, leafCut, more);
                        continue;
                    }
                    var world = MapSpace.FromGame(at * thicken * part.fit);
                    for (int k = 0; k < part.mesh.subMeshCount; k++)
                    {
                        if (part.materials[k] == null) continue;
                        first ??= part.materials[k];
                        BatchOf(part.materials[k]).Add(part.mesh, k, world, outer, inner, cut, more);
                    }
                }
                // Its cap (buildWalls CAP_QUAD): a lid over its footprint at its top, which the cutaway lowers with the wall.
                if (piece.cap && first != null && CapBox(piece, parts, thicken) is Bounds box)
                {
                    var lid = Matrix4x4.Translate(new Vector3(box.min.x, box.max.y, box.min.z)) * Matrix4x4.Scale(new Vector3(box.size.x, 1, box.size.z));
                    BatchOf(first).AddCap(MapSpace.FromGame(at * lid), inner, cut, new Vector4(more.x, 1, 0, 0));
                }
            }
            var clear = PieceCatalog.Load()?.glass;
            if (clear != null)
                foreach (var pane in glass)
                {
                    if (!panes.TryGetValue(pane.kit, out var mesh) || mesh == null) panes[pane.kit] = mesh = WallLayout.PaneMesh(pane.kit);
                    var cut = CutOf(pane.building, pane.roof, pane.between, pane.outward, pane.floor);
                    // Lit by night, one in three or so (buildings.ts: by the pane's place).
                    float lit = BuildingPlan.Hash(Mathf.RoundToInt(pane.matrix.m03 * 2), Mathf.RoundToInt(pane.matrix.m23 * 2), 90) < 0.35 ? 1 : 0;
                    BatchOf(clear).Add(mesh, 0, MapSpace.FromGame(InMetres(pane.matrix)), Vector4.one, Vector4.one, cut, new Vector4(StoreyOf(pane.building, pane.floor), 0, lit, 0));
                }
            foreach (var (material, batch) in byMaterial) Draw(holder.transform, material.name, batch.Make($"{holder.name} {material.name}"), material, material != clear);
            foreach (var prop in props) Prop(holder.transform, prop);
            return holder;
        }

        /// <summary>Height the cut walls drop to, and a door's leaf in the cutaway (tiles: buildings.ts CUT_LOW, LEAF_CUT).</summary>
        const float CutLow = 0.95f, LeafCut = 0.3f;
        /// <summary>aCut's way out for what fades with the roof.</summary>
        const float OnRoof = 9;

        /// <summary>
        /// What Bastion/Building's cutaway reads of a piece (buildWalls aCut, TEXCOORD4): its building (+1), the way its
        /// outer face looks (none between rooms; ON_ROOF for a façade's upper storeys, which go with the roof), the height
        /// of its floor (m).
        /// </summary>
        static Vector4 CutOf(int building, bool roof, bool between, Vector2 outward, float floor) =>
            roof ? new Vector4(building + 1, OnRoof, OnRoof, floor * MapSpace.Tile)
                : new Vector4(building + 1, between ? 0 : outward.x, between ? 0 : outward.y, floor * MapSpace.Tile);

        /// <summary>The storey a piece of building `b` at height `floor` (tiles) stands on (buildings.ts storeyOf).</summary>
        int StoreyOf(int b, float floor)
        {
            var bd = plan != null && b >= 0 && b < plan.Buildings.Count ? plan.Buildings[b] : null;
            return bd == null ? 0 : Mathf.Max(0, Mathf.RoundToInt((floor - bd.@base) / BuildingPlan.Storey));
        }

        /// <summary>A piece's footprint and top in its own frame (m), thickened, its leaves left out (buildings.ts capBox); null without any.</summary>
        static Bounds? CapBox(WallPiece piece, IReadOnlyList<PiecePart> parts, Matrix4x4 thicken)
        {
            string key = $"{piece.id}:{piece.thick}:{(piece.square ? 1 : 0)}";
            if (capBoxes.TryGetValue(key, out var known)) return known;
            Bounds? box = null;
            foreach (var part in parts)
            {
                if (part.leaf) continue;
                var m = thicken * part.fit;
                var b = part.mesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    if (box is Bounds have)
                    {
                        have.Encapsulate(corner);
                        box = have;
                    }
                    else box = new Bounds(corner, Vector3.zero);
                }
            }
            return capBoxes[key] = box;
        }

        static readonly Dictionary<string, Bounds?> capBoxes = new Dictionary<string, Bounds?>();

        /// <summary>
        /// A piece's colours (buildWalls put): the first of a list in its building's (outer and inner), the rest white,
        /// as the leaves and what has no building.
        /// </summary>
        (Vector4 outer, Vector4 inner) ColoursOf(WallPiece piece)
        {
            if (!piece.tint || piece.building < 0 || colours == null) return (Vector4.one, Vector4.one);
            var (outer, inner) = colours(piece.building);
            return (new Vector4(outer.x, outer.y, outer.z, 1), new Vector4(inner.x, inner.y, inner.z, 1));
        }

        static void Draw(Transform parent, string name, Mesh mesh, Material material, bool shadows)
        {
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        /// <summary>
        /// A mesh being put together from pieces placed in the world, each vertex with its piece's colours (the
        /// Bastion/Building shader's TEXCOORD2 and 3: linear, and over 1 at times, so not vertex colours). What
        /// CombineMeshes does, with what it can't carry.
        /// </summary>
        sealed class Batch
        {
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<Vector4> outer = new List<Vector4>();
            readonly List<Vector4> inner = new List<Vector4>();
            readonly List<Vector4> cuts = new List<Vector4>();
            readonly List<Vector4> mores = new List<Vector4>();
            readonly List<int> triangles = new List<int>();

            public void Add(Mesh mesh, int subMesh, Matrix4x4 world, Vector4 outerColour, Vector4 innerColour, Vector4 cut, Vector4 more)
            {
                var source = Source.Of(mesh);
                int start = positions.Count;
                var turn = world.inverse.transpose;
                for (int k = 0; k < source.positions.Length; k++)
                {
                    positions.Add(world.MultiplyPoint3x4(source.positions[k]));
                    normals.Add(k < source.normals.Length ? turn.MultiplyVector(source.normals[k]).normalized : Vector3.up);
                    uvs.Add(k < source.uvs.Length ? source.uvs[k] : Vector2.zero);
                    outer.Add(outerColour);
                    inner.Add(innerColour);
                    cuts.Add(cut);
                    mores.Add(more);
                }
                foreach (int i in source.triangles[subMesh]) triangles.Add(start + i);
            }

            /// <summary>A cap: the unit square facing up (0.10.1's CAP_QUAD, turned round for Unity's mirrored world), placed.</summary>
            public void AddCap(Matrix4x4 world, Vector4 innerColour, Vector4 cut, Vector4 more)
            {
                int start = positions.Count;
                var up = world.inverse.transpose.MultiplyVector(Vector3.up).normalized;
                foreach (var corner in new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0) })
                {
                    positions.Add(world.MultiplyPoint3x4(corner));
                    normals.Add(up);
                    uvs.Add(Vector2.zero);
                    outer.Add(Vector4.one);
                    inner.Add(innerColour);
                    cuts.Add(cut);
                    mores.Add(more);
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            public Mesh Make(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(2, outer);
                mesh.SetUVs(3, inner);
                mesh.SetUVs(4, cuts);
                mesh.SetUVs(5, mores);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>A model's mesh read once (they are imported readable: PieceMake).</summary>
        sealed class Source
        {
            public Vector3[] positions, normals;
            public Vector2[] uvs;
            public int[][] triangles;
            static readonly Dictionary<Mesh, Source> read = new Dictionary<Mesh, Source>();

            public static Source Of(Mesh mesh)
            {
                if (read.TryGetValue(mesh, out var s) && s != null) return s;
                s = new Source { positions = mesh.vertices, normals = mesh.normals, uvs = mesh.uv, triangles = new int[mesh.subMeshCount][] };
                for (int k = 0; k < mesh.subMeshCount; k++) s.triangles[k] = mesh.GetTriangles(k);
                return read[mesh] = s;
            }
        }

        /// <summary>
        /// A prop at its matrix: 0.10.1's placement turned into Unity's (z the other way: S·m·S, metres), over the model
        /// as MapBuilder.Place sets it up at its origin (which does the same for the map's props).
        /// </summary>
        void Prop(Transform parent, WallProp prop)
        {
            var o = place?.Invoke(parent, prop.id);
            if (o == null)
            {
                Missing.Add(prop.id);
                return;
            }
            var flip = Matrix4x4.Scale(new Vector3(1, 1, -1));
            var m = flip * InMetres(prop.matrix) * flip;
            o.transform.localPosition = m.GetPosition();
            o.transform.localRotation = m.rotation;
            o.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
            // A shop's sign and a shed's shutter go while their building is cut on their storey (the motel's stands outside).
            if (prop.building >= 0) Cutaway.Current?.Track(o, prop.building, prop.level, true);
        }

        /// <summary>
        /// A door's leaf (doors.ts place): turned on its hinge (on the other jamb, `otherHinge`, the other way) a quarter
        /// turn open, or shut; between the piece's place and its thickening, as there. A mesh of its own, placed.
        /// </summary>
        void Leaf(Transform parent, WallPiece piece, PiecePart part, Matrix4x4 place, Matrix4x4 thicken, Vector4 outer, Vector4 inner, Vector4 cut, Vector4 more)
        {
            var hinge = new Vector3(piece.otherHinge ? part.otherEnd : part.hinge.x, 0, part.hinge.z * piece.thick);
            bool far = piece.otherHinge ? !part.far : part.far;
            var swing = piece.open
                ? Matrix4x4.Translate(hinge) * Pieces.RotationY((far ? -1 : 1) * Mathf.PI / 2) * Matrix4x4.Translate(-hinge)
                : Matrix4x4.identity;
            var world = MapSpace.FromGame(place * swing * thicken * part.fit);
            var o = new GameObject("hoja").transform;
            o.SetParent(parent, false);
            for (int k = 0; k < part.mesh.subMeshCount; k++)
            {
                if (part.materials[k] == null) continue;
                var batch = new Batch();
                batch.Add(part.mesh, k, world, outer, inner, cut, more);
                Draw(o, part.materials[k].name, batch.Make($"hoja {piece.tile}"), part.materials[k], true);
            }
            // (A siege's door on the ground wears and shakes: Wear, Knock.)
            if (piece.level != 0 || piece.tile < 0) return;
            var leaf = new Hung { o = o, place = place, rest = thicken * part.fit, hinge = hinge, far = far, open = piece.open, baked = world.inverse };
            if (!hung.TryGetValue(piece.tile, out var list)) hung[piece.tile] = list = new List<Hung>();
            list.RemoveAll(h => h.o == null);
            list.Add(leaf);
            if (wear.ContainsKey(piece.tile)) Hang(piece.tile, leaf);
        }

        // ------------------------------------------------------------ a siege's doors as they give (doors.ts)

        /// <summary>Seconds a blow shakes a siege's gate (KNOCK_SECONDS).</summary>
        const float KnockSeconds = 0.4f;

        /// <summary>
        /// A siege's door as it gives, by its stage of wear (doors.ts WEAR; 1 knocked about, 2 about to go): a little ajar,
        /// leaning in or out of its wall, and hanging off its hinge (radians).
        /// </summary>
        static readonly (float ajar, float lean, float sag)[] Wears = { (0, 0, 0), (0.1f, 0.06f, 0.03f), (0.28f, 0.15f, 0.085f) };

        /// <summary>A door's leaf as it was drawn (Leaf): its piece's place, hinge and frame, and its drawn turn undone.</summary>
        sealed class Hung
        {
            public Transform o;
            public Matrix4x4 place, rest, baked;
            public Vector3 hinge;
            public bool far, open;
        }

        readonly Dictionary<int, List<Hung>> hung = new Dictionary<int, List<Hung>>();
        Dictionary<int, int> wear = new Dictionary<int, int>();
        readonly Dictionary<int, float> knocks = new Dictionary<int, float>();
        bool worn;

        /// <summary>A siege's doors as worn as they are now (BasePieces.SiegeWear), by ground tile (buildings.ts wearDoors).</summary>
        public void Wear(Dictionary<int, int> now)
        {
            var doors = now.Where(kv => kv.Value > 0 && hung.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (doors.Count == wear.Count && doors.All(kv => wear.TryGetValue(kv.Key, out int was) && was == kv.Value)) return;
            foreach (int tile in wear.Keys) if (!doors.ContainsKey(tile)) knocks.TryAdd(tile, 0);
            wear = doors;
            worn = true;
        }

        /// <summary>For the labs: the ground tiles with a leaf hung, and those worn now (tile:stage).</summary>
        public string Doors => $"{hung.Count} hung ({string.Join(" ", hung.Keys.Take(12))}), worn {string.Join(" ", wear.Select(kv => $"{kv.Key}:{kv.Value}"))}";

        /// <summary>A blow on the door at ground tile `tile`: it shakes on its hinges for a moment.</summary>
        public void Knock(int tile)
        {
            if (hung.ContainsKey(tile)) knocks[tile] = KnockSeconds;
        }

        /// <summary>The doors worn or struck put as they hang now (doors.ts update), at the battle's pace.</summary>
        public void Step(float dt)
        {
            if (knocks.Count == 0 && !worn) return;
            var tiles = new HashSet<int>(knocks.Keys);
            if (worn) tiles.UnionWith(wear.Keys);
            foreach (int tile in tiles.ToList()) if (knocks.TryGetValue(tile, out float left)) knocks[tile] = left - dt;
            foreach (int tile in tiles)
                if (hung.TryGetValue(tile, out var list))
                    foreach (var leaf in list) if (leaf.o != null) Hang(tile, leaf);
            foreach (int tile in knocks.Keys.ToList()) if (knocks[tile] <= 0) knocks.Remove(tile);
            worn = false;
        }

        /// <summary>
        /// A leaf as worn as its door and shaken by a blow (doors.ts place): turned on its hinge, leant and sagging, in
        /// its piece's frame; set as its object's turn from where it was drawn (its vertices are the world's, its storey's
        /// object at the origin).
        /// </summary>
        void Hang(int tile, Hung leaf)
        {
            var w = Wears[wear.TryGetValue(tile, out int stage) ? Mathf.Clamp(stage, 0, 2) : 0];
            float knock = knocks.TryGetValue(tile, out float left) ? Mathf.Max(0, left) : 0;
            float shake = knock > 0 ? Mathf.Sin((KnockSeconds - knock) * 46) * 0.05f * (knock / KnockSeconds) : 0;
            float turn = (leaf.open ? Mathf.PI / 2 : 0) + w.ajar + shake, dir = leaf.far ? -1 : 1;
            var m = leaf.place * Matrix4x4.Translate(leaf.hinge) * Pieces.RotationY(turn * dir)
                * Matrix4x4.Rotate(Quaternion.AngleAxis((w.lean + shake * 0.4f) * Mathf.Rad2Deg, Vector3.right))
                * Matrix4x4.Rotate(Quaternion.AngleAxis(-w.sag * dir * Mathf.Rad2Deg, Vector3.forward))
                * Matrix4x4.Translate(-leaf.hinge) * leaf.rest;
            var d = MapSpace.FromGame(m) * leaf.baked;
            leaf.o.localPosition = d.GetColumn(3);
            leaf.o.localRotation = d.rotation;
        }

        /// <summary>0.10.1's matrix in tiles as one in metres: the same turn and stretch, its translation 1.5 times.</summary>
        public static Matrix4x4 InMetres(Matrix4x4 tiles)
        {
            var m = tiles;
            m.m03 *= MapSpace.Tile;
            m.m13 *= MapSpace.Tile;
            m.m23 *= MapSpace.Tile;
            return m;
        }

        public void Clear()
        {
            foreach (var o in chunks.Values) DestroyNow(o);
            chunks.Clear();
            signatures.Clear();
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
