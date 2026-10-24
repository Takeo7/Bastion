using System.Linq;
using Bastion.Client.Effects;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// What the scene «Partida» is drawn from (Arte's): each whole state that comes (UI's CombatSession hands it on at
    /// the start, on a return and after a resync) builds the map, unless it is the map already built, so a resync in the
    /// middle of a mission costs nothing. The units' figures follow UI's event player (UnitViews.Sync with the state
    /// seen), not this. On «Mapa», by MapView.
    /// </summary>
    [RequireComponent(typeof(MapView))]
    public sealed class Battlefield : MonoBehaviour
    {
        public static Battlefield Current { get; private set; }

        MapView map;
        string built;
        string place;

        void Awake() => Current = this;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>A mission's state, whole: its map built if it is not the one standing.</summary>
        public void Show(GameState state)
        {
            if (map == null) map = GetComponent<MapView>();
            string signature = SignatureOf(state);
            if (signature == built) return;
            // The same map with some tiles changed (a resync after events missed): those alone.
            string where = $"{state.MapId}/{state.Width}×{state.Height}/{state.Levels?.Count ?? 0}";
            if (where == place && map.State != null)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                int redone = map.Rebuild(state);
                built = signature;
                Debug.Log($"[battlefield] {state.MapId}: {redone} tiles made again in {watch.ElapsedMilliseconds} ms");
                return;
            }
            var catalog = MapCatalog.Load();
            if (catalog == null)
            {
                Debug.LogError("[battlefield] Resources/MapCatalog.asset is missing: unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapCatalogMake.Run -quit");
                return;
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Fx.Current?.Clear();
            var builder = map.Build(state, catalog.PrefabOf(), catalog.MaterialOf());
            built = signature;
            place = where;
            // A new map's fog: what the squad sees from where it starts, at once.
            Fog.Current?.Reset(state);
            Fog.Current?.Show(state, true);
            Debug.Log($"[battlefield] {state.MapId} {state.Width}×{state.Height}: {builder.Placed} props in {clock.ElapsedMilliseconds} ms"
                + (builder.Missing.Count > 0 ? "; missing: " + string.Join(", ", builder.Missing.OrderBy(m => m)) : ""));
        }

        /// <summary>What the map is built from (where it is, its tiles, decor and storeys): the same, nothing to build again.</summary>
        static string SignatureOf(GameState s)
        {
            unchecked
            {
                int h = 17;
                foreach (var t in s.Tiles) h = h * 31 + (t?.GetHashCode() ?? 0);
                if (s.Decor != null) foreach (int d in s.Decor) h = h * 31 + d;
                if (s.Storeys != null) foreach (int k in s.Storeys) h = h * 31 + k;
                foreach (int e in s.Elev) h = h * 31 + e;
                return $"{s.MapId}/{s.Width}×{s.Height}/{h}";
            }
        }
    }
}
