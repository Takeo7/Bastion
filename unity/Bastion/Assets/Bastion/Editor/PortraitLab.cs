using System;
using System.IO;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The busts (Bastion.Client.Portraits) drawn to PNG files, to set beside 0.10.1's: the soldiers of UI's sample campaign
    /// (BASTION_PARTIDA, .dev-snapshots/ui/estrategia-0101/partida-muestra.json unless said) and every radio voice, into
    /// BASTION_OUT (<id>.png, voz-<speaker>.png). Makes Resources/PortraitTone.mat if it is missing.
    ///   BASTION_OUT=dir unity/tools/unity.sh -executeMethod Bastion.EditorTools.PortraitLab.Muestra -quit
    /// </summary>
    public static class PortraitLab
    {
        const string Tone = "Assets/Bastion/Client/Resources/PortraitTone.mat";

        public static void Muestra()
        {
            EnsureTone();
            if (float.TryParse(Environment.GetEnvironmentVariable("BASTION_GIRO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float giro)) Portraits.SoldierTurn = giro;
            if (float.TryParse(Environment.GetEnvironmentVariable("BASTION_CABEZA"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float cabeza)) Portraits.HeadTurn = cabeza;
            if (float.TryParse(Environment.GetEnvironmentVariable("BASTION_INCLINA"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float inclina)) Portraits.HeadBow = inclina;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string file = Environment.GetEnvironmentVariable("BASTION_PARTIDA") ?? Path.Combine(root, ".dev-snapshots/ui/estrategia-0101/partida-muestra.json");
            string folder = Environment.GetEnvironmentVariable("BASTION_OUT") ?? throw new ArgumentException("BASTION_OUT is missing");
            Directory.CreateDirectory(folder);
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
            var campaign = json["campaign"] ?? json;
            var serializer = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() });
            var ids = new System.Collections.Generic.List<string>();
            foreach (var s in campaign["soldiers"])
            {
                string id = (string)s["id"];
                Portraits.Know(id, s["appearance"]?.ToObject<Appearance>(serializer), (string)s["name"]);
                ids.Add(id);
            }
            var speakers = typeof(SpeakerIds).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetValue(null)).ToList();
            foreach (string speaker in speakers) Portraits.Voice(speaker);
            Portraits.Flush();
            foreach (string id in ids) Save(Portraits.Soldier(id), Path.Combine(folder, id + ".png"));
            foreach (string speaker in speakers) Save(Portraits.Voice(speaker), Path.Combine(folder, "voz-" + speaker + ".png"));
            Debug.Log($"[portraits] {ids.Count} soldiers and {speakers.Count} voices in {Path.GetFullPath(folder)}");
        }

        // TEMP: the idle's yaw of a few bones against the rest pose (to set beside 0.10.1's).
        public static void Medir()
        {
            {
                var s = new Color(1, 0, 0); var g = new Color(0, 0, 1);
                var sh = new UnityEngine.Rendering.SphericalHarmonicsL2();
                sh.AddAmbientLight((s + g) * 0.5f);
                sh.AddDirectionalLight(Vector3.up, (s - g) * 0.5f, 1);
                sh.AddDirectionalLight(Vector3.down, (s - g) * -0.5f, 1);
                var dirs = new[] { Vector3.up, Vector3.down, Vector3.forward, (Vector3.up + Vector3.forward).normalized };
                var res = new Color[4];
                sh.Evaluate(dirs, res);
                for (int i = 0; i < 4; i++) Debug.Log($"[medida] sh {dirs[i]} {res[i]}");
                var one = new UnityEngine.Rendering.SphericalHarmonicsL2();
                one.AddDirectionalLight(Vector3.up, Color.white, 1);
                one.Evaluate(dirs, res);
                for (int i = 0; i < 4; i++) Debug.Log($"[medida] dir {dirs[i]} {res[i]}");
                Debug.Log($"[medida] reflection mode {RenderSettings.defaultReflectionMode} intensity {RenderSettings.reflectionIntensity} ambient {RenderSettings.ambientMode}");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var looks = new (string id, Appearance look)[] {
                ("c1", new Appearance { Head = "bare", Build = "normal", Sex = "m" }),
                ("c5", new Appearance { Head = "helmet", Build = "slim", Sex = "f" }) };
            foreach (var (id, look) in looks)
            {
                var dress = Dress.Of(look, id, null);
                var figure = SoldierFigure.Make(dress, "Medida");
                var animator = figure.GetComponentInChildren<Animator>();
                var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head };
                var rest = bones.Select(b => animator.GetBoneTransform(b)).Select(b => b == null ? Quaternion.identity : Quaternion.Inverse(figure.transform.rotation) * b.rotation).ToArray();
                var idle = FigureCatalog.Load()?.Clip(Anims.Motion(dress.sex == "f" ? "femn" : "masc", "idle", false));
                var anim = new FigureAnimator(animator, m => m == "idle" ? idle : null);
                for (int k = 0; k < 12; k++) anim.Step(0.1f);
                for (int i = 0; i < bones.Length; i++)
                {
                    var b = animator.GetBoneTransform(bones[i]);
                    if (b == null) continue;
                    var q = Quaternion.Inverse(figure.transform.rotation) * b.rotation * Quaternion.Inverse(rest[i]);
                    var f = q * Vector3.forward;
                    Debug.Log($"[medida] {id} {bones[i]} {b.name} yaw {Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg:0.0} pitch {Mathf.Asin(-f.y) * Mathf.Rad2Deg:0.0} pos {figure.transform.InverseTransformPoint(b.position)}");
                }
                anim.Dispose();
                UnityEngine.Object.DestroyImmediate(figure);
            }
        }

        static void Save(Texture texture, string path)
        {
            if (!(texture is RenderTexture rt))
            {
                Debug.Log($"[portraits] {Path.GetFileName(path)}: none");
                return;
            }
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var picture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            picture.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(path, picture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(picture);
        }

        /// <summary>The busts' tone (Shaders/PortraitTone.shader) as a material in Resources, for the game to find.</summary>
        internal static void EnsureTone()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(Tone) != null) return;
            var shader = Shader.Find("Hidden/Bastion/PortraitTone");
            if (shader == null)
            {
                Debug.LogWarning("[portraits] no Hidden/Bastion/PortraitTone");
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader) { name = "PortraitTone" }, Tone);
            AssetDatabase.SaveAssets();
        }
    }
}
