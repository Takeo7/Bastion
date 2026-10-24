using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>A kit piece's place in a tile (syntyManifest.ts PieceAsset; Resources/Pieces.json, unity/tools/pieces-json.mts).</summary>
    public sealed class PieceArt
    {
        public string pack;
        public string fbx;
        /// <summary>The module's middle in the FBX's units: along its run, and across it (the wall's middle plane).</summary>
        public float[] centre;
        public string run = "x";
        /// <summary>The way across the run its outer face looks (−1: −y).</summary>
        public int @out = -1;
        public float unit = 1;
        /// <summary>The kit's floor-to-ceiling height (metres), stretched to one storey.</summary>
        public float storey = 2.9f;
        public float @base;
        public float squeeze = 1;
        public bool hingeFar;
        public bool parts;
        public string leaf;
        public string atlas;
    }

    /// <summary>One mesh of a piece: drawn with `materials`, placed by `fit` (the piece's frame), the door leaf among them its hinge.</summary>
    public sealed class PiecePart
    {
        public string name;
        public Mesh mesh;
        public Material[] materials;
        /// <summary>From the mesh as Unity has it to 0.10.1's piece frame (x along the wall, y up, z across it out), in metres.</summary>
        public Matrix4x4 fit;
        public bool leaf;
        /// <summary>A leaf's hinge (and its other end), in the piece's frame; hinged on its far end (a gate's second leaf).</summary>
        public Vector3 hinge;
        public float otherEnd;
        public bool far;
    }

    /// <summary>
    /// The buildings' kit pieces (0.10.1's buildings.ts pieces, prepare): each cut from its Synty FBX (pieces-cut.py),
    /// made to sit in a tile as 0.10.1 did: its module's middle on the tile's, its outer face out, KIT_SCALE bigger and a
    /// storey of its kit stretched to the game's. Their meshes and materials are PieceCatalog's (Resources).
    /// </summary>
    public static class Pieces
    {
        /// <summary>Buildings drawn this much bigger than true scale (syntyManifest.ts KIT_SCALE).</summary>
        public const float KitScale = 1.2f;
        /// <summary>A storey, in metres (coords.ts STOREY: 2.3 tiles).</summary>
        const float StoreyMetres = 2.3f * MapSpace.Tile;

        static Dictionary<string, PieceArt> art;
        static readonly Dictionary<string, List<PiecePart>> made = new Dictionary<string, List<PiecePart>>();

        public static PieceArt Art(string id)
        {
            Load();
            return id != null && art.TryGetValue(id, out var a) ? a : null;
        }

        static void Load()
        {
            if (art != null) return;
            var text = Resources.Load<TextAsset>("Pieces") ?? throw new InvalidOperationException("Resources/Pieces.json is missing");
            art = JsonConvert.DeserializeObject<Tables>(text.text).pieces;
        }

        sealed class Tables
        {
            public Dictionary<string, PieceArt> pieces = new Dictionary<string, PieceArt>();
        }

        /// <summary>
        /// A piece's parts, made to sit in a tile (buildings.ts prepare); empty if it is not in the catalog. Everything in
        /// 0.10.1's frame (MapSpace.FromGame takes it to Unity's).
        /// </summary>
        public static IReadOnlyList<PiecePart> Parts(string id)
        {
            if (made.TryGetValue(id, out var list)) return list;
            made[id] = list = new List<PiecePart>();
            var a = Art(id);
            var entry = PieceCatalog.Load()?.Entry(id);
            if (a == null || entry == null || entry.model == null) return list;
            // The cut meshes are in metres (the FBX's units times its unit), Unity's axes: Blender's (x, y, z) as (−x, z, −y).
            // 0.10.1's frame had them as (x, z, −y): the run's x the other way round.
            var mirror = Matrix4x4.Scale(new Vector3(-1, 1, 1));
            float along = a.centre[0] * a.unit, across = a.centre[1] * a.unit;
            var centre = a.run == "y" ? new Vector3(across, 0, -along) : new Vector3(along, 0, -across);
            float turn = a.run == "y" ? (a.@out > 0 ? -Mathf.PI / 2 : Mathf.PI / 2) : a.@out > 0 ? Mathf.PI : 0;
            var fit = Matrix4x4.Scale(new Vector3(KitScale * a.squeeze, StoreyMetres / a.storey, KitScale)) * RotationY(turn)
                * Matrix4x4.Translate(new Vector3(-centre.x, -a.@base * a.unit, -centre.z));
            var root = entry.model.transform;
            int slot = 0;
            foreach (var filter in entry.model.GetComponentsInChildren<MeshFilter>())
            {
                var matrix = fit * mirror * (root.worldToLocalMatrix * filter.transform.localToWorldMatrix);
                var mesh = filter.sharedMesh;
                var materials = new Material[mesh.subMeshCount];
                for (int k = 0; k < materials.Length; k++) materials[k] = entry.materials.Length == 0 ? null : entry.materials[Mathf.Min(slot + k, entry.materials.Length - 1)];
                slot += materials.Length;
                bool leaf = !string.IsNullOrEmpty(a.leaf) && filter.name == a.leaf;
                var box = Box(mesh, matrix);
                list.Add(new PiecePart
                {
                    name = filter.name, mesh = mesh, materials = materials, fit = matrix, leaf = leaf,
                    hinge = new Vector3(a.hingeFar ? box.max.x : box.min.x, 0, box.center.z), otherEnd = a.hingeFar ? box.min.x : box.max.x, far = a.hingeFar,
                });
            }
            return list;
        }

        /// <summary>three.js's makeRotationY (right-handed, about y), the numbers as they are.</summary>
        public static Matrix4x4 RotationY(float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            var m = Matrix4x4.identity;
            m.m00 = c;
            m.m02 = s;
            m.m20 = -s;
            m.m22 = c;
            return m;
        }

        static Bounds Box(Mesh mesh, Matrix4x4 m)
        {
            var b = mesh.bounds;
            var box = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int k = 0; k < 8; k++)
                box.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
            return box;
        }
    }
}
