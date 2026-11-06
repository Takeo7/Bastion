using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A whole car's boot open once it is searched (Looting.CarBoot; Arturo's #3 of 5/11: «al registrarlo, el maletero
    /// abierto»; a wreck turns into Apocalypse's own, carOpen). Synty's whole cars have their lid in their one shell, so it
    /// is cut out of it: a triangle whose middle is behind `cut` and above `lip` (within `side` of the middle, not facing
    /// sideways) goes with the lid, which turns on its hinge across the car by `turn` degrees (up, its back rising; down, a
    /// tailgate falling back), its inside dark, as in its own shade; and the hole is a dark tub. The van's back doors and the tow
    /// truck's flap are Synty's own leaves, turned on their hinges. Each in metres of the car as MapBuilder.Prop stands it
    /// facing north at its own fit (1.95 tiles long, x across, y up, z to the nose; MapLab.CarSides), scaled to the one drawn.
    /// </summary>
    public static class CarBoot
    {
        sealed class Lid
        {
            /// <summary>The cut: behind `cut` (z), above `lip`, below `top`, within `side` of the middle; `sideways` the most a facing may lean across.</summary>
            public float cut, lip, top = 9, side = 9, sideways = 0.7f;
            /// <summary>The hinge (y, z) and its turn about the car's x, degrees (up positive).</summary>
            public float high, hinge, turn;
            /// <summary>The tub in the hole: across (either side), low, high, back, front; none if `tubAcross` is 0.</summary>
            public float tubAcross, tubLow, tubHigh, tubBack, tubFront;
            /// <summary>Synty's own leaves instead: a part's name ending, its upright hinge (x, z) and its swing, degrees.</summary>
            public (string part, float x, float z, float swing)[] leaves;
            /// <summary>Or a leaf hung across the car (the flap): its name ending, and it turns on `high`, `hinge` by `turn`.</summary>
            public string flap;
        }

        /// <summary>A saloon's lid, hinged at the foot of its back window (the taxi and the patrol car are its body).</summary>
        static readonly Lid Saloon = new Lid { cut = -0.93f, lip = 0.45f, high = 0.62f, hinge = -0.93f, turn = 80, tubAcross = 0.5f, tubLow = 0.3f, tubHigh = 0.6f, tubBack = -1.38f, tubFront = -0.95f };

        static readonly Dictionary<string, Lid> Lids = new Dictionary<string, Lid>
        {
            ["carSedan"] = Saloon,
            ["carTaxi"] = Saloon,
            ["sirenPolice"] = Saloon,
            ["carMuscle"] = new Lid { cut = -0.92f, lip = 0.36f, high = 0.56f, hinge = -0.9f, turn = 80, tubAcross = 0.5f, tubLow = 0.25f, tubHigh = 0.5f, tubBack = -1.38f, tubFront = -0.95f },
            ["carConvertible"] = new Lid { cut = -1.05f, lip = 0.45f, high = 0.66f, hinge = -1.03f, turn = 80, tubAcross = 0.55f, tubLow = 0.3f, tubHigh = 0.63f, tubBack = -1.38f, tubFront = -1.07f },
            // The hatchbacks': the hatch with its window, hinged at the roof's back edge.
            ["carSmall"] = new Lid { cut = -1.15f, lip = 0.45f, high = 1.02f, hinge = -1.15f, turn = 75, tubAcross = 0.55f, tubLow = 0.42f, tubHigh = 0.95f, tubBack = -1.38f, tubFront = -0.85f },
            ["carMedium"] = new Lid { cut = -1.12f, lip = 0.48f, high = 1.04f, hinge = -1.12f, turn = 75, tubAcross = 0.52f, tubLow = 0.45f, tubHigh = 0.98f, tubBack = -1.36f, tubFront = -0.8f },
            // Apocalypse's armoured one: the hatch with the spare wheel on it, and nothing below its bumper.
            ["carHatch"] = new Lid { cut = -1.12f, lip = 0.26f, side = 0.56f, sideways = 1.01f, high = 0.95f, hinge = -1.17f, turn = 70, tubAcross = 0.48f, tubLow = 0.4f, tubHigh = 0.9f, tubBack = -1.32f, tubFront = -0.85f },
            // The pickup's tailgate, between the posts of its tail lights, falling back on its foot onto its bed's open box.
            ["carPickup"] = new Lid { cut = -1.36f, lip = 0.42f, top = 0.8f, side = 0.5f, sideways = 1.01f, high = 0.42f, hinge = -1.42f, turn = -90 },
            // The van's two back doors, each swung out on its outer edge.
            ["carVan"] = new Lid { leaves = new[] { ("_Door_l", -0.46f, -1.38f, 100f), ("_Door_r", 0.46f, -1.38f, -100f) }, tubAcross = 0.45f, tubLow = 0.42f, tubHigh = 1.05f, tubBack = -1.3f, tubFront = -0.4f },
            // The tow truck's flap at the back of its bed, down on its foot.
            ["carUte"] = new Lid { flap = "_Rear_Flap_01", high = 0.55f, hinge = -1.12f, turn = -90 },
        };

        /// <summary>A whole car's length at its own fit (MapArt: 1.95 tiles), what the numbers are measured on.</summary>
        const float Length = 1.95f * MapSpace.Tile;

        /// <summary>Each body cut so (its mesh's id and the car's): what stays, and the lid; destroyed meshes made again.</summary>
        static readonly Dictionary<(int, string), (Mesh rest, Mesh lid)> cuts = new Dictionary<(int, string), (Mesh, Mesh)>();
        static Material tubMaterial, insideMaterial;

        /// <summary>Whether car `id` has a boot to open here.</summary>
        public static bool Has(string id) => id != null && Lids.ContainsKey(id);

        /// <summary>
        /// Car `id` as MapBuilder placed it (`car`, its holder, before it is painted: the lid and its inside are painted with
        /// it) with its boot open; false if it has none to open or its meshes can't be read.
        /// </summary>
        public static bool Open(GameObject car, string id)
        {
            if (car == null || !Lids.TryGetValue(id, out var lid)) return false;
            var space = car.transform;
            var filters = car.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).ToList();
            if (filters.Count == 0) return false;
            // The car's length here against what the numbers were measured on.
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (var f in filters)
            {
                var toCar = space.worldToLocalMatrix * f.transform.localToWorldMatrix;
                var b = f.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    float z = toCar.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))).z;
                    zMin = Mathf.Min(zMin, z);
                    zMax = Mathf.Max(zMax, z);
                }
            }
            float k = (zMax - zMin) / Length;
            bool opened = false;
            if (lid.leaves != null)
                foreach (var (part, x, z, swing) in lid.leaves)
                    opened |= Swing(space, filters.Where(f => f.name.EndsWith(part) || f.name.Contains(part + "_")), new Vector3(x * k, 0, z * k), Quaternion.AngleAxis(swing, Vector3.up));
            else if (lid.flap != null)
                opened = Swing(space, filters.Where(f => f.name.EndsWith(lid.flap)), new Vector3(0, lid.high * k, lid.hinge * k), Quaternion.AngleAxis(lid.turn, Vector3.right));
            else
                opened = Cut(space, filters, lid, id, k);
            if (opened && lid.tubAcross > 0) Tub(space, lid, k);
            return opened;
        }

        /// <summary>The parts `leaves` hung on a hinge at `at` (the car's frame) and turned on it so.</summary>
        static bool Swing(Transform space, IEnumerable<MeshFilter> leaves, Vector3 at, Quaternion turn)
        {
            var parts = leaves.ToList();
            if (parts.Count == 0) return false;
            var hinge = new GameObject("bisagra").transform;
            hinge.SetParent(space, false);
            hinge.localPosition = at;
            // (A part under another that moves already goes with it.)
            foreach (var f in parts)
                if (!parts.Any(o => o != f && f.transform.IsChildOf(o.transform))) f.transform.SetParent(hinge, true);
            hinge.localRotation = turn;
            return true;
        }

        /// <summary>The lid cut out of every part but the wheels, onto its hinge, turned.</summary>
        static bool Cut(Transform space, List<MeshFilter> filters, Lid lid, string id, float k)
        {
            Transform hinge = null;
            foreach (var f in filters)
            {
                if (f.name.Contains("Wheel") || f.name.Contains("Steering")) continue;
                var mesh = f.sharedMesh;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"[maletero] {mesh.name} isn't readable: {id}'s boot stays shut (MapCatalogMake.Run)");
                    continue;
                }
                var key = (mesh.GetInstanceID(), id);
                if (!cuts.TryGetValue(key, out var made) || made.rest == null)
                    made = cuts[key] = Split(mesh, space.worldToLocalMatrix * f.transform.localToWorldMatrix, lid, k);
                if (made.lid == null) continue;
                f.sharedMesh = made.rest;
                if (hinge == null)
                {
                    hinge = new GameObject("bisagra").transform;
                    hinge.SetParent(space, false);
                    hinge.localPosition = new Vector3(0, lid.high * k, lid.hinge * k);
                }
                // As the part was, then onto the hinge.
                var piece = new GameObject(f.name + " (maletero)");
                piece.layer = f.gameObject.layer;
                piece.transform.SetParent(f.transform.parent, false);
                piece.transform.localPosition = f.transform.localPosition;
                piece.transform.localRotation = f.transform.localRotation;
                piece.transform.localScale = f.transform.localScale;
                piece.transform.SetParent(hinge, true);
                piece.AddComponent<MeshFilter>().sharedMesh = made.lid;
                var r = f.GetComponent<MeshRenderer>();
                var pr = piece.AddComponent<MeshRenderer>();
                pr.sharedMaterials = r.sharedMaterials.Append(Dark(ref insideMaterial, "Maletero (tapa)", 0.2f)).ToArray();
                pr.shadowCastingMode = r.shadowCastingMode;
                pr.receiveShadows = r.receiveShadows;
            }
            if (hinge == null) return false;
            hinge.localRotation = Quaternion.AngleAxis(lid.turn, Vector3.right);
            return true;
        }

        /// <summary>
        /// `mesh` (to the car's frame by `toCar`) without its lid's triangles, and the lid: those triangles, and again
        /// turned the other way with their normals so in a last submesh of its own (its inside: not the outside's paint
        /// seen through, a patrol car's lettering backwards); its lid null if none is in it.
        /// </summary>
        static (Mesh rest, Mesh lid) Split(Mesh mesh, Matrix4x4 toCar, Lid lid, float k)
        {
            var vertices = mesh.vertices;
            var at = vertices.Select(v => toCar.MultiplyPoint3x4(v)).ToArray();
            bool InLid(int a, int b, int c)
            {
                var m = (at[a] + at[b] + at[c]) / 3;
                if (m.z > lid.cut * k || m.y < lid.lip * k || m.y > lid.top * k || Mathf.Abs(m.x) > lid.side * k) return false;
                var n = Vector3.Cross(at[b] - at[a], at[c] - at[a]);
                return n.sqrMagnitude > 1e-12f && Mathf.Abs(n.normalized.x) <= lid.sideways;
            }
            var lidTris = new List<int>[mesh.subMeshCount];
            var keptTris = new List<int>[mesh.subMeshCount];
            int count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                keptTris[s] = new List<int>(tris.Length);
                lidTris[s] = new List<int>();
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    var into = InLid(tris[t], tris[t + 1], tris[t + 2]) ? lidTris[s] : keptTris[s];
                    into.Add(tris[t]);
                    into.Add(tris[t + 1]);
                    into.Add(tris[t + 2]);
                }
                count += lidTris[s].Count;
            }
            if (count == 0) return (mesh, null);
            var rest = Object.Instantiate(mesh);
            rest.name = mesh.name + " (sin maletero)";
            for (int s = 0; s < mesh.subMeshCount; s++) rest.SetTriangles(keptTris[s], s, false);
            rest.RecalculateBounds();
            // The lid's corners, each twice: as they are, and for its inside, its normal the other way.
            var used = new Dictionary<int, int>();
            foreach (var list in lidTris)
                foreach (int i in list)
                    if (!used.ContainsKey(i)) used[i] = used.Count;
            var order = used.OrderBy(p => p.Value).Select(p => p.Key).ToArray();
            int n = order.Length;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var colours = mesh.colors32;
            var made = new Mesh { name = mesh.name + " (maletero)" };
            if (2 * n > 65535) made.indexFormat = IndexFormat.UInt32;
            made.vertices = order.Concat(order).Select(i => vertices[i]).ToArray();
            if (normals.Length == vertices.Length) made.normals = order.Select(i => normals[i]).Concat(order.Select(i => -normals[i])).ToArray();
            if (tangents.Length == vertices.Length) made.tangents = order.Concat(order).Select(i => tangents[i]).ToArray();
            if (colours.Length == vertices.Length) made.colors32 = order.Concat(order).Select(i => colours[i]).ToArray();
            for (int c = 0; c < 4; c++)
            {
                var uv = new List<Vector2>();
                mesh.GetUVs(c, uv);
                if (uv.Count == vertices.Length) made.SetUVs(c, order.Concat(order).Select(i => uv[i]).ToList());
            }
            made.subMeshCount = mesh.subMeshCount + 1;
            var inside = new List<int>(count);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                made.SetTriangles(lidTris[s].Select(i => used[i]).ToList(), s, false);
                for (int t = 0; t + 2 < lidTris[s].Count; t += 3)
                {
                    inside.Add(n + used[lidTris[s][t]]);
                    inside.Add(n + used[lidTris[s][t + 2]]);
                    inside.Add(n + used[lidTris[s][t + 1]]);
                }
            }
            made.SetTriangles(inside, mesh.subMeshCount, false);
            made.RecalculateBounds();
            return (rest, made);
        }

        /// <summary>The boot's dark inside: a box of the car's frame seen from within (its faces inward, its top open to the eye).</summary>
        static void Tub(Transform space, Lid lid, float k)
        {
            float x = lid.tubAcross * k, y0 = lid.tubLow * k, y1 = lid.tubHigh * k, z0 = lid.tubBack * k, z1 = lid.tubFront * k;
            var corners = new[]
            {
                new Vector3(-x, y0, z0), new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(-x, y0, z1),
                new Vector3(-x, y1, z0), new Vector3(x, y1, z0), new Vector3(x, y1, z1), new Vector3(-x, y1, z1),
            };
            // The floor, then the walls (no lid: nobody looks up from inside), each listed clockwise seen from outside and
            // drawn the other way round, facing in; with its own corners, for flat light.
            var faces = new[] { new[] { 0, 1, 2, 3 }, new[] { 0, 4, 5, 1 }, new[] { 1, 5, 6, 2 }, new[] { 2, 6, 7, 3 }, new[] { 3, 7, 4, 0 } };
            var vertices = new List<Vector3>();
            var tris = new List<int>();
            foreach (var face in faces)
            {
                int first = vertices.Count;
                vertices.AddRange(face.Select(i => corners[i]));
                tris.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }
            var mesh = new Mesh { name = "Maletero (hueco)" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var tub = new GameObject("hueco");
            tub.layer = space.gameObject.layer;
            tub.transform.SetParent(space, false);
            tub.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = tub.AddComponent<MeshRenderer>();
            r.sharedMaterial = Dark(ref tubMaterial, "Maletero", 0.07f);
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A dark warm grey of the kit's (`grey` its red, a little less green and blue), made the first time.</summary>
        static Material Dark(ref Material made, string name, float grey)
        {
            if (made != null) return made;
            made = KitMaterials.New(KitMaterials.Plain, name);
            if (made != null) made.SetColor("_BaseColor", new Color(grey, grey * 0.93f, grey * 0.86f));
            return made;
        }
    }
}
