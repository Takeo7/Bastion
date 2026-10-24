using System;
using System.Collections.Generic;
using System.Linq;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A soldier's figure from their Dress (0.10.1's wardrobe.ts modelOf): the pieces' parts, out of Sidekick's database,
    /// put together by the Sidekicks tool's runtime API into one mesh on one rig, in a palette of their own
    /// (SoldierPalette). About a quarter of a second each: made as a mission loads, never in the middle of a turn. One per
    /// dress is kept (a copy each after the first).
    /// </summary>
    public static class SoldierFigure
    {
        static DatabaseManager database;
        static readonly Dictionary<string, GameObject> made = new Dictionary<string, GameObject>();
        static Transform shelf;

        /// <summary>A new figure in that dress (inactive copies are kept on a shelf of their own); null if a part is missing.</summary>
        public static GameObject Make(Dress dress, string name)
        {
            if (!made.TryGetValue(dress.Key, out var model) || model == null) made[dress.Key] = model = Build(dress);
            if (model == null) return null;
            var figure = UnityEngine.Object.Instantiate(model);
            figure.name = name;
            figure.SetActive(true);
            return figure;
        }

        static GameObject Build(Dress dress)
        {
            var catalog = FigureCatalog.Load();
            if (catalog == null)
            {
                Debug.LogError("[figures] Resources/FigureCatalog.asset is missing: unity/tools/unity.sh -executeMethod Bastion.EditorTools.FigureCatalogMake.Run -quit");
                return null;
            }
            if (database == null)
            {
                database = new DatabaseManager();
                database.GetDbConnection();
            }
            var pieces = dress.Pieces().Select(p => (p.piece, art: Wardrobe.Piece(p.piece, p.id))).Where(p => p.art != null).ToList();
            var parts = new Dictionary<string, SkinnedMeshRenderer>();
            foreach (string name in pieces.SelectMany(p => p.art.parts).Distinct())
            {
                var part = SidekickPart.SearchForByName(database, name)?.GetPartModel();
                var renderer = part != null ? part.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (renderer == null)
                {
                    Debug.LogWarning($"[figures] no Sidekick part {name}");
                    continue;
                }
                parts[name] = renderer;
            }
            var outfit = pieces[0].art;
            var hat = pieces.Where(p => p.piece == "headwear").Select(p => p.art).FirstOrDefault();
            List<Mesh> Meshes(WardrobePiece piece) => piece?.parts.Where(parts.ContainsKey).Select(n => parts[n].sharedMesh).ToList();
            var palette = SoldierPalette.Compose(outfit, Meshes(outfit), hat, Meshes(hat), dress.skin, dress.hairColor, catalog.ColourMap);

            var baseModel = Resources.Load<GameObject>("Meshes/SK_BaseModel");
            var material = new Material(Resources.Load<Material>("Materials/M_BaseMaterial"));
            material.SetTexture("_ColorMap", palette);
            var runtime = new SidekickRuntime(baseModel, material, null, database)
            {
                BodyTypeBlendValue = dress.sex == "f" ? 100 : -100,
                BodySizeSkinnyBlendValue = dress.build == "slim" ? 100 : 0,
                // wardrobe.ts BUILD_WEIGHTS: a heavy build at 0.4 of Sidekick's full.
                BodySizeHeavyBlendValue = dress.build == "heavy" ? 40 : 0,
                MusclesBlendValue = -100,
            };
            var model = runtime.CreateCharacter("Soldado", parts.Values.ToList(), true, true);
            OwnColours(model);
            if (shelf == null)
            {
                shelf = new GameObject("Soldados hechos").transform;
                shelf.gameObject.hideFlags = HideFlags.HideInHierarchy;
            }
            model.transform.SetParent(shelf, false);
            model.SetActive(false);
            return model;
        }

        /// <summary>
        /// Each renderer's colour map on itself (a MaterialPropertyBlock): with URP's SRP Batcher two Sidekick characters
        /// drawn one after another came out both in one's colours (docs/UNITY-ARTE.md §8).
        /// </summary>
        public static void OwnColours(GameObject figure)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var map = renderer.sharedMaterial != null && renderer.sharedMaterial.HasTexture("_ColorMap") ? renderer.sharedMaterial.GetTexture("_ColorMap") : null;
                if (map == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetTexture("_ColorMap", map);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
