using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The plain materials the client makes as it goes (the squad's places, a fallen soldier's candle, the armbands, the
    /// stand-in capsules), each a copy of one kept in Resources/Kit (KitMaterialsMake): a build has only the shaders, and
    /// the variants of them, that its own materials use, so Shader.Find("Universal Render Pipeline/Unlit") came back null in
    /// the app (Red, 26/10) and a see-through or glowing material made from nothing could come out magenta.
    /// </summary>
    public static class KitMaterials
    {
        /// <summary>Unlit, see-through (alpha blended), both sides, no depth written.</summary>
        public const string SeeThrough = "Kit/SeeThrough";
        /// <summary>Lit, glowing (_EMISSION).</summary>
        public const string Glowing = "Kit/Glowing";
        /// <summary>Lit, plain.</summary>
        public const string Plain = "Kit/Plain";
        /// <summary>The target's mark (Bastion/Mark: reddened and glowing at the edges), over a body.</summary>
        public const string Mark = "Kit/Mark";
        /// <summary>A figure dithered in or out (Bastion/FigureFade: UnitViews.Fade), its colour map set on the copy.</summary>
        public const string Fade = "Kit/Fade";
        /// <summary>A figure's night rim (Bastion/NightRim: UnitViews.Rim), its colour map set on the copy.</summary>
        public const string NightRim = "Kit/NightRim";
        /// <summary>Where a figure is, a stencil bit, for the fog to leave it be (Bastion/FigureMask: UnitViews.Rim).</summary>
        public const string FigureMask = "Kit/FigureMask";
        /// <summary>A whole car of the street, worn (Bastion/Car: MapBuilder.PaintWhole), its paint's textures set on the copy.</summary>
        public const string Car = "Kit/Car";

        /// <summary>A new material copied from `kind`'s, named `name`; null if it isn't there (KitMaterialsMake not run).</summary>
        public static Material New(string kind, string name)
        {
            var source = Resources.Load<Material>(kind);
            if (source == null)
            {
                Debug.LogError($"[kit] Resources/{kind}.mat missing (Bastion.EditorTools.KitMaterialsMake.Run)");
                return null;
            }
            return new Material(source) { name = name, hideFlags = HideFlags.DontSave };
        }
    }
}
