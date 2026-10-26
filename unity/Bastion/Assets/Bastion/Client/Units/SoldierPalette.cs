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
