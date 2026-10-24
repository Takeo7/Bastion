using System;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The map in the scene (Partida): built from the state (MapBuilder), storey by storey, and the storey in view, which
    /// UI's controller decides (ShowLevel): what stands above it is not drawn, so its floor and what is on it show.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        /// <summary>The scene's map, if one is built.</summary>
        public static MapView Current { get; private set; }

        public GameState State { get; private set; }
        /// <summary>The storey in view; Levels (one past the top) when the whole map shows, roofs and all.</summary>
        public int Level { get; private set; }
        /// <summary>How many storeys the map has (1: the ground alone).</summary>
        public int Levels => storeys.Length;

        Transform[] storeys = Array.Empty<Transform>();
        Transform roofs;
        MapBuilder builder;
        Func<string, GameObject> prefabs;
        Func<string, Material> materials;

        /// <summary>Builds the map of `state` under this object (what was there goes); every storey shown.</summary>
        public MapBuilder Build(GameState state, Func<string, GameObject> prefabOf, Func<string, Material> materialOf = null)
        {
            for (int k = transform.childCount - 1; k >= 0; k--) DestroyNow(transform.GetChild(k).gameObject);
            State = state;
            prefabs = prefabOf;
            materials = materialOf;
            builder = new MapBuilder(state, transform, prefabOf, materialOf);
            builder.Build();
            storeys = builder.Storeys;
            roofs = builder.Roofs;
            Level = storeys.Length;
            Current = this;
            return builder;
        }

        /// <summary>
        /// The map brought to `state`, the same map changed (UI's event player calls it as a tileChanged or a floorBroken
        /// settles; a door opening is a tileChanged): only the tiles that changed are made again. How many; if `state`
        /// has other storeys, it is built whole (and -1).
        /// </summary>
        public int Rebuild(GameState state)
        {
            if (builder == null) return -1;
            int redone = builder.Update(state);
            if (redone < 0)
            {
                int level = Level;
                Build(state, prefabs, materials);
                ShowLevel(level);
                return -1;
            }
            State = state;
            return redone;
        }

        /// <summary>
        /// The storey in view (0 the ground): those above it and the roofs are not drawn. Levels or more: the whole map,
        /// roofs and all (as it is built).
        /// </summary>
        public void ShowLevel(int level)
        {
            Level = Mathf.Clamp(level, 0, storeys.Length);
            for (int k = 0; k < storeys.Length; k++) storeys[k].gameObject.SetActive(k <= Level);
            if (roofs != null) roofs.gameObject.SetActive(Level >= storeys.Length);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        static void DestroyNow(GameObject o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
