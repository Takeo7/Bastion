using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// La Lata, the squad's vehicle (0.10.1's strategy/mula.ts and game/evacBus.ts). Before it is La Mula it is one of
    /// Apocalypse's vehicles with its own armour (playtest 18/10, #45; GameState.vehicle): 0 the 4×4 with bars on its
    /// windows and spikes on its nose, 1 the TV van with its dish on the roof (its back doors swing open), 2 the army lorry
    /// under its canvas; 3 La Mula, POLYGON Town's bus with Apocalypse's steel plates over its windows and on its roof. Its
    /// wheels turn, its doors open and shut, its headlights glow while it drives.
    /// Parked (Park), it goes along its nose gathering speed and is gone (Depart), and comes back backing in, slowing to a
    /// stop (Arrive); each moves on with the game's time, or with a strip's step (IShotStep).
    /// Its places are 0.10.1's, in tiles of its world (x east, z south, its heading from +z towards +x); its model is
    /// Unity's prefab, nose +z and its right +x: 0.10.1's body mirrored across x (its doors on its −x side there).
    /// </summary>
    public sealed class LaLata : MonoBehaviour, IShotStep
    {
        /// <summary>The prefabs each vehicle is built from (MapArt.ModelKey's form), for the game's catalog (MapCatalogMake).</summary>
        public static readonly string[] Bodies = { "apocalypse:SM_Veh_Car_Medium_01", "apocalypse:SM_Veh_NewsVan_01", "apocalypse:SM_Veh_Army_Truck_01", "town:SM_Veh_Bus_01" };
        /// <summary>La Mula's plates (mula.ts MULA_MODELS): plain, with a slit to see through, and the roof's.</summary>
        const string Plate = "apocalypse:SM_Veh_Attach_Plating_01", PlateSlit = "apocalypse:SM_Veh_Attach_Plating_10", PlateRoof = "apocalypse:SM_Veh_Attach_Plating_05";
        public static IEnumerable<string> Models => Bodies.Concat(new[] { Plate, PlateSlit, PlateRoof });

        /// <summary>Seconds the doors take to shut, the vehicle to pull away and be gone, and to back in (evacBus.ts).</summary>
        const float ShutSeconds = 0.35f, AwaySeconds = 2.2f, ArriveSeconds = 2.6f, FadeSeconds = 0.9f;
        /// <summary>The headlights' glow standing, and over it driving by day and by night (Arturo 17/10: by night they dazzled).</summary>
        const float LightsStanding = 0.2f, LightsDay = 2.6f, LightsNight = 1.3f;
        const float Tile = MapSpace.Tile;

        /// <summary>0 4×4, 1 van, 2 lorry, 3 the bus.</summary>
        public int Vehicle { get; private set; } = 3;
        /// <summary>Its length and width, in tiles (once built; the bus's until then).</summary>
        public float Length => built ? size.z / Tile : 7;
        public float Width => built ? size.x / Tile : 1.7f;
        /// <summary>Its width over the bus's plates (the others have none), in tiles: what one leaning on it has its shoulder to.</summary>
        public float PlatedWidth => built ? plated.x / Tile : 1.7f;
        /// <summary>Whether one gets in on its one side only (the bus): it is mirrored to have its doors towards the squad.</summary>
        public bool OneSided => Vehicle >= 3;
        /// <summary>Whether it is going or coming.</summary>
        public bool Moving => motions.Count > 0;
        /// <summary>Whether it is out of sight (gone, or parked hidden).</summary>
        public bool Shown => body != null && body.activeSelf;

        /// <summary>Where it is parked (0.10.1's world, tiles: its middle, past the bay's shift) and the way its nose points (radians from +z).</summary>
        public Vector3 At => at;
        public float Heading => heading;

        bool built;
        GameObject body;
        Vector3 size, plated;
        Bounds box;
        readonly List<Transform> wheels = new List<Transform>();
        readonly List<Quaternion> wheelsAt = new List<Quaternion>();
        float wheelAngle, wheelRadius = 0.45f;
        /// <summary>Each door leaf: where it is shut and open (its parent's space), and how far it swings (degrees about y) open.</summary>
        sealed class Door
        {
            public Transform leaf;
            public Vector3 shut, open;
            public Quaternion at;
            public float swing;
            public float k;
            /// <summary>Its middle shut, in 0.10.1's body (tiles), to tell which doorway it closes.</summary>
            public Vector3 centre;
        }
        readonly List<Door> doors = new List<Door>();
        /// <summary>Each doorway's middle, in 0.10.1's body (tiles: its doors on −x).</summary>
        readonly List<Vector3> doorCentres = new List<Vector3>();
        Material lights;
        float glow = LightsStanding;
        bool night;

        /// <summary>Where it is parked, in 0.10.1's world (tiles), its heading, mirrored or not.</summary>
        Vector3 at;
        float heading;
        bool mirror;
        bool open;

        /// <summary>What it is doing, a step at a time: each for so long, told how far through it is (0 to 1).</summary>
        sealed class Motion
        {
            public float seconds, t;
            public Action<float> step;
            public Action done;
        }
        readonly Queue<Motion> motions = new Queue<Motion>();

        /// <summary>La Lata as `vehicle`, built from `prefabOf`'s models (MapLab.Prefabs, MapCatalog.PrefabOf); `night` its lights by night.</summary>
        public static LaLata Make(Transform parent, int vehicle, Func<string, GameObject> prefabOf, bool night = false)
        {
            var o = new GameObject("La Lata");
            o.transform.SetParent(parent, false);
            var lata = o.AddComponent<LaLata>();
            lata.night = night;
            lata.Build(vehicle, prefabOf);
            return lata;
        }

        void Build(int vehicle, Func<string, GameObject> prefabOf)
        {
            Vehicle = Mathf.Clamp(vehicle, 0, 3);
            var prefab = prefabOf(Bodies[Vehicle]);
            if (prefab == null)
            {
                Debug.LogWarning($"[lata] no model {Bodies[Vehicle]}");
                return;
            }
            body = new GameObject("Cuerpo");
            body.transform.SetParent(transform, false);
            var model = Instantiate(prefab, body.transform, false);
            model.name = prefab.name;
            // The lorry as 0.10.1's: its body, glass, armour, bars and canvas (the prefab has the canvas off), not its trailer or tank.
            foreach (var t in model.GetComponentsInChildren<Transform>(true).ToList())
            {
                if (t == model.transform) continue;
                if (t.name.Contains("Trailer") || t.name.Contains("_Tank_")) DestroyNow(t.gameObject);
                else if (t.name.Contains("Canopy")) t.gameObject.SetActive(true);
            }
            // On the middle of its own body's parts (mula.ts: the kit's model, centred, on the floor), its armour and the rest
            // round it where they were made to go.
            var own = BoundsOf(model.transform, body.transform, r => !Extra(r.name));
            model.transform.localPosition = new Vector3(-own.center.x, -own.min.y, -own.center.z);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Wheel"))
                {
                    wheels.Add(t);
                    wheelsAt.Add(t.localRotation);
                }
            }
            if (wheels.Count > 0) wheelRadius = BoundsOf(wheels[0], wheels[0], r => true).size.y / 2;
            // Its size without the bus's plates (mula.ts: the bus's model.size): one leaning on it stands LeanOff past its
            // side, that already its shoulder to the plates (with them, a third of a tile off: Gameplay 26/10).
            box = BoundsOf(body.transform, body.transform, r => true);
            size = box.size;
            if (Vehicle == 3) Plates(model.transform, prefabOf);
            plated = BoundsOf(body.transform, body.transform, r => true).size;
            if (Vehicle == 0) CutDoors(model.transform);
            Doors(model.transform);
            Lamps();
            built = true;
        }

        /// <summary>What 0.10.1 put on a vehicle's body from other models: its armour, its window bars, the van's roof props, the lorry's canvas.</summary>
        static bool Extra(string name) => name.Contains("Armour") || name.Contains("Roof_Props") || name.Contains("Canopy");

        /// <summary>The doors (mula.ts): the van's back doors on their outer edges, swinging out and round; the bus's leaves sliding in, along it.</summary>
        void Doors(Transform model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("_Glass")) continue;
                if (Vehicle == 1 && (t.name.EndsWith("Door_l") || t.name.EndsWith("Door_r")))
                {
                    // Its hinge is its pivot: the outer edge. The left one (−x here) swings its free end back and out.
                    float swing = (t.localPosition.x < 0 ? 1 : -1) * 1.97f * Mathf.Rad2Deg;
                    doors.Add(new Door { leaf = t, shut = t.localPosition, open = t.localPosition, at = t.localRotation, swing = swing, k = 1, centre = Body(BoundsOf(t, body.transform, r => true).center) });
                }
                else if (Vehicle == 3 && System.Text.RegularExpressions.Regex.IsMatch(t.name, @"Door_0\d$"))
                {
                    var b = BoundsOf(t, body.transform, r => true);
                    int digit = t.name[t.name.Length - 1] - '0';
                    // In, off its side (0.10.1's +x is in, here −x), and along the bus away from its doorway's middle.
                    var open = t.localPosition + t.parent.InverseTransformVector(body.transform.TransformVector(new Vector3(-0.14f * Tile, 0, (digit % 2 == 1 ? 1 : -1) * b.size.z * 0.85f)));
                    doors.Add(new Door { leaf = t, shut = t.localPosition, open = open, at = t.localRotation, swing = 0, k = 1, centre = Body(b.center) });
                    doorCentres.Add(Body(b.center));
                }
            }
            if (Vehicle == 0)
                // By its doors on either side, each door's middle (mula.ts CAR_DOORS: along it, from its back), in 0.10.1's
                // order: its −x side's first (here +x), the driver's.
                foreach (float x in new[] { box.max.x, box.min.x })
                    foreach (var (from, to, _) in CarDoors)
                        doorCentres.Add(Body(new Vector3(x, 0.5f * Tile, box.min.z + size.z * (from + to) / 2)));
            else if (Vehicle < 3) doorCentres.Add(Body(new Vector3(box.center.x, 0.6f * Tile, box.min.z)));
        }

        /// <summary>
        /// The 4×4's doors (mula.ts CAR_DOORS; playtest 20/10 #7: get in, not vanish in the gap): Synty's body has them in its
        /// one shell, so they are cut out of it and its glass: along the body (fractions of its length from the back), front
        /// and rear, as its handles and mirrors place them; from the sill to the roof's edge (fractions of its height); the
        /// outer share of its width either side. Each hangs on its front edge, `hinge` tiles in from the widest, and swings
        /// out (radians).
        /// </summary>
        static readonly (float from, float to, float hinge)[] CarDoors = { (0.435f, 0.587f, 0.13f), (0.253f, 0.435f, 0.13f) };
        const float CarDoorLow = 0.09f, CarDoorHigh = 0.845f, CarDoorSide = 0.55f, CarDoorSwing = 1.15f;

        /// <summary>Each shell's mesh cut (CutDoors): what stays, and each door's (none: nothing of it there), by the mesh's id.</summary>
        static readonly Dictionary<int, (Mesh rest, Mesh[] doors)> cuts = new Dictionary<int, (Mesh, Mesh[])>();

        /// <summary>
        /// The 4×4's four doors cut out of its shell and glass (mula.ts carDoors; not its armour or its window bars, which
        /// 0.10.1 left on the body): a triangle whose three corners are in one door's box goes with that door; each door a
        /// leaf on its hinge, in 0.10.1's order (its −x side's, here +x, front and rear; then the other side's).
        /// </summary>
        void CutDoors(Transform model)
        {
            float midX = box.center.x, half = size.x / 2;
            float low = box.min.y + size.y * CarDoorLow, high = box.min.y + size.y * CarDoorHigh;
            var cut = new List<(int side, float z0, float z1)>();
            var leaves = new List<Transform>();
            foreach (int side in new[] { 1, -1 })
                foreach (var (from, to, hinge) in CarDoors)
                {
                    cut.Add((side, box.min.z + size.z * from, box.min.z + size.z * to));
                    var leaf = new GameObject("Puerta").transform;
                    leaf.SetParent(body.transform, false);
                    leaf.localPosition = new Vector3(midX + side * (half - hinge * Tile), 0, box.min.z + size.z * to);
                    leaves.Add(leaf);
                }
            float slack = 0.01f * Tile;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (Extra(r.name) || r.name.Contains("Wheel")) continue;
                var f = r.GetComponent<MeshFilter>();
                var mesh = f != null ? f.sharedMesh : null;
                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"[lata] {mesh.name} isn't readable: the 4×4's doors stay shut in it (MapCatalogMake.Run)");
                    continue;
                }
                if (!cuts.TryGetValue(mesh.GetInstanceID(), out var made))
                {
                    var toBody = body.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    int Which(Vector3 v)
                    {
                        var p = toBody.MultiplyPoint3x4(v);
                        if (p.y < low || p.y > high || Mathf.Abs(p.x - midX) < half * CarDoorSide) return -1;
                        for (int k = 0; k < cut.Count; k++)
                            if (Mathf.Sign(p.x - midX) == cut[k].side && p.z >= cut[k].z0 - slack && p.z <= cut[k].z1 + slack) return k;
                        return -1;
                    }
                    made = cuts[mesh.GetInstanceID()] = Split(mesh, mesh.vertices.Select(Which).ToArray(), cut.Count);
                }
                f.sharedMesh = made.rest;
                for (int k = 0; k < cut.Count; k++)
                {
                    if (made.doors[k] == null) continue;
                    // As the shell was, then onto the leaf (which, shut, is only moved off it).
                    var piece = new GameObject(r.name);
                    piece.layer = r.gameObject.layer;
                    piece.transform.SetParent(r.transform.parent, false);
                    piece.transform.localPosition = r.transform.localPosition;
                    piece.transform.localRotation = r.transform.localRotation;
                    piece.transform.localScale = r.transform.localScale;
                    piece.transform.SetParent(leaves[k], true);
                    piece.AddComponent<MeshFilter>().sharedMesh = made.doors[k];
                    var pr = piece.AddComponent<MeshRenderer>();
                    pr.sharedMaterials = r.sharedMaterials;
                    pr.shadowCastingMode = r.shadowCastingMode;
                    pr.receiveShadows = r.receiveShadows;
                }
            }
            for (int k = 0; k < leaves.Count; k++)
            {
                var leaf = leaves[k];
                // A point behind the hinge swings out to its side (0.10.1's turn about y, here the other way: x mirrored).
                float swing = (cut[k].side > 0 ? -1 : 1) * CarDoorSwing * Mathf.Rad2Deg;
                doors.Add(new Door { leaf = leaf, shut = leaf.localPosition, open = leaf.localPosition, at = Quaternion.identity, swing = swing, k = 0, centre = Body(BoundsOf(leaf, body.transform, r => true).center) });
            }
        }

        /// <summary>`mesh` cut by `which` (each vertex's door, or −1): what stays, and each of `count` doors' triangles (null if none).</summary>
        static (Mesh rest, Mesh[] doors) Split(Mesh mesh, int[] which, int count)
        {
            var rest = UnityEngine.Object.Instantiate(mesh);
            rest.name = mesh.name + " (sin puertas)";
            var parts = new List<int>[count, mesh.subMeshCount];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                var kept = new List<int>(tris.Length);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int k = which[tris[t]];
                    if (k >= 0 && which[tris[t + 1]] == k && which[tris[t + 2]] == k)
                    {
                        (parts[k, s] ??= new List<int>()).AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
                        continue;
                    }
                    kept.Add(tris[t]);
                    kept.Add(tris[t + 1]);
                    kept.Add(tris[t + 2]);
                }
                rest.SetTriangles(kept, s, false);
            }
            rest.RecalculateBounds();
            var doors = new Mesh[count];
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var colours = mesh.colors32;
            var uvs = Enumerable.Range(0, 4).Select(c => { var l = new List<Vector2>(); mesh.GetUVs(c, l); return l; }).ToArray();
            for (int k = 0; k < count; k++)
            {
                var used = new Dictionary<int, int>();
                for (int s = 0; s < mesh.subMeshCount; s++)
                    if (parts[k, s] != null)
                        foreach (int i in parts[k, s])
                            if (!used.ContainsKey(i)) used[i] = used.Count;
                if (used.Count == 0) continue;
                var order = used.OrderBy(p => p.Value).Select(p => p.Key).ToArray();
                var door = new Mesh { name = $"{mesh.name} (puerta {k})" };
                if (order.Length > 65535) door.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                door.vertices = order.Select(i => vertices[i]).ToArray();
                if (normals.Length == vertices.Length) door.normals = order.Select(i => normals[i]).ToArray();
                if (tangents.Length == vertices.Length) door.tangents = order.Select(i => tangents[i]).ToArray();
                if (colours.Length == vertices.Length) door.colors32 = order.Select(i => colours[i]).ToArray();
                for (int c = 0; c < 4; c++)
                    if (uvs[c].Count == vertices.Length) door.SetUVs(c, order.Select(i => uvs[c][i]).ToList());
                door.subMeshCount = mesh.subMeshCount;
                for (int s = 0; s < mesh.subMeshCount; s++)
                    door.SetTriangles(parts[k, s] != null ? parts[k, s].Select(i => used[i]).ToArray() : new int[0], s, false);
                door.RecalculateBounds();
                doors[k] = door;
            }
            return (rest, doors);
        }

        /// <summary>A point of the body (here, metres) in 0.10.1's body (tiles, mirrored across x).</summary>
        static Vector3 Body(Vector3 p) => new Vector3(-p.x, p.y, p.z) / Tile;

        /// <summary>The headlights (mula.ts): a pair of glowing boxes at its front, dim while it stands.</summary>
        void Lamps()
        {
            var source = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultMaterial : null;
            lights = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lights.name = "Faros";
            lights.SetColor("_BaseColor", new Color32(0x2a, 0x2a, 0x20, 255));
            lights.EnableKeyword("_EMISSION");
            lights.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Glow(LightsStanding);
            bool bus = Vehicle == 3;
            var lamp = bus ? new Vector3(0.28f, 0.14f, 0.05f) : new Vector3(0.2f, 0.1f, 0.04f);
            foreach (int side in new[] { -1, 1 })
            {
                var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
                o.name = "Faro";
                DestroyNow(o.GetComponent<Collider>());
                o.transform.SetParent(body.transform, false);
                o.transform.localScale = lamp * Tile;
                float x = bus ? side * 0.75f * Tile : box.center.x + side * (size.x / 2 - 0.25f * Tile);
                float y = (bus ? 0.55f * Tile : size.y * 0.3f) + lamp.y * Tile / 2;
                o.transform.localPosition = new Vector3(x, y, box.max.z + (bus ? 0.04f : 0.005f) * Tile);
                var r = o.GetComponent<MeshRenderer>();
                r.sharedMaterial = lights;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void Glow(float k)
        {
            glow = k;
            // three's emissive (sRGB) times its intensity, in linear; given as sRGB, which the material makes linear again.
            if (lights != null) lights.SetColor("_EmissionColor", (((Color)new Color32(0xff, 0xf2, 0xc0, 255)).linear * k).gamma);
        }

        /// <summary>
        /// La Mula's plates (mula.ts buildMula): steel over its windows on both sides but its doorways, the windscreen half
        /// covered with slits to drive by, plates on the roof. The places are 0.10.1's on its kit bus (tiles, centred);
        /// a plate is Apocalypse's prefab, a quarter turn off its source as its kit model was (MapArt.UnityTurn).
        /// </summary>
        void Plates(Transform model, Func<string, GameObject> prefabOf)
        {
            var glass = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SM_Veh_Bus_Glass");
            var all = BoundsOf(model, body.transform, r => true);
            var band = glass != null ? BoundsOf(glass, body.transform, r => true) : new Bounds(new Vector3(0, 1.4f, -0.1f) * Tile, new Vector3(2.2f, 0.8f, 7f) * Tile);
            // In 0.10.1's tiles, on its body (x mirrored, which the bus is across).
            float minY = band.min.y / Tile, maxY = band.max.y / Tile, minZ = band.min.z / Tile, maxZ = band.max.z / Tile;
            float windowY = (minY + maxY) / 2 + 0.05f;
            float plateH = SizeOf(prefabOf(PlateSlit)).y / Tile * 1.15f;
            float sy = Mathf.Max(1, (maxY - minY) / (plateH > 0 ? plateH : 0.45f * 1.15f));
            float halfW = all.size.x / Tile / 2 + 0.02f;
            // The doorways' middles along it (as yet unknown: from the door leaves).
            var leaves = model.GetComponentsInChildren<Transform>(true).Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, @"Door_0\d$")).Select(t => BoundsOf(t, body.transform, r => true).center.z / Tile).ToList();
            bool InDoorway(float z) => leaves.Any(c => Mathf.Abs(c - z) < 0.75f);
            var root = new GameObject("Planchas").transform;
            root.SetParent(body.transform, false);
            for (float z = minZ + 0.35f; z < maxZ - 0.2f; z += 0.68f)
                foreach (int side in new[] { -1, 1 })
                {
                    // 0.10.1's doors on −x: here +x.
                    if (side < 0 && InDoorway(z)) continue;
                    PutPlate(root, prefabOf, Mathf.RoundToInt(z * 3) % 3 != 0 ? Plate : PlateSlit, side * halfW, windowY - 0.25f * sy, z, 0, 1.15f, sy);
                }
            float nose = all.size.z / Tile / 2 + 0.03f;
            foreach (float x in new[] { -0.55f, 0.55f }) PutPlate(root, prefabOf, PlateSlit, x, windowY - 0.35f, nose - 0.15f, Mathf.PI / 2, 1.3f, 1);
            for (float z = -2.6f; z <= 2.6f + 1e-4f; z += 1.3f) PutPlate(root, prefabOf, PlateRoof, Mathf.RoundToInt(z) % 2 * 0.2f, all.size.y / Tile + 0.01f, z, 0.05f * z, 1.1f, 1);
        }

        /// <summary>
        /// A kit model (centred on its box, on the floor) at (x, y, z) of 0.10.1's body, turned `yaw` (counter-clockwise from
        /// above), scaled s and sy more up: baseKit.ts putMatrix, in the body here (x mirrored, metres). The prefab stands a
        /// quarter turn from its source (Apocalypse's: MapArt.UnityTurn), and the kit had the source's frame.
        /// </summary>
        void PutPlate(Transform parent, Func<string, GameObject> prefabOf, string key, float x, float y, float z, float yaw, float s, float sy)
        {
            var prefab = prefabOf(key);
            if (prefab == null) return;
            var holder = new GameObject(prefab.name).transform;
            holder.SetParent(parent, false);
            var plate = Instantiate(prefab, holder, false);
            var b = BoundsOf(plate.transform, holder, r => true);
            plate.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            var flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
            var flipZ = Matrix4x4.Scale(new Vector3(1, 1, -1));
            // three's turn about y and Unity's Euler have the same matrix: 0.10.1's body frame, mirrored after.
            var put = Matrix4x4.TRS(new Vector3(x, y, z) * Tile, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0), new Vector3(s, s * sy, s));
            var m = flipX * put * flipZ * Matrix4x4.Rotate(Quaternion.Euler(0, 90 * MapArt.UnityTurnOf(key), 0));
            holder.localPosition = m.GetColumn(3);
            holder.localRotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
            holder.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
        }

        static Vector3 SizeOf(GameObject prefab)
        {
            if (prefab == null) return Vector3.zero;
            var o = Instantiate(prefab);
            var b = BoundsOf(o.transform, o.transform, r => true);
            DestroyNow(o);
            return b.size;
        }

        /// <summary>The box of what renders under `root` (its renderers that `take`), in `space`'s frame.</summary>
        static Bounds BoundsOf(Transform root, Transform space, Func<Renderer, bool> take)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!take(r)) continue;
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var mb = mesh.bounds;
                var m = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) b = new Bounds(c, Vector3.zero);
                    else b.Encapsulate(c);
                    any = true;
                }
            }
            return b;
        }

        /// <summary>
        /// Parked at `at` (0.10.1's world, tiles), its nose the way `heading` (radians from +z), its doors on the side
        /// towards `doorsTo` (the bus mirrored if they'd face away: the others get in either side or at the back), shown or
        /// not; in a bay `bay` tiles long centred on `at`, a shorter one at its front; its doors open or shut (evacBus.ts park).
        /// </summary>
        public void Park(Vector3 at, float heading, Vector3 doorsTo, bool shown = true, float bay = 0, bool open = true)
        {
            motions.Clear();
            float ahead = Mathf.Max(0, (bay - Length) / 2);
            this.at = at + new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading)) * ahead;
            this.heading = heading;
            // The bus's doors are on its −x side: (−cos, 0, sin) of its heading in 0.10.1's world.
            var side = new Vector3(-Mathf.Cos(heading), 0, Mathf.Sin(heading));
            mirror = OneSided && Vector3.Dot(side, doorsTo - at) < 0;
            this.open = open;
            Place(this.at);
            if (body != null) body.SetActive(shown);
            Glow(LightsStanding);
            SetDoors(open ? 1 : 0);
        }

        /// <summary>Where it stands now (0.10.1's world, tiles), its nose along its heading.</summary>
        void Place(Vector3 tiles)
        {
            transform.localPosition = new Vector3(tiles.x * Tile, tiles.y * Tile, -tiles.z * Tile);
            transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(heading), 0, -Mathf.Cos(heading)), Vector3.up);
            transform.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
        }

        /// <summary>Every door at `k` of the way open.</summary>
        public void SetDoors(float k)
        {
            foreach (var d in doors) DoorAt(d, k);
        }

        /// <summary>How many ways in it has (Doorways), and the leaves of way `way` at `k` of the way open (a double doorway's both).</summary>
        public int Ways => doorCentres.Count;

        /// <summary>`twins` false, only that doorway's own leaves (the 4×4's: its front and rear doors are nearer than 0.7).</summary>
        public void SetWay(int way, float k, bool twins = true)
        {
            if (way < 0 || way >= doorCentres.Count) return;
            var centre = doorCentres[way];
            foreach (var d in doors)
            {
                // Each leaf with the doorway nearest it (evacBus.ts leavesByDoorway), and a doorway's twin within 0.7.
                int nearest = 0;
                for (int i = 1; i < doorCentres.Count; i++)
                    if (Vector3.Distance(doorCentres[i], d.centre) < Vector3.Distance(doorCentres[nearest], d.centre)) nearest = i;
                if (twins ? Vector3.Distance(doorCentres[nearest], centre) < 0.7f : nearest == way) DoorAt(d, k);
            }
        }

        static void DoorAt(Door d, float k)
        {
            d.k = k;
            d.leaf.localPosition = Vector3.Lerp(d.shut, d.open, k);
            d.leaf.localRotation = d.at * Quaternion.Euler(0, d.swing * k, 0);
        }

        /// <summary>Where one gets in, in 0.10.1's world (tiles): each doorway's middle, on the ground.</summary>
        public List<Vector3> Doorways()
        {
            float c = Mathf.Cos(heading), s = Mathf.Sin(heading);
            return doorCentres.Select(p =>
            {
                float x = mirror ? -p.x : p.x;
                return at + new Vector3(x * c + p.z * s, 0, -x * s + p.z * c);
            }).ToList();
        }

        void Turn(float tiles)
        {
            wheelAngle += tiles * Tile / Mathf.Max(wheelRadius, 0.05f) * Mathf.Rad2Deg;
            for (int i = 0; i < wheels.Count; i++) wheels[i].localRotation = wheelsAt[i] * Quaternion.Euler(wheelAngle, 0, 0);
        }

        /// <summary>
        /// Its doors shut, its lights on, then away along its nose `distance` tiles, faster and faster, and gone (back where
        /// it parks, out of sight: the way it comes back to). With `fade` (its way out is built over) it goes where it stands.
        /// </summary>
        public void Depart(float distance = 12, bool fade = false) => Depart(distance, fade, null);

        /// <summary>Depart, waited for: done once it is gone (at once if it isn't there), or when `ct` is cancelled.</summary>
        public async Awaitable DepartAsync(float distance = 12, bool fade = false, System.Threading.CancellationToken ct = default)
        {
            if (!Shown) return;
            bool gone = false;
            Depart(distance, fade, () => gone = true);
            while (!gone && !ct.IsCancellationRequested && this != null) await Awaitable.NextFrameAsync(ct);
        }

        void Depart(float distance, bool fade, Action gone)
        {
            if (!Shown)
            {
                gone?.Invoke();
                return;
            }
            var from = doors.Select(d => d.k).ToList();
            float lit = night ? LightsNight : LightsDay;
            Then(ShutSeconds, t =>
            {
                for (int i = 0; i < doors.Count; i++) DoorAt(doors[i], from[i] * (1 - t));
                Glow(LightsStanding + t * lit);
            });
            if (fade)
            {
                Then(FadeSeconds, t => Fade(1 - t), () =>
                {
                    body.SetActive(false);
                    Unfade();
                    gone?.Invoke();
                });
                return;
            }
            float last = 0;
            Then(AwaySeconds, t =>
            {
                float along = distance * t * t;
                Place(at + new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading)) * along);
                Turn(along - last);
                last = along;
            }, () =>
            {
                body.SetActive(false);
                Place(at);
                gone?.Invoke();
            });
        }

        /// <summary>
        /// Back to where it is parked, backed in along its line from `distance` tiles out, slowing to a stop, nose out, its
        /// lights off and its doors open at the end (evacBus.ts arrive). With `fade` (its way in is built over) it comes where it stands.
        /// </summary>
        public void Arrive(float distance = 14, bool fade = false)
        {
            if (body == null) return;
            float lit = night ? LightsNight : LightsDay;
            Then(0, _ =>
            {
                body.SetActive(true);
                SetDoors(0);
                Glow(LightsStanding + lit);
            });
            if (fade) Then(FadeSeconds, t => Fade(t), Unfade);
            else
            {
                float last = distance;
                Then(ArriveSeconds, t =>
                {
                    float along = distance * (1 - t) * (1 - t);
                    Place(at + new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading)) * along);
                    Turn(along - last);
                    last = along;
                });
            }
            Then(ShutSeconds, t =>
            {
                Place(at);
                SetDoors(t);
                Glow(LightsStanding + (1 - t) * lit);
            });
        }

        /// <summary>Each renderer's own materials while it is seen through (their copies, transparent: Synty's shader allows it), and the copies' colours.</summary>
        readonly Dictionary<Renderer, Material[]> unfaded = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Material, Color> fadeColours = new Dictionary<Material, Color>();

        /// <summary>
        /// Seen through at opacity `a` (evacBus.ts fade, its way built over): on transparent copies of its materials meanwhile
        /// (shared with every other model of them), the alpha of their base colour, which their shader takes (Synty's).
        /// </summary>
        void Fade(float a)
        {
            if (unfaded.Count == 0)
                foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    unfaded[r] = mats;
                    r.sharedMaterials = mats.Select(Seethrough).ToArray();
                }
            foreach (var kv in fadeColours) kv.Key.SetColor("_BaseColor", new Color(kv.Value.r, kv.Value.g, kv.Value.b, kv.Value.a * a));
        }

        Material Seethrough(Material m)
        {
            if (m == null) return null;
            var c = new Material(m) { name = m.name + " (se desvanece)" };
            c.SetFloat("_Surface", 1);
            c.SetFloat("_Blend", 0);
            c.SetOverrideTag("RenderType", "Transparent");
            c.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            c.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            c.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            c.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            c.SetFloat("_ZWrite", 0);
            c.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            c.renderQueue = (int)RenderQueue.Transparent;
            fadeColours[c] = c.HasProperty("_BaseColor") ? c.GetColor("_BaseColor") : Color.white;
            return c;
        }

        /// <summary>Its own materials back.</summary>
        void Unfade()
        {
            foreach (var kv in unfaded) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            unfaded.Clear();
            foreach (var c in fadeColours.Keys) DestroyNow(c);
            fadeColours.Clear();
        }

        void Then(float seconds, Action<float> step, Action done = null) => motions.Enqueue(new Motion { seconds = seconds, step = step, done = done });

        void Update()
        {
            if (Application.isPlaying) Advance(Time.deltaTime);
        }

        public void ShotStep(float dt) => Advance(dt);

        void Advance(float dt)
        {
            while (motions.Count > 0 && dt >= 0)
            {
                var m = motions.Peek();
                float left = m.seconds - m.t;
                float used = Mathf.Min(dt, left);
                m.t += used;
                dt -= used;
                m.step(m.seconds > 0 ? Mathf.Clamp01(m.t / m.seconds) : 1);
                if (m.t < m.seconds) return;
                motions.Dequeue();
                m.done?.Invoke();
                if (dt <= 0 && motions.Count > 0 && motions.Peek().seconds > 0) return;
            }
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
