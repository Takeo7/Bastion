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
    /// walls round it (Ana, 22/10 #36), but only once the squad has found the building (the engine's Buildings: a soldier
    /// has gone into it, or looked in at one of its windows from the tile just outside it: Arturo, 31/10, Ana's #5); till
    /// then its inside stays unknown, whatever is seen of it through a window or a door from further off (Arturo, 28/10,
    /// Ana's p1; 0.10.1 showed the room seen through it), and nobody inside is in sight (Ana's #1). In a siege by day the
    /// base and its clearing are known from the start, the pinar round them fogged till a soldier sees it (Arturo, 31/10,
    /// his #5; 0.10.1 had no fog in a siege: 18/10 #18). Each tile's zone in a small texture, faded as it changes, which a full-screen pass
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
        /// Each ground tile of a building the squad has not found yet (its walls and windows too, while every building
        /// beside them is unfound), kept unknown (Close); `all`, none (SeeAll).
        /// </summary>
        bool[] closed;
        bool all;
        /// <summary>
        /// Where an enemy in sight stands (its node): on a closed tile, its tile alone shown, so that it stands on its floor
        /// (who stands in the doorway of a building not found shows: the engine's Buildings.Hidden).
        /// </summary>
        readonly HashSet<int> spotted = new HashSet<int>();
        /// <summary>A siege's by day (Home): by tile, the base and its clearing, seen from the start; null on any other map.</summary>
        bool[] home;
        /// <summary>
        /// How far from the map's middle a siege's clearing goes at most (tiles, on the base's 56 a side): its own (8 at most,
        /// Base.FreshBase) and the 6 over which its pines thicken; open ground further out is the pinar's.
        /// </summary>
        const float HomeReach = 14;
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
            closed = new bool[n];
            all = false;
            Close(s);
            home = Home(s);
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

        /// <summary>
        /// What is still closed (the engine's Buildings, Arturo 31/10, Ana's #1 and #5): a building's ground tiles (its
        /// inside: not a wall nor a window; its doors in it) until the squad finds it (Buildings.FoundTiles: gone into, or
        /// looked into from the tile just before one of its windows, as GameState.discovered keeps it and as the squad
        /// stands now); a wall or a window while every building beside it is. None in a siege, on the squad's own base.
        /// </summary>
        void Close(GameState s)
        {
            var storeys = s.Storeys;
            bool open = all || s.Mission?.Siege != null || storeys == null || storeys.Count != closed.Length || s.Tiles == null || s.Tiles.Count != closed.Length;
            var found = open ? null : Bastion.Engine.Buildings.FoundTiles(s);
            for (int i = 0; i < closed.Length; i++)
            {
                closed[i] = false;
                if (open || storeys[i] <= 0) continue;
                string t = s.Tiles[i];
                if (t != "wall" && t != "window" && t != "windowBroken") closed[i] = !found.Contains(i);
                // A wall or a window: closed while every building it stands by is.
                else closed[i] = !Beside(i).Any(j => storeys[j] > 0 && found.Contains(j));
            }
        }

        /// <summary>
        /// A siege's base and its clearing by day (Arturo, 31/10, his #5): the ground that is not a pine, but for the ways
        /// through the pines (two tiles wide or less: a tile's erosion takes them), joined to the middle and within HomeReach
        /// of it, given back the tile round it; the pines most of whose round is in it (one standing alone in the clearing);
        /// and what the base has built, wherever, with the tiles round it. On a siege's map without pines, all of it. Null on
        /// any other map, and by night (as it was: what is near and what the floodlights light).
        /// </summary>
        static bool[] Home(GameState s)
        {
            int w = s.Width, h = s.Height, n = w * h;
            if (s.Mission?.Siege == null || s.Mission.Night == true || s.Tiles == null || s.Tiles.Count != n) return null;
            bool Pine(int x, int y)
            {
                int i = y * w + x;
                return s.Tiles[i] == TileKinds.High && s.Decor != null && i < s.Decor.Count && Decor.DecorOf(s.Decor[i])?.Kind == DecorKinds.Tree;
            }
            bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
            // (Off the map counts as open: a way out along the track keeps its edge.)
            bool Open(int x, int y) => !In(x, y) || !Pine(x, y);
            var home = new bool[n];
            bool pines = false;
            for (int i = 0; i < n && !pines; i++) pines = Pine(i % w, i / w);
            if (!pines)
            {
                for (int i = 0; i < n; i++) home[i] = true;
                return home;
            }
            float cx = (w - 1) / 2f, cy = (h - 1) / 2f, reach = HomeReach * Mathf.Max(w, h) / Base.BaseMap;
            var core = new bool[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool open = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= reach * reach;
                    for (int dy = -1; dy <= 1 && open; dy++)
                        for (int dx = -1; dx <= 1 && open; dx++) open = Open(x + dx, y + dy);
                    core[y * w + x] = open;
                }
            // From the middle, side by side.
            var reached = new bool[n];
            var todo = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (!core[i] || Mathf.Abs(i % w - cx) > 4 || Mathf.Abs(i / w - cy) > 4) continue;
                reached[i] = true;
                todo.Enqueue(i);
            }
            while (todo.Count > 0)
            {
                int i = todo.Dequeue(), x = i % w, y = i / w;
                foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                {
                    if (!In(nx, ny) || !core[ny * w + nx] || reached[ny * w + nx]) continue;
                    reached[ny * w + nx] = true;
                    todo.Enqueue(ny * w + nx);
                }
            }
            int Round(bool[] set, int x, int y)
            {
                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && In(x + dx, y + dy) && set[(y + dy) * w + x + dx]) k++;
                return k;
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    home[y * w + x] = Open(x, y) && (reached[y * w + x] || Round(reached, x, y) > 0);
            var lone = new List<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Pine(x, y) && Round(home, x, y) >= 5) lone.Add(y * w + x);
            foreach (int i in lone) home[i] = true;
            if (s.Base?.Pieces != null)
                foreach (var p in s.Base.Pieces)
                    foreach (var c in Base.CellsOf(p))
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if (In(c.X + dx, c.Y + dy)) home[(c.Y + dy) * w + c.X + dx] = true;
            return home;
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
            using var shown = ShowMarker.Auto();
            if (map == null || seen.Width != width || seen.Height != height) Reset(seen);
            last = seen;
            // (What the figures worked out for this same state just before, when they did: UnitViews.Sync, as UI's
            // ShowSeen calls them; UI, 7/11: worked out again, 2.5 ms a Settle.)
            using (SightMarker.Auto()) visible = Units.UnitViews.Current?.SightOf(seen) ?? Los.TeamVisibility(seen, "xcom");
            // Lit at night (the engine's lit mask: street lights, a Bengala, a fire, as far as their light reaches).
            lights = Night ? Los.LitMask(seen) : null;
            using (ViewMarker.Auto()) view = Los.TeamView(seen, "xcom");
            using (CloseMarker.Auto()) Close(seen);
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
            // A siege's base and clearing by day, on every storey (its towers' decks): seen, whoever looks.
            if (home != null)
                for (int k = 0; k < explored.Length; k++) explored[k] |= home[k % area];
            using (RefogMarker.Auto()) Refog(instant);
        }

        /// <summary>Show's parts for the profiler (RendimientoLab.Parones; UI, 7/11: ShowSeen 6 to 35 ms).</summary>
        static readonly Unity.Profiling.ProfilerMarker ShowMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Fog.Show"),
            SightMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Fog.Sight"), ViewMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Fog.View"),
            CloseMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Fog.Close"), RefogMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Fog.Refog");

        /// <summary>Everything in sight (a lab's look at the whole map, as 0.10.1's reference block), at once.</summary>
        public void SeeAll()
        {
            if (visible == null) return;
            // (A copy: the one Show took may be the figures' own, UnitViews.SightOf.)
            visible = (bool[])visible.Clone();
            for (int k = 0; k < visible.Length; k++) visible[k] = true;
            all = true;
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

        /// <summary>
        /// Whether ground tile (x, y) is in the squad's sight as the fog shows it (where it is going); with `roof`, a
        /// building's roof over it (seen as the street along it). For what shows only in sight: Fauna's animals.
        /// </summary>
        public bool InSight(int x, int y, bool roof = false)
        {
            if (sightTo == null || x < 0 || y < 0 || x >= width || y >= height) return false;
            int i = y * width + x;
            return (roof ? roofSightTo[i] : sightTo[i]) > 0.5f;
        }

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
