using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The kit pieces' models and materials for the game: each piece's cut FBX (Assets/Synty/Pieces) and, for each material
    /// of the pack's own prefab of the same model, one of Bastion/Building with its atlas (the buildings' colours and, to
    /// come, their cutaway). Resources/PieceCatalog.asset, made by Bastion.EditorTools.PieceMake.Run.
    /// </summary>
    public sealed class PieceCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class PieceEntry
        {
            public string id;
            public GameObject model;
            /// <summary>Its meshes' materials in order, slot by slot.</summary>
            public Material[] materials = Array.Empty<Material>();
        }

        public List<PieceEntry> pieces = new List<PieceEntry>();
        /// <summary>The windows' glass (buildings.ts glassMaterial): pale blue, see-through, both faces.</summary>
        public Material glass;
        /// <summary>Floors and stairs, and roofs (Bastion/Building with swatches of Town's atlas: BuildingSurfaces).</summary>
        public Material floors, roofs;

        static PieceCatalog loaded;
        Dictionary<string, PieceEntry> byId;

        public static PieceCatalog Load() => loaded != null ? loaded : loaded = Resources.Load<PieceCatalog>("PieceCatalog");

        public PieceEntry Entry(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, PieceEntry>();
                foreach (var p in pieces) if (p != null && p.id != null) byId[p.id] = p;
            }
            return id != null && byId.TryGetValue(id, out var e) ? e : null;
        }
    }
}
