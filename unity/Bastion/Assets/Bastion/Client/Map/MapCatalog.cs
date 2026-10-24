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
        /// <summary>The quarter turns each model stands from its source FBX (MapArt.UnityTurn), as MapCatalogMake found where it comes from.</summary>
        public int[] modelTurns = Array.Empty<int>();
        public string[] materialNames = Array.Empty<string>();
        public Material[] materials = Array.Empty<Material>();

        static MapCatalog loaded;

        /// <summary>The catalog in Resources, or null if it has not been made.</summary>
        public static MapCatalog Load() => loaded != null ? loaded : loaded = Resources.Load<MapCatalog>("MapCatalog");

        /// <summary>
        /// A model by its key (MapArt.ModelKey, `pack:fbx`; MapBuilder's prefabOf), or by its Synty name alone (the first
        /// pack's of that name); null if it is not in the catalog.
        /// </summary>
        public Func<string, GameObject> PrefabOf()
        {
            var byKey = By(modelNames, models);
            var byName = new Dictionary<string, GameObject>();
            for (int k = 0; k < Mathf.Min(modelNames.Length, models.Length); k++)
            {
                string name = modelNames[k];
                int colon = name.IndexOf(':');
                if (colon >= 0 && models[k] != null && !byName.ContainsKey(name.Substring(colon + 1))) byName[name.Substring(colon + 1)] = models[k];
            }
            // A key's model, or its name's (a catalog made before the keys had names alone).
            string NameOf(string key) => key != null && key.IndexOf(':') >= 0 ? key.Substring(key.IndexOf(':') + 1) : key;
            return name => byKey(name) ?? byKey(NameOf(name)) ?? (name != null && byName.TryGetValue(NameOf(name), out var m) ? m : null);
        }

        /// <summary>The quarter turns a model (by its key) stands from its source FBX; -1 if the catalog doesn't say.</summary>
        public int TurnOf(string key)
        {
            if (turns == null)
            {
                turns = new Dictionary<string, int>();
                for (int k = 0; k < Mathf.Min(modelNames.Length, modelTurns.Length); k++) turns[modelNames[k]] = modelTurns[k];
            }
            return key != null && turns.TryGetValue(key, out int t) ? t : -1;
        }

        [NonSerialized] Dictionary<string, int> turns;

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
