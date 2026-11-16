using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The gear's pictures for the runs' grids (the stash and the pack: H0-DISENO §2), one per kind and type, drawn from
    /// the thing's model: laid along the picture with its muzzle, tip or head to the right, a little from above and turned,
    /// on nothing. 128 px a cell (CellSize): a rifle of 4×1 is 512×128, a vest of 2×2 256×256; 10 px clear round it.
    /// Rendered in the editor (Bastion.EditorTools.GearPicturesMake) into Resources/GearPictures: «&lt;kind&gt;_&lt;type&gt;.png»,
    /// or «&lt;kind&gt;.png» for the kinds without a type (mags, medicine, supplies).
    /// </summary>
    public static class GearPictures
    {
        public const string Folder = "GearPictures";

        /// <summary>The pictures' pixels a cell.</summary>
        public const int CellSize = 128;

        static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The picture of a gear by its kind (GearKinds: "weapon", "item", "mod", "vest", "mags", "medicine", "supplies") and
        /// its type (none for the last three), or null if it has none yet. A type without a picture of its own takes its
        /// kind's, if there is one.
        /// </summary>
        public static Texture Of(string kind, string type)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            string name = string.IsNullOrEmpty(type) ? kind : kind + "_" + type;
            var texture = Load(name);
            return texture != null || name == kind ? texture : Load(kind);
        }

        static Texture2D Load(string name)
        {
            if (!loaded.TryGetValue(name, out var texture)) loaded[name] = texture = Resources.Load<Texture2D>(Folder + "/" + name);
            return texture;
        }
    }
}
