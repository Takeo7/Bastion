using System;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The settlement's clearing (0.10.1's strategy/clearingArt.ts ClearingArtView): the base drawn as the map its siege
    /// would be fought on, with nobody on it (ClearingState), in the battle's daylight or the winter's (Mood.SnowDay) with
    /// the snow on it; the sun's shadows over the whole clearing; and La Lata parked by the caravan (Park), going down its
    /// track and coming back (Depart, Arrive), as the campaign's vehicle (SetVehicle).
    /// </summary>
    public sealed partial class Settlement : MonoBehaviour
    {
        public static Settlement Current { get; private set; }

        public MapView Map { get; private set; }
        public LaLata Lata { get; private set; }
        public BaseState Base { get; private set; }
        public bool Snow { get; private set; }
        /// <summary>How far from the camera the sun's shadows reach while the clearing is shown (metres: it is seen from 51 m).</summary>
        public float ShadowMetres = 90;

        Func<string, GameObject> prefabOf;
        Func<string, Material> materialOf;
        /// <summary>How far it goes to be out of the clearing (tiles), and whether its way out is clear (else it fades).</summary>
        float busWay = 14;
        bool busRoute = true;
        /// <summary>Gone on a mission: hidden where it parks until it comes back.</summary>
        bool away;
        int vehicle = 3;
        int? nextVehicle;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Mood.ShadowReach(null);
            // No fade left over the next scene's 3D.
            Shader.SetGlobalFloat("_StageFade", 0);
        }

        /// <summary>The clearing as a battlefield with nobody on it: the map its siege would be fought on (clearingArt.ts clearingState).</summary>
        public static GameState ClearingState(BaseState @base, bool snow)
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.OfBase(@base), Slots = new System.Collections.Generic.List<int> { 0 }, Rng = new Rng(1), Difficulty = 1, Snow = snow ? true : (bool?)null });
            s.Units.Clear();
            s.Traps = null;
            if (s.Base != null) s.Base.Felled = @base.Felled ?? new System.Collections.Generic.List<int>();
            return s;
        }

        /// <summary>
        /// The clearing of `base` (made again whole if it was shown), in winter or not; models from `prefabOf` and
        /// `materialOf` (the game's catalog if not given: MapCatalog).
        /// </summary>
        public void Show(BaseState @base, bool snow = false, Func<string, GameObject> prefabOf = null, Func<string, Material> materialOf = null)
        {
            Current = this;
            // The same clearing as shown: nothing to make again (UI shows it on every campaign state).
            string key = @base == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { @base, snow });
            if (key != null && key == shownKey && Map != null && prefabOf == null && materialOf == null) return;
            shownKey = key;
            this.prefabOf = prefabOf ?? this.prefabOf ?? MapCatalog.Load()?.PrefabOf() ?? (_ => null);
            this.materialOf = materialOf ?? this.materialOf ?? MapCatalog.Load()?.MaterialOf();
            Base = @base;
            pieceGrid = null;
            Snow = snow;
            var state = ClearingState(@base, snow);
            Mood.Show(snow ? Mood.SnowDay : Mood.Day);
            if (Map == null)
            {
                Map = new GameObject("Mapa").AddComponent<MapView>();
                // La Lata is the settlement's own here (Park), not the evacuation's.
                Map.WithLata = false;
                Map.transform.SetParent(transform, false);
            }
            built = Map.Build(state, this.prefabOf, this.materialOf);
            // The fire pits lit (clearingArt.ts camp: its fires).
            SetFlames(built.PiecesPlaced.Where(p => p.id == "bFirePit").Select(p => (Vector3)p.matrix.GetColumn(3)).ToList());
            pines = null;
            cleared.Clear();
            Mood.ShadowReach(ShadowMetres);
            if (Lata == null || Lata.Vehicle != vehicle) MakeLata();
            Park();
            CampPlaces();
            Reframe();
        }

        string shownKey;

        /// <summary>The campaign's clearing (screen.ts on each state): its base, under snow while it is still winter (Campaign.Wintry), its vehicle.</summary>
        public void Show(CampaignState campaign)
        {
            if (campaign?.Bastion == null) return;
            // Their clothes, by the order they joined, unless they chose (wardrobe.ts setRoster).
            Dress.Roster(campaign.Soldiers);
            vehicle = Campaign.VehicleOf(campaign);
            Show(campaign.Bastion, Campaign.Wintry(campaign));
        }

        void MakeLata()
        {
            if (Lata != null) DestroyNow(Lata.gameObject);
            Lata = LaLata.Make(transform, vehicle, prefabOf);
        }

        /// <summary>La Lata as the campaign has it (vehicleOf: 0 4×4, 1 van, 2 lorry, 3 the bus); after its going or coming if it is on its way.</summary>
        public void SetVehicle(int v)
        {
            if (Lata != null && Lata.Moving)
            {
                nextVehicle = v;
                return;
            }
            if (v == vehicle && Lata != null) return;
            vehicle = v;
            if (Base == null) return;
            MakeLata();
            Park();
            CampPlaces();
            Reframe();
        }

        void Update()
        {
            StepShowcase(Time.deltaTime);
            StepLineup(Time.deltaTime);
            StepFlames(Time.deltaTime);
            StepBuild(Time.time);
            if (nextVehicle != null && Lata != null && !Lata.Moving)
            {
                int v = nextVehicle.Value;
                nextVehicle = null;
                SetVehicle(v);
            }
        }

        /// <summary>La Lata away down its track, out of the clearing.</summary>
        public void Depart()
        {
            if (Lata == null) return;
            Lata.Depart(busWay, !busRoute);
            away = true;
        }

        /// <summary>La Lata backed in along its track to stand by the caravan again (faded in where it stands, its track built over).</summary>
        public void Arrive()
        {
            if (Lata == null) return;
            away = false;
            Lata.Arrive(busWay, !busRoute);
        }

        /// <summary>
        /// La Lata by the caravan (clearingArt.ts parkBus), nose out along a track, where nothing is built and no pine stands
        /// under it: the nearest such spot up each track from the Bastion's lot (never on it), a lane to either side if the
        /// middle is taken; its doors to the lot. Best where its way out along the track to the clearing's edge is clear too
        /// (it leaves and comes back by it: busRoute); if no spot has one, it fades out and in where it stands. Nowhere
        /// clear, it is away (hidden). In 0.10.1's tiles (x east, z south).
        /// </summary>
        void Park()
        {
            if (Lata == null || Base == null) return;
            var b = Base;
            var lot = Bastion.Engine.Base.BastionLot(b);
            float cw = lot != null ? lot.W : 0, ch = lot != null ? lot.H : 0;
            var mid = lot != null ? new Vector2(lot.X + lot.W / 2f, lot.Y + lot.H / 2f) : new Vector2(b.Width / 2f, b.Height / 2f);
            // As 0.10.1 parked it: the bus it starts with by the size it has until its model is built (evacBus.ts: 7 by
            // 1.7), the spot then kept; the others (setVehicle) parked again once built, by their own. With its own, the
            // bus stood further up the track (its 2.27 across caught the memorial beside it), the squad on its other side.
            float length = Lata.Vehicle == 3 ? 7 : Lata.Length, halfWidth = (Lata.Vehicle == 3 ? 1.7f : Lata.Width) / 2;
            bool Blocked(int x, int y) => PieceOn(x, y) != null || Bastion.Engine.Base.TreeAt(b, x, y) || Bastion.Engine.Base.OnBastionLot(b, x, y);
            // Nothing under it from `from` to `to` tiles along `dir` from `at`, its width across; `out`: past the clearing's
            // edge the way goes on (its way out), else it is no place to stand.
            bool Clear(Vector3 at, Vector2 dir, float from, float to, bool @out = false)
            {
                for (float k = from; k <= to + 1e-6f; k += 0.5f)
                    foreach (float side in new[] { -halfWidth, 0, halfWidth })
                    {
                        int x = Mathf.FloorToInt(at.x + dir.x * k + dir.y * side);
                        int y = Mathf.FloorToInt(at.z + dir.y * k + dir.x * side);
                        if (x < 0 || y < 0 || x >= b.Width || y >= b.Height) return @out;
                        if (Blocked(x, y)) return false;
                    }
                return true;
            }
            bool found = false;
            Vector3 bestAt = default;
            Vector2 bestDir = default;
            float bestWay = 0, bestCost = 0;
            bool bestRoute = false;
            foreach (var dir in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
            {
                float half = (dir.x != 0 ? cw : ch) / 2;
                for (float reach = half + 0.5f; reach <= half + 9 + 1e-6f; reach += 0.5f)
                    foreach (float lane in new[] { 0, -1.5f, 1.5f })
                    {
                        float along = reach + length / 2;
                        var at = new Vector3(b.Width / 2f + dir.x * along + dir.y * lane, 0, b.Height / 2f + dir.y * along + dir.x * lane);
                        // Nearest the caravan, the middle of the track first, and above all with its way out clear.
                        float cost = reach + Mathf.Abs(lane) * 1.5f;
                        if ((found && cost >= bestCost) || !Clear(at, dir, -length / 2, length / 2)) continue;
                        float way = (dir.x != 0 ? b.Width : b.Height) / 2f - along + length + 2;
                        bool route = Clear(at, dir, length / 2, way + length / 2, true);
                        if (!route) cost += 40;
                        if (found && cost >= bestCost) continue;
                        found = true;
                        bestAt = at;
                        bestDir = dir;
                        bestWay = way;
                        bestRoute = route;
                        bestCost = cost;
                    }
            }
            busParked = found;
            if (!found)
            {
                Lata.Park(new Vector3(b.Width / 2f, 0, b.Height / 2f), 0, new Vector3(mid.x, 0, mid.y), false);
                return;
            }
            busWay = bestWay;
            busRoute = bestRoute;
            busAt = new Vector2(bestAt.x, bestAt.z);
            busDir = bestDir;
            busHalf = length / 2;
            busHalfWidth = halfWidth;
            // Its doors shut, as it stands waiting (who gets in opens them).
            Lata.Park(bestAt, Mathf.Atan2(bestDir.x, bestDir.y), new Vector3(mid.x, 0, mid.y), !away, 0, false);
            Debug.Log(FormattableString.Invariant($"[settlement] La Lata ({Lata.Vehicle}) parked at {bestAt.x:0.##},{bestAt.z:0.##} heading {Mathf.Atan2(bestDir.x, bestDir.y):0.###}, its way out {busWay:0.#} tiles{(busRoute ? "" : ", built over")}"));
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
