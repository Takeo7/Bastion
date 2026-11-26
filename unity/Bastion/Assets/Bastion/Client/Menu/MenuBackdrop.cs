using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.Client.Menu
{
    /// <summary>
    /// The 3D behind the menus (Arturo's notes of 14/11, menu-inicial p1 and p2; the mockups he chose set the composition,
    /// the mood and the light, never the art). Road, the main menu's: La Lata as the 4×4, pulled over on a snowy road through
    /// the pinar at nightfall, its headlights on, a watchtower over a walled post far up the road. Crossroads, «Partida
    /// multijugador»'s: a snowy crossroads of the town seen from above, its houses, cars and street lights, quiet at dusk, its
    /// middle plain for UI's two cards. The left of each frame is left dark and empty: UI lays its column there.
    /// All of it our assets (Synty POLYGON, MenuArt), on the game's ground (Bastion/Ground under snow, from a ground map of
    /// its own), the winter's snow cap (SnowCover), a light of its own through Mood, a sky of its own (Sky's shader), a light
    /// snowfall (Weather's flakes). Laid out by hand and the same every time: what is scattered goes by a hash of its place.
    /// Its own root and camera, drawn behind UI's document (transparent); the scene's other cameras off meanwhile, and put
    /// back by Hide. No engine state, no network. Slow life only: the snow falling, the pines in the wind (their shader), a
    /// lamp that flickers. Arte, 0.12.24; UI calls it from its StartScreen.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour, IShotStep
    {
        public enum Scene { Road, Crossroads }

        /// <summary>The backdrop up now, if any.</summary>
        public static MenuBackdrop Current { get; private set; }
        /// <summary>Which of the two it is.</summary>
        public Scene Shown { get; private set; }
        /// <summary>Its camera («Cámara del menú»).</summary>
        public Camera Eye { get; private set; }
        /// <summary>How long it took to build (ms; the log says it too).</summary>
        public float BuiltMs { get; private set; }

        const float Tile = MapSpace.Tile;

        /// <summary>
        /// `s` built in the active scene, the one shown before taken away: its own root («Fondo del menú») and camera, behind
        /// the UI's document.
        /// </summary>
        public static void Show(Scene s)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Tear();
            var root = new GameObject("Fondo del menú");
            var backdrop = root.AddComponent<MenuBackdrop>();
            Current = backdrop;
            backdrop.Shown = s;
            backdrop.Build(s);
            backdrop.BuiltMs = (float)watch.Elapsed.TotalMilliseconds;
            Debug.Log(FormattableString.Invariant($"[menu] {s} built in {backdrop.BuiltMs:0} ms: {root.GetComponentsInChildren<Renderer>().Length} renderers, {root.GetComponentsInChildren<Light>().Length} lights"));
        }

        /// <summary>
        /// The backdrop taken away, and what it changed put back (the scene's cameras, its light); its models let go (MenuArt
        /// held no more: the next scene's load frees what nothing else uses).
        /// </summary>
        public static void Hide()
        {
            Tear();
            MenuArt.Release();
        }

        static void Tear()
        {
            if (Current == null) return;
            var going = Current;
            Current = null;
            going.Restore();
            // (At once: a Show right after it builds the next one in the same frame, with the same light.)
            DestroyNow(going.gameObject);
        }

        void OnDestroy()
        {
            Restore();
            if (Current != this) return;
            // (Gone with its scene, not Hide: its models let go as well.)
            Current = null;
            MenuArt.Release();
        }

        /// <summary>Every model it is built from (MenuArt; MenuLab.Make).</summary>
        public static IEnumerable<string> Models => new[] { LaLata.Bodies[0], Tower, DeadPine, Wire, Pole, PoleLine, Floodlight, Spikes, Barrier, Rock, Log, Bush, StreetLight, StopSign, Fence, GasPump }
            .Concat(Pines).Concat(Wrecks).Concat(Houses).Concat(Farm).Distinct();

        const string Tower = "military:SM_Bld_GuardTower_01", DeadPine = "nature:SM_Tree_Pine_Dead_01", Wire = "farm:SM_Prop_Fence_Wire_01",
            Pole = "apocalypse:SM_Prop_Powerpole_Single_01", PoleLine = "apocalypse:SM_Prop_Powerpole_Single_Line_01",
            Floodlight = "apocalypse:SM_Prop_Floodlights_01", Spikes = "apocalypse:SM_Prop_SpikeWall_01", Barrier = "apocalypse:SM_Prop_Barrier_Long_01",
            Rock = "nature:SM_Rock_01", Log = "nature:SM_Tree_Log_01", Bush = "nature:SM_Plant_Bush_01",
            StreetLight = "apocalypse:SM_Prop_LightPole_Base_01", StopSign = "apocalypse:SM_Prop_Sign_Stop_01",
            Fence = "farm:SM_Prop_Fence_Wood_01", GasPump = "apocalypse:SM_Prop_Gaspump_01";
        static readonly string[] Pines = { "nature:SM_Tree_PolyPine_01", "nature:SM_Tree_PolyPine_02", "nature:SM_Tree_PolyPine_03" };
        static readonly string[] Wrecks = { "apocalypse:SM_Prop_Car_Wrecked_01", "apocalypse:SM_Prop_Car_Wrecked_Rusted_01", "apocalypse:SM_Prop_Car_Wrecked_SUV_01", "town:SM_Veh_Pickup_01" };
        static readonly string[] Houses =
        {
            "town:SM_Bld_House_Preset_01", "town:SM_Bld_House_Preset_05", "town:SM_Bld_House_Preset_09",
            "town:SM_Bld_House_Preset_10", "town:SM_Bld_House_Preset_Garage_01", "apocalypse:SM_Bld_House_01", "apocalypse:SM_Bld_House_03",
        };
        static readonly string[] Farm = { "farm:SM_Bld_Barn_01", "farm:SM_Bld_Silo_01", "farm:SM_Bld_Farmhouse_02", "farm:SM_Prop_Windmill_01", "military:SM_Bld_GasStation_01" };

        Func<string, GameObject> prefabOf;
        Transform props;
        /// <summary>What it made and frees with it: materials, textures, meshes.</summary>
        readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();
        /// <summary>The scene's cameras it turned off, as they were (Mood colours them all).</summary>
        readonly List<(Camera camera, bool enabled, CameraClearFlags flags, Color colour)> others = new List<(Camera, bool, CameraClearFlags, Color)>();
        /// <summary>A light up before it that it borrowed (not its own), and what that light showed.</summary>
        Mood borrowed;
        Mood.Values borrowedValues;
        bool restored;

        /// <summary>
        /// Nightfall in the snow (the mockups' blue hour): the sky's light blue and dim, a low cold light from the west, the
        /// haze a dusky blue that the far pines go into; the grade a little darker and bluer than the winter's day.
        /// </summary>
        /// <summary>
        /// The road's own night, the start menu's (Arturo's cuadros 149 to 151, 24/11: the picture he sent, measured patch
        /// by patch against it): a dull indigo, far from ultramarine (the road's snow #2f4ca7, the sky #1b2c61), its
        /// shadows navy and long across the road from a sun low on the left, the post's lamp warm. The crossroads keeps Dusk.
        /// </summary>
        static readonly Mood.Values RoadDusk = new Mood.Values
        {
            haze = 0x22306a, hazeDensity = 0.017f, sun = 0xa8baf6, sunIntensity = 2.6f, sunFrom = new Vector3(-30, 6.5f, 3),
            skyLight = 0x6a82dc, groundLight = 0x2c3a70, skyIntensity = 1.5f, fill = 0xa4b6f0, fillIntensity = 0.45f, exposure = 1.16f,
            bloomStrength = 0.75f, bloomThreshold = 0.8f, saturation = 0.84f, contrast = 1.06f, vignette = 0.42f,
            shadowTint = new Vector3(0.94f, 0.97f, 1.06f), highlightTint = new Vector3(1.0f, 1.0f, 1.02f), lift = new Vector3(0.022f, 0.032f, 0.07f),
        };
        static readonly Mood.Values Dusk = new Mood.Values
        {
            haze = 0x4b5784, hazeDensity = 0.012f, sun = 0xaab8e6, sunIntensity = 1.2f, sunFrom = new Vector3(-30, 12, 8),
            skyLight = 0x7f8cc2, groundLight = 0x3a4158, skyIntensity = 1.5f, fill = 0xb4c2ea, fillIntensity = 0.4f, exposure = 1.08f,
            bloomStrength = 0.45f, bloomThreshold = 0.88f, saturation = 0.95f, contrast = 1.06f, vignette = 0.3f,
            shadowTint = new Vector3(0.9f, 0.95f, 1.08f), highlightTint = new Vector3(1.0f, 1.0f, 1.02f), lift = new Vector3(0.02f, 0.025f, 0.045f),
        };

        /// <summary>The sky's colours at nightfall (Sky's globals, sRGB): the horizon is the haze; the afterglow low in the west.</summary>
        const int SkyMiddle = 0x4d5884, SkyZenith = 0x161c36, RoadSkyMiddle = 0x394f9a, RoadSkyZenith = 0x182756;
        const int SkyGlow = 0xd9a58a, SkyLand = 0x8490c0;
        static readonly Vector3 GlowFrom = new Vector3(-0.85f, 0.02f, 0.5f);

        /// <summary>Whether it is the road that is shown (its own night and sky: RoadDusk).</summary>
        bool road;

        void Build(Scene s)
        {
            var art = MenuArt.Load();
            if (art == null) Debug.LogWarning("[menu] Resources/Menu/MenuArt.asset missing (Bastion.EditorTools.MenuLab.Make)");
            prefabOf = art != null ? art.PrefabOf() : _ => null;
            // The scene's own cameras off while it is up (UI's draws nothing behind it but a colour).
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.targetTexture == null)
                {
                    others.Add((c, c.enabled, c.clearFlags, c.backgroundColor));
                    c.enabled = false;
                }
            Eye = new GameObject("Cámara del menú").AddComponent<Camera>();
            Eye.transform.SetParent(transform, false);
            Eye.tag = "MainCamera";
            Eye.nearClipPlane = 0.3f;
            Eye.farClipPlane = 600;
            var data = Eye.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            // Its sky is its own (Sky's would bring the map's catalog for its clouds).
            Sky.Without.Add(Eye);
            var had = Mood.Current;
            if (had != null)
            {
                borrowed = had;
                borrowedValues = had.Now;
            }
            road = s == Scene.Road;
            var mood = Mood.Show(road ? RoadDusk : Dusk);
            if (had == null) mood.transform.SetParent(transform, false);
            // The road's sun throws its shadows no further than the 4×4 and the trees round it need (the sun's shadows are
            // 0.8 to 1.1 ms of its frame on «Alto» on the Mac, MenuLab.Coste); the crossroads', as far as the project has them.
            if (s == Scene.Road) Mood.ShadowReach(RoadShadows);
            // No contact shadows of a map left behind on its ground.
            Shader.SetGlobalTexture("_GroundAO", Texture2D.whiteTexture);
            props = new GameObject("Cosas").transform;
            props.SetParent(transform, false);
            if (s == Scene.Road) Road();
            else Crossroads();
            Dome();
            // The snow's cap on all of it but La Lata's (its own: its wheels and lamps left out) and the ground (its own snow).
            SnowCover.Snowy(props, true, r => r.GetComponentInParent<LaLata>() != null || r.name == "Bombilla");
            foreach (var lata in props.GetComponentsInChildren<LaLata>()) lata.Snowy(true);
            var eye = Eye.transform.position;
            SnowCover.Over(transform, new Vector3(eye.x, 0, eye.z), new Vector2(500, 500));
            RenderPipelineManager.beginCameraRendering += BeforeDrawing;
        }

        /// <summary>
        /// The camera at `at`, turned `yaw` degrees from north (clockwise from above), `down` degrees below level, fov `fov`
        /// (upright, on a 16:9 screen).
        /// </summary>
        void Aim(Vector3 at, float yaw, float down, float fov)
        {
            Eye.transform.SetPositionAndRotation(at, Quaternion.Euler(down, yaw, 0));
            Eye.fieldOfView = Fov = fov;
        }

        /// <summary>
        /// Its fov upright on a 16:9 screen (a lab may set it). On a narrower one (a Mac's 16:10, an iPad's 4:3) as wide as
        /// that across, taller instead: the 4×4 and the houses on the sides stay in the picture.
        /// </summary>
        public float Fov;

        void Widen(Camera camera)
        {
            const float Wide = 16f / 9;
            camera.fieldOfView = camera.aspect >= Wide ? Fov : Camera.HorizontalToVerticalFieldOfView(Camera.VerticalToHorizontalFieldOfView(Fov, Wide), camera.aspect);
        }

        // ---- Road ----

        /// <summary>How far from the camera the road's shadows reach (metres).</summary>
        // (75 m since Arturo's menu of 24/11: the fence's and the pines' long shadows across the road, the sun low. It was 30;
        // what the 45 m more cost a frame is not measured yet.)
        const float RoadShadows = 75;

        /// <summary>The road's middle (x, metres) and its tiles (x), north–south through the whole ground map.</summary>
        const float RoadX = 48;
        const int RoadFrom = 30, RoadTo = 33;

        void Road()
        {
            const int width = 64, height = 96;
            // The wilds' ground (biome 1) with its road tarmacked; the centre line between tiles 31 and 32.
            Ground(width, height, 1, 1, data =>
            {
                for (int y = 0; y < height; y++)
                    for (int x = RoadFrom; x <= RoadTo; x++)
                        Paint(data, width, x, y, GroundMap.Street, (byte)(x == 31 || x == 32 ? 2 : 0), (byte)(x == 31 ? 255 : 0));
            });
            var eye = new Vector3(46.6f, 1.45f, -129.6f);
            Aim(eye, 9, -1, 38);
            // La Lata pulled over on the right, across the verge, its nose to the road (west-south-west), lights on.
            var lata = LaLata.Make(props, 0, prefabOf, night: true);
            var lataAt = new Vector3(49.6f, 0, -122.6f);
            lata.Park(Tiles(lataAt), Heading(250), Tiles(new Vector3(RoadX, 0, -120)), true, 0, false);
            lata.Headlights(true);
            // (No pool of light on the snow before it in his picture: its lamps lit, their beams off.)
            // (And a cold light of its own, from above the camera's left, on La Lata alone: in the navy night it was near black
            // against the snow; his picture reads the snow on its bonnet and its roof and the edges of its plates.)
            {
                var from = lataAt + new Vector3(-3.5f, 6f, -5.5f);
                Spot(from, Quaternion.LookRotation(lataAt + Vector3.up - from), 0xc4d2ff, 6, 13, 46);
            }
            // The post up the road: a watchtower on the right behind a concrete wall, its lamp under the eaves (on the
            // road's side) flickering.
            var towerAt = new Vector3(56.5f, 0, -58);
            var tower = Put(Tower, towerAt, 200, 1.4f);
            // By the wall's gate, the post's floodlight still on, its lamps flickering, lighting the snow before it.
            var flood = Put(Floodlight, new Vector3(51.6f, 0, -64.5f), 235);
            if (flood != null)
            {
                var head = BoundsOf(flood.transform);
                Lamp(new Vector3(head.center.x, head.max.y - 0.35f, head.center.z), 0xffb468, 0, 0, true, 0.7f,
                    Spot(new Vector3(head.center.x, head.max.y - 0.2f, head.center.z), Quaternion.Euler(28, 235, 0), 0xffd9a0, 30, 26, 70));
                // (His picture's: a warm point with a halo by the tower.)
                // (Cuadro 171, 26/11: as his picture's, a small bright point, yellower; out of the haze, as the app drew it.)
                Halo(new Vector3(head.center.x, head.max.y - 0.35f, head.center.z), new Color(1, 0.5f, 0.16f, 0.9f), 2.6f, 2f);
                Halo(new Vector3(head.center.x, head.max.y - 0.35f, head.center.z), new Color(1, 0.66f, 0.3f, 1), 1f, 2.4f);
            }
            // (A palisade of Apocalypse's stakes, not Construction's concrete: each pack's atlas loads whole with MenuArt,
            // Construction's 4096² for one wall; MenuLab.Make lists them.)
            for (int k = 0; k < 7; k++) Put(Spikes, new Vector3(53.6f + k * 2.05f, 0, -66 - 0.3f * Hash(k, 1, 13)), 180 + 8 * (Hash(k, 2, 13) - 0.5f));
            Put(Spikes, new Vector3(52.4f, 0, -67.6f), 205);
            Put(Barrier, new Vector3(51.8f, 0, -69), 80);
            // The wire fence along the left verge, and the power line along the right of the road, pole to pole.
            for (float z = -150; z < 40; z += 5) Put(Wire, new Vector3(43.6f, 0, z), 90, 1, Hash((int)z, 3, 7) < 0.12f);
            for (float z = -165; z < 60; z += 15)
            {
                Put(Pole, new Vector3(54.2f, 0, z), 0);
                Put(PoleLine, new Vector3(54.2f, 0, z), 0);
            }
            // Rocks and a log on the verges.
            Put(Rock, new Vector3(56.2f, -0.3f, -123.5f), 40, 5.5f);
            Put(Rock, new Vector3(54.5f, 0, -126), 10, 2.2f);
            Put(Log, new Vector3(57.5f, 0.15f, -113), 70, 1.2f);
            Put(Rock, new Vector3(41, -0.4f, -112), 120, 6.5f);
            Put(Rock, new Vector3(42.2f, 0, -121.5f), 200, 2.6f);
            foreach (var (x, z) in new[] { (57.8f, -108f), (40.2f, -100f), (58.4f, -95f), (39.6f, -84f), (56.9f, -86f) })
                Put(Bush, new Vector3(x, -0.2f, z), Hash((int)x, (int)z, 4) * 360, 1.1f + 0.5f * Hash((int)z, (int)x, 4));
            // The pinar either side, its edge ragged, but the road and its verges, the post's yard and the near view of the 4×4.
            bool Clear(Vector2 p)
            {
                float ragged = 2.5f * Hash(Mathf.FloorToInt(p.y / 5), p.x < RoadX ? 1 : 2, 9);
                if (p.x > RoadX - 7 - ragged && p.x < RoadX + 7.5f + ragged) return false;
                if ((p - new Vector2(towerAt.x + 3, towerAt.z - 2)).magnitude < 13) return false;
                if ((p - new Vector2(lataAt.x, lataAt.z)).magnitude < 7) return false;
                return true;
            }
            Forest(eye, 9, 40, 6, 170, 4.2f, Clear, 1);
            // Young pines along the wood's edge on both verges, up to where the haze takes them: a wall of branches, not
            // the big ones' bare trunks.
            for (float z = -150; z < -50; z += 2.6f)
                foreach (int side in new[] { -1, 1 })
                {
                    int k = Mathf.RoundToInt(z * 10) + side;
                    float edge = side < 0 ? RoadX - 7.6f - 2.5f * Hash(Mathf.FloorToInt(z / 5), 1, 9) : RoadX + 8.1f + 2.5f * Hash(Mathf.FloorToInt(z / 5), 2, 9);
                    var p = new Vector2(edge + side * (0.3f + 2.2f * Hash(k, 5, 21)), z + 1.2f * Hash(k, 6, 21));
                    if (Hash(k, 7, 21) > 0.8f || (p - new Vector2(lataAt.x, lataAt.z)).magnitude < 8 || (p - new Vector2(towerAt.x + 3, towerAt.z - 2)).magnitude < 13) continue;
                    Put(Pines[2], new Vector3(p.x, -0.1f, p.y), Hash(k, 8, 21) * 360, 0.75f + 0.75f * Hash(k, 9, 21));
                }
            // Two big pines close on the right, framing it.
            PutPine(0, new Vector3(60.5f, 0, -124), 35, 1);
            PutPine(1, new Vector3(58.6f, 0, -118), 110, 1);
            // (Fewer flakes in his picture; and, cuadro 171, big and cream: few, thick, warm.)
            Snowfall(eye + Eye.transform.forward * 18 + Vector3.up * 5, new Vector3(44, 12, 36), 0.035f, new Color(1, 0.9f, 0.7f, 0.9f), 1.3f, null, 10, 0.16f, 0.3f, 0.013f);
        }

        // ---- Crossroads ----

        /// <summary>The crossroads' streets: tiles across (x) of the one north–south, down (y) of the one east–west.</summary>
        const int StreetFrom = 30, StreetTo = 34, CrossFrom = 40, CrossTo = 44;

        void Crossroads()
        {
            const int width = 64, height = 80;
            float midX = (StreetFrom + StreetTo + 1) / 2f * Tile, midZ = -(CrossFrom + CrossTo + 1) / 2f * Tile;
            // A town's ground (biome 0): streets with their kerbs, lawns off them; zebra crossings before the crossing.
            Ground(width, height, 0, 0, data =>
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        bool along = x >= StreetFrom && x <= StreetTo, across = y >= CrossFrom && y <= CrossTo;
                        if (along || across)
                        {
                            byte b = 0, a = 0;
                            // The zebra a tile out from the crossing (4: the street along x; 5: along y), a centre line further on.
                            if (along && !across && y == CrossTo + 2) b = 5;
                            else if (across && !along && (x == StreetFrom - 2 || x == StreetTo + 2)) b = 4;
                            else if (along && !across && x == 32 && (y < CrossFrom - 3 || y > CrossTo + 3)) (b, a) = (2, 128);
                            else if (across && !along && y == 42 && (x < StreetFrom - 3 || x > StreetTo + 3)) (b, a) = (1, 128);
                            Paint(data, width, x, y, GroundMap.Street, b, a);
                        }
                        else Paint(data, width, x, y, GroundMap.Open, (byte)(Hash(x / 6, y / 6, 11) < 0.7f ? GroundMap.Lawn : 0), 0);
                    }
            });
            var eye = new Vector3(midX + 2.5f, 12, midZ - 24);
            Aim(eye, -2, 16, 42);
            // The houses round it, their fronts to the streets: the barn and the silo on the near left, the gas station on the
            // right; up the street, houses either side; a farmhouse and the windmill past them.
            Put("farm:SM_Bld_Barn_01", new Vector3(midX - 21, 0, midZ + 13), 90);
            Put("farm:SM_Bld_Silo_01", new Vector3(midX - 33, 0, midZ + 31), 0, 0.8f);
            Put("military:SM_Bld_GasStation_01", new Vector3(midX + 18, 0, midZ + 13), 180);
            Put(GasPump, new Vector3(midX + 13.5f, 0, midZ + 9), 90);
            Put(GasPump, new Vector3(midX + 13.5f, 0, midZ + 13), 90);
            Put("town:SM_Bld_House_Preset_05", new Vector3(midX - 14, 0, midZ + 36), 90);
            Put("apocalypse:SM_Bld_House_03", new Vector3(midX + 14.5f, 0, midZ + 34), -90);
            Put("town:SM_Bld_House_Preset_01", new Vector3(midX - 12.5f, 0, midZ + 55), 90);
            Put("town:SM_Bld_House_Preset_Garage_01", new Vector3(midX + 13, 0, midZ + 52), -90);
            Put("town:SM_Bld_House_Preset_09", new Vector3(midX - 15, 0, midZ + 73), 90);
            Put("farm:SM_Bld_Farmhouse_02", new Vector3(midX + 14, 0, midZ + 72), -90);
            Put("apocalypse:SM_Bld_House_01", new Vector3(midX - 34, 0, midZ + 52), 90);
            Put("town:SM_Bld_House_Preset_10", new Vector3(midX + 34, 0, midZ + 36), -90);
            Put("farm:SM_Prop_Windmill_01", new Vector3(midX + 32, 0, midZ + 60), 210);
            // Cars left where they stopped, and La Lata parked at the near right corner.
            Put(Wrecks[0], new Vector3(midX - 1.8f, 0, midZ + 24), 168);
            Put(Wrecks[1], new Vector3(midX - 13, 0, midZ + 1.2f), 84);
            Put(Wrecks[2], new Vector3(midX + 11, 0, midZ + 10), 20);
            Put(Wrecks[3], new Vector3(midX + 2, 0, midZ + 45), 4);
            var lata = LaLata.Make(props, 0, prefabOf, night: true);
            lata.Park(Tiles(new Vector3(midX + 9.5f, 0, midZ - 6)), Heading(255), Tiles(new Vector3(midX, 0, midZ)), true, 0, false);
            // The street lights at the corners (one of them still lit, and flickering), the stop sign, the power line along
            // the street's right, the yards' fences.
            foreach (var (dx, dz, yaw, lit) in new[] { (-5.2f, -5.2f, 45f, false), (5.4f, 5.4f, 225f, true), (-5.2f, 22f, 90f, false), (5.4f, 40f, 270f, false) })
            {
                var pole = Put(StreetLight, new Vector3(midX + dx, 0, midZ + dz), yaw);
                if (lit && pole != null)
                {
                    var b = BoundsOf(pole.transform);
                    Lamp(new Vector3(b.center.x, b.max.y - 0.35f, b.center.z), 0xffcf8a, 6, 13, true);
                }
            }
            Put(StopSign, new Vector3(midX - 5, 0, midZ - 6.5f), 180);
            for (float z = midZ + 8; z < midZ + 140; z += 15)
            {
                Put(Pole, new Vector3(midX + 6.5f, 0, z), 0);
                Put(PoleLine, new Vector3(midX + 6.5f, 0, z), 0);
            }
            for (int k = 0; k < 7; k++) Put(Fence, new Vector3(midX - 6.2f, 0, midZ + 27 + k * 2.5f), 90, 1, k == 4);
            for (int k = 0; k < 5; k++) Put(Fence, new Vector3(midX - 31 + k * 2.5f, 0, midZ - 6.5f), 0, 1, k == 2);
            for (int k = 0; k < 5; k++) Put(Fence, new Vector3(midX + 7, 0, midZ + 44 + k * 2.5f), 90);
            // Pines round it all, but the streets, the houses' lots and the near view.
            var lots = new List<(Vector2 at, float r)>
            {
                (new Vector2(midX - 21, midZ + 13), 12), (new Vector2(midX - 33, midZ + 31), 5), (new Vector2(midX + 18, midZ + 13), 13), (new Vector2(midX - 14, midZ + 36), 9),
                (new Vector2(midX + 14.5f, midZ + 34), 8), (new Vector2(midX - 12.5f, midZ + 55), 8), (new Vector2(midX + 13, midZ + 52), 7), (new Vector2(midX - 15, midZ + 73), 9),
                (new Vector2(midX + 14, midZ + 72), 8), (new Vector2(midX - 34, midZ + 52), 10), (new Vector2(midX + 34, midZ + 36), 10), (new Vector2(midX + 32, midZ + 60), 5),
                (new Vector2(midX + 9.5f, midZ - 6), 9), (new Vector2(midX - 11, midZ - 6), 6),
            };
            bool Clear(Vector2 p)
            {
                if (Mathf.Abs(p.x - midX) < 7.5f || Mathf.Abs(p.y - midZ) < 7.5f) return false;
                foreach (var (at, r) in lots) if ((p - at).magnitude < r) return false;
                return true;
            }
            Forest(eye, -2, 42, 14, 210, 5.2f, Clear, 2, 0.6f);
            // Pines on the near left corner, framing it.
            PutPine(0, new Vector3(midX - 10.5f, 0, midZ - 6.2f), 20, 1);
            PutPine(2, new Vector3(midX - 13.5f, 0, midZ - 5.6f), 140, 0.5f);
            Snowfall(new Vector3(midX, 9, midZ + 14), new Vector3(70, 18, 60), 0.12f, new Color(1, 1, 1, 0.85f), 1.3f);
        }

        // ---- Parts ----

        /// <summary>
        /// The ground: one plane far past the scene, painted by Bastion/Ground from a map of `width` × `height` tiles that
        /// `paint` fills (GroundMap's texel a tile: its kind, its level, its line or lawn, the line's place), in `biome` (0
        /// a town's, 1 the wilds'), tarmac on its roads or not, under snow. Past the map, its edge tiles go on.
        /// </summary>
        void Ground(int width, int height, float biome, float tarmac, Action<byte[]> paint)
        {
            var data = new byte[width * height * 4];
            paint(data);
            var map = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { name = "Suelo del menú", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            map.SetPixelData(data, 0);
            map.Apply(false);
            made.Add(map);
            var source = Resources.Load<Material>("Ground");
            if (source == null)
            {
                Debug.LogError("[menu] Resources/Ground.mat missing (Bastion.EditorTools.GroundMake.Run)");
                return;
            }
            var material = new Material(source) { name = "Suelo del menú" };
            made.Add(material);
            material.SetTexture("_GroundMap", map);
            material.SetVector("_GroundSize", new Vector4(width, height, 0, 0));
            material.SetFloat("_Biome", biome);
            material.SetFloat("_Tarmac", tarmac);
            material.SetFloat("_Snow", 1);
            material.SetVector("_Water", new Vector4(-1, MapOutline.FarBank, 0, 0));
            const float past = 500;
            float x0 = -past, x1 = width * Tile + past, z0 = past, z1 = -height * Tile - past;
            var mesh = new Mesh { name = "Suelo del menú" };
            mesh.vertices = new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            made.Add(mesh);
            var plane = new GameObject("Suelo");
            plane.transform.SetParent(transform, false);
            plane.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = plane.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        static void Paint(byte[] data, int width, int x, int y, byte kind, byte b = 0, byte a = 0)
        {
            int i = (y * width + x) * 4;
            data[i] = kind;
            data[i + 1] = 0;
            data[i + 2] = b;
            data[i + 3] = a;
        }

        /// <summary>A model by its key at `at` (metres), turned `yaw` degrees (clockwise from above), scaled; leaning over if `fallen`.</summary>
        GameObject Put(string key, Vector3 at, float yaw, float scale = 1, bool fallen = false)
        {
            var prefab = prefabOf(key);
            if (prefab == null)
            {
                Debug.LogWarning($"[menu] no model {key}");
                return null;
            }
            var o = Instantiate(prefab, props, false);
            o.transform.localPosition = at;
            o.transform.localRotation = Quaternion.Euler(fallen ? 14 : 0, yaw, fallen ? -9 : 0);
            o.transform.localScale = Vector3.one * scale;
            return o;
        }

        /// <summary>
        /// Pines over the cells of a grid `cell` metres a side (one each, somewhere in it, by its hash), those the camera at
        /// `eye` looking `yaw` sees (within `half` degrees of it, `near` to `far` metres) and `clear` lets be; thinner further
        /// off (the haze takes them). `keep` scales how many.
        /// </summary>
        void Forest(Vector3 eye, float yaw, float half, float near, float far, float cell, Func<Vector2, bool> clear, int salt, float keep = 1)
        {
            int x0 = Mathf.FloorToInt((eye.x - far) / cell), x1 = Mathf.CeilToInt((eye.x + far) / cell);
            int z0 = Mathf.FloorToInt((eye.z - far) / cell), z1 = Mathf.CeilToInt((eye.z + far) / cell);
            int count = 0, young = 0;
            for (int gx = x0; gx <= x1; gx++)
                for (int gz = z0; gz <= z1; gz++)
                {
                    var p = new Vector2((gx + Hash(gx, gz, salt)) * cell, (gz + Hash(gx, gz, salt + 1)) * cell);
                    var d = p - new Vector2(eye.x, eye.z);
                    float distance = d.magnitude;
                    if (distance < near || distance > far) continue;
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg)) > half) continue;
                    // (Far off, fewer and bigger: the haze shows their line, not each one; MenuLab.Coste, the pines are most of its cost.)
                    float odds = (distance < 60 ? 0.78f : distance < 110 ? 0.36f : 0.18f) * keep;
                    if (Hash(gx, gz, salt + 2) > odds || !clear(p)) continue;
                    float kind = Hash(gx, gz, salt + 3);
                    float size = Hash(gx, gz, salt + 4);
                    float turn = Hash(gx, gz, salt + 5) * 360;
                    var at = new Vector3(p.x, 0, p.y);
                    // Now and then a dead one; else the three pines, the big ones the most.
                    if (kind < 0.04f) Put(DeadPine, at - Vector3.up * 0.3f, turn, 1.3f + size * 0.6f);
                    else PutPine(kind < 0.45f ? 0 : kind < 0.85f ? 1 : 2, at, turn, size, distance < 60 ? 1 : 1.25f);
                    count++;
                    // Young ones round the near trees' feet, so that the wood doesn't stand on bare trunks.
                    if (distance < 70 && Hash(gx, gz, salt + 6) < 0.6f)
                    {
                        float a = Hash(gx, gz, salt + 7) * Mathf.PI * 2;
                        var by = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (1.4f + 1.2f * Hash(gx, gz, salt + 8)) + p;
                        if (clear(by))
                        {
                            Put(Pines[2], new Vector3(by.x, -0.1f, by.y), turn + 137, 0.7f + 0.7f * Hash(gx, gz, salt + 9));
                            young++;
                        }
                    }
                }
            Debug.Log($"[menu] {count} pines, {young} young");
        }

        /// <summary>
        /// Pine `kind` (0 the tallest of Nature's three) at `at`, sized between its least and most by `size` (0–1), sunk so
        /// that less of its bare trunk shows (the bigger, the deeper).
        /// </summary>
        void PutPine(int kind, Vector3 at, float turn, float size, float more = 1)
        {
            var (least, most) = kind == 0 ? (1.25f, 1.75f) : kind == 1 ? (1.7f, 2.4f) : (2.0f, 2.8f);
            float scale = Mathf.Lerp(least, most, Mathf.Clamp01(size)) * more;
            Put(Pines[kind], at - Vector3.up * (0.6f * scale), turn, scale);
        }

        /// <summary>A lamp's bulb at `at` (glowing, for the bloom) and its light, `range` metres; flickering now and then if `flicker`.</summary>
        void Lamp(Vector3 at, int colour, float intensity, float range, bool flicker, float size = 0.28f, Light light = null)
        {
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bombilla";
            DestroyNow(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(props, false);
            bulb.transform.position = at;
            bulb.transform.localScale = Vector3.one * size;
            var glow = KitMaterials.New(KitMaterials.Glowing, "Bombilla");
            var r = bulb.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (glow != null)
            {
                made.Add(glow);
                glow.SetColor("_BaseColor", Hex(colour));
                r.sharedMaterial = glow;
            }
            var l = light;
            if (l == null)
            {
                l = new GameObject("Luz de la lámpara").AddComponent<Light>();
                l.transform.SetParent(props, false);
                l.transform.position = at - Vector3.up * 0.3f;
                l.type = LightType.Point;
                l.color = Hex(colour);
                l.range = range;
                l.shadows = LightShadows.None;
            }
            lamps.Add((l, glow, light != null ? light.intensity : intensity, Hex(colour), flicker));
            Glow(lamps.Count - 1, 1);
        }

        /// <summary>A spot light at `at` turned `turn`, `intensity`, `range` metres, its cone `angle` degrees, no shadows.</summary>
        Light Spot(Vector3 at, Quaternion turn, int colour, float intensity, float range, float angle)
        {
            var l = new GameObject("Foco").AddComponent<Light>();
            l.transform.SetParent(props, false);
            l.transform.SetPositionAndRotation(at, turn);
            l.type = LightType.Spot;
            l.color = Hex(colour);
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.4f;
            l.shadows = LightShadows.None;
            return l;
        }

        readonly List<(Light light, Material glow, float intensity, Color colour, bool flicker)> lamps = new List<(Light, Material, float, Color, bool)>();

        void Glow(int k, float level)
        {
            var (light, glow, intensity, colour, _) = lamps[k];
            light.intensity = intensity * level;
            if (glow != null) glow.SetColor("_EmissionColor", colour.linear * (3.5f * level));
        }

        /// <summary>
        /// Snow falling over a box `size` (metres) round `middle`, turned `turn`: `rate` flakes a square metre a second, each
        /// living `life` seconds, as Weather's snowfall falls (ClimaGota, its soft dot), in `colour` times `gain`; the air
        /// filled from the start.
        /// </summary>
        /// <summary>A lamp's halo: the flakes' soft dot, once, `across` metres, in `colour`, standing where the lamp is.</summary>
        void Halo(Vector3 at, Color colour, float across, float gain = 1.6f)
        {
            var material = KitMaterials.New(KitMaterials.ClimaGota, "Halo del farol");
            if (material == null) return;
            made.Add(material);
            material.mainTexture = Dot();
            material.SetFloat("_Gain", gain);
            material.SetFloat("_Haze", 0);
            var ps = new GameObject("Halo").AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(transform, false);
            ps.transform.position = at;
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 100000;
            main.startSpeed = 0;
            main.startSize = across;
            main.startColor = colour;
            main.maxParticles = 1;
            var emission = ps.emission;
            emission.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Emit(1);
        }

        void Snowfall(Vector3 middle, Vector3 size, float rate, Color colour, float gain, Quaternion? turn = null, float life = 10, float least = 0.06f, float most = 0.12f, float onScreen = 0.012f)
        {
            var material = KitMaterials.New(KitMaterials.ClimaGota, "Copos del menú");
            if (material == null) return;
            made.Add(material);
            material.mainTexture = Dot();
            material.SetFloat("_Gain", gain);
            material.SetFloat("_Haze", 0);
            var ps = new GameObject("Copos").AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(transform, false);
            ps.transform.SetPositionAndRotation(middle, turn ?? Quaternion.identity);
            var main = ps.main;
            main.duration = 600;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(least, most);
            main.startColor = colour;
            main.gravityModifier = 0;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = Mathf.CeilToInt(rate * size.x * size.z * life * 1.2f) + 10;
            // The same flakes every time it is shown.
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 1124;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var emission = ps.emission;
            emission.rateOverTime = rate * size.x * size.z;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0.4f;
            velocity.y = -1.0f;
            velocity.z = 0.25f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = onScreen;
            ps.Simulate(life + 0.5f, true, true, false);
            if (Application.isPlaying) ps.Play();
        }

        static Texture2D dot;

        /// <summary>A soft round spot, white, its alpha falling off to the edge (Weather's flakes').</summary>
        static Texture2D Dot()
        {
            if (dot != null) return dot;
            const int size = 64;
            dot = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Copo", hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d)));
                }
            dot.SetPixels32(pixels);
            dot.Apply(true);
            return dot;
        }

        /// <summary>
        /// Its sky (Sky's parts, with Sky's shader): the dome from the haze at the horizon to the zenith, the afterglow low in
        /// the west, and the mountains round it under snow; centred on the camera, which never moves.
        /// </summary>
        void Dome()
        {
            var shader = Resources.Load<Shader>("Sky");
            if (shader == null) return;
            float far = Eye.farClipPlane;
            Material Of(string keyword, Texture texture)
            {
                var m = new Material(shader) { name = "Cielo del menú " + keyword.Trim('_').ToLowerInvariant() };
                m.EnableKeyword(keyword);
                if (texture != null) m.SetTexture("_MainTex", texture);
                m.SetFloat("_Cull", 0);
                made.Add(m);
                return m;
            }
            void Part(string name, Mesh mesh, Material m, float at, float turn)
            {
                made.Add(mesh);
                var o = new GameObject(name);
                o.transform.SetParent(transform, false);
                o.transform.SetPositionAndRotation(Eye.transform.position, Quaternion.Euler(0, turn, 0));
                o.transform.localScale = Vector3.one * (far * at);
                o.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = o.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Part("Cielo", Sphere(32, 16), Of("_DOME", null), 0.9f, 0);
            var mountains = Resources.Load<Texture2D>("Sky/mountains");
            if (mountains == null) return;
            mountains.wrapModeU = TextureWrapMode.Repeat;
            var band = Of("_BAND", mountains);
            band.SetVector("_Rows", new Vector4(0, 1, 0, 0));
            band.SetFloat("_Repeat", 3);
            // (After the dome: neither writes the depth, and drawn before it the dome covered them.)
            band.renderQueue = shader.renderQueue + 1;
            band.SetFloat("_Air", 0.36f);
            band.SetFloat("_Foot", 0.4f);
            Part("Montañas", Ring(-0.035f, 0.22f), band, 0.8f, 40);
        }

        /// <summary>Before its camera draws: the sky's colours at nightfall (Sky's globals, which Sky sets again with the next light).</summary>
        void BeforeDrawing(ScriptableRenderContext context, Camera camera)
        {
            if (this == null || camera != Eye) return;
            Widen(camera);
            var haze = Linear((road ? RoadDusk : Dusk).haze);
            Shader.SetGlobalVector("_SkyHorizon", haze);
            Shader.SetGlobalVector("_SkyMiddle", Linear(road ? RoadSkyMiddle : SkyMiddle));
            Shader.SetGlobalVector("_SkyZenith", Linear(road ? RoadSkyZenith : SkyZenith));
            Shader.SetGlobalVector("_SkySunDir", GlowFrom.normalized);
            Shader.SetGlobalVector("_SkySun", Linear(SkyGlow) * 0.35f);
            Shader.SetGlobalFloat("_SkyGlow", 1);
            Shader.SetGlobalVector("_SkyLand", Linear(SkyLand));
            Shader.SetGlobalFloat("_SkySnow", 0.75f);
            // Its flakes fade within so many metres of the camera (ClimaGota), none in the way of the picture.
            Shader.SetGlobalVector("_ClimaNear", new Vector4(1.5f, 5, 0, 0));
            Shader.SetGlobalVector("_ClimaSize", Vector4.zero);
        }

        float clock;

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        public void ShotStep(float dt) => Step(dt);

        /// <summary>The lamps that flicker: a slow waver, and now and then a dip (by the clock: the same every time).</summary>
        void Step(float dt)
        {
            clock += dt;
            for (int k = 0; k < lamps.Count; k++)
            {
                if (!lamps[k].flicker) continue;
                float waver = 0.88f + 0.12f * Mathf.PerlinNoise(clock * 2.3f, k * 3.1f);
                float dip = Mathf.PerlinNoise(clock * 0.9f + 7, k * 1.7f) > 0.74f ? 0.25f + 0.5f * Mathf.PerlinNoise(clock * 18, k) : 1;
                Glow(k, waver * dip);
            }
        }

        void Restore()
        {
            if (restored) return;
            restored = true;
            RenderPipelineManager.beginCameraRendering -= BeforeDrawing;
            if (Eye != null) Sky.Without.Remove(Eye);
            foreach (var (camera, enabled, flags, colour) in others)
            {
                if (camera == null) continue;
                camera.enabled = enabled;
                camera.clearFlags = flags;
                camera.backgroundColor = colour;
            }
            others.Clear();
            Mood.ShadowReach(null);
            if (borrowed != null && borrowedValues != null) Mood.Show(borrowedValues);
            foreach (var o in made) if (o != null) DestroyNow(o);
            made.Clear();
        }

        /// <summary>A place in metres as 0.10.1's tiles (La Lata's: x east, z south).</summary>
        static Vector3 Tiles(Vector3 at) => new Vector3(at.x / Tile, 0, -at.z / Tile);

        /// <summary>A yaw (degrees clockwise from north) as La Lata's heading (radians from 0.10.1's +z, south, towards east).</summary>
        static float Heading(float yaw) => (180 - yaw) * Mathf.Deg2Rad;

        /// <summary>A place's hash, 0 to 1 (what is scattered goes by it: the same every time).</summary>
        static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777216f;
            }
        }

        static Bounds BoundsOf(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(root.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        static Vector4 Linear(int rgb)
        {
            var c = Hex(rgb).linear;
            return new Vector4(c.r, c.g, c.b, 1);
        }

        /// <summary>A sphere of radius 1 (seen from inside), its vertices its directions (Sky's dome).</summary>
        static Mesh Sphere(int around, int up)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int j = 0; j <= up; j++)
            {
                var v = Mathf.PI * j / up;
                for (int i = 0; i <= around; i++)
                {
                    var u = 2 * Mathf.PI * i / around;
                    vertices.Add(new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)));
                }
            }
            for (int j = 0; j < up; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = j * (around + 1) + i, b = a + around + 1;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            var mesh = new Mesh { name = "Cielo del menú" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>An open ring round the origin, radius 1, from `bottom` to `top` (shares of the radius), u round it and v up it (Sky's).</summary>
        static Mesh Ring(float bottom, float top)
        {
            const int around = 128;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                var u = 2 * Mathf.PI * i / around;
                var d = new Vector3(Mathf.Sin(u), 0, Mathf.Cos(u));
                vertices.Add(d + Vector3.up * bottom);
                vertices.Add(d + Vector3.up * top);
                uvs.Add(new Vector2((float)i / around, 0));
                uvs.Add(new Vector2((float)i / around, 1));
                normals.Add(d);
                normals.Add(d);
            }
            for (int i = 0; i < around; i++)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }
            var mesh = new Mesh { name = "Montañas del menú" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying && !(o is GameObject)) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
