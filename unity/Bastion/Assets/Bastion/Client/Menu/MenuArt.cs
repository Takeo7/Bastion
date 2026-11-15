using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Menu
{
    /// <summary>
    /// The Synty models the menu's backdrops are built from (MenuBackdrop.Models), by key (MapArt.ModelKey's form,
    /// `pack:model`), for the game, where there is no AssetDatabase: Resources/Menu/MenuArt.asset, made by
    /// Bastion.EditorTools.MenuLab.Make. Its own and small: the map's catalog (MapCatalog, some 400 models with their meshes
    /// and textures) would load whole as the app opens. Arte, 0.12.24 (Arturo's notes of 14/11, menu-inicial).
    /// </summary>
    public sealed class MenuArt : ScriptableObject
    {
        public string[] modelNames = Array.Empty<string>();
        public GameObject[] models = Array.Empty<GameObject>();

        static MenuArt loaded;

        /// <summary>The asset in Resources, or null if it has not been made.</summary>
        public static MenuArt Load() => loaded != null ? loaded : loaded = Resources.Load<MenuArt>("Menu/MenuArt");

        /// <summary>
        /// Not held any more (MenuBackdrop.Hide): its models, their meshes and atlases (some 200 MB as the editor counts
        /// them, MenuLab.Make) go with the next unloading of unused assets, as a scene loads; Load brings them back.
        /// </summary>
        public static void Release() => loaded = null;

        /// <summary>A model by its key; null if it is not here.</summary>
        public Func<string, GameObject> PrefabOf()
        {
            var map = new Dictionary<string, GameObject>();
            for (int k = 0; k < Mathf.Min(modelNames.Length, models.Length); k++) if (models[k] != null) map[modelNames[k]] = models[k];
            return key => key != null && map.TryGetValue(key, out var m) ? m : null;
        }
    }
}
