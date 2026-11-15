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
    /// (BASTION_PARTIDA, .dev-snapshots/ui/estrategia-0101/partida-muestra.json unless said), into BASTION_OUT (<id>.png).
    /// Makes Resources/PortraitTone.mat if it is missing.
    ///   BASTION_LUZ_RETRATO=#rrggbb: each with that studio light behind (Portraits.Light; BASTION_LUZ_OTRO=#rrggbb, the
    ///   partner's, owner 1, another) · BASTION_RIM=k its strength · BASTION_ENCUADRES=name:turn,headTurn,height,lift,eyes,fov,bow,brim;…
    ///   framings to try, each into BASTION_OUT/name (Portraits.Frame; an empty value keeps the game's) · BASTION_COMO_MISION=1
    ///   also each as a mission's unit asks for it, by its unit id («s1»…; the battle's squad list) without and with its
    ///   campaign id as the seed (BASTION_OUT/mision-sin, mision-con).
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
            string mine = Environment.GetEnvironmentVariable("BASTION_LUZ_RETRATO"), theirs = Environment.GetEnvironmentVariable("BASTION_LUZ_OTRO") ?? mine;
            if (float.TryParse(Environment.GetEnvironmentVariable("BASTION_RIM"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rim)) Portraits.RimLight = rim;
            bool mission = Environment.GetEnvironmentVariable("BASTION_COMO_MISION") == "1";
            int k = 0;
            foreach (var s in campaign["soldiers"])
            {
                string id = (string)s["id"];
                int owner = (int?)s["owner"] ?? 0;
                string light = owner == 0 ? mine : theirs;
                var look = s["appearance"]?.ToObject<Appearance>(serializer);
                string band = Bastion.Client.Units.SquadTint.ColourOf(owner);
                if (light != null && ColorUtility.TryParseHtmlString(light, out var colour)) Portraits.Light(id, colour);
                Portraits.Know(id, look, (string)s["name"], band);
                ids.Add(id);
                if (!mission) continue;
                // As the battle's squad list asks for it today (by the unit's id alone), and with the campaign id as the seed.
                k++;
                Portraits.Know("sin-s" + k, look, (string)s["name"], band);
                Portraits.Know("con-s" + k, look, (string)s["name"], band, id);
            }
            var framings = (Environment.GetEnvironmentVariable("BASTION_ENCUADRES") ?? "").Split(';').Where(f => f.Contains(':')).ToList();
            if (framings.Count == 0) framings.Add(":");
            var game = Portraits.Frame;
            float bow = Portraits.HeadBow, brim = Portraits.BrimBow;
            foreach (var framing in framings)
            {
                string name = framing.Substring(0, framing.IndexOf(':'));
                var v = framing.Substring(framing.IndexOf(':') + 1).Split(',');
                float At(int i, float was) => i < v.Length && float.TryParse(v[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : was;
                Portraits.Frame = new Portraits.Framing { turn = At(0, game.turn), headTurn = At(1, game.headTurn), height = At(2, game.height), lift = At(3, game.lift), eyes = At(4, game.eyes), fov = At(5, game.fov) };
                Portraits.HeadBow = At(6, bow);
                Portraits.BrimBow = At(7, brim);
                Portraits.Again();
                Portraits.Flush();
                string into = Path.Combine(folder, name);
                Directory.CreateDirectory(into);
                foreach (string id in ids) Save(Portraits.Soldier(id), Path.Combine(into, id + ".png"));
                if (mission)
                    for (int i = 1; i <= k; i++)
                    {
                        Directory.CreateDirectory(Path.Combine(into, "mision-sin"));
                        Directory.CreateDirectory(Path.Combine(into, "mision-con"));
                        Save(Portraits.Soldier("sin-s" + i), Path.Combine(into, "mision-sin", ids[i - 1] + ".png"));
                        Save(Portraits.Soldier("con-s" + i), Path.Combine(into, "mision-con", ids[i - 1] + ".png"));
                    }
                Debug.Log(System.FormattableString.Invariant($"[portraits] {ids.Count} soldiers in {Path.GetFullPath(into)}, the last in {Portraits.LastDrawMs:0.0} ms"));
            }
            Portraits.Frame = game;
            Portraits.HeadBow = bow;
            Portraits.BrimBow = brim;
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
