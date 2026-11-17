using System;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The camping's models for the game (runs, H9 §5: Apocalypse's mobile homes, each its shell, glass, skirt and inside;
    /// the tents, chairs and the rest of its marks, MapBuilder.CampScenery): Resources/CaravanArt.asset, made by
    /// Bastion.EditorTools.CampingLab.MakeArt, which also makes their meshes readable (MapBuilder.Caravans reads the caravans'
    /// into meshes of its own). Apart from MapCatalog: the packs' own prefabs whole, which MapCatalog's names (`pack:fbx`)
    /// would take for Extra's shell alone.
    /// </summary>
    public sealed class CaravanArt : ScriptableObject
    {
        public string[] names = Array.Empty<string>();
        public GameObject[] models = Array.Empty<GameObject>();

        static CaravanArt loaded;

        /// <summary>The asset in Resources, or null if it has not been made.</summary>
        public static CaravanArt Load() => loaded != null ? loaded : loaded = Resources.Load<CaravanArt>("CaravanArt");

        /// <summary>A model by its prefab's name (SM_Bld_Trailer_01…); null if it isn't there.</summary>
        public GameObject Model(string name)
        {
            for (int k = 0; k < Mathf.Min(names.Length, models.Length); k++) if (names[k] == name) return models[k];
            return null;
        }
    }
}
