using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// What the figures are made of, by name, for the game (where there is no AssetDatabase): Sidekick's presets whole
    /// (their prefabs) and their colour maps (what a soldier's palette is composed from), the clips by 0.10.1's
    /// animation ids (Anims.json), the weapons, and the POLYGON characters the radio's voices are drawn as. Resources/FigureCatalog.asset, made by Bastion.EditorTools.FigureCatalogMake.Run.
    /// </summary>
    public sealed class FigureCatalog : ScriptableObject
    {
        public string[] presetNames = Array.Empty<string>();
        public GameObject[] presets = Array.Empty<GameObject>();
        public string[] colourMapNames = Array.Empty<string>();
        public Texture2D[] colourMaps = Array.Empty<Texture2D>();
        public string[] clipIds = Array.Empty<string>();
        public AnimationClip[] clips = Array.Empty<AnimationClip>();
        public string[] weaponNames = Array.Empty<string>();
        public GameObject[] weapons = Array.Empty<GameObject>();
        /// <summary>POLYGON's characters and attachments by their prefab's name (the radio's voices: Voices).</summary>
        public string[] characterNames = Array.Empty<string>();
        public GameObject[] characters = Array.Empty<GameObject>();
        /// <summary>POLYGON's other atlases the special dead may wear by their name (UnitViews.DeadAtlases: the Hinchados').</summary>
        public string[] atlasNames = Array.Empty<string>();
        public Texture2D[] atlases = Array.Empty<Texture2D>();
        /// <summary>Seconds into each clip after which the body no longer moves (a death: it lies still; syntyAnims.ts settleOf).</summary>
        public float[] settles = Array.Empty<float>();
        /// <summary>
        /// Each of a Sidekick palette's 256 cells by its kind (FigureCatalogMake.CellKinds: what Sidekick's material maps
        /// make of it, metal, gloss, reflection, opacity, its folds' shade, skin): cells of one kind look alike but for
        /// their colour (SoldierFigure.Split moves corners from one to another).
        /// </summary>
        public int[] cellKinds = Array.Empty<int>();

        static FigureCatalog loaded;
        Dictionary<string, GameObject> presetBy;
        Dictionary<string, Texture2D> colourMapBy;
        Dictionary<string, AnimationClip> clipBy;
        Dictionary<string, GameObject> weaponBy;
        Dictionary<string, GameObject> characterBy;
        Dictionary<string, Texture2D> atlasBy;

        /// <summary>A POLYGON character's (or attachment's) prefab by its name (SM_Chr_Boss_Female_01…), or null.</summary>
        public GameObject Character(string name) => Find(ref characterBy, characterNames, characters, name);

        /// <summary>A POLYGON atlas by its texture's name (PolygonZombieBoss_03_A…), or null.</summary>
        public Texture2D Atlas(string name) => Find(ref atlasBy, atlasNames, atlases, name);

        /// <summary>A weapon's model by its Synty name (WeaponArt.fbx), or null.</summary>
        public GameObject Weapon(string fbx) => Find(ref weaponBy, weaponNames, weapons, fbx);

        /// <summary>The catalog in Resources, or null if it has not been made.</summary>
        public static FigureCatalog Load() => loaded != null ? loaded : loaded = Resources.Load<FigureCatalog>("FigureCatalog");

        /// <summary>A Sidekick preset's prefab (ApocalypseZombie_01…), or null.</summary>
        public GameObject Preset(string name) => Find(ref presetBy, presetNames, presets, name);

        /// <summary>A Sidekick preset's colour map (T_&lt;preset&gt;ColorMap) by the preset's name, or null.</summary>
        public Texture2D ColourMap(string preset) => Find(ref colourMapBy, colourMapNames, colourMaps, preset);

        /// <summary>A clip by its 0.10.1 id (idleMasc, rifleAim…), or null.</summary>
        public AnimationClip Clip(string id) => Find(ref clipBy, clipIds, clips, id);

        /// <summary>When the body of clip `id` stops moving (its length if not measured).</summary>
        public float Settle(string id)
        {
            int k = Array.IndexOf(clipIds, id);
            return k >= 0 && k < settles.Length && settles[k] > 0 ? settles[k] : k >= 0 && clips[k] != null ? clips[k].length : 0;
        }

        static T Find<T>(ref Dictionary<string, T> by, string[] names, T[] things, string name) where T : UnityEngine.Object
        {
            if (by == null)
            {
                by = new Dictionary<string, T>();
                for (int k = 0; k < Mathf.Min(names.Length, things.Length); k++) if (things[k] != null) by[names[k]] = things[k];
            }
            return name != null && by.TryGetValue(name, out var t) ? t : null;
        }
    }
}
