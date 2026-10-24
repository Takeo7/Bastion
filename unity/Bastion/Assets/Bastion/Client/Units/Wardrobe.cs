using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The soldiers' wardrobe (0.10.1's syntyManifest.ts WARDROBE, docs/SIDEKICK.md): each piece's Sidekick parts and the
    /// preset each part is painted from, read from Resources/Wardrobe.json (unity/tools/wardrobe-json.py).
    /// </summary>
    [Serializable]
    public sealed class WardrobePiece
    {
        /// <summary>outfit, face, hair, beard or headwear.</summary>
        public string piece;
        public string id;
        public string[] parts;
        /// <summary>The preset whose colour map each part is painted from, part by part.</summary>
        public string[] palettes;
        /// <summary>The preset for the cells no part of the piece covers.</summary>
        public string @base;
        /// <summary>The preset for a cell its own has no use for (pure red).</summary>
        public string fallback;
        /// <summary>Cells ('x,y' of 16 × 16, from the top left) given a colour of their own, as hex.</summary>
        public Dictionary<string, string> paint = new Dictionary<string, string>();
    }

    public static class Wardrobe
    {
        static Dictionary<string, WardrobePiece> pieces;

        /// <summary>A piece by its kind and id (outfit survivor4, face face1…).</summary>
        public static WardrobePiece Piece(string piece, string id)
        {
            if (pieces == null) Load();
            return pieces.TryGetValue($"{piece}:{id}", out var p) ? p : throw new ArgumentException($"no {piece} {id} in the wardrobe");
        }

        static void Load()
        {
            pieces = new Dictionary<string, WardrobePiece>();
            var text = Resources.Load<TextAsset>("Wardrobe") ?? throw new InvalidOperationException("Resources/Wardrobe.json is missing");
            foreach (var p in Newtonsoft.Json.JsonConvert.DeserializeObject<Pieces>(text.text).pieces) pieces[$"{p.piece}:{p.id}"] = p;
        }

        sealed class Pieces
        {
            public WardrobePiece[] pieces = Array.Empty<WardrobePiece>();
        }
    }
}
