using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The fog of war of the map in view (0.10.1's fogOfWar.ts), in three zones: in sight (where enemies show:
    /// TeamVisibility), as it is; seen (as far as the squad's line of sight goes, TeamView, and wherever it has seen this
    /// mission), under a haze and a low mist; never seen, a dark mist. On the storey in view (MapView.Level: each tile as
    /// its highest floor up to it, Los.FloorLevels). A building's walls show as the street they look onto, its roof as the
    /// street along it at its best, its inside room by room: a room any of which has been seen is seen whole, with the
    /// walls round it (Ana, 22/10 #36), but only once a soldier has gone into the building; till then its inside stays
    /// unknown, whatever is seen of it through a window or a door (Arturo, 28/10, Ana's p1; 0.10.1 showed the room seen
    /// through it). Each tile's zone in a small texture, faded as it changes, which a full-screen pass
    /// (Bastion/Fog) reads at every pixel's place. UI's event player shows it the state seen (Show) after each event;
    /// Battlefield starts it with each map. On «Niebla» in Partida.
    /// </summary>
    public sealed class Fog : MonoBehaviour, IShotStep
    {
        public static Fog Current { get; private set; }

        /// <summary>Seconds a tile takes to come into sight or view or leave them (SIGHT_FADE).</summary>
        const float SightFade = 0.35f;
        /// <summary>fogOfWar.ts UNKNOWN, HAZE, MIST: the dark mist, the haze over what is seen, the low mist's tint.</summary>
        static readonly Color Unknown = Hex(0x161e28), Haze = Hex(0xa9b8c8), MistTint = Hex(0x2e3640);
        /// <summary>By night (fogOfWar.ts setNight): the mist darker, the haze teal; lit tiles glow warm (LAMP, linear).</summary>
        static readonly Color UnknownNight = Hex(0x070d11), HazeNight = Hex(0x10202a), MistNight = Hex(0x070d12);
        static readonly Color Lamp = new Color(1.0f, 0.55f, 0.22f) * 0.6f;
        /// <summary>How thick the low mist is at most (MIST_LEVELS: 0, 0.35, 0.6; Arturo chose it dense).</summary>
        public float mist = 0.6f;
        /// <summary>What parts a building's rooms (BETWEEN_ROOMS).</summary>
        static readonly HashSet<string> BetweenRooms = new HashSet<string> { "wall", "door", "doorOpen", "window", "windowBroken", "roof" };

        static readonly int FogMap = Shader.PropertyToID("_FogMap"), FogShell = Shader.PropertyToID("_FogShell"), FogSize = Shader.PropertyToID("_FogSize"),
            FogUnknown = Shader.PropertyToID("_FogUnknown"), FogHaze = Shader.PropertyToID("_FogHaze"), FogMistTint = Shader.PropertyToID("_FogMistTint"),
            FogMist = Shader.PropertyToID("_FogMist"), FogTime = Shader.PropertyToID("_FogTime");

        sealed class Shell
        {
            public readonly List<int> tiles = new List<int>();
            /// <summary>The street along it (the tiles outside it beside its own).</summary>
            public int[] along;
        }

        Texture2D map, shellMap;
        Color32[] pixels, shellPixels;
        /// <summary>
        /// The buildings as one goes in (each tile of one, its walls and windows aside, joined to the next through its
        /// doors: two houses wall to wall are two), by the ground's tiles; whether a soldier has been in each this mission;
        /// each tile of a building nobody has been in yet (its walls and windows too), kept unknown.
        /// </summary>
        int[] buildingOf;
        bool[] entered, closed;
        /// <summary>Where an enemy in sight stands (its node): in a closed building, its tile alone shown, so that it stands on its floor.</summary>
        readonly HashSet<int> spotted = new HashSet<int>();
        float[] sight, known, sightTo, knownTo, roofSight, roofKnown, roofSightTo, roofKnownTo, lit, litTo;
        bool[] explored, shut;
        bool[] visible, view, lights;
        readonly List<Shell> shells = new List<Shell>();
        GameState last;
        int width, height;
        float time;
        bool changed;

        void Awake() => Current = this;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            Shader.SetGlobalVector(FogSize, Vector4.zero);
            if (Current == this) Current = null;
        }

        /// <summary>A new map: nothing seen yet; its buildings (GameState.storeys) and its outline (shut for good).</summary>
        public void Reset(GameState s)
        {
            last = s;
            width = s.Width;
            height = s.Height;
            int n = width * height;
            sight = new float[n];
            known = new float[n];
            sightTo = new float[n];
            knownTo = new float[n];
            lit = new float[n];
            litTo = new float[n];
            roofSight = new float[n];
            roofKnown = new float[n];
            roofSightTo = new float[n];
            roofKnownTo = new float[n];
            explored = null;
            visible = view = null;
            // The map's outline (the frame round what is played) under the fog for good (setShut).
            shut = new bool[n];
            if (s.Frame != null && s.Frame.Count > 0 && s.Ground != null)
                for (int i = 0; i < n && i < s.Ground.Count; i++) shut[i] = s.Ground[i] == 4;
            pixels = new Color32[n];
            shellPixels = new Color32[n];
            Remake(ref map, "Niebla");
            // (Tile by tile, no blend, as fogOfWar.ts's: which tile a wall's face stands in and which it looks onto. Blended, a
            // façade's next tile read half its building's, and its face took the inside's fog: tall blocks went dark whole.)
            Remake(ref shellMap, "Niebla de los edificios", FilterMode.Point);
            Shells(s);
            Buildings(s);
            changed = true;
            Shader.SetGlobalTexture(FogMap, map);
            Shader.SetGlobalTexture(FogShell, shellMap);
            Shader.SetGlobalVector(FogSize, new Vector4(width, height, MapSpace.Tile, 1));
            // In the pass's linear colour: a global colour is passed as it is (a material's would be turned to linear).
            Tints();
            Shader.SetGlobalFloat(FogMist, mist);
        }

        static bool Night => Mood.Current != null && Mood.Current.Now == Mood.Night;

        /// <summary>The fog's colours by day or by night (in the pass's linear colour: a global colour is passed as it is).</summary>
        static void Tints()
        {
            bool night = Night;
            Shader.SetGlobalColor(FogUnknown, (night ? UnknownNight : Unknown).linear);
            Shader.SetGlobalColor(FogHaze, (night ? HazeNight : Haze).linear);
            Shader.SetGlobalColor(FogMistTint, (night ? MistNight : MistTint).linear);
            Shader.SetGlobalFloat("_FogNight", night ? 1 : 0);
            Shader.SetGlobalColor("_FogLamp", Lamp);
        }

        void Remake(ref Texture2D texture, string name, FilterMode filter = FilterMode.Bilinear)
        {
            if (texture != null)
            {
                if (Application.isPlaying) Destroy(texture);
                else DestroyImmediate(texture);
            }
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { name = name, filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        }

        /// <summary>
        /// The buildings (fogOfWar.ts setShells): their tiles joined side by side, each with the street along it; each
        /// tile's building storeys in the shell texture (red, an eighth a storey).
        /// </summary>
        void Shells(GameState s)
        {
            shells.Clear();
            int n = width * height;
            var storeys = s.Storeys;
            if (storeys == null || storeys.Count != n) return;
            var home = Enumerable.Repeat(-1, n).ToArray();
            for (int start = 0; start < n; start++)
            {
                if (storeys[start] <= 0 || home[start] >= 0) continue;
                var shell = new Shell();
                var outside = new HashSet<int>();
                home[start] = shells.Count;
                shell.tiles.Add(start);
                for (int k = 0; k < shell.tiles.Count; k++)
                {
                    int i = shell.tiles[k], x = i % width, y = i / width;
                    foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int j = ny * width + nx;
                        if (storeys[j] <= 0)
                        {
                            if (!shut[j]) outside.Add(j);
                        }
                        else if (home[j] < 0)
                        {
                            home[j] = home[start];
                            shell.tiles.Add(j);
                        }
                    }
                }
                shell.along = outside.ToArray();
                shells.Add(shell);
            }
            for (int i = 0; i < n; i++) shellPixels[i].r = (byte)Mathf.Clamp(storeys[i] * 32, 0, 255);
        }

        /// <summary>The buildings as one goes in (buildingOf), none gone into yet.</summary>
        void Buildings(GameState s)
        {
            int n = width * height;
            buildingOf = Enumerable.Repeat(-1, n).ToArray();
            closed = new bool[n];
            var storeys = s.Storeys;
            if (storeys == null || storeys.Count != n || s.Tiles == null || s.Tiles.Count != n)
            {
                entered = new bool[0];
                return;
            }
            bool Room(int i) => storeys[i] > 0 && s.Tiles[i] != "wall" && s.Tiles[i] != "window" && s.Tiles[i] != "windowBroken";
            int count = 0;
            var tiles = new List<int>();
            for (int start = 0; start < n; start++)
            {
                if (buildingOf[start] >= 0 || !Room(start)) continue;
                tiles.Clear();
                tiles.Add(start);
                buildingOf[start] = count;
                for (int k = 0; k < tiles.Count; k++)
                {
                    int i = tiles[k], x = i % width, y = i / width;
                    foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int j = ny * width + nx;
                        if (buildingOf[j] >= 0 || !Room(j)) continue;
                        buildingOf[j] = count;
                        tiles.Add(j);
                    }
                }
                count++;
            }
            entered = new bool[count];
            // A siege's map is the squad's own base: its buildings known inside.
            if (s.Mission?.Siege != null) for (int k = 0; k < count; k++) entered[k] = true;
            Close(s);
        }

        /// <summary>The buildings the squad is in now marked gone into, and what is still closed worked out again.</summary>
        void Enter(GameState s)
        {
            var storeys = s.Storeys;
            if (buildingOf == null || storeys == null || storeys.Count != width * height) return;
            foreach (var u in s.Units)
            {
                // (Who sees for the squad: not one held there to be rescued.)
                if (!Los.IsViewer(u, "xcom") || u.Evacuated || u.Pos == null) continue;
                int x = u.Pos.X, y = u.Pos.Y;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                int i = y * width + x;
                // In it: on one of its storeys (on its roof, not).
                if (storeys[i] <= 0 || u.Pos.Z >= storeys[i]) continue;
                // (On a wall's or a window's tile, as a storey above may be laid out otherwise: the buildings beside it.)
                if (buildingOf[i] >= 0) entered[buildingOf[i]] = true;
                else foreach (int j in Beside(i)) if (buildingOf[j] >= 0) entered[buildingOf[j]] = true;
            }
            Close(s);
        }

        void Close(GameState s)
        {
            var storeys = s.Storeys;
            for (int i = 0; i < closed.Length; i++)
            {
                closed[i] = false;
                if (storeys == null || i >= storeys.Count || storeys[i] <= 0) continue;
                if (buildingOf[i] >= 0) closed[i] = !entered[buildingOf[i]];
                // A wall or a window: closed while every building it stands by is.
                else closed[i] = !Beside(i).Any(j => buildingOf[j] >= 0 && entered[buildingOf[j]]);
            }
        }

        IEnumerable<int> Beside(int i)
        {
            int x = i % width, y = i / width;
            if (x > 0) yield return i - 1;
            if (x < width - 1) yield return i + 1;
            if (y > 0) yield return i - width;
            if (y < height - 1) yield return i + width;
        }

        /// <summary>
        /// What the squad sees in `seen` (the state as the player has been shown it): in sight, in view, and what it has
        /// seen this mission, storey by storey; `instant`, at once (a new map, a return), else fading over SIGHT_FADE. What
        /// is seen of a building nobody has been in is not kept.
        /// </summary>
        public void Show(GameState seen, bool instant = false)
        {
            if (map == null || seen.Width != width || seen.Height != height) Reset(seen);
            last = seen;
            visible = Los.TeamVisibility(seen, "xcom");
            // Lit at night (the engine's lit mask: street lights, a Bengala, a fire, as far as their light reaches).
            lights = Night ? Los.LitMask(seen) : null;
            view = Los.TeamView(seen, "xcom");
            Enter(seen);
            spotted.Clear();
            foreach (var u in seen.Units)
            {
                if (!u.Alive || u.Team == "xcom" || u.Evacuated) continue;
                int node = Bastion.Engine.Grid.NodeOf(seen, u.Pos);
                if (node >= 0 && node < visible.Length && visible[node]) spotted.Add(node);
            }
            if (explored == null || explored.Length != view.Length) explored = new bool[view.Length];
            int area = width * height;
            for (int k = 0; k < view.Length; k++) explored[k] |= (view[k] || (k < visible.Length && visible[k])) && !closed[k % area];
            Refog(instant);
        }

        /// <summary>Everything in sight (a lab's look at the whole map, as 0.10.1's reference block), at once.</summary>
        public void SeeAll()
        {
            if (visible == null) return;
            for (int k = 0; k < visible.Length; k++) visible[k] = true;
            for (int k = 0; k < entered.Length; k++) entered[k] = true;
            if (last != null) Close(last);
            Refog(true);
        }

        /// <summary>The storey in view changed (MapView.ShowLevel) or the map did: the fog worked out for it again.</summary>
        public void Refog(bool instant = false)
        {
            Tints();
            if (last == null || visible == null) return;
            var s = last;
            int n = width * height;
            var mapView = MapView.Current;
            // The storey in view; the whole map, roofs and all, as the ground's (its roofs are the buildings' shells).
            int floor = mapView == null || mapView.Level >= mapView.Levels ? 0 : mapView.Level;
            var levels = Los.FloorLevels(s, floor);
            for (int i = 0; i < n; i++)
            {
                int node = levels[i] * n + i;
                bool inSight = node < visible.Length && visible[node];
                bool seen = inSight || (node < view.Length && view[node]) || (node < explored.Length && explored[node]);
                // (A building nobody has been in: nothing of its inside, though its tiles be in sight through a window; but
                // the tile an enemy in sight stands on, while it is, with its ring and its shadow: Gameplay, 28/10.)
                if (closed[i] && !spotted.Contains(node)) inSight = seen = false;
                sightTo[i] = inSight && !shut[i] ? 1 : 0;
                litTo[i] = lights != null && node < lights.Length && lights[node] ? 1 : 0;
                knownTo[i] = seen && !shut[i] ? 1 : 0;
                // The floor of each tile there, for the low mist (setFloors).
                float storey = levels[i] > 0 ? levels[i] : (i < s.Elev.Count ? s.Elev[i] : 0);
                pixels[i].a = (byte)Mathf.Clamp(Mathf.RoundToInt(storey / 8 * 255), 0, 255);
            }
            Rooms(s, levels);
            // A building's roof: in sight when any of the street along it is, seen when any of it was.
            foreach (var shell in shells)
            {
                float goal = shell.along.Any(i => sightTo[i] > 0) ? 1 : 0;
                float seen = shell.along.Any(i => knownTo[i] > 0) ? 1 : 0;
                foreach (int i in shell.tiles)
                {
                    roofSightTo[i] = goal;
                    roofKnownTo[i] = seen;
                }
            }
            if (instant)
            {
                System.Array.Copy(sightTo, sight, n);
                System.Array.Copy(litTo, lit, n);
                System.Array.Copy(knownTo, known, n);
                System.Array.Copy(roofSightTo, roofSight, n);
                System.Array.Copy(roofKnownTo, roofKnown, n);
            }
            changed = true;
            if (instant) Step(0);
        }

        /// <summary>
        /// The rooms of the storey in view (setRooms): the inside of each building parted by its walls, doors and windows;
        /// a room any of which is seen is seen whole, with the walls round it.
        /// </summary>
        void Rooms(GameState s, int[] levels)
        {
            int n = width * height;
            var storeys = s.Storeys;
            if (storeys == null || storeys.Count != n) return;
            string KindAt(int i) => levels[i] == 0 ? s.Tiles[i] : s.Levels[levels[i] - 1].Tiles[i];
            bool Inside(int i) => storeys[i] > 0 && KindAt(i) is string k && !BetweenRooms.Contains(k);
            var done = new bool[n];
            var tiles = new List<int>();
            var round = new HashSet<int>();
            for (int start = 0; start < n; start++)
            {
                if (done[start] || !Inside(start)) continue;
                tiles.Clear();
                round.Clear();
                tiles.Add(start);
                done[start] = true;
                bool any = false;
                for (int k = 0; k < tiles.Count; k++)
                {
                    int i = tiles[k], x = i % width, y = i / width;
                    any |= knownTo[i] > 0;
                    foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int j = ny * width + nx;
                        if (Inside(j))
                        {
                            if (!done[j])
                            {
                                done[j] = true;
                                tiles.Add(j);
                            }
                        }
                        else if (storeys[j] > 0) round.Add(j);
                    }
                }
                if (!any) continue;
                foreach (int i in tiles.Concat(round))
                    if (!shut[i] && !closed[i]) knownTo[i] = 1;
            }
        }

        /// <summary>How many tiles are in sight and seen (now, fading included).</summary>
        /// <summary>Whether the squad has seen place `node` this mission (UI's marks over the loot boxes: lootMarks.ts explored).</summary>
        public bool Explored(int node) => explored != null && node >= 0 && node < explored.Length && explored[node];

        public (int sight, int known) Counts() => (sight?.Count(v => v > 0.5f) ?? 0, known?.Count(v => v > 0.5f) ?? 0);

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        public void ShotStep(float dt)
        {
            if (!Application.isPlaying) Step(dt);
        }

        void Step(float dt)
        {
            time += dt;
            Shader.SetGlobalFloat(FogTime, time);
            if (map == null) return;
            float step = dt / SightFade;
            for (int i = 0; i < sight.Length; i++)
            {
                if (sight[i] == sightTo[i] && known[i] == knownTo[i] && roofSight[i] == roofSightTo[i] && roofKnown[i] == roofKnownTo[i] && lit[i] == litTo[i]) continue;
                lit[i] = Mathf.MoveTowards(lit[i], litTo[i], step);
                sight[i] = Mathf.MoveTowards(sight[i], sightTo[i], step);
                known[i] = Mathf.MoveTowards(known[i], knownTo[i], step);
                roofSight[i] = Mathf.MoveTowards(roofSight[i], roofSightTo[i], step);
                roofKnown[i] = Mathf.MoveTowards(roofKnown[i], roofKnownTo[i], step);
                changed = true;
            }
            if (!changed) return;
            changed = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r = (byte)(sight[i] * 255);
                pixels[i].g = (byte)(known[i] * 255);
                pixels[i].b = (byte)(lit[i] * 255);
                shellPixels[i].g = (byte)(roofSight[i] * 255);
                shellPixels[i].b = (byte)(roofKnown[i] * 255);
            }
            map.SetPixels32(pixels);
            map.Apply(false);
            shellMap.SetPixels32(shellPixels);
            shellMap.Apply(false);
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
