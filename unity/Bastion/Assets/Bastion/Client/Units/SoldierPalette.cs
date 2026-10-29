using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A soldier's palette, as 0.10.1 made it (wardrobe.ts paletteOf, convert.py wardrobe): a Sidekick colour map of
    /// 16 × 16 cells (32 × 32 texels) from the outfit's parts, each cell in the colours of the preset its part is painted
    /// from, the rest the outfit's base preset's, any left unused (pure red) its fallback's, its own paint over them; what
    /// goes on the head in its own colours; and their skin tone and hair colour on the body's cells.
    /// </summary>
    public static class SoldierPalette
    {
        /// <summary>Skin tones (Sidekick's own, from its presets), a colour each SkinCells cell.</summary>
        public static readonly string[][] SkinTones =
        {
            "fcc19c f8c5a5 fcc19c fcc19c d9a78b d9a78b fcc19c d9a78b d9a78b daa07f d9a78b d9a78b fcc19c fcc19c".Split(' '),
            "eab38b eab38b eab38b eab38b cd9c7d cd9c7d eab38b cd9c7d cd9c7d d09a78 cd9c7d cd9c7d eab38b eab38b".Split(' '),
            "d5a57b d5a57b d5a57b d5a57b bb8b67 bb8b67 d5a57b bb8b67 bb8b67 bb8b67 bb8b67 bb8b67 d5a57b d5a57b".Split(' '),
            "ac7c56 ac7c56 ac7c56 ab805d 976b4d 976b4d ab805d 976b4d 976b4d 976b4d 976b4d 976b4d ab805d ac7c56".Split(' '),
            "805843 805843 805843 7e5d4c 29160c 29160c 7e5d4c 805843 805843 7e5742 805843 805843 7e5d4c 805843".Split(' '),
            "553d34 553d34 553d34 4c3b34 5d483f 5d483f 4c3b34 5d483f 5d483f 5b4c45 5d483f 5d483f 4c3b34 553d34".Split(' '),
        };

        /// <summary>A risen soldier's skin: Sidekick's grey-green dead (ApocalypseZombie_04).</summary>
        public static readonly string[] RisenTone = "5c6b59 5c6b59 5c6b59 494a40 494a40 494a40 373831 494a40 494a40 383f27 494a40 494a40 6d6f5b 5c6b59".Split(' ');

        /// <summary>Hair colours: black, dark brown, brown, auburn, ginger, blonde, grey.</summary>
        public static readonly string[] HairColors = { "151515", "2b1915", "5b3b21", "8a4f2c", "b06545", "c9a46a", "8c8782" };

        /// <summary>The skin's cells (x, y from the top left): face, ears, nose, hands, bare arms and legs.</summary>
        static readonly (int x, int y)[] SkinCells =
        {
            (0, 10), (1, 10), (2, 10), (0, 11), (1, 11), (2, 11), (0, 12), (1, 12), (2, 12), (0, 13), (1, 13), (2, 13), (0, 14), (1, 14),
        };

        /// <summary>The hair's: the eyebrows (row 10, a little darker), the hair and the beard.</summary>
        static readonly (int x, int y)[] HairCells = { (6, 10), (7, 10), (6, 11), (7, 11), (6, 12), (7, 12), (6, 14), (7, 14) };

        /// <summary>The body's cells, not the clothes': the skin, the eyes, the teeth, the hair (a hat leaves them be).</summary>
        static readonly HashSet<int> BodyCells = Body();

        static HashSet<int> Body()
        {
            var cells = new HashSet<int> { 9 * 16 + 3, 9 * 16 + 8 };
            for (int y = 10; y < 15; y++) for (int x = 0; x < 8; x++) cells.Add(y * 16 + x);
            return cells;
        }

        /// <summary>
        /// The palette for an outfit (its parts' meshes in the order of its parts) with what goes on the head (null: bare),
        /// a skin tone (SkinTones, -1 the risen's) and a hair colour (HairColors). `map` gives a preset's colour map.
        /// </summary>
        public static Texture2D Compose(WardrobePiece outfit, IReadOnlyList<Mesh> outfitMeshes, WardrobePiece hat, IReadOnlyList<Mesh> hatMeshes,
            int skin, int hairColor, Func<string, Texture2D> map) => Compose(outfit, outfitMeshes, hat, hatMeshes, skin, hairColor, map, out _);

        /// <summary>Compose, and the palette's colours a cell each (y · 16 + x), for its paint (SquadTint).</summary>
        public static Texture2D Compose(WardrobePiece outfit, IReadOnlyList<Mesh> outfitMeshes, WardrobePiece hat, IReadOnlyList<Mesh> hatMeshes,
            int skin, int hairColor, Func<string, Texture2D> map, out Color32[] cells)
        {
            var cache = new Dictionary<string, Color32[]>();
            Color32[] Of(string preset) => cache.TryGetValue(preset, out var c) ? c : cache[preset] = Cells(map(preset));
            var colors = Piece(outfit, outfitMeshes, Of);
            if (hat != null)
            {
                var own = Piece(hat, hatMeshes, Of);
                foreach (var mesh in hatMeshes)
                    foreach (int cell in CellsOf(mesh))
                        if (!BodyCells.Contains(cell)) colors[cell] = own[cell];
            }
            var tone = skin < 0 ? RisenTone : SkinTones[skin];
            for (int i = 0; i < SkinCells.Length; i++) colors[SkinCells[i].y * 16 + SkinCells[i].x] = Hex(tone[i]);
            var hair = Hex(HairColors[hairColor]);
            foreach (var (x, y) in HairCells) colors[y * 16 + x] = y == 10 ? Darker(hair, 0.8f) : hair;
            cells = (Color32[])colors.Clone();
            return Texture(colors);
        }

        /// <summary>A piece's own colours, a cell each (y · 16 + x).</summary>
        static Color32[] Piece(WardrobePiece piece, IReadOnlyList<Mesh> meshes, Func<string, Color32[]> of)
        {
            var colors = (Color32[])of(piece.@base).Clone();
            for (int i = 0; i < meshes.Count && i < piece.palettes.Length; i++)
            {
                var own = of(piece.palettes[i]);
                foreach (int cell in CellsOf(meshes[i])) colors[cell] = own[cell];
            }
            var fallback = of(piece.fallback ?? piece.@base);
            for (int c = 0; c < 256; c++) if (Unused(colors[c])) colors[c] = fallback[c];
            foreach (var paint in piece.paint)
            {
                var xy = paint.Key.Split(',');
                colors[int.Parse(xy[1]) * 16 + int.Parse(xy[0])] = Hex(paint.Value);
            }
            return colors;
        }

        /// <summary>The cell a UV is in (v from the bottom, cells from the top).</summary>
        public static int CellOf(Vector2 uv) => Mathf.Clamp(Mathf.FloorToInt((1 - uv.y) * 16), 0, 15) * 16 + Mathf.Clamp(Mathf.FloorToInt(uv.x * 16), 0, 15);

        /// <summary>The cells a mesh is painted from, by its first UVs (v from the bottom, cells from the top).</summary>
        static IEnumerable<int> CellsOf(Mesh mesh)
        {
            var seen = new HashSet<int>();
            foreach (var uv in mesh.uv)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * 16), 0, 15);
                int y = Mathf.Clamp(Mathf.FloorToInt((1 - uv.y) * 16), 0, 15);
                if (seen.Add(y * 16 + x)) yield return y * 16 + x;
            }
        }

        /// <summary>A colour map's cells, read off the GPU (the maps need not be readable): the middle texel of each.</summary>
        public static Color32[] Cells(Texture2D map)
        {
            var target = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var filter = map.filterMode;
            map.filterMode = FilterMode.Point;
            Graphics.Blit(map, target);
            map.filterMode = filter;
            var was = RenderTexture.active;
            RenderTexture.active = target;
            var read = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            read.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(target);
            var texels = read.GetPixels32();
            if (Application.isPlaying) UnityEngine.Object.Destroy(read);
            else UnityEngine.Object.DestroyImmediate(read);
            var cells = new Color32[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    cells[y * 16 + x] = texels[(31 - y * 2) * 32 + x * 2];
            return cells;
        }

        static readonly Dictionary<Material, Material> dulled = new Dictionary<Material, Material>();

        /// <summary>
        /// The dead's colours dulled (Ana 28/10, p11: «Los zombies no deberían ser azules y morados»; Arturo: in now): each
        /// material of `figure` that paints from a Sidekick palette swapped for a copy whose clothes' cells in blue to purple
        /// have a third of their colour and a little less light, and their loud greens half (Ana's #20, 29/10). And the
        /// body's (#20: «si es completamente azul no me da sensación de persona»): a skin, hair or eye in green to purple
        /// as little coloured as the clothes', so that the rotten flesh still shows, greyed. One copy a material, kept:
        /// the figures' fades and night rims are copies of it, so they paint the same.
        /// </summary>
        public static void DullDead(GameObject figure)
        {
            foreach (var r in figure.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool any = false;
                for (int k = 0; k < mats.Length; k++)
                {
                    var m = mats[k];
                    if (m == null || !m.HasTexture("_ColorMap") || !(m.GetTexture("_ColorMap") is Texture2D map)) continue;
                    if (!dulled.TryGetValue(m, out var copy) || copy == null)
                    {
                        var cells = Cells(map);
                        for (int c = 0; c < 256; c++) if (!Unused(cells[c])) cells[c] = IsBody(c) ? DullFlesh(cells[c]) : Dull(cells[c]);
                        var palette = Texture(cells);
                        palette.name = m.name + " (apagada)";
                        copy = dulled[m] = new Material(m) { name = m.name + " (apagada)", hideFlags = HideFlags.DontSave };
                        copy.SetTexture("_ColorMap", palette);
                    }
                    mats[k] = copy;
                    any = true;
                }
                if (any) r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// A clothes' colour: blue to purple (hue 190°–330°, whole from 205° to 315°) brought down to a third of its
        /// saturation and 85 % of its light; a loud green (hue 70°–175°, whole from 85° to 160°, as much as its saturation
        /// and light are both over a half) to half its saturation and 90 % of its light. Olive and dark greens, as they are.
        /// </summary>
        static Color32 Dull(Color32 c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float deg = h * 360;
            float w = Mathf.Clamp01(Mathf.Min((deg - 190) / 15, (330 - deg) / 15));
            if (s >= 0.12f && w > 0) return Hsv(c, h, s * (1 - 0.67f * w), v * (1 - 0.15f * w));
            float green = Mathf.Clamp01(Mathf.Min((deg - 70) / 15, (175 - deg) / 15)) * Mathf.Clamp01((Mathf.Min(s, v) - 0.5f) / 0.1f);
            return green > 0 ? Hsv(c, h, s * (1 - 0.5f * green), v * (1 - 0.1f * green)) : c;
        }

        /// <summary>
        /// A body's colour: green to purple (hue 55°–335°, whole from 70° to 320°) to a third of its saturation, never under
        /// 0.15, and 90 % of its light. Sidekick's dead skins (olive 76° at 0.34, blue 227° at 0.4, purple 291° at 0.31)
        /// come down to 0.15, a hint of their hue; the greys (blue-grey 224° at 0.13, the risen's RisenTone at 0.17) about
        /// as they are; the living's tones (reds, browns, yellows: 23°–28°) untouched.
        /// </summary>
        static Color32 DullFlesh(Color32 c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float deg = h * 360;
            float w = Mathf.Clamp01(Mathf.Min((deg - 55) / 15, (335 - deg) / 15));
            if (w <= 0 || s <= 0.15f) return c;
            return Hsv(c, h, Mathf.Max(0.15f, s * (1 - 0.67f * w)), v * (1 - 0.1f * w));
        }

        static readonly Dictionary<Texture, RenderTexture> dulledAtlases = new Dictionary<Texture, RenderTexture>();
        static Material dullAtlas;

        /// <summary>
        /// A POLYGON atlas (the special dead's, UnitViews.Dead) with its colours dulled as a common dead's skin (DullFlesh:
        /// skin and clothes are one texture there): drawn once on the GPU (Resources/Shaders/DullAtlas) into a texture of
        /// its own at half its size, with its mips, kept.
        /// The atlas as it is if the shader is missing.
        /// </summary>
        public static Texture DullAtlas(Texture atlas)
        {
            if (atlas == null) return null;
            if (dulledAtlases.TryGetValue(atlas, out var made) && made != null && made.IsCreated()) return made;
            if (dullAtlas == null)
            {
                var shader = Resources.Load<Shader>("Shaders/DullAtlas");
                if (shader == null) return atlas;
                dullAtlas = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            var rt = new RenderTexture(Mathf.Max(1, atlas.width / 2), Mathf.Max(1, atlas.height / 2), 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = atlas.name + " (apagada)",
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = atlas.filterMode,
                wrapMode = atlas.wrapMode,
                hideFlags = HideFlags.DontSave,
            };
            rt.Create();
            Graphics.Blit(atlas, rt, dullAtlas);
            return dulledAtlases[atlas] = rt;
        }

        static Color32 Hsv(Color32 c, float h, float s, float v)
        {
            var to = Color.HSVToRGB(h, s, v);
            return new Color(to.r, to.g, to.b, c.a / 255f);
        }

        /// <summary>Whether a cell (y · 16 + x) is the body's, not the clothes': the skin, the eyes, the teeth, the hair.</summary>
        public static bool IsBody(int cell) => BodyCells.Contains(cell);

        /// <summary>A palette of these colours (a cell each), as Sidekick's shader reads it.</summary>
        public static Texture2D Texture(Color32[] colors)
        {
            var texels = new Color32[32 * 32];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                            texels[(31 - y * 2 - dy) * 32 + x * 2 + dx] = colors[y * 16 + x];
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Soldier palette" };
            texture.SetPixels32(texels);
            texture.Apply(false, true);
            return texture;
        }

        static bool Unused(Color32 c) => c.r > 252 && c.g < 3 && c.b < 3;

        static Color32 Hex(string hex) => new Color32(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16), 255);

        static Color32 Darker(Color32 c, float k) => new Color32((byte)Mathf.RoundToInt(c.r * k), (byte)Mathf.RoundToInt(c.g * k), (byte)Mathf.RoundToInt(c.b * k), 255);
    }
}
