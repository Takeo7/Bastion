using System;
using System.Collections.Generic;
using System.Linq;
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
            wearLeft = -1;
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
            // Snow on it in winter as on the map's cars (the 3D review's E-02, 10/11: beside the map, SnowCover.Mark misses it).
            Lata.Snowy(Mood.Snowy(s));
            // Parked, in the fog as the map is (out of it only while it drives off: LataDeparts).
            Lata.OutOfFog(false);
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
            // Out of the fog while it goes (the 3D review's E-07, 10/11): over tiles seen, seen before and never seen at once,
            // the fog cut it into lit and dark pieces.
            Lata.OutOfFog(true);
            Grazes();
            var going = Lata.DepartAsync(lataWay.Value, false, ct);
            _ = RunOver(ct);
            return going;
        }

        /// <summary>
        /// The cars of the map its way out passes beside or through (E-07, 10/11: a parked taxi seemed grazed): each one whose
        /// box reaches into the strip from where it stands to past the map's edge, its width over its plates, logged with how
        /// far into it (m).
        /// </summary>
        void Grazes()
        {
            var fwd = Lata.transform.forward;
            fwd.y = 0;
            fwd.Normalize();
            var right = new Vector3(fwd.z, 0, -fwd.x);
            var start = Lata.transform.position;
            float back = Lata.Length / 2 * MapSpace.Tile, ahead = (lataWay ?? 0) * MapSpace.Tile + back, half = Lata.PlatedWidth / 2 * MapSpace.Tile;
            int n = 0;
            foreach (var (anchor, car) in builder.Cars)
            {
                var rs = car.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var box = rs[0].bounds;
                foreach (var r in rs) box.Encapsulate(r.bounds);
                float a0 = float.MaxValue, a1 = float.MinValue, s0 = float.MaxValue, s1 = float.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    var c = new Vector3((k & 1) == 0 ? box.min.x : box.max.x, 0, (k & 2) == 0 ? box.min.z : box.max.z) - start;
                    float a = Vector3.Dot(c, fwd), b = Vector3.Dot(c, right);
                    a0 = Mathf.Min(a0, a);
                    a1 = Mathf.Max(a1, a);
                    s0 = Mathf.Min(s0, b);
                    s1 = Mathf.Max(s1, b);
                }
                if (a1 < -back || a0 > ahead) continue;
                float into = Mathf.Min(half - s0, s1 + half);
                if (into <= 0) continue;
                n++;
                Debug.Log(FormattableString.Invariant($"[lata] its way out grazes car {anchor} ({anchor % State.Width},{anchor / State.Width}) by {Mathf.Min(into, s1 - s0):0.00} m"));
            }
            Debug.Log($"[lata] its way out: {n} cars in it");
        }

        /// <summary>
        /// How far to its sides beyond its width it hits one of the dead (m), and how far before its nose gets to one's middle
        /// (m): their bodies' breadth.
        /// </summary>
        const float RunOverSide = 0.25f, RunOverReach = 0.3f;
        /// <summary>
        /// One of the dead thrown by it: on ahead and off to its side (m/s, so much for each m/s of the bus and so much more,
        /// at least `ThrowLeast`), off the way it goes, or the bus going on faster would drive through it in the air.
        /// </summary>
        const float ThrowOn = 0.8f, ThrowOnMore = 1, ThrowAside = 0.35f, ThrowAsideMore = 2, ThrowLeast = 3;

        /// <summary>
        /// La Lata going over the dead in its way as it pulls away (Arturo's #5 of 7/11: «que lo atropelle y salga volando»;
        /// only what is seen, the mission over as it goes): each of the dead standing and seen within its width ahead of it,
        /// thrown as its nose gets there at the speed it has then (UnitViews.RunOver).
        /// </summary>
        async Awaitable RunOver(System.Threading.CancellationToken ct)
        {
            var units = Bastion.Client.Units.UnitViews.Current;
            if (Lata == null || units == null || lataWay == null) return;
            var start = Lata.transform.position;
            var ahead = new Vector3(Mathf.Sin(Lata.Heading), 0, Mathf.Cos(Lata.Heading));
            var aside = new Vector3(ahead.z, 0, -ahead.x);
            float half = Lata.Width / 2 * MapSpace.Tile + RunOverSide, nose = Lata.Length / 2 * MapSpace.Tile, way = lataWay.Value * MapSpace.Tile + nose;
            var waiting = units.DeadStanding()
                .Select(d => (d.id, along: Vector3.Dot(d.at - start, ahead), side: Vector3.Dot(d.at - start, aside)))
                .Where(d => d.along > nose - RunOverReach && d.along < way && Mathf.Abs(d.side) < half)
                .OrderBy(d => d.along).ToList();
            if (waiting.Count == 0) return;
            Debug.Log($"[lata] in its way: {string.Join(", ", waiting.Select(d => $"{d.id} {d.along:0.0} m ahead"))}");
            var was = start;
            float went = 0;
            int thrown = 0;
            try
            {
                while (waiting.Count > 0)
                {
                    await Awaitable.NextFrameAsync(ct);
                    if (Lata == null || !Lata.Shown) return;
                    var now = Lata.transform.position;
                    float speed = Vector3.Distance(now, was) / Mathf.Max(Time.deltaTime, 1e-4f), front = Vector3.Dot(now - start, ahead);
                    was = now;
                    // (Gone, it is put back where it parks: done.)
                    if (front < went - 1) return;
                    went = front;
                    while (waiting.Count > 0 && waiting[0].along - RunOverReach <= front + nose)
                    {
                        // (Off to the side it stood on; one right in the middle, to either side in turn.)
                        float side = Mathf.Abs(waiting[0].side) > 0.05f ? Mathf.Sign(waiting[0].side) : thrown++ % 2 == 0 ? 1 : -1;
                        var push = ahead * Mathf.Max(ThrowLeast, speed * ThrowOn + ThrowOnMore) + aside * side * Mathf.Max(ThrowLeast, speed * ThrowAside + ThrowAsideMore);
                        _ = units.RunOver(waiting[0].id, push);
                        waiting.RemoveAt(0);
                    }
                }
            }
            catch (System.OperationCanceledException) { }
        }

        static async Awaitable Done() => await Awaitable.NextFrameAsync();

        /// <summary>What built the map as it is (for the labs: what is drawn on a tile, MapBuilder.On).</summary>
        public MapBuilder Builder => builder;

        /// <summary>A copy of the car whose nose is on ground tile `anchor`, as drawn, under the map (MapBuilder.CarCopy); null if none.</summary>
        public GameObject CarCopy(int anchor) => builder?.CarCopy(anchor, transform);

        /// <summary>What is drawn on `tile` that a soldier opens, searches or pries, each mesh again with `material` alone, going with it (MapBuilder.Outline); none if nothing.</summary>
        public List<GameObject> Outline(Pos tile, Material material) => builder?.Outline(tile, material) ?? new List<GameObject>();

        /// <summary>How high the top of what is drawn on `tile` stands (m, the world's), or null if nothing (MapBuilder.TopOf).</summary>
        public float? TopOf(Pos tile) => builder?.TopOf(tile);

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
            wearLeft = -1;
            return redone;
        }

        /// <summary>
        /// What boards up the window by `tile` pulled down (the crowbar, BoardsRemovedEvent; Arturo's A11 of 2/11): its
        /// boards and its heap of furniture toppling sideways along the wall over Topples, each tipped on an edge of its
        /// foot, as it goes; a doorway's planks off the wall one after another and down at its foot (Fall). The map's next
        /// Rebuild takes them away.
        /// </summary>
        public void Topple(Pos tile)
        {
            if (builder == null || tile == null || State == null) return;
            // (The way the window looks out, the heap's facing: Decor.)
            var decor = tile.Z == 0 ? State.Decor : State.Levels != null && tile.Z - 1 < State.Levels.Count ? State.Levels[tile.Z - 1].Decor : null;
            int i = tile.Y * State.Width + tile.X;
            var look = decor != null && i < decor.Count ? Bastion.Engine.Decor.DecorOf(decor[i]) : null;
            var d = Bastion.Engine.Grid.Dirs4[look?.Facing ?? 0];
            var outward = new Vector3(d.X, 0, -d.Y);
            float side = MapArt.Hash(tile.X, tile.Y, 90) < 0.5f ? 1 : -1;
            // (Its pieces made again apart: the map's are joined and can't move.)
            var loose = builder.LooseBarricade(tile);
            if (loose == null) return;
            if (look.Kind == DecorKinds.DoorBoards)
            {
                // The top one first, as a crowbar pries them; down onto the ground in front of the door.
                float ground = transform.position.y + tile.Z * MapSpace.Storey;
                var planks = loose.Cast<Transform>().OrderByDescending(p => p.position.y).ToList();
                for (int n = 0; n < planks.Count; n++) StartCoroutine(Fall(planks[n], outward, ground, n * PlankAfter, MapArt.Hash(tile.X, tile.Y, 91 + n)));
                StartCoroutine(Sink(loose, planks.Count * PlankAfter + PlankFalls + 0.35f));
                return;
            }
            foreach (Transform thing in loose) StartCoroutine(Tip(thing, outward, side));
            StartCoroutine(Sink(loose, Topples + 0.35f));
        }

        /// <summary>A pulled-down heap, once over, down through the floor and gone (the tile is open now) `after` seconds on.</summary>
        static System.Collections.IEnumerator Sink(Transform loose, float after)
        {
            yield return new WaitForSeconds(after);
            for (float t = 0; t < Sinks && loose != null; t += Time.deltaTime)
            {
                loose.position += Vector3.down * (SinkBy / Sinks) * Time.deltaTime;
                yield return null;
            }
            if (loose != null) Destroy(loose.gameObject);
        }

        /// <summary>How long a pulled-down heap takes to sink out of sight, and how far (m).</summary>
        const float Sinks = 0.4f, SinkBy = 1.2f;

        /// <summary>Seconds a heap takes to topple, and how far over it goes (degrees).</summary>
        const float Topples = 0.45f, ToppleBy = 80;

        /// <summary>Seconds between a doorway's planks coming off, and the longest one takes to fall.</summary>
        const float PlankAfter = 0.06f, PlankFalls = 0.55f;

        /// <summary>
        /// A doorway's plank pried off (Topple), `after` seconds on: out from the wall and down onto the ground at its foot
        /// (`ground`, m) as anything falls, turning over flat as it goes, a little round on itself (`spin`, 0 to 1).
        /// </summary>
        static System.Collections.IEnumerator Fall(Transform plank, Vector3 outward, float ground, float after, float spin)
        {
            yield return new WaitForSeconds(after);
            if (plank == null) yield break;
            var from = CentreOf(plank);
            // To lie on the ground a little way out, as a plank lies (its middle a couple of centimetres up).
            var to = new Vector3(from.x, ground + 0.03f, from.z) + outward * (0.35f + spin * 0.3f);
            float falls = Mathf.Min(PlankFalls, Mathf.Sqrt(2 * Mathf.Max(0.05f, from.y - to.y) / 9.8f));
            var wall = Vector3.Cross(Vector3.up, outward).normalized;
            float turned = 0, spun = 0, over = 90, round = (spin - 0.5f) * 50;
            for (float t = 0; t < falls && plank != null; t += Time.deltaTime)
            {
                float k = t / falls;
                var at = Vector3.Lerp(from, to, k);
                at.y = from.y - (from.y - to.y) * k * k;
                plank.position += at - CentreOf(plank);
                plank.RotateAround(at, wall, over * k - turned);
                plank.RotateAround(at, Vector3.up, round * k - spun);
                (turned, spun) = (over * k, round * k);
                yield return null;
            }
            if (plank == null) yield break;
            plank.position += to - CentreOf(plank);
            plank.RotateAround(to, wall, over - turned);
            plank.RotateAround(to, Vector3.up, round - spun);
        }

        static Vector3 CentreOf(Transform thing)
        {
            var box = new Bounds(thing.position, Vector3.zero);
            bool first = true;
            foreach (var r in thing.GetComponentsInChildren<Renderer>())
            {
                if (first) box = r.bounds;
                else box.Encapsulate(r.bounds);
                first = false;
            }
            return box.center;
        }

        static System.Collections.IEnumerator Tip(Transform thing, Vector3 axis, float side)
        {
            if (thing == null) yield break;
            var box = new Bounds(thing.position, Vector3.zero);
            foreach (var r in thing.GetComponentsInChildren<Renderer>()) box.Encapsulate(r.bounds);
            // (Round the window's way, over the edge of its foot along the wall.)
            var wall = Vector3.Cross(Vector3.up, axis).normalized;
            float reach = Mathf.Abs(Vector3.Dot(box.extents, new Vector3(Mathf.Abs(wall.x), 0, Mathf.Abs(wall.z))));
            var pivot = new Vector3(box.center.x, box.min.y, box.center.z) + wall * side * reach * 0.9f;
            float done = 0;
            for (float t = 0; t < Topples && thing != null; t += Time.deltaTime)
            {
                float angle = ToppleBy * (t / Topples) * (t / Topples);
                thing.RotateAround(pivot, axis, -(angle - done) * side);
                done = angle;
                yield return null;
            }
            if (thing != null) thing.RotateAround(pivot, axis, -(ToppleBy - done) * side);
        }

        // ------------------------------------------------------------------------------------------- a siege's doors

        /// <summary>A blow on a siege's door or barricade at `tile` (mapView.ts knock, the 'smashed' event): a gate shakes on its hinges.</summary>
        public void Knock(Pos tile)
        {
            if (State != null && tile != null && tile.Z == 0) builder?.Walls?.Knock(tile.Y * State.Width + tile.X);
        }

        int wearLeft = -1;

        /// <summary>For the labs: the doors' leaves and their wear (WallsView.Doors).</summary>
        public string Doors => builder?.Walls?.Doors ?? "-";

        /// <summary>
        /// A siege's doors to another stage of wear (mapView.ts update: worked out again only when a blow lands, what is
        /// left adding up to less), and the ones struck shaken.
        /// </summary>
        void Update()
        {
            var s = Units.UnitViews.Current?.State ?? State;
            if (s?.Mission?.Siege != null && s.Breakables != null && builder?.Walls != null)
            {
                int left = s.Breakables.Values.Sum();
                if (left != wearLeft)
                {
                    wearLeft = left;
                    builder.Walls.Wear(BasePieces.SiegeWear(s));
                }
            }
            builder?.Walls?.Step(Time.deltaTime);
        }

        /// <summary>What is outlined to be searched or opened outlined again if the map made it again since (SearchOutline.KeepUp).</summary>
        void LateUpdate() => Units.SearchOutline.KeepUp();

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
