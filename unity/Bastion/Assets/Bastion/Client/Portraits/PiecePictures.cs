using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The base's pieces as pictures, for the Asentamiento's palette (StrategyRoot.PieceArtOf; 0.10.1's pieceIcons.ts and
    /// lab/renders/iconRenders.ts piece-&lt;id&gt;): each as the clearing draws it at its first level, 3/4 from the front left and
    /// above, on nothing; the buildings on their plot. Rendered in the editor (Bastion.EditorTools.PiecePicturesMake) into
    /// Resources/PiecePictures, 256 px square.
    /// </summary>
    public static class PiecePictures
    {
        public const string Folder = "PiecePictures";

        static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();

        /// <summary>The picture of a piece (PieceIds), or null if it has none.</summary>
        public static Texture Of(string piece)
        {
            if (string.IsNullOrEmpty(piece)) return null;
            if (!loaded.TryGetValue(piece, out var texture)) loaded[piece] = texture = Resources.Load<Texture2D>(Folder + "/" + piece);
            return texture;
        }
    }
}
