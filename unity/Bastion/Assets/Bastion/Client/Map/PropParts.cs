using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The map's props as meshes to merge (UI's, for what is drawn by the hundred: the ground's cover, the outline): each
    /// prop's meshes fitted as MapBuilder.Place fits it (MapArt.json: the longest side to `fit` tiles, centred or on its
    /// origin, on the floor or sunk, turned to face north and shifted), and many of them merged into a mesh per material
    /// (and per whether they cast a shadow).
    /// </summary>
    public sealed class PropParts
    {
        /// <summary>A prop's mesh: which submesh, its material, where it sits in the prop.</summary>
        public sealed class Part
        {
            public Mesh Mesh;
            public int Submesh;
            public Material Material;
            public Matrix4x4 Local;
        }

        readonly Func<string, GameObject> prefabOf;
        readonly Dictionary<string, List<Part>> parts = new Dictionary<string, List<Part>>();

        /// <summary>Props by Synty name; MapCatalog's when null.</summary>
        public PropParts(Func<string, GameObject> prefabOf = null)
        {
            this.prefabOf = prefabOf ?? MapCatalog.Load()?.PrefabOf() ?? (_ => null);
        }

        /// <summary>A prop's parts (none if it isn't in MapArt or the catalog), fitted once.</summary>
        public List<Part> Of(string id)
        {
            if (parts.TryGetValue(id, out var ready)) return ready;
            var list = new List<Part>();
            parts[id] = list;
            var art = MapArt.Prop(id);
            var prefab = art != null && !string.IsNullOrEmpty(art.fbx) ? prefabOf(art.fbx) : null;
            if (prefab == null) return list;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var toRoot = prefab.transform.worldToLocalMatrix;
            // Its box in its own space, over all its meshes' corners.
            var bounds = new Bounds();
            bool first = true;
            foreach (var f in filters)
            {
                if (f.sharedMesh == null) continue;
                var local = toRoot * f.transform.localToWorldMatrix;
                var b = f.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = local.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    if (first) bounds = new Bounds(corner, Vector3.zero);
                    else bounds.Encapsulate(corner);
                    first = false;
                }
            }
            if (first) return list;
            var size = bounds.size;
            float along = art.fit > 0 ? art.fit * MapSpace.Tile / Mathf.Max(size.x, size.z, 1e-4f) : 1;
            float across = (art.stretch ? 1 : along) * (art.thick > 0 ? art.thick : 1);
            var scale = size.x >= size.z ? new Vector3(along, across, across) : new Vector3(across, across, along);
            if (art.tall > 0) scale.y = art.tall * MapSpace.Tile / Mathf.Max(size.y, 1e-4f);
            var centre = art.origin ? Vector3.zero : bounds.center;
            var shift = art.shift != null && art.shift.Length >= 2 ? new Vector3(art.shift[0] * MapSpace.Tile, 0, -art.shift[1] * MapSpace.Tile) : Vector3.zero;
            // Its turn to face north as 0.10.1's (quarter turns), less what Unity's prefab stands turned from the source FBX.
            var fit = Matrix4x4.Translate(shift) * Matrix4x4.Rotate(Quaternion.Euler(0, -(art.yaw - MapArt.UnityTurn(art.fbx)) * 90, 0)) *
                      Matrix4x4.Translate(new Vector3(-centre.x * scale.x, -bounds.min.y * scale.y - art.sink * MapSpace.Tile, -centre.z * scale.z)) * Matrix4x4.Scale(scale);
            foreach (var f in filters)
            {
                var r = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null || r == null) continue;
                var local = fit * toRoot * f.transform.localToWorldMatrix;
                var materials = r.sharedMaterials;
                for (int k = 0; k < f.sharedMesh.subMeshCount && k < materials.Length; k++)
                    if (materials[k] != null) list.Add(new Part { Mesh = f.sharedMesh, Submesh = k, Material = materials[k], Local = local });
            }
            return list;
        }

        /// <summary>
        /// The props `items` (an id, where it stands in Unity's world, whether it casts a shadow) merged under `parent`: a
        /// mesh per material and shadow, none of them picked.
        /// </summary>
        public GameObject Merge(Transform parent, string name, IEnumerable<(string id, Matrix4x4 m, bool cast)> items)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var groups = new Dictionary<(Material, bool), List<CombineInstance>>();
            foreach (var (id, m, cast) in items)
            {
                foreach (var part in Of(id))
                {
                    if (!groups.TryGetValue((part.Material, cast), out var into)) groups[(part.Material, cast)] = into = new List<CombineInstance>();
                    into.Add(new CombineInstance { mesh = part.Mesh, subMeshIndex = part.Submesh, transform = m * part.Local });
                }
            }
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                var holder = new GameObject(pair.Key.Item1.name);
                holder.transform.SetParent(go.transform, false);
                holder.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = holder.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key.Item1;
                renderer.shadowCastingMode = pair.Key.Item2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return go;
        }

        /// <summary>0.10.1's matrix (tiles, z south) as Unity's (metres, z north): S·M·S, its translation in metres.</summary>
        public static Matrix4x4 InMetres(Matrix4x4 game)
        {
            var flip = Matrix4x4.Scale(new Vector3(1, 1, -1));
            var m = flip * game * flip;
            m.SetColumn(3, new Vector4(m.m03 * MapSpace.Tile, m.m13 * MapSpace.Tile, m.m23 * MapSpace.Tile, 1));
            return m;
        }
    }
}
