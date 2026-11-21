using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Caza's quiet weapons (Arturo, cuadro 101: «Ballesta, arco»; .dev-snapshots/arte/unity/runs/caza/arco-ballesta/), a
    /// look before the engine has them: a row of soldiers on a cleared patch of a generated map, from the game's camera.
    /// One as ever (rifle), then Synty's three bows (ANIMATION - Bow Combat: compound, recurve, long) in the left hand in
    /// the pack's own clips (UnitViews.LabPose), the compound one across the back, and Apocalypse's crossbow in the hands
    /// and across the back, put where the soldier's rifle is. The bows' materials made here for URP from the pack's texture.
    ///   BASTION_ARCO_MANO=x,y,z,rx,ry,rz (the bow in the left hand: metres and degrees) BASTION_DIST BASTION_GIRO
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.ArcoLab.Fila -shotCamera "Shot camera" -shotFrames 8 -shotFps 10 …
    /// </summary>
    public static class ArcoLab
    {
        const string Bows = "Assets/Synty/AnimationBowCombat/Prefabs/Bows/";
        const string Crossbow = "Assets/Synty/PolygonApocalypse/Prefabs/Weapons/Misc/SM_Wep_CrossBow_01.prefab";

        static float[] Floats(string name, string or) => (Environment.GetEnvironmentVariable(name) ?? or).Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        public static void Fila()
        {
            // (In play mode, -shotPlay: made once it starts, so that the figures are posed and step; the camera before, for the tool.)
            new GameObject("Shot camera").AddComponent<Camera>().farClipPlane = 800;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Environment.GetEnvironmentVariable("BASTION_QUIETO") == "1")
            {
                Build();
                return;
            }
            void Started(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                EditorApplication.playModeStateChanged -= Started;
                Build();
            }
            EditorApplication.playModeStateChanged += Started;
        }

        static void Build()
        {
            var state = MapLab.Generated("wilds", 3, "elimination");
            int ox = state.Width / 2 - 6, oy = state.Height / 2 - 5;
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 13; x++)
                {
                    int i = (oy + y) * state.Width + ox + x;
                    state.Tiles[i] = TileKinds.Floor;
                    if (state.Decor != null && i < state.Decor.Count) state.Decor[i] = 0;
                }
            state.Lights?.RemoveAll(l => l.Tile.X >= ox && l.Tile.X < ox + 13 && l.Tile.Y >= oy && l.Tile.Y < oy + 9);
            state.Fires?.Clear();
            state.Concealed = false;
            // Who: what each holds, where, and the clip it is held in (none: as the game has it).
            var row = new (string id, string what, bool back, string clip)[]
            {
                ("fusil", null, false, null), ("poleas", "Compound/Wep_CompoundBow_01", false, "bowIdle"), ("recurvo", "Recurve/Wep_RecurveBow_01", false, "bowDrawn"),
                ("largo", "Longbow/Wep_LongBow_01", false, "bowAim"), ("espalda", "Compound/Wep_CompoundBow_01", true, null),
                ("ballesta", "ballesta", false, null), ("ballestaEspalda", "ballesta", true, null),
            };
            var model = state.Units.First(u => u.Team == Teams.Xcom);
            string json = Bastion.Net.Json.Write(model);
            state.Units.Clear();
            for (int k = 0; k < row.Length; k++)
            {
                var u = Bastion.Net.Json.Read<Unit>(json);
                u.Id = row[k].id;
                u.Name = row[k].id;
                u.Pos = new Pos { X = ox + 2 + k * 1 + k / 1 * 0, Y = oy + 4 };
                u.Pos.X = ox + 1 + (int)(k * 1.5f + 0.5f);
                u.Facing = Math.PI / 2;
                u.Hidden = null;
                u.Hunkered = false;
                u.Overwatch = false;
                u.Arms = new Arms { Primary = "assaultRifle", Pistol = false };
                state.Units.Add(u);
            }
            Environment.SetEnvironmentVariable("BASTION_POSE", string.Join(";", row.Where(r => r.clip != null).Select(r => r.id + "=" + r.clip)));

            Bastion.Client.NightLights.StateForLab = state;
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
            MapLab.Map(state, "arco");
            var units = new GameObject("Unidades").AddComponent<UnitViews>();
            units.Sync(state);
            foreach (var u in state.Units) units.Reveal(u.Id);
            units.MasksNow();
            // (Those with theirs across the back: their knife out, as on guard.)
            foreach (var r in row.Where(r => r.back)) units.GuardForLab(r.id, 0);

            // The bows' material for URP, of the pack's own texture.
            Material bowMaterial = null;
            Material ForUrp(Material from)
            {
                if (bowMaterial != null) return bowMaterial;
                bowMaterial = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Arco");
                var texture = from != null ? from.mainTexture : null;
                if (texture != null) bowMaterial.SetTexture("_BaseMap", texture);
                return bowMaterial;
            }
            var hand = Floats("BASTION_ARCO_MANO", "0,0,0,0,0,0");
            foreach (var r in row.Where(r => r.what != null))
            {
                var figure = Figures.FeetOf(r.id);
                var animator = figure != null ? figure.GetComponentInChildren<Animator>() : null;
                var carried = units.CarriedForLab(r.id);
                Debug.Log($"[arco] {r.id}: figure {(figure != null)}, animator {(animator != null)}, carried {string.Join(", ", carried.Select(c => c.model.name + (c.drawn ? " (drawn)" : "")))}");
                if (animator == null) continue;
                if (r.id == "recurvo")
                {
                    Debug.Log("[arco] soldier bones with prop/hand: " + string.Join(", ", animator.GetComponentsInChildren<Transform>(true).Where(b => b.name.ToLowerInvariant().Contains("prop") || b.name.ToLowerInvariant().Contains("hand") || b.name.ToLowerInvariant().Contains("attach")).Select(b => b.name + "<" + b.parent.name)));
                    var bow = AssetDatabase.LoadAssetAtPath<GameObject>(Bows + r.what + ".prefab");
                    foreach (var b in bow.GetComponentsInChildren<Transform>(true)) Debug.Log(FormattableString.Invariant($"[arco] bow part {b.name} < {(b.parent != null ? b.parent.name : "-")}: {b.localPosition.x:0.000}, {b.localPosition.y:0.000}, {b.localPosition.z:0.000}; {b.GetComponent<Renderer>()?.GetType().Name}"));
                }
                bool crossbow = r.what == "ballesta";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(crossbow ? Crossbow : Bows + r.what + ".prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"[arco] {r.what}: none");
                    continue;
                }
                // The long gun it has: gone, and the crossbow in its place; a bow's own place.
                var gun = carried.Where(c => !c.model.name.ToLowerInvariant().Contains("knife")).OrderByDescending(c => Size(c.model)).Select(c => c.model).FirstOrDefault();
                var made = (GameObject)UnityEngine.Object.Instantiate(prefab);
                made.name = r.what;
                if (!crossbow)
                    foreach (var part in made.GetComponentsInChildren<Renderer>()) part.sharedMaterial = ForUrp(part.sharedMaterial);
                float scale = animator.transform.lossyScale.x;
                if (crossbow && gun != null)
                {
                    made.transform.SetParent(gun.parent, false);
                    made.transform.localPosition = gun.localPosition;
                    made.transform.localRotation = gun.localRotation;
                    made.transform.localScale = gun.localScale;
                }
                else if (r.back)
                {
                    var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                    var body = animator.transform;
                    made.transform.SetPositionAndRotation(chest.position - body.forward * 0.2f * scale, body.rotation * Quaternion.Euler(0, 90, 0) * Quaternion.Euler(28, 0, 0));
                    made.transform.localScale = Vector3.one * scale;
                    made.transform.SetParent(chest, true);
                }
                else
                {
                    var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    made.transform.SetParent(left, false);
                    made.transform.localPosition = new Vector3(hand[0], hand[1], hand[2]);
                    made.transform.localRotation = Quaternion.Euler(hand[3], hand[4], hand[5]);
                }
                if (gun != null) foreach (var part in gun.GetComponentsInChildren<Renderer>()) part.enabled = false;
            }

            // The bows in the hand turned as the drawn one asks: upright, its string towards the hand that draws it. Found
            // a second in, on «recurvo» (posed by then), and the same in the hand for the others ([arco] mano: to keep).
            var held = new List<Transform>();
            foreach (var r in row.Where(r => r.what != null && r.what != "ballesta" && !r.back))
            {
                var lh = Figures.FeetOf(r.id)?.GetComponentInChildren<Animator>()?.GetBoneTransform(HumanBodyBones.LeftHand);
                if (lh != null) held.AddRange(lh.Cast<Transform>().Where(c => c.name == r.what));
            }
            async void Turn()
            {
                await Awaitable.WaitForSecondsAsync(1.2f);
                var a = Figures.FeetOf("recurvo")?.GetComponentInChildren<Animator>();
                if (a == null) return;
                Transform lh = a.GetBoneTransform(HumanBodyBones.LeftHand), rh = a.GetBoneTransform(HumanBodyBones.RightHand);
                var pull = Vector3.ProjectOnPlane(rh.position - lh.position, Vector3.up).normalized;
                if (Environment.GetEnvironmentVariable("BASTION_ARCO_VUELTA") == "1") pull = -pull;
                var world = Quaternion.LookRotation(Vector3.Cross(pull, Vector3.up), Vector3.up);
                var local = Quaternion.Inverse(lh.rotation) * world;
                Debug.Log(FormattableString.Invariant($"[arco] mano: local euler {local.eulerAngles.x:0.0}, {local.eulerAngles.y:0.0}, {local.eulerAngles.z:0.0}; the hands {Vector3.Distance(lh.position, rh.position):0.00} m apart"));
                foreach (var bow in held) bow.localRotation = local;
            }
            if (Application.isPlaying && Environment.GetEnvironmentVariable("BASTION_ARCO_MANO") == null) Turn();
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            float F(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : or;
            var middle = MapSpace.TileToWorld(state, new Pos { X = ox, Y = oy + 4 }) + new Vector3(5.5f * MapSpace.Tile, 0.9f, 0);
            if (Environment.GetEnvironmentVariable("BASTION_VISTA") == "frente")
            {
                // From the front, a little above: to judge how each is held.
                float look = F("BASTION_MIRA", 5.5f);
                var at = MapSpace.TileToWorld(state, new Pos { X = ox, Y = oy + 4 }) + new Vector3(look * MapSpace.Tile, 1f, 0);
                var from = at + new Vector3(F("BASTION_LADO", 0), 1.2f, -F("BASTION_DIST", 7));
                camera.fieldOfView = 30;
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            }
            else MapSpace.Aim(camera, middle, F("BASTION_DIST", 9), F("BASTION_GIRO", 0));
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
        }

        /// <summary>
        /// The recurve bow (Arturo, cuadro 103) as it would be worn: «tira», a soldier who lifts, draws and looses it (the
        /// pack's clips in a row, LabPose), the bow on the pack's own prop bone (prop_l), its string drawn by the fingers
        /// (BowString), an arrow nocked, a quiver at the back; «espalda», another with the bow across the back and the
        /// quiver, knife in hand. BASTION_VISTA=lado|frente|detras|juego, BASTION_ARCO_ESPALDA=x,y,z,rx,ry,rz,
        /// BASTION_CARCAJ=x,y,z,rx,ry,rz (in backAttach's frame). -shotPlay.
        /// </summary>
        public static void Recurvo()
        {
            new GameObject("Shot camera").AddComponent<Camera>().farClipPlane = 800;
            void Started(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                EditorApplication.playModeStateChanged -= Started;
                BuildRecurve();
            }
            EditorApplication.playModeStateChanged += Started;
        }

        static void BuildRecurve()
        {
            var state = MapLab.Generated("wilds", 3, "elimination");
            int ox = state.Width / 2 - 6, oy = state.Height / 2 - 5;
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 13; x++)
                {
                    int i = (oy + y) * state.Width + ox + x;
                    state.Tiles[i] = TileKinds.Floor;
                    if (state.Decor != null && i < state.Decor.Count) state.Decor[i] = 0;
                }
            state.Lights?.RemoveAll(l => l.Tile.X >= ox && l.Tile.X < ox + 13 && l.Tile.Y >= oy && l.Tile.Y < oy + 9);
            state.Fires?.Clear();
            state.Concealed = false;
            string json = Bastion.Net.Json.Write(state.Units.First(u => u.Team == Teams.Xcom));
            state.Units.Clear();
            foreach (var (id, x) in new[] { ("tira", 5), ("espalda", 7) })
            {
                var u = Bastion.Net.Json.Read<Unit>(json);
                u.Id = id;
                u.Name = id;
                u.Pos = new Pos { X = ox + x, Y = oy + 4 };
                // (East: the side camera, from the south, sees the archer in profile.)
                u.Facing = id == "tira" ? 0 : Math.PI / 2;
                u.Hidden = null;
                u.Hunkered = false;
                u.Overwatch = false;
                // (No gun of the game's: its hold would take the arms from the bow's clips.)
                u.Arms = null;
                u.Template = TemplateIds.Vip;
                state.Units.Add(u);
            }
            Environment.SetEnvironmentVariable("BASTION_POSE", "tira=bowIdle:1.2>bowToAim>bowAim:0.4>bowToDrawn>bowDrawn:0.9>bowShoot>bowAim:0.5>bowIdle");
            Bastion.Client.NightLights.StateForLab = state;
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
            MapLab.Map(state, "recurvo");
            var units = new GameObject("Unidades").AddComponent<UnitViews>();
            units.Sync(state);
            foreach (var u in state.Units) units.Reveal(u.Id);
            units.MasksNow();

            Material material = null;
            GameObject Make(string path, Transform parent)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[arco] {path}: none");
                    return null;
                }
                var made = (GameObject)UnityEngine.Object.Instantiate(prefab, parent, false);
                foreach (var part in made.GetComponentsInChildren<Renderer>(true))
                {
                    if (material == null)
                    {
                        material = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Arco");
                        if (part.sharedMaterial != null && part.sharedMaterial.mainTexture != null) material.SetTexture("_BaseMap", part.sharedMaterial.mainTexture);
                    }
                    part.sharedMaterial = material;
                    if (part is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                return made;
            }
            const string Pack = "Assets/Synty/AnimationBowCombat/Prefabs/";
            foreach (var u in state.Units)
            {
                var animator = Figures.FeetOf(u.Id).GetComponentInChildren<Animator>();
                var all = animator.GetComponentsInChildren<Transform>(true);
                Transform Bone(string name) => all.FirstOrDefault(b => b.name == name);
                // (Their rifle gone: it is the bow they carry.)
                foreach (var c in units.CarriedForLab(u.Id).Where(c => Size(c.model) > 0.6f))
                    foreach (var part in c.model.GetComponentsInChildren<Renderer>()) part.enabled = false;
                var back = Bone("backAttach");
                var q = Floats("BASTION_CARCAJ", "0.12,-0.05,-0.02,0,0,-25");
                var quiver = Make(Pack + "Quivers/Wep_Quiver_01.prefab", back);
                if (quiver != null)
                {
                    quiver.transform.localPosition = new Vector3(q[0], q[1], q[2]);
                    quiver.transform.localRotation = Quaternion.Euler(q[3], q[4], q[5]);
                    var insert = Make(Pack + "Quivers/Arrow_Inserts/Wep_Quiver_Modern_01.prefab", quiver.transform);
                    Debug.Log(FormattableString.Invariant($"[arco] {u.Id} quiver: box {Box(quiver).size.x:0.00} × {Box(quiver).size.y:0.00} × {Box(quiver).size.z:0.00}, parts {string.Join(", ", quiver.GetComponentsInChildren<Transform>().Select(p => p.name))}"));
                }
                if (u.Id == "espalda")
                {
                    var b = Floats("BASTION_ARCO_ESPALDA", "-0.25,-0.16,0,0,0,35");
                    var slung = Make(Bows + "Recurve/Wep_RecurveBow_01.prefab", back);
                    slung.transform.localPosition = new Vector3(b[0], b[1], b[2]);
                    slung.transform.localRotation = Quaternion.Euler(b[3], b[4], b[5]);
                    continue;
                }
                var bow = Make(Bows + "Recurve/Wep_RecurveBow_01.prefab", Bone("prop_l"));
                var parts = bow.GetComponentsInChildren<Transform>(true);
                var arrow = Make(Pack + "Arrows/Wep_Arrow_Modern_01.prefab", null);
                var cord = bow.AddComponent<BowString>();
                cord.joint = parts.FirstOrDefault(p => p.name == "string_jnt");
                cord.rest = parts.FirstOrDefault(p => p.name == "arrow_rest_jnt");
                cord.fingers = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal) ?? animator.GetBoneTransform(HumanBodyBones.RightHand);
                cord.arrow = arrow != null ? arrow.transform : null;
                if (arrow != null) arrow.transform.localScale = Vector3.one * animator.transform.lossyScale.x;
                Debug.Log(FormattableString.Invariant($"[arco] bow on prop_l: joint {(cord.joint != null)}, rest {(cord.rest != null)}, arrow box {(arrow != null ? Box(arrow).size.ToString("F2") : "-")}, clips {string.Join(", ", new[] { "bowIdle", "bowToAim", "bowAim", "bowToDrawn", "bowDrawn", "bowShoot" }.Select(c => c + " " + (FigureCatalog.Load()?.Clip(c)?.length.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "none")))}"));
                var follow = bow.AddComponent<ArcoFollow>();
                follow.units = units;
                follow.cord = cord;
            }

            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            float F(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : or;
            string view = Environment.GetEnvironmentVariable("BASTION_VISTA") ?? "lado";
            var at = MapSpace.TileToWorld(state, new Pos { X = ox, Y = oy + 4 }) + new Vector3(F("BASTION_MIRA", 6.5f) * MapSpace.Tile, 1f, 0);
            if (view == "juego") MapSpace.Aim(camera, at, F("BASTION_DIST", 11), 0);
            else
            {
                float d = F("BASTION_DIST", 6);
                var from = at + (view == "detras" ? new Vector3(0, 0.9f, d) : view == "frente" ? new Vector3(d, 0.6f, -1.5f) : new Vector3(0, 0.7f, -d));
                camera.fieldOfView = 30;
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            }
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
        }

        static Bounds Box(GameObject o)
        {
            var renderers = o.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(o.transform.position, Vector3.zero);
            var box = renderers[0].bounds;
            foreach (var r in renderers) box.Encapsulate(r.bounds);
            return box;
        }

        static float Size(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            return renderers.Length == 0 ? 0 : renderers.Max(r => r.bounds.size.magnitude);
        }
    }

    /// <summary>The lab's archer: its string pulled and its arrow nocked by the clip it is in (ArcoLab.Recurvo).</summary>
    public sealed class ArcoFollow : MonoBehaviour
    {
        public UnitViews units;
        public BowString cord;

        void Update()
        {
            if (units != null) units.LabStep("tira");
            string clip = units != null ? units.LabClipOf("tira") : null;
            cord.pull = clip == "bowToDrawn" || clip == "bowDrawn";
            cord.nocked = clip == "bowAim" || clip == "bowToAim" || cord.pull;
        }
    }
}
