// M13 (Arturo, 23/11: «quiero que los accesorios que pongo en las armas aparezcan tanto en el icono del objeto del arma como
// en el arma en sí»): the inventory's pictures. A lab alone: nothing here is the game's.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bastion.Client.Units;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Each firearm with each of the engine's four mods on it (WeaponModIds: scope, stock, extended magazine, silencer), from
    /// its side, a row a gun: alone, with its silencer, its scope, its stock, its magazine, and all four. The pieces are
    /// Synty's (POLYGON Military's and Apocalypse's modular weapons, in the project already), put on by the gun's own
    /// shape (Place): the silencer on the muzzle's end, the scope on top over the grip, the stock at its back, the magazine
    /// under where its own hangs lowest. A line [accesorio] for each, with where it went in the gun's frame, unscaled.
    ///   BASTION_ACC_ARMAS=rifle,smg (all the firearms unless said)   BASTION_ACC_PIEZAS="silencer=…prefab;scope=…" (others than these)
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.AccesoriosLab.Tabla -shotCamera "Shot camera" -shotSize 1920x1800 -shotOut &lt;file&gt;
    /// </summary>
    public static class AccesoriosLab
    {
        static readonly string[] Guns = { "rifle", "rifle2", "rifle3", "smg", "shotgun", "shotgunPump", "huntingRifle", "boltRifle", "sniper", "sniper2", "machineGun", "machineGun2", "pistol", "crossbow" };
        static readonly string[] Kinds = { "silencer", "scope", "stock", "mag" };
        const string Military = "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular/", Apocalypse = "Assets/Synty/PolygonApocalypse/Prefabs/Weapons/Modular/";

        static Dictionary<string, string> Pieces()
        {
            var pieces = new Dictionary<string, string>
            {
                ["silencer"] = Military + "Attachments/SM_Wep_Mod_Silencer_01.prefab",
                ["scope"] = Military + "Scopes/SM_Wep_Mod_Scope_01.prefab",
                ["stock"] = Apocalypse + "SM_Wep_Mod_Stock_01.prefab",
                ["mag"] = Military + "Weapon_A/SM_Wep_Mod_A_Mag_01.prefab",
            };
            foreach (string pair in (Environment.GetEnvironmentVariable("BASTION_ACC_PIEZAS") ?? "").Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) pieces[pair.Substring(0, eq).Trim()] = pair.Substring(eq + 1).Trim();
            }
            return pieces;
        }

        /// <summary>A model's vertices in `frame`'s own.</summary>
        static List<Vector3> Points(Transform model, Transform frame)
        {
            var points = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                foreach (var p in mf.sharedMesh.vertices) points.Add(frame.InverseTransformPoint(mf.transform.TransformPoint(p)));
            }
            return points;
        }

        static Bounds Box(IEnumerable<Vector3> points)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var p in points)
            {
                if (!any) box = new Bounds(p, Vector3.zero);
                else box.Encapsulate(p);
                any = true;
            }
            return box;
        }

        /// <summary>Where a piece of `kind` goes on a gun, by the gun's shape: the point of the gun and the point of the piece's box that meets it (0..1 along each of its axes).</summary>
        static (Vector3 onGun, Vector3 ofPiece) Place(string kind, List<Vector3> gun)
        {
            var box = Box(gun);
            switch (kind)
            {
                case "silencer":
                {
                    // The muzzle: the middle of what the gun has in its last 2 cm.
                    var tip = Box(gun.Where(p => p.z > box.max.z - 0.02f));
                    return (new Vector3(tip.center.x, tip.center.y, box.max.z - 0.01f), new Vector3(0.5f, 0.5f, 0));
                }
                case "scope":
                {
                    // On top, over the grip and a little ahead of it (the receiver).
                    float z = Mathf.Lerp(box.min.z, box.max.z, 0.45f);
                    var top = Box(gun.Where(p => Mathf.Abs(p.z - z) < 0.05f));
                    return (new Vector3(0, top.max.y - 0.004f, z), new Vector3(0.5f, 0, 0.5f));
                }
                case "stock":
                {
                    // Its back to the gun's back, at the height of what the gun has in its last 6 cm there.
                    var butt = Box(gun.Where(p => p.z < box.min.z + 0.06f));
                    return (new Vector3(0, butt.center.y, box.min.z), new Vector3(0.5f, 0.5f, 0));
                }
                default:
                {
                    // Under the gun's middle third, where it hangs lowest (its own magazine, if it shows one); the piece's top a little inside.
                    float z0 = Mathf.Lerp(box.min.z, box.max.z, 0.35f), z1 = Mathf.Lerp(box.min.z, box.max.z, 0.7f);
                    var low = gun.Where(p => p.z > z0 && p.z < z1).OrderBy(p => p.y).First();
                    var under = Box(gun.Where(p => Mathf.Abs(p.z - low.z) < 0.03f));
                    return (new Vector3(0, under.min.y + 0.05f, low.z), new Vector3(0.5f, 1, 0.5f));
                }
            }
        }

        static string F(Vector3 p) => FormattableString.Invariant($"({p.x:0.000}, {p.y:0.000}, {p.z:0.000})");

        /// <summary>The gun `id` at `at`, its muzzle along the world's +z, with the pieces of `kinds` on it.</summary>
        static void Gun(string id, Vector3 at, string[] kinds, FigureCatalog catalog, Dictionary<string, string> pieces, bool say)
        {
            var art = Anims.Weapon(id);
            var prefab = art != null ? catalog.Weapon(art.fbx) : null;
            if (prefab == null)
            {
                if (say) Debug.Log($"[accesorio] {id}: no model");
                return;
            }
            var gun = UnityEngine.Object.Instantiate(prefab).transform;
            gun.name = id;
            // (As the game draws it: Apocalypse's own silencers off, UnitViews.WeaponModel.)
            foreach (var part in gun.GetComponentsInChildren<Transform>(true).Where(t => t != gun && (t.name.Contains("Silencer") || t.name.Contains("Sheath"))).ToList()) part.gameObject.SetActive(false);
            gun.position = at;
            var shape = Points(gun, gun);
            if (say)
            {
                var box = Box(shape);
                Debug.Log($"[accesorio] {id} ({art.fbx}): its box {F(box.min)} to {F(box.max)}, {shape.Count} vertices");
            }
            Dress(gun, id, kinds, pieces, shape, say);
        }

        /// <summary>The pieces of `kinds` put on a gun's model (its muzzle along its own +z), as its children.</summary>
        static void Dress(Transform gun, string id, string[] kinds, Dictionary<string, string> pieces, List<Vector3> shape, bool say)
        {
            foreach (string kind in kinds)
            {
                var piecePrefab = pieces.TryGetValue(kind, out string path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
                if (piecePrefab == null)
                {
                    if (say) Debug.Log($"[accesorio] {kind}: no piece at {path}");
                    continue;
                }
                var piece = UnityEngine.Object.Instantiate(piecePrefab, gun, false).transform;
                piece.localPosition = Vector3.zero;
                piece.localRotation = Quaternion.identity;
                var own = Box(Points(piece, gun));
                var (onGun, ofPiece) = Place(kind, shape);
                var meets = own.min + Vector3.Scale(own.size, ofPiece);
                piece.localPosition = onGun - meets;
                if (say) Debug.Log($"[accesorio] {id} + {kind} ({piecePrefab.name}, {F(own.size)} m): at {F(piece.localPosition)} in the gun's frame, meeting it at {F(onGun)}");
            }
        }

        /// <summary>
        /// The same pieces on the guns in the soldiers' hands: CombatLab.Agarres' row (BASTION_ARMAS, its rests alone with
        /// BASTION_SOLO_REPOSO=1, its pictures from near and from the game's camera into BASTION_TIRAS), each gun drawn
        /// dressed a second in with BASTION_ACC (silencer,scope unless said).
        ///   BASTION_ARMAS=rifle,smg BASTION_SOLO_REPOSO=1 BASTION_TIRAS=&lt;folder&gt; unity/tools/captura.sh -shotSetup Bastion.EditorTools.AccesoriosLab.Manos
        ///     -shotPlay -shotCamera "Shot camera" -shotFrames 60 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Manos()
        {
            // (Where each piece goes worked out now, out of play, by the gun's mesh: in play a mesh's vertices can't be read.)
            var catalog = FigureCatalog.Load();
            var pieces = Pieces();
            string[] kinds = (Environment.GetEnvironmentVariable("BASTION_ACC") ?? "silencer,scope").Split(',');
            var places = new Dictionary<string, List<(GameObject piece, Vector3 at)>>();
            foreach (string id in (Environment.GetEnvironmentVariable("BASTION_ARMAS") ?? "rifle").Split(','))
            {
                var art = Anims.Weapon(id.Trim());
                var prefab = art != null ? catalog.Weapon(art.fbx) : null;
                if (prefab == null) continue;
                var gun = UnityEngine.Object.Instantiate(prefab).transform;
                foreach (var part in gun.GetComponentsInChildren<Transform>(true).Where(x => x != gun && (x.name.Contains("Silencer") || x.name.Contains("Sheath"))).ToList()) part.gameObject.SetActive(false);
                string mesh = gun.GetComponentsInChildren<MeshFilter>().Select(mf => mf.sharedMesh).FirstOrDefault(m => m != null)?.name ?? "";
                Dress(gun, id, kinds, pieces, Points(gun, gun), true);
                var list = new List<(GameObject, Vector3)>();
                foreach (Transform child in gun)
                    if (kinds.Any(k => pieces.TryGetValue(k, out string path) && AssetDatabase.LoadAssetAtPath<GameObject>(path) is GameObject source && child.name.StartsWith(source.name)))
                        list.Add((AssetDatabase.LoadAssetAtPath<GameObject>(pieces[kinds.First(k => pieces.TryGetValue(k, out string path) && child.name.StartsWith(System.IO.Path.GetFileNameWithoutExtension(path)))]), child.localPosition));
                places[mesh] = list;
                Debug.Log($"[accesorio] {id}: mesh {mesh}, {list.Count} pieces to put on in play");
                UnityEngine.Object.DestroyImmediate(gun.gameObject);
            }
            CombatLab.Agarres();
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                new GameObject("Accesorios") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Dresser>().places = places;
            };
        }

        sealed class Dresser : MonoBehaviour
        {
            public Dictionary<string, List<(GameObject piece, Vector3 at)>> places;
            readonly HashSet<Transform> done = new HashSet<Transform>();

            void Update()
            {
                if (Time.time < 1 || places == null) return;
                // (UnitViews.WeaponModel names every weapon's model «arma».)
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name != "arma" || !done.Add(t)) continue;
                    string mesh = t.GetComponentsInChildren<MeshFilter>().Select(mf => mf.sharedMesh).FirstOrDefault(m => m != null)?.name ?? "";
                    if (!places.TryGetValue(mesh, out var list)) continue;
                    foreach (var (piece, at) in list)
                    {
                        var made = Instantiate(piece, t, false).transform;
                        made.localRotation = Quaternion.identity;
                        made.localPosition = at;
                        foreach (var c in made.GetComponentsInChildren<Collider>()) Destroy(c);
                    }
                    Debug.Log($"[accesorio] dressed in play: {mesh}, {list.Count} pieces");
                }
            }
        }

        public static void Tabla()
        {
            var catalog = FigureCatalog.Load();
            var pieces = Pieces();
            string[] guns = Environment.GetEnvironmentVariable("BASTION_ACC_ARMAS") is string only && only != "" ? only.Split(',') : Guns;
            var columns = new List<string[]> { new string[0] };
            foreach (string kind in Kinds) columns.Add(new[] { kind });
            columns.Add(Kinds);
            const float Wide = 1.5f, High = 0.52f;
            for (int r = 0; r < guns.Length; r++)
            {
                for (int c = 0; c < columns.Count; c++) Gun(guns[r], new Vector3(0, -r * High, c * Wide), columns[c], catalog, pieces, c > 0 && c < columns.Count - 1 || c == 0);
                Label(guns[r], new Vector3(0, -r * High + 0.2f, -0.55f));
            }
            string[] heads = { "sola", "silenciador", "mira", "culata", "cargador", "los cuatro" };
            for (int c = 0; c < heads.Length; c++) Label(heads[c], new Vector3(0, 0.38f, c * Wide - 0.1f));

            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(35, -70, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.80f, 0.82f, 0.85f);
            camera.orthographic = true;
            float height = guns.Length * High + 0.5f, width = columns.Count * Wide + 0.4f;
            // (From the gun's right side, its muzzle to the picture's right.)
            camera.transform.position = new Vector3(6, 0.45f - height / 2, -0.75f + width / 2);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            camera.orthographicSize = height / 2;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20;
        }

        static void Label(string text, Vector3 at)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.fontSize = 64;
            label.characterSize = 0.011f;
            label.color = Color.black;
            label.anchor = TextAnchor.MiddleLeft;
            label.transform.position = at + Vector3.right * 0.3f;
            // (Read from +x: its own +x along the world's +z.)
            label.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        }
    }
}
