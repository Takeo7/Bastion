using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// N5, the motorbike (Arturo, 17/11: a new expedition sets out with 2 seats on one; 4 the 4×4, 6 the van, 8 the bus).
    ///   Candidatas: Synty POLYGON's candidates, one a frame, side on (their fronts east) with a soldier standing by at the
    ///   game's size (Figures.CharacterScale against the vehicle's 1, as La Lata and the figures are drawn), from three
    ///   quarters and a little above. In the log each one's size and the top of its middle line along it, a tenth of a metre
    ///   a step (to find its seat). BASTION_MOTOS: prefab paths under Assets/Synty without .prefab, by commas (all the
    ///   candidates if not said); BASTION_GIRO the camera's turn from the south (degrees, 35), BASTION_ALTO how high it
    ///   looks down from (degrees, 18). BASTION_MONTADOS=1: two soldiers sat on it instead of one standing by (Mixamo's
    ///   «Sitting Idle 3» on a chair, the nearest thing there is to riding), the rider and the one behind at Seats' places
    ///   along it from its back, their hips on the top there (Tops); on the Military one's tray, sat on its floor
    ///   («Sitting Idle 2»).
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.MotoLab.Candidatas -shotFrames 9 -shotSize 1600x900 -shotOut dir
    /// </summary>
    public static class MotoLab
    {
        static readonly string[] Candidates =
        {
            "PolygonApocalypse/Prefabs/Vehicles/SM_Veh_Motorbike_01", "PolygonApocalypse/Prefabs/Vehicles/SM_Veh_Motorbike_Apoco_01",
            "PolygonApocalypse/Prefabs/Vehicles/SM_Veh_Moped_01", "PolygonMilitary/Prefabs/Vehicles/SM_Veh_Motorbike_01",
            "PolygonPoliceStation/Prefabs/Vehicles/SM_Veh_Motorbike_01", "PolygonPoliceStation/Prefabs/Vehicles/SM_Veh_Motorbike_02",
            "PolygonPalmCity/Prefabs/Vehicles/SM_Veh_Motorbike_01", "PolygonPalmCity/Prefabs/Vehicles/SM_Veh_Quad_Bike_01",
        };

        /// <summary>
        /// Where each one's rider and the one behind sit (metres from its back, Synty's size), by eye from its top along it
        /// (the seat's flat behind the tank); the Military one's second is on its tray's floor.
        /// </summary>
        static readonly Dictionary<string, (float rider, float behind)> Seats = new Dictionary<string, (float, float)>
        {
            ["PolygonApocalypse/Prefabs/Vehicles/SM_Veh_Motorbike_01"] = (1.0f, 0.5f),
            ["PolygonApocalypse/Prefabs/Vehicles/SM_Veh_Motorbike_Apoco_01"] = (1.05f, 0.55f),
            ["PolygonApocalypse/Prefabs/Vehicles/SM_Veh_Moped_01"] = (0.7f, 0.35f),
            ["PolygonMilitary/Prefabs/Vehicles/SM_Veh_Motorbike_01"] = (2.65f, 1.2f),
            ["PolygonPoliceStation/Prefabs/Vehicles/SM_Veh_Motorbike_01"] = (0.85f, 0.5f),
            ["PolygonPoliceStation/Prefabs/Vehicles/SM_Veh_Motorbike_02"] = (1.0f, 0.65f),
            ["PolygonPalmCity/Prefabs/Vehicles/SM_Veh_Motorbike_01"] = (0.95f, 0.55f),
            ["PolygonPalmCity/Prefabs/Vehicles/SM_Veh_Quad_Bike_01"] = (1.35f, 0.95f),
        };

        public static void Candidatas()
        {
            bool ridden = Environment.GetEnvironmentVariable("BASTION_MONTADOS") == "1";
            MapLab.Lighting(true);
            var paths = Environment.GetEnvironmentVariable("BASTION_MOTOS")?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray() ?? Candidates;
            var shown = new List<(string name, GameObject o, Bounds box)>();
            float x = 0;
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Synty/{path}.prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"[moto] missing {path}");
                    continue;
                }
                var holder = new GameObject(path.Split('/').Last()).transform;
                var bike = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                bike.transform.SetParent(holder, false);
                // (Its front, +z as Synty's vehicles, to the east.)
                bike.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var box = BoundsOf(bike.transform);
                bike.transform.position += new Vector3(-box.center.x, -box.min.y, -box.center.z);
                box = BoundsOf(bike.transform);
                Debug.Log(FormattableString.Invariant($"[moto] {path}: {box.size.x:0.00} long, {box.size.z:0.00} wide, {box.size.y:0.00} high"));
                // (Measured where it was made, before its place in the row.)
                var tops = Tops(bike.transform, box);
                Debug.Log($"[moto] {holder.name} top along it (x: y, its middle line ±0.15 m): {Profile(tops)}");
                holder.position = new Vector3(x, 0, 0);
                if (ridden && Seats.TryGetValue(path, out var seats))
                {
                    Sit(holder, box, tops, seats.rider, "sitChair", 0);
                    Sit(holder, box, tops, seats.behind, path.StartsWith("PolygonMilitary/") ? "sitFloor" : "sitChair", 1);
                }
                else
                {
                    // A soldier standing by it, behind it from the camera, by its back wheel: as tall as it shows over it.
                    var figure = Figure(holder, 0, "rifleIdle");
                    if (figure != null)
                    {
                        figure.transform.localPosition = new Vector3(box.min.x + 0.4f, 0, box.extents.z + 0.6f);
                        figure.transform.localRotation = Quaternion.Euler(0, 200, 0);
                    }
                }
                x += 40;
                holder.gameObject.SetActive(shown.Count == 0);
                shown.Add((holder.name, holder.gameObject, BoundsOf(holder)));
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(x / 2, -0.05f, 0);
            floor.transform.localScale = new Vector3(x + 60, 0.1f, 60);
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.55f, 0.54f, 0.5f) };
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 30;
            camera.farClipPlane = 500;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            var cycle = new GameObject("Motos").AddComponent<Cycle>();
            cycle.shown = shown;
            cycle.camera = camera;
            cycle.Frame();
        }

        /// <summary>
        /// La Lata's motorbike (C2, Arturo 17/11: Apocalypse's rigged one) measured in its own frame (nose +z, centred on its
        /// box, on the floor, as LaLata.Build puts it), metres: each part's box; its front along its middle line (|x| under
        /// 0.12) slice by slice up, to find its headlight; the handlebar's ends, where the rider's hands go. Edit mode.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MotoLab.Medidas -quit
        /// </summary>
        public static void Medidas()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonApocalypse/Prefabs/Vehicles/SM_Veh_Motorbike_Apoco_01.prefab");
            var bike = UnityEngine.Object.Instantiate(prefab);
            var box = BoundsOf(bike.transform);
            bike.transform.position = new Vector3(-box.center.x, -box.min.y, -box.center.z);
            box = BoundsOf(bike.transform);
            Debug.Log(FormattableString.Invariant($"[moto] whole: x {box.min.x:0.00}..{box.max.x:0.00}, y {box.min.y:0.00}..{box.max.y:0.00}, z {box.min.z:0.00}..{box.max.z:0.00}"));
            foreach (var r in bike.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                Debug.Log(FormattableString.Invariant($"[moto] part {r.name} (pivot {r.transform.position.x:0.00},{r.transform.position.y:0.00},{r.transform.position.z:0.00}): x {b.min.x:0.00}..{b.max.x:0.00}, y {b.min.y:0.00}..{b.max.y:0.00}, z {b.min.z:0.00}..{b.max.z:0.00}"));
            }
            var front = new SortedDictionary<int, float>();
            var bars = new List<Vector3>();
            foreach (var f in bike.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null || f.name.Contains("Wheel") || f.name.Contains("Shock")) continue;
                foreach (var v in f.sharedMesh.vertices)
                {
                    var w = f.transform.TransformPoint(v);
                    if (f.name.Contains("HandleBars")) bars.Add(w);
                    if (Mathf.Abs(w.x) > 0.12f) continue;
                    int slot = Mathf.FloorToInt(w.y * 20);
                    if (!front.TryGetValue(slot, out float z) || w.z > z) front[slot] = w.z;
                }
            }
            Debug.Log("[moto] front along its middle (y: z): " + string.Join(" ", front.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.00}:{1:0.00}", p.Key / 20f, p.Value))));
            if (bars.Count > 0)
            {
                var left = bars.OrderBy(p => p.x).First();
                var right = bars.OrderByDescending(p => p.x).First();
                Debug.Log(FormattableString.Invariant($"[moto] handlebar ends: left {left.x:0.00},{left.y:0.00},{left.z:0.00}, right {right.x:0.00},{right.y:0.00},{right.z:0.00}"));
            }
            UnityEngine.Object.DestroyImmediate(bike);
        }

        /// <summary>
        /// T21 (Arturo, 18/11: the evacuation's squares as many as its seats, by its doors): each vehicle of the runs (4 the
        /// motorbike, 0 the 4×4, 1 the van, 3 the bus) as La Lata builds it, parked at the origin with its nose north (heading
        /// 0, 0.10.1's −z... its +z in tiles: x east, z south), its doors towards the east: its length and width (tiles), and
        /// each way in (Doorways: a doorway's middle on the ground, tiles from its middle; the motorbike's, where one gets on
        /// each seat). Edit mode.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MotoLab.Puertas -quit
        /// </summary>
        public static void Puertas()
        {
            var prefabOf = MapLab.Prefabs();
            foreach (int vehicle in new[] { 4, 0, 1, 3 })
            {
                var lata = LaLata.Make(null, vehicle, prefabOf);
                lata.Park(Vector3.zero, 0, new Vector3(5, 0, 0));
                var ways = lata.Doorways();
                Debug.Log(FormattableString.Invariant($"[puertas] {vehicle}: {lata.Length:0.00} long, {lata.Width:0.00} wide (tiles), one-sided {lata.OneSided}; ways in (x across, + east; z along, + south, its nose to −z): {string.Join(" ", ways.Select(w => $"({w.x:0.00}, {w.z:0.00})"))}"));
                UnityEngine.Object.DestroyImmediate(lata.gameObject);
            }
        }

        /// <summary>A soldier of the game (a man, the default look) under `holder`, at the game's size, held in `clip` (a camp loop) or standing.</summary>
        static GameObject Figure(Transform holder, int k, string clip)
        {
            var look = Appearances.DefaultAppearance(k % 2, "#e8a33a", "m", "moto" + k);
            var figure = SoldierFigure.Make(Dress.Of(look, "moto" + k, null), "Soldado", true);
            if (figure == null) return null;
            figure.transform.SetParent(holder, false);
            figure.transform.localScale = Vector3.one * Figures.CharacterScale;
            var catalog = FigureCatalog.Load();
            var animator = figure.GetComponentInChildren<Animator>();
            if (animator != null && catalog != null)
            {
                var anim = new FigureAnimator(animator, m => catalog.Clip(Anims.Motion("masc", m, false)));
                if (clip != "rifleIdle") anim.CampLoop(catalog.Clip(clip), true);
                anim.Step(0);
                anim.Step(0.02f);
            }
            return figure;
        }

        /// <summary>
        /// A soldier sat facing its front, its hips `along` metres from its back over its middle line, on the top of what is
        /// there (Tops: the seat, or the Military one's tray floor); the place and its height in the log.
        /// </summary>
        static void Sit(Transform holder, Bounds box, Dictionary<int, float> tops, float along, string clip, int k)
        {
            var at = holder.position + new Vector3(box.min.x + along, 0, box.center.z);
            int slot = Mathf.FloorToInt(along * 10);
            var near = tops.Keys.OrderBy(t => Mathf.Abs(t - slot)).FirstOrDefault();
            float top = tops.Count > 0 ? tops[near] : box.max.y;
            var figure = Figure(holder, k, clip);
            if (figure == null) return;
            figure.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var animator = figure.GetComponentInChildren<Animator>();
            var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            var off = hips != null ? hips.position - figure.transform.position : Vector3.zero;
            // (The hips' bone is at their middle: half a hand over the seat, for the seat of the trousers.)
            figure.transform.position = new Vector3(at.x - off.x, top + 0.12f - off.y, at.z - off.z);
            Debug.Log(FormattableString.Invariant($"[moto] {holder.name} {(k == 0 ? "rider" : "behind")} at {along:0.00} m from its back: seat {top:0.00} m ({clip}, hips {off.y:0.00} over the feet)"));
        }

        /// <summary>The top of what is along its middle line (±0.15 m across), every tenth of a metre from its back to its front.</summary>
        static string Profile(Dictionary<int, float> tops) =>
            string.Join(" ", tops.OrderBy(p => p.Key).Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.0}:{1:0.00}", p.Key / 10f, p.Value)));

        /// <summary>Profile's numbers: by its tenth of a metre from the back, the top there.</summary>
        static Dictionary<int, float> Tops(Transform bike, Bounds box)
        {
            var tops = new Dictionary<int, float>();
            foreach (var f in bike.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = f.sharedMesh;
                if (mesh == null || f.GetComponent<Renderer>() == null || !f.GetComponent<Renderer>().enabled) continue;
                foreach (var v in mesh.vertices)
                {
                    var w = f.transform.TransformPoint(v);
                    if (Mathf.Abs(w.z - box.center.z) > 0.15f) continue;
                    int slot = Mathf.FloorToInt((w.x - box.min.x) * 10);
                    if (!tops.TryGetValue(slot, out float top) || w.y > top) tops[slot] = w.y;
                }
            }
            return tops;
        }

        /// <summary>One candidate a frame, the camera on it.</summary>
        sealed class Cycle : MonoBehaviour, IShotStep
        {
            public List<(string name, GameObject o, Bounds box)> shown;
            public Camera camera;
            int at;

            public void ShotStep(float dt)
            {
                at = Mathf.Min(at + 1, shown.Count - 1);
                for (int k = 0; k < shown.Count; k++) shown[k].o.SetActive(k == at);
                Frame();
            }

            public void Frame()
            {
                if (shown.Count == 0) return;
                var box = shown[at].box;
                float Env(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : or;
                float r = Mathf.Max(box.extents.magnitude, 0.5f);
                var from = Quaternion.Euler(Env("BASTION_ALTO", 18), Env("BASTION_GIRO", 35), 0) * Vector3.back;
                camera.transform.position = box.center + from * (r / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f);
                camera.transform.LookAt(box.center);
            }
        }

        static Bounds BoundsOf(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(root.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
