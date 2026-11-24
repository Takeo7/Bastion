// Synty's 4096 atlases brought in at 2048 (Arturo's cuadro 148, Producto's «A», 24/11): 266 MB less of the build's 595 MB
// of textures, and nothing to see of it (TexturasLab's pictures: the soldier's sheet, a map, a shop's front, the busts).
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    public static class TexturasImport
    {
        const int Most = 2048;

        /// <summary>
        /// Every texture under Assets/Synty whose file is wider or taller than 2048 imported at 2048 at most, on every
        /// platform that had it larger (the iPhone's own smaller sizes stay). Written into their .meta (out of git with
        /// Synty: a clean copy takes them with the .meta, as ever; importar-synty.sh runs this after a fresh import).
        /// Again, it changes nothing. BASTION_TEX_SOLO_VER=1: only says what it would do.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.TexturasImport.Atlas2048 -quit
        /// </summary>
        public static void Atlas2048()
        {
            bool dry = Environment.GetEnvironmentVariable("BASTION_TEX_SOLO_VER") == "1";
            int changed = 0, seen = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Synty" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (Mathf.Max(width, height) <= Most) continue;
                seen++;
                bool any = false;
                if (importer.maxTextureSize > Most)
                {
                    if (!dry) importer.maxTextureSize = Most;
                    any = true;
                }
                foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "iPhone", "Android", "WebGL", "Server" })
                {
                    var settings = platform == "DefaultTexturePlatform" ? importer.GetDefaultPlatformTextureSettings() : importer.GetPlatformTextureSettings(platform);
                    if (settings == null || settings.maxTextureSize <= Most) continue;
                    settings.maxTextureSize = Most;
                    if (!dry) importer.SetPlatformTextureSettings(settings);
                    any = true;
                }
                if (!any) continue;
                changed++;
                Debug.Log($"[texturas] {path} {width}×{height}: at {Most}" + (dry ? " (not done: BASTION_TEX_SOLO_VER)" : ""));
                if (!dry) importer.SaveAndReimport();
            }
            Debug.Log($"[texturas] {seen} textures over {Most} under Assets/Synty, {changed} brought to {Most}" + (dry ? " (would be)" : ""));
        }
    }
}
