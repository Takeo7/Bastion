using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// The HUD's Font Assets (docs/UNITY-UI.md §4), made from the TTFs in Assets/Bastion/UI/Fonts (tools/ui/woff-to-ttf.py,
    /// from @fontsource's WOFF): Oswald for titles and big numbers, Barlow for what is read; OFL, the only two families.
    ///
    /// Static, their letters drawn here once (Latin-1 and the signs the game writes): a dynamic one changed as it drew a
    /// new letter in play, which reimported the style sheets using it, and UI Toolkit's live reload then rebuilt the HUD
    /// from its UXML, dropping all it had drawn (23/10). Run again it makes the missing and fixes any still dynamic.
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.UI.EditorTools.HudFonts.Make
    /// </summary>
    public static class HudFonts
    {
        const string Folder = "Assets/Bastion/UI/Fonts";

        static readonly string[] Faces = { "Oswald-Medium", "Oswald-SemiBold", "Barlow-Medium", "Barlow-SemiBold", "Barlow-Bold" };

        /// <summary>Printable ASCII, Latin-1 (á, ñ, ¿, «»…) and the signs in the HUD's and Lore's texts.</summary>
        static string Charset()
        {
            var chars = new System.Text.StringBuilder();
            for (var c = 0x20; c <= 0x7e; c++) chars.Append((char)c);
            for (var c = 0xa0; c <= 0xff; c++) chars.Append((char)c);
            chars.Append("–—‘’“”…•·−×✓→←↑↓▲▼▴▾‹›€");
            return chars.ToString();
        }

        public static void Make()
        {
            var charset = Charset();
            foreach (var face in Faces)
            {
                var path = $"{Folder}/{face} SDF.asset";
                var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                if (asset == null)
                {
                    var font = AssetDatabase.LoadAssetAtPath<Font>($"{Folder}/{face}.ttf");
                    if (font == null)
                    {
                        Debug.LogError($"HUD fonts: {Folder}/{face}.ttf missing");
                        continue;
                    }
                    asset = FontAsset.CreateFontAsset(font);
                    asset.name = face + " SDF";
                    AssetDatabase.CreateAsset(asset, path);
                    asset.material.name = face + " Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }
                if (asset.atlasPopulationMode != AtlasPopulationMode.Static)
                {
                    asset.TryAddCharacters(charset, out var missing);
                    if (!string.IsNullOrEmpty(missing)) Debug.Log($"HUD fonts: {face} has no {missing} (drawn by the fallback)");
                    asset.atlasPopulationMode = AtlasPopulationMode.Static;
                }
                // Its atlases inside it, or they would be lost on saving (more than one if the letters overflow the first).
                for (var i = 0; i < asset.atlasTextures.Length; i++)
                {
                    var atlas = asset.atlasTextures[i];
                    if (atlas == null || AssetDatabase.Contains(atlas)) continue;
                    atlas.name = $"{face} Atlas {i}";
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }
                EditorUtility.SetDirty(asset);
                Debug.Log("HUD fonts: " + path + " static, " + asset.characterTable.Count + " letters");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
