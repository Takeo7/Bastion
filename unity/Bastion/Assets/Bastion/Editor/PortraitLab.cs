using System;
using System.IO;
using System.Linq;
using Bastion.Client;
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
            // BASTION_TINTE=1: with «Ropa con el color del jugador» (their colours, their player's band).
            if (Environment.GetEnvironmentVariable("BASTION_TINTE") == "1") Bastion.Client.Units.SquadTint.On = true;
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
                Portraits.Know(id, s["appearance"]?.ToObject<Appearance>(serializer), (string)s["name"], Bastion.Client.Units.SquadTint.ColourOf((int?)s["owner"] ?? 0));
                ids.Add(id);
            }
            var speakers = typeof(SpeakerIds).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetValue(null)).ToList();
            foreach (string speaker in speakers) Portraits.Voice(speaker);
            Portraits.Flush();
            foreach (string id in ids) Save(Portraits.Soldier(id), Path.Combine(folder, id + ".png"));
            foreach (string speaker in speakers) Save(Portraits.Voice(speaker), Path.Combine(folder, "voz-" + speaker + ".png"));
            Debug.Log($"[portraits] {ids.Count} soldiers and {speakers.Count} voices in {Path.GetFullPath(folder)}");
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
