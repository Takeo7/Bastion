using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// What a soldier's figure is made of, for its paint (SquadTint): which pieces of the body each cell of its palette is
    /// worn on (bits: 1 the headgear, 2 the torso, 3 the arms, 4 the legs; convert.py write_parts, by each vertex's main
    /// bone), the palette as made, and the owner's band's radius round each upper arm (model units). Kept on the figure
    /// SoldierFigure makes, and so on every copy of it.
    /// </summary>
    public sealed class SoldierMarks : MonoBehaviour
    {
        public int[] cells = new int[256];
        public Color32[] palette = new Color32[256];
        public float bandLeft, bandRight;
    }

    /// <summary>
    /// «Ropa con el color del jugador» (0.10.1's settings.ts squadTint, syntyFigures.ts paint; Arturo, 21/10): on, the
    /// soldiers' clothes dyed with their Personalizar colours (the helmet and the torso their armour's, the sleeves and the
    /// trousers only a colour of their own) and their player's band glowing on the upper arms; off (as it starts), each
    /// outfit's own colours and no band, the ring under them telling whose. In the battle, on the base, in the previews
    /// and the busts. UI sets On from its Ajustes (GameSettings.SquadTint): the soldiers on screen are painted again.
    /// </summary>
    public static class SquadTint
    {
        static bool on;

        public static bool On
        {
            get => on;
            set
            {
                if (on == value) return;
                on = value;
                foreach (var p in painted.ToList())
                    if (p == null) painted.Remove(p);
                    else p.Repaint();
                Changed?.Invoke(value);
            }
        }

        /// <summary>The tint turned on or off (its new value): what drew a figure into a texture draws it again (the busts).</summary>
        public static event Action<bool> Changed;

        /// <summary>A player's colour by their slot (#rrggbb): the seats' if UI tells, else the game's own.</summary>
        public static Func<int, string> ColourOf = slot => Content.PlayerColors[((slot % Content.PlayerColors.Count) + Content.PlayerColors.Count) % Content.PlayerColors.Count];

        static readonly HashSet<Painted> painted = new HashSet<Painted>();

        /// <summary>
        /// A soldier's figure (SoldierFigure's) painted for `look` (their Personalizar colours; none: as it is) and `band`
        /// (their player's colour, #rrggbb; none: no band), now and whenever the tint changes.
        /// </summary>
        public static void Paint(GameObject figure, Appearance look, string band)
        {
            if (figure == null || figure.GetComponentInChildren<SoldierMarks>(true) == null) return;
            var p = figure.GetComponent<Painted>() ?? figure.AddComponent<Painted>();
            p.look = look;
            p.band = band;
            painted.Add(p);
            p.Repaint();
        }

        internal static void Gone(Painted p) => painted.Remove(p);

        /// <summary>One dyed palette per look and figure (they are few: the campaign's soldiers).</summary>
        static readonly Dictionary<string, Texture2D> palettes = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The palette dyed (synty.ts's dye: on the cloth of each piece, its colour at 0.6, keeping the cloth's shading: the
        /// colour times its luminance over 0.14, from 0.35 to 1.6; the cloth not near black, not near white, not the
        /// skin's, eyes', teeth's or hair's cells).
        /// </summary>
        internal static Texture2D Dyed(SoldierMarks marks, Appearance look)
        {
            string head = string.IsNullOrEmpty(look.Head) ? HeadIds.Helmet : look.Head;
            var full = new Appearance { Primary = look.Primary, Secondary = look.Secondary, Head = head, Helmet = look.Helmet, Torso = look.Torso, Arms = look.Arms, Legs = look.Legs };
            string Own(string part) => part == ArmorParts.Arms ? look.Arms : part == ArmorParts.Legs ? look.Legs : Appearances.PartColor(full, part);
            // The headgear in the armour's colour (a helmet, a mask) or the trim's (a beret, a cap, a hood: Appearances.PartColor).
            string helmet = head != HeadIds.Bare ? Own(ArmorParts.Helmet) : null;
            var dyes = new[] { null, helmet, Own(ArmorParts.Torso), Own(ArmorParts.Arms), Own(ArmorParts.Legs) };
            string key = string.Join("|", marks.GetInstanceID(), string.Join(",", dyes));
            if (palettes.TryGetValue(key, out var made) && made != null) return made;
            var colours = (Color32[])marks.palette.Clone();
            for (int c = 0; c < 256; c++)
            {
                int bits = marks.cells[c];
                if (bits == 0 || SoldierPalette.IsBody(c)) continue;
                // The piece it is on, the torso first (a cell it shares with the trousers is the jacket's).
                string dye = null;
                foreach (int part in new[] { 2, 1, 3, 4 })
                    if ((bits & (1 << part)) != 0 && dyes[part] != null)
                    {
                        dye = dyes[part];
                        break;
                    }
                if (dye == null || !ColorUtility.TryParseHtmlString(dye, out var d)) continue;
                Color srgb = colours[c];
                Color.RGBToHSV(srgb, out _, out float s, out float v);
                if (v < 0.16f || (v > 0.93f && s < 0.08f)) continue;
                var lin = srgb.linear;
                float lum = 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
                var shaded = d.linear * Mathf.Clamp(lum / 0.14f, 0.35f, 1.6f);
                var mixed = Color.Lerp(lin, shaded, 0.6f).gamma;
                colours[c] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(mixed.r) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(mixed.g) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(mixed.b) * 255), 255);
            }
            return palettes[key] = SoldierPalette.Texture(colours);
        }

        static Mesh tube;

        /// <summary>An open tube a unit across and a unit long (along +y), its outside out: the band, scaled to each arm.</summary>
        internal static Mesh Tube()
        {
            if (tube != null) return tube;
            const int n = 16;
            var v = new List<Vector3>();
            var nrm = new List<Vector3>();
            var t = new List<int>();
            for (int k = 0; k <= n; k++)
            {
                float a = k * Mathf.PI * 2 / n;
                var r = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(r * 0.5f);
                v.Add(r * 0.5f + Vector3.up);
                nrm.Add(r);
                nrm.Add(r);
                if (k == n) continue;
                int b = k * 2;
                t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            tube = new Mesh { name = "Brazalete", vertices = v.ToArray(), normals = nrm.ToArray(), triangles = t.ToArray() };
            tube.RecalculateBounds();
            return tube;
        }

        static readonly Dictionary<string, Material> bands = new Dictionary<string, Material>();

        /// <summary>The band's material: the player's colour, glowing (synty.ts: 0.9, by night 2).</summary>
        internal static Material BandMaterial(string colour, bool night)
        {
            string key = colour + (night ? "|night" : "");
            if (bands.TryGetValue(key, out var m) && m != null) return m;
            ColorUtility.TryParseHtmlString(colour, out var c);
            m = KitMaterials.New(KitMaterials.Glowing, "Brazalete " + colour);
            if (m == null) return null;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.3f);
            m.SetColor("_EmissionColor", c.linear * (night ? 2f : 0.9f));
            return bands[key] = m;
        }
    }

    /// <summary>A figure painted by SquadTint: what it is painted for, and its bands.</summary>
    public sealed class Painted : MonoBehaviour
    {
        public Appearance look;
        public string band;
        readonly List<GameObject> rings = new List<GameObject>();

        void OnDestroy() => SquadTint.Gone(this);

        /// <summary>Its colours again (the tint changed, or it was painted for another look).</summary>
        public void Repaint()
        {
            var marks = GetComponentInChildren<SoldierMarks>(true);
            if (marks == null) return;
            bool tint = SquadTint.On;
            var block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMaterial == null || !r.sharedMaterial.HasTexture("_ColorMap")) continue;
                // Who has no look of their own (older saves) wears their player's armour (syntyFigures.ts: the accent towards grey).
                var paint = look ?? (band != null ? Appearances.DefaultAppearance(0, band) : null);
                var map = tint && paint != null ? SquadTint.Dyed(marks, paint) : r.sharedMaterial.GetTexture("_ColorMap");
                r.GetPropertyBlock(block);
                block.SetTexture("_ColorMap", map);
                r.SetPropertyBlock(block);
            }
            foreach (var g in rings) if (g != null) Destroy(g);
            rings.Clear();
            if (!tint || string.IsNullOrEmpty(band)) return;
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            bool night = Mood.Current != null && Mood.Current.Now == Mood.Night;
            foreach (var (upper, lower, radius) in new[] { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, marks.bandLeft), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, marks.bandRight) })
            {
                var a = animator.GetBoneTransform(upper);
                var b = animator.GetBoneTransform(lower);
                if (a == null || b == null || radius <= 0) continue;
                // From 0.18 to 0.5 of the way to the elbow (convert.py BAND_SPAN), round the sleeve.
                var axis = b.localPosition;
                float length = axis.magnitude;
                var ring = new GameObject("brazalete");
                ring.layer = gameObject.layer;
                ring.transform.SetParent(a, false);
                ring.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis / Mathf.Max(length, 1e-5f));
                ring.transform.localPosition = axis * 0.18f;
                // The bone's frame is the model's units: the radius as measured, a little out over the cloth.
                ring.transform.localScale = new Vector3(radius * 2.2f, length * 0.32f, radius * 2.2f);
                ring.AddComponent<MeshFilter>().sharedMesh = SquadTint.Tube();
                var mr = ring.AddComponent<MeshRenderer>();
                mr.sharedMaterial = SquadTint.BandMaterial(band, night);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings.Add(ring);
            }
        }

        static void Destroy(GameObject o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
