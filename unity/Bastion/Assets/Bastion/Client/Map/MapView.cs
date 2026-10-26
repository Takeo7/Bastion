using System;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The map in the scene (Partida): built from the state (MapBuilder), storey by storey, and the storey in view, which
    /// UI's controller decides (ShowLevel): the buildings' cutaway (Cutaway) opens them down to it.
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
            // La Lata made again for this map (PlaceLata).
            if (Lata != null) DestroyNow(Lata.transform.parent != null ? Lata.transform.parent.gameObject : Lata.gameObject);
            Lata = null;
            State = state;
            prefabs = prefabOf;
            materials = materialOf;
            builder = new MapBuilder(state, transform, prefabOf, materialOf);
            builder.Build();
            storeys = builder.Storeys;
            roofs = builder.Roofs;
            Level = storeys.Length;
            Current = this;
            PlaceLata(state);
            // (BASTION_SOMBRAS_FIJAS=1: the pipeline's own, to weigh what fitting them costs: a test.)
            if (FitShadows && GetComponent<ShadowFit>() == null && Environment.GetEnvironmentVariable("BASTION_SOMBRAS_FIJAS") != "1") gameObject.AddComponent<ShadowFit>();
            return builder;
        }

        // ------------------------------------------------------------------------------------------- La Lata, evacuating

        /// <summary>Whether La Lata waits by the evacuation on this map (the settlement's clearing has its own: off there).</summary>
        public bool WithLata = true;

        /// <summary>Whether the sun's shadows follow the camera's view (ShadowFit: the battle's; the clearing has its own reach, Settlement.ShadowMetres).</summary>
        public bool FitShadows = true;

        /// <summary>La Lata by the evacuation (mapView.ts placeBus, evacBus.ts), on a map whose mission has a zone; null without.</summary>
        public LaLata Lata { get; private set; }

        /// <summary>Where La Lata stands (Unity's world, on the ground): for the camera when it goes; null without one.</summary>
        public Vector3? LataAt => Lata != null && lataWay != null ? Lata.transform.position : (Vector3?)null;

        /// <summary>How far it drives to be gone (tiles): past the map's edge the way it heads, its whole length.</summary>
        float? lataWay;

        /// <summary>
        /// La Lata (La Mula, or the campaign's vehicle before it: GameState.vehicle; a skirmish's, the bus) by the evacuation
        /// zone, its doors to it, unless it has gone (mission.evacDone): on its bay (mission.evacBus), its nose to the edge it
        /// leaves by; on a map without one, just off the edge nearest the zone and along it, its nose to the nearer corner.
        /// </summary>
        void PlaceLata(GameState s)
        {
            lataWay = null;
            var zoneTiles = s.Mission?.EvacZone;
            if (!WithLata || zoneTiles == null || zoneTiles.Count == 0 || prefabs == null)
            {
                if (Lata != null) Lata.gameObject.SetActive(false);
                return;
            }
            int vehicle = s.Vehicle ?? 3;
            if (Lata != null && Lata.Vehicle != vehicle)
            {
                DestroyNow(Lata.transform.parent != null ? Lata.transform.parent.gameObject : Lata.gameObject);
                Lata = null;
            }
            if (Lata == null)
            {
                // Beside the map, not in it: what is under the map's root is baked into the ground's occlusion (UI's
                // GroundOcclusion), and its shade stayed where it had stood once it had gone.
                var holder = new GameObject("La Lata de la evacuación").transform;
                holder.SetParent(transform.parent, false);
                holder.SetPositionAndRotation(transform.position, transform.rotation);
                Lata = LaLata.Make(holder, vehicle, prefabs, s.Mission.Night == true);
            }
            Lata.gameObject.SetActive(true);
            Vector3 Mid(System.Collections.Generic.IReadOnlyList<Vec2> tiles)
            {
                float x = 0, z = 0;
                foreach (var t in tiles)
                {
                    x += t.X + 0.5f;
                    z += t.Y + 0.5f;
                }
                return new Vector3(x / tiles.Count, 0, z / tiles.Count);
            }
            var zone = Mid(zoneTiles);
            var bay = s.Mission.EvacBus;
            Vector3 at;
            Vector2 d;
            if (bay?.Tiles != null && bay.Tiles.Count > 0)
            {
                at = Mid(bay.Tiles);
                var dir = Bastion.Engine.Grid.Dirs4[((bay.Heading % 4) + 4) % 4];
                d = new Vector2(dir.X, dir.Y);
            }
            else
            {
                // Off the nearest edge, a little more than half its width out, along it.
                float alongX = zone.x < s.Width / 2f ? -1 : 1, alongZ = zone.z < s.Height / 2f ? -1 : 1;
                var edges = new (float gap, Vector3 at, Vector2 d)[]
                {
                    (zone.z, new Vector3(zone.x, 0, -1.4f), new Vector2(alongX, 0)),
                    (s.Height - zone.z, new Vector3(zone.x, 0, s.Height + 1.4f), new Vector2(alongX, 0)),
                    (zone.x, new Vector3(-1.4f, 0, zone.z), new Vector2(0, alongZ)),
                    (s.Width - zone.x, new Vector3(s.Width + 1.4f, 0, zone.z), new Vector2(0, alongZ)),
                };
                var e = edges[0];
                foreach (var x in edges) if (x.gap < e.gap) e = x;
                at = e.at;
                d = e.d;
            }
            Lata.Park(at, Mathf.Atan2(d.x, d.y), zone, s.Mission.EvacDone != true, bay?.Tiles != null && bay.Tiles.Count > 0 ? bay.Tiles.Count / 2f : 0);
            float ahead = d.x > 0 ? s.Width - at.x : d.x < 0 ? at.x : d.y > 0 ? s.Height - at.z : at.z;
            lataWay = Mathf.Max(0, ahead) + Lata.Length / 2 + 1;
        }

        /// <summary>
        /// La Lata goes (the engine's evacDeparted, once everyone aboard has boarded: game.ts busLeaves): its doors, its
        /// lights, and away along its heading off the map; done once it is gone (at once without one).
        /// </summary>
        public Awaitable LataDeparts(System.Threading.CancellationToken ct = default)
        {
            if (Lata == null || lataWay == null) return Done();
            return Lata.DepartAsync(lataWay.Value, false, ct);
        }

        static async Awaitable Done() => await Awaitable.NextFrameAsync();

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
        /// The storey in view (0 the ground): the cutaway's chosen storey (Cutaway.Focus.level), the buildings with storeys
        /// above it open down to it; and the fog's.
        /// </summary>
        public void ShowLevel(int level)
        {
            Level = Mathf.Clamp(level, 0, storeys.Length);
            // Every storey stands: what is above the one in view fades building by building in the cutaway (buildings.ts
            // Focus.level), as the roofs do.
            for (int k = 0; k < storeys.Length; k++) storeys[k].gameObject.SetActive(true);
            if (roofs != null) roofs.gameObject.SetActive(true);
            var cutaway = GetComponent<Cutaway>();
            if (cutaway != null) cutaway.Focus.level = Level;
            // The fog is the storey in view's.
            Fog.Current?.Refog();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (Lata != null) DestroyNow(Lata.transform.parent != null ? Lata.transform.parent.gameObject : Lata.gameObject);
        }

        static void DestroyNow(GameObject o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
