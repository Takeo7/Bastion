using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The Synty models and materials the map is built with, by name, for the game (where there is no AssetDatabase):
    /// Resources/MapCatalog.asset, made by Bastion.EditorTools.MapCatalogMake.Run from MapArt.json's props and the cars'
    /// paints. Being in Resources, what it names goes into the build.
    /// </summary>
    public sealed class MapCatalog : ScriptableObject
    {
        public string[] modelNames = Array.Empty<string>();
        public GameObject[] models = Array.Empty<GameObject>();
        public string[] materialNames = Array.Empty<string>();
        public Material[] materials = Array.Empty<Material>();

        static MapCatalog loaded;

        /// <summary>The catalog in Resources, or null if it has not been made.</summary>
        public static MapCatalog Load() => loaded != null ? loaded : loaded = Resources.Load<MapCatalog>("MapCatalog");

        /// <summary>A model by its Synty name (MapBuilder's prefabOf); null if it is not in the catalog.</summary>
        public Func<string, GameObject> PrefabOf() => By(modelNames, models);

        /// <summary>A material by its Synty name (MapBuilder's materialOf).</summary>
        public Func<string, Material> MaterialOf() => By(materialNames, materials);

        static Func<string, T> By<T>(string[] names, T[] things) where T : UnityEngine.Object
        {
            var map = new Dictionary<string, T>();
            for (int k = 0; k < Mathf.Min(names.Length, things.Length); k++) if (things[k] != null) map[names[k]] = things[k];
            return name => name != null && map.TryGetValue(name, out var t) ? t : null;
        }
    }
}
