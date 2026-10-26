using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The settlement's camera, by the screen's tab (0.10.1's screen.ts shotFor and stage.ts show): the clearing from above
    /// on the caravan (Asentamiento), the soldier by the barracks (Soldados and Personalizar: clearingArt.ts soldierShot),
    /// the Armería's bench (armoryShot) and the squad's row (lineupShot), these two kept whole in what the screen's panels
    /// leave free (stage.ts fitted, by SetInsets); nothing while the valley is shown (Mapa: the valley's own camera). From
    /// one to the next it eases (0.9 s); into the Mapa and out of it, it goes through black (Stage.show), and at the bottom
    /// of it UI's valley takes the screen or gives it back (MapShown).
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>The settlement on screen (the scene «Base»'s), for UI to find.</summary>
        public static Settlement Instance => Current;

        /// <summary>The camera it moves: the one given, else the scene's main one.</summary>
        public Camera Camera
        {
            get => cam != null ? cam : cam = Camera.main;
            set => cam = value;
        }

        /// <summary>The tab shown (screen.ts BaseView): geoscape, facilities, engineering, barracks, squad, debrief.</summary>
        public string View => view;

        Camera cam;
        string view = Views.Facilities;

        /// <summary>The tabs' names, as UI's (screen.ts BaseView).</summary>
        public static class Views
        {
            public const string Geoscape = "geoscape";
            public const string Facilities = "facilities";
            public const string Engineering = "engineering";
            public const string Barracks = "barracks";
            public const string Squad = "squad";
            public const string Debrief = "debrief";
        }

        /// <summary>A camera placement in 0.10.1's tiles (x east, z south, y up): where it stands, what it looks at, its field of view.</summary>
        public struct Shot
        {
            public Vector3 pos;
            public Vector3 target;
            public float fov;
            /// <summary>What else it shows (tiles): no pine stands before them (ClearShot).</summary>
            public List<Vector3> seen;
        }

        /// <summary>
        /// UI's valley takes the screen (true) or gives it back (false): at the bottom of the fade into or out of the Mapa,
        /// as the clearing's camera goes off or on (0.10.1's Stage.show, which changed the set there).
        /// </summary>
        public event Action<bool> MapShown;

        /// <summary>The valley is what is shown (the clearing's camera off), as last changed.</summary>
        public bool MapOn { get; private set; }

        /// <summary>The change into (true) or out of (false) the Mapa waiting for the black; null, none.</summary>
        bool? changing;
        /// <summary>How black the 3D is (0 to 1: Grade.shader's _StageFade, under the panels: stage.css .stage-fade).</summary>
        float black;
        /// <summary>
        /// UI's camera rig has the camera (the Asentamiento's free look, and the Escuadra's in a siege, its posts: screen.ts
        /// fixedShot none): while true the tab's shot is eased to and then left alone (nothing fits it again), and UI's rig
        /// takes it from there once CameraMoving is false (screen.ts: «only once the stage has eased it home»); back to false,
        /// it eases from where it is to the tab's shot.
        /// </summary>
        public bool CameraFree
        {
            get => cameraFree;
            set
            {
                if (cameraFree == value) return;
                cameraFree = value;
                if (!value && !MapOn && placed) Go(ShotFor(view), false);
            }
        }
        bool cameraFree;

        /// <summary>The camera is being eased to a shot (0.9 s), not yet still on it.</summary>
        public bool CameraMoving => moved < MoveSeconds;

        /// <summary>What the Asentamiento's shot looks at (metres): the point over the caravan the free look starts from (UI's rig, Focus).</summary>
        public Vector3 ClearingTarget => World(ClearingShot().target);

        /// <summary>The clearing's ground (metres, x and z: its south-west corner and its size), for the free look's bounds.</summary>
        public Rect ClearingBounds => Base == null ? new Rect() : new Rect(0, -Base.Height * MapSpace.Tile, Base.Width * MapSpace.Tile, Base.Height * MapSpace.Tile);

        /// <summary>The fade's each way (stage.css: opacity over 0.25 s; Stage.show waited 260 ms before changing the set).</summary>
        const float FadeSeconds = 0.26f;

        // The move under way: from where the camera was to the shot asked for, `moved` seconds into it.
        Vector3 fromPos, fromTarget;
        float fromFov;
        Shot goal;
        float moved = MoveSeconds;
        bool placed;
        const float MoveSeconds = 0.9f;

        /// <summary>
        /// The panels over the screen, as fractions of it from its top left (StrategyRoot.SettlementPanels): kept for the
        /// shots that fit a box in what they leave free (the Armería's bench, the squad's row: stage.ts fitted).
        /// </summary>
        public IReadOnlyList<Rect> Insets => insets;
        readonly List<Rect> insets = new List<Rect>();

        public void SetInsets(IReadOnlyList<Rect> panels)
        {
            // (Only what has a size: a panel not laid out yet comes as NaN, and with it every shot fitted to it.)
            var next = panels?.Where(r => !float.IsNaN(r.x + r.y + r.width + r.height) && !float.IsInfinity(r.x + r.y + r.width + r.height) && r.width > 0 && r.height > 0).ToList() ?? new List<Rect>();
            if (next.SequenceEqual(insets)) return;
            insets.Clear();
            insets.AddRange(next);
            Refit();
        }

        /// <summary>The tab shown now: the camera goes to its shot (at once if `instant`, or if it had none yet).</summary>
        public void SetView(string tab, bool instant = false)
        {
            view = tab ?? Views.Facilities;
            // Soldados' soldier only on Soldados (screen.ts: the showcase cleared on leaving it).
            if (showcase != null && showcase.gameObject.activeSelf != (view == Views.Barracks))
            {
                showcase.gameObject.SetActive(view == Views.Barracks);
                // Its pose written again now that it is on.
                if (view == Views.Barracks) StepShowcase(0);
            }
            var camera = Camera;
            if (camera == null) return;
            bool map = view == Views.Geoscape;
            if (map != MapOn || changing != null)
            {
                // Into the Mapa or out of it: through black, the newest asked for winning (Stage.show's moveId); at once if
                // asked, or with nothing shown yet, or outside play (the labs' shots).
                if (instant || !placed || !Application.isPlaying) Change(map);
                else changing = map;
                return;
            }
            if (!map) Go(ShotFor(view), instant || !placed);
        }

        /// <summary>The valley shown or the clearing: the clearing's camera off or on (placed at once on the tab's shot), and UI told.</summary>
        void Change(bool map)
        {
            changing = null;
            MapOn = map;
            var camera = Camera;
            if (camera != null && camera.enabled == map) camera.enabled = !map;
            if (!map) Go(ShotFor(view), true);
            MapShown?.Invoke(map);
        }

        /// <summary>The fade down to black while a change waits, the change at the bottom, and back up.</summary>
        void StepFade(float dt)
        {
            float to = changing != null ? 1 : 0;
            if (black == to && changing == null) return;
            black = Mathf.MoveTowards(black, to, dt / FadeSeconds);
            if (changing != null && black >= 1) Change(changing.Value);
            Shader.SetGlobalFloat("_StageFade", black);
        }

        /// <summary>The shot of a tab, as it stands now.</summary>
        public Shot ShotFor(string tab)
        {
            switch (tab)
            {
                case Views.Barracks:
                    return SoldierShot();
                case Views.Engineering:
                    return ArmoryShot();
                case Views.Squad:
                case Views.Debrief:
                    return LineupShot();
                default:
                    return ClearingShot();
            }
        }

        /// <summary>
        /// The clearing from above (clearing.ts and camera.ts CameraRig as it starts): on the caravan's middle, 24 tiles off
        /// at 52° down, from the south-east, 40° of view.
        /// </summary>
        public Shot ClearingShot()
        {
            var b = Base;
            var caravan = b?.Pieces.FirstOrDefault(p => p.Piece == PieceIds.Caravan);
            Vector3 target;
            if (caravan != null)
            {
                var cells = Bastion.Engine.Base.CellsOf(caravan);
                float w = cells.Max(c => c.X) - cells.Min(c => c.X) + 1, h = cells.Max(c => c.Y) - cells.Min(c => c.Y) + 1;
                target = new Vector3(caravan.X + (w - 1) / 2 + 0.5f, 0, caravan.Y + (h - 1) / 2 + 0.5f);
            }
            else target = b != null ? new Vector3(b.Width / 2f, 0, b.Height / 2f) : Vector3.zero;
            float yaw = Mathf.PI / 4, pitch = 52 * Mathf.Deg2Rad, distance = 24;
            var back = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
            return new Shot { pos = target + back * distance, target = target, fov = 40 };
        }

        /// <summary>Soldados: the soldier by the caravan (or the barracks), seen from out in the clearing, the pines behind it (clearingArt.ts soldierShot).</summary>
        public Shot SoldierShot()
        {
            var target = new Vector3(soldierAt.x, 1.0f, soldierAt.y);
            return new Shot { pos = target + new Vector3(soldierView.x * soldierDistance, 1.5f, soldierView.y * soldierDistance), target = target, fov = 38 };
        }

        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        void Go(Shot shot, bool instant)
        {
            if (!Finite(shot.pos) || !Finite(shot.target)) return;
            // (The squad leaving has the camera: Leave.)
            if (Leaving) return;
            var camera = Camera;
            goal = shot;
            ClearShot(shot);
            if (instant || camera == null)
            {
                moved = MoveSeconds;
                Place(shot.pos, shot.target, shot.fov);
                return;
            }
            fromPos = camera.transform.position;
            if (!Finite(fromPos) || !placed)
            {
                moved = MoveSeconds;
                Place(shot.pos, shot.target, shot.fov);
                return;
            }
            fromTarget = placed ? World(lookingAt) : camera.transform.position + camera.transform.forward * 10;
            fromFov = camera.fieldOfView;
            moved = 0;
        }

        /// <summary>
        /// How far the screen's panels reach in from each edge (stage.ts insets, in pixels of `camera`): a bar (much wider
        /// than tall) at the top or the bottom edge, or a few pixels off it, takes that side; a sheet the side its middle
        /// is on.
        /// </summary>
        (float top, float right, float bottom, float left) InsetsOn(Camera camera)
        {
            var size = SizeOf(camera);
            float w = size.x, h = size.y;
            float top = 0, right = 0, bottom = 0, left = 0;
            foreach (var f in insets)
            {
                var r = new Rect(f.x * w, f.y * h, f.width * w, f.height * h);
                if (r.width < 4 || r.height < 4) continue;
                bool bar = r.width > r.height * 3;
                // (A wide panel low on the screen is the bottom's though it stops short of the edge: UI's debrief, its foot
                // 6 % up, was taken for a sheet on the right and the row framed over a third of the screen.)
                bool wide = bar || r.width > w * 0.5f;
                if (r.yMin <= 2 || (bar && r.yMin <= 24)) top = Mathf.Max(top, r.yMax);
                else if (r.yMax >= h - 2 || (bar && r.yMax >= h - 24) || (wide && r.center.y > h * 0.6f)) bottom = Mathf.Max(bottom, h - r.yMin);
                else if (r.center.x < w / 2) left = Mathf.Max(left, r.xMax);
                else right = Mathf.Max(right, w - r.xMin);
            }
            return (top, right, bottom, left);
        }

        /// <summary>
        /// `shot` moved along its view and sideways until `points` (tiles) fill the part of the screen the panels leave
        /// free, 12 px in (stage.ts fitted: a few rounds of measuring them on screen converge); `centred`, kept in the
        /// middle across whatever is on one side only. On a small screen the box keeps at least half of it each way.
        /// </summary>
        /// <summary>Whether the Armería's and the squad's shots fit what the panels leave (as the game; off, as claro.html shows them).</summary>
        public bool FitShots = true;

        Shot Fitted(Shot shot, IReadOnlyList<Vector3> points, bool centred)
        {
            var camera = Camera;
            if (!FitShots || camera == null || points == null || points.Count == 0) return shot;
            var size = SizeOf(camera);
            float w = size.x, h = size.y;
            if (w <= 0 || h <= 0) return shot;
            const float margin = 12;
            var inset = InsetsOn(camera);
            float left = inset.left + margin, right = inset.right + margin, top = inset.top + margin, bottom = inset.bottom + margin;
            if (centred) left = right = Mathf.Max(left, right);
            float Squeeze(float a, float b, float size) => a + b > size / 2 ? size / 2 / (a + b) : 1;
            float kx = Squeeze(left, right, w), ky = Squeeze(top, bottom, h);
            left *= kx;
            right *= kx;
            top *= ky;
            bottom *= ky;
            float fx0 = left / w * 2 - 1, fx1 = 1 - right / w * 2, fy0 = bottom / h * 2 - 1, fy1 = 1 - top / h * 2;
            var world = points.Select(World).ToList();
            var target = World(shot.target);
            var back = World(shot.pos) - target;
            float distance = back.magnitude;
            back /= Mathf.Max(distance, 1e-4f);
            var was = (camera.transform.position, camera.transform.rotation, camera.fieldOfView);
            camera.fieldOfView = shot.fov;
            camera.aspect = w / h;
            for (int round = 0; round < 12; round++)
            {
                camera.transform.position = target + back * distance;
                camera.transform.LookAt(target);
                float x0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y0 = float.PositiveInfinity, y1 = float.NegativeInfinity;
                foreach (var p in world)
                {
                    var v = camera.WorldToViewportPoint(p);
                    float px = v.x * 2 - 1, py = v.y * 2 - 1;
                    x0 = Mathf.Min(x0, px);
                    x1 = Mathf.Max(x1, px);
                    y0 = Mathf.Min(y0, py);
                    y1 = Mathf.Max(y1, py);
                }
                float half = Mathf.Tan(shot.fov / 2 * Mathf.Deg2Rad) * distance;
                target += camera.transform.right * (((x0 + x1) / 2 - (fx0 + fx1) / 2) * half * camera.aspect);
                target += camera.transform.up * (((y0 + y1) / 2 - (fy0 + fy1) / 2) * half);
                distance *= Mathf.Max((x1 - x0) / (fx1 - fx0), (y1 - y0) / (fy1 - fy0));
            }
            camera.transform.SetPositionAndRotation(was.position, was.rotation);
            camera.fieldOfView = was.fieldOfView;
            // Back to following the screen's (the game's camera never had one of its own).
            camera.ResetAspect();
            var fitted = new Shot { pos = Tiles(target + back * distance), target = Tiles(target), fov = shot.fov, seen = shot.seen };
            return Finite(fitted.pos) && Finite(fitted.target) ? fitted : shot;
        }

        /// <summary>The screen's size the shots are fitted to (pixels): ScreenSize if given (the labs' shots), else the camera's.</summary>
        public Vector2? ScreenSize;

        Vector2 SizeOf(Camera camera) => ScreenSize ?? (camera.targetTexture != null ? new Vector2(camera.targetTexture.width, camera.targetTexture.height) : new Vector2(camera.pixelWidth, camera.pixelHeight));

        /// <summary>The panels moved (another layout, another size): a fitted tab's shot made again.</summary>
        void Refit()
        {
            if (view == Views.Engineering || view == Views.Squad || view == Views.Debrief) Reframe();
        }

        /// <summary>
        /// The pines between a fixed shot's camera and what it looks at, and round the camera, taken out of the picture while
        /// it is on (0.10.1 laced them through: treeFade.ts; a soldier's or the squad's camera fitted back could stand in a
        /// pine's crown, its inside filling half the frame). The clearing's own camera keeps them all.
        /// </summary>
        void ClearShot(Shot shot)
        {
            foreach (var r in cleared) if (r != null) r.enabled = true;
            cleared.Clear();
            if (Map == null || view == Views.Facilities || view == Views.Geoscape) return;
            if (pines == null)
                pines = Map.GetComponentsInChildren<Renderer>().Where(r =>
                {
                    for (var t = r.transform; t != null && t != Map.transform; t = t.parent)
                    {
                        string n = t.name.ToLowerInvariant();
                        if (n.Contains("pine") || n.Contains("tree")) return true;
                    }
                    return false;
                }).ToList();
            var from = World(shot.pos);
            // To what it looks at and what else it shows: not what stands right by them (their background), nor behind.
            var rays = new[] { shot.target }.Concat(shot.seen ?? Enumerable.Empty<Vector3>()).Select(p =>
            {
                var to = World(p);
                return (ray: new Ray(from, to - from), reach: Mathf.Max(0, Vector3.Distance(from, to) - 1.2f * MapSpace.Tile));
            }).ToList();
            foreach (var r in pines)
            {
                if (r == null || !r.enabled) continue;
                var b = r.bounds;
                bool round = b.SqrDistance(from) < 1.2f * MapSpace.Tile * 1.2f * MapSpace.Tile;
                bool between = !round && rays.Any(x => b.IntersectRay(x.ray, out float d) && d < x.reach);
                if (!round && !between) continue;
                r.enabled = false;
                cleared.Add(r);
            }
        }

        readonly List<Renderer> cleared = new List<Renderer>();
        List<Renderer> pines;

        /// <summary>The tab's shot again, where things stand now (the base changed, La Lata parked elsewhere), easing there.</summary>
        void Reframe()
        {
            if (!placed || view == Views.Geoscape || cameraFree) return;
            var shot = ShotFor(view);
            if ((shot.pos - goal.pos).sqrMagnitude < 1e-4f && (shot.target - goal.target).sqrMagnitude < 1e-4f) return;
            Go(shot, false);
        }

        Vector3 lookingAt;

        void Place(Vector3 pos, Vector3 target, float fov)
        {
            var camera = Camera;
            if (camera == null) return;
            camera.transform.position = World(pos);
            camera.transform.LookAt(World(target));
            camera.fieldOfView = fov;
            if (camera.farClipPlane < 800) camera.farClipPlane = 800;
            lookingAt = target;
            placed = true;
        }

        void MoveCamera(float dt)
        {
            if (moved >= MoveSeconds || Camera == null) return;
            moved = Mathf.Min(MoveSeconds, moved + dt);
            float t = moved / MoveSeconds;
            float k = t < 0.5f ? 2 * t * t : 1 - (-2 * t + 2) * (-2 * t + 2) / 2;
            var pos = Vector3.Lerp(Tiles(fromPos), goal.pos, k);
            var target = Vector3.Lerp(Tiles(fromTarget), goal.target, k);
            Place(pos, target, fromFov + (goal.fov - fromFov) * k);
        }

        /// <summary>0.10.1's tiles (x east, z south) to Unity's metres (x east, z north), and back.</summary>
        static Vector3 World(Vector3 tiles) => new Vector3(tiles.x, tiles.y, -tiles.z) * MapSpace.Tile;
        static Vector3 Tiles(Vector3 world) => new Vector3(world.x, world.y, -world.z) / MapSpace.Tile;

        // ------------------------------------------------------------------------------------------------- the camp

        /// <summary>Where Soldados' soldier stands (tiles) and the clearest way to look at it: from it to the camera, how far.</summary>
        Vector2 soldierAt;
        Vector2 soldierView = new Vector2(0, 1);
        float soldierDistance = 4;

        /// <summary>La Lata as it is parked, for what keeps off it: its middle, its heading's unit, half its length and width (tiles).</summary>
        Vector2 busAt, busDir;
        float busHalf, busHalfWidth;
        bool busParked;

        /// <summary>
        /// Where the camp's things go (clearingArt.ts campPlaces): Soldados' soldier out from a side of the barracks (the
        /// caravan if there is none), the first with nothing built and no pine there and the clearest view of it, its
        /// front first; the squad's row by La Lata (PlaceLineup); the Armería's corner by the caravan (PlaceArmory).
        /// </summary>
        void CampPlaces()
        {
            var b = Base;
            if (b == null) return;
            // The row stands again where it now can (the clearing or La Lata changed: clearingArt.ts relineup).
            place = null;
            if (lineupKey.Length > 0)
            {
                lineupKey = "";
                SetLineup(lineupCampaign, lineupEntries, lineupMirror, lineupHalves);
            }
            var caravan = b.Pieces.FirstOrDefault(p => p.Piece == PieceIds.Caravan);
            var quarters = b.Pieces.FirstOrDefault(p => p.Piece == PieceIds.Quarters && p.Hp > 0);
            bool InBus(float x, float z, float margin)
            {
                if (!busParked) return false;
                float ox = x - busAt.x, oz = z - busAt.y;
                return Mathf.Abs(ox * busDir.x + oz * busDir.y) <= busHalf + margin && Mathf.Abs(ox * busDir.y - oz * busDir.x) <= busHalfWidth + margin;
            }
            bool Free(float x, float z)
            {
                int tx = Mathf.FloorToInt(x), tz = Mathf.FloorToInt(z);
                return tx >= 0 && tz >= 0 && tx < b.Width && tz < b.Height && PieceOn(tx, tz) == null && !Bastion.Engine.Base.TreeAt(b, tx, tz)
                    && !Bastion.Engine.Base.OnBastionLot(b, tx, tz) && !InBus(x, z, 0.7f);
            }
            var footprint = new HashSet<int>(Bastion.Engine.Base.BastionFootprint(b).Select(c => c.Y * b.Width + c.X));
            bool Tall(float x, float z)
            {
                int tx = Mathf.FloorToInt(x), tz = Mathf.FloorToInt(z);
                if (InBus(x, z, 0.3f)) return true;
                if (tx < 0 || tz < 0 || tx >= b.Width || tz >= b.Height) return false;
                if (Bastion.Engine.Base.TreeAt(b, tx, tz) || footprint.Contains(tz * b.Width + tx)) return true;
                var p = PieceOn(tx, tz);
                if (p == null || p.Hp <= 0) return false;
                var def = Bastion.Engine.Base.Pieces[p.Piece];
                return def.Kind == PieceKinds.Building ? def.Tile != "low" : TallPieces.Contains(p.Piece);
            }
            // The clearest way for a camera to look at (x, z) from `out`: turned up to 70° either way and near or far,
            // the fewest tall tiles between them (and none under the camera), the least turned.
            (Vector2 dir, float dist, float cost) ViewOf(float x, float z, Vector2 @out, float far)
            {
                var best = (dir: @out, dist: far, cost: float.PositiveInfinity);
                foreach (float a in new[] { 0, 0.4f, -0.4f, 0.8f, -0.8f, 1.2f, -1.2f })
                {
                    var dir = new Vector2(@out.x * Mathf.Cos(a) - @out.y * Mathf.Sin(a), @out.x * Mathf.Sin(a) + @out.y * Mathf.Cos(a));
                    foreach (float dist in new[] { far, far * 0.75f })
                    {
                        float cost = Mathf.Abs(a) + (dist < far ? 0.5f : 0);
                        if (Tall(x + dir.x * dist, z + dir.y * dist)) cost += 50;
                        for (float t = 0.7f; t < dist; t += 0.25f)
                            if (Tall(x + dir.x * t, z + dir.y * t)) cost += 10;
                        if (cost < best.cost) best = (dir, dist, cost);
                    }
                }
                return best;
            }
            var home = quarters ?? caravan;
            if (home == null) return;
            var cells = home.Piece == PieceIds.Caravan ? Bastion.Engine.Base.BastionFootprint(b) : Bastion.Engine.Base.CellsOf(home).Cast<Vec2>().ToList();
            if (cells.Count == 0) return;
            int x0 = cells.Min(c => c.X), x1 = cells.Max(c => c.X), y0 = cells.Min(c => c.Y), y1 = cells.Max(c => c.Y);
            var mid = new Vector2((x0 + x1 + 1) / 2f, (y0 + y1 + 1) / 2f);
            var half = new Vector2((x1 - x0 + 1) / 2f, (y1 - y0 + 1) / 2f);
            var front = new[] { new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0) }[((home.Rot % 4) + 4) % 4];
            var all = new[] { front, new Vector2(front.y, -front.x), new Vector2(-front.y, front.x), -front };
            bool found = false;
            float bestCost = 0;
            for (int k = 0; k < all.Length; k++)
            {
                var @out = all[k];
                float edge = @out.x != 0 ? half.x : half.y;
                // Out from its side, past its lot's yard if need be.
                float reach = edge + 1.4f;
                while (reach < edge + 5 && !Free(mid.x + @out.x * reach, mid.y + @out.y * reach)) reach += 0.5f;
                float x = mid.x + @out.x * reach, z = mid.y + @out.y * reach;
                if (!Free(x, z)) continue;
                // Its front first, if nothing hides the soldier there.
                var v = ViewOf(x, z, @out, 4);
                float cost = v.cost + k * 0.6f;
                if (found && cost >= bestCost) continue;
                found = true;
                bestCost = cost;
                soldierAt = new Vector2(x, z);
                soldierView = v.dir;
                soldierDistance = v.dist;
            }
            if (showcase != null) showcase.position = World(new Vector3(soldierAt.x, 0, soldierAt.y));
            PlaceArmory(b, Free, ViewOf);
        }

        /// <summary>Which piece stands on each tile of the base shown (Base.PieceAt, made once a base: it is asked thousands of times).</summary>
        BasePiece[] pieceGrid;
        BaseState gridOf;

        BasePiece PieceOn(int x, int y)
        {
            var b = Base;
            if (b == null || x < 0 || y < 0 || x >= b.Width || y >= b.Height) return null;
            if (pieceGrid == null || gridOf != b)
            {
                gridOf = b;
                pieceGrid = new BasePiece[b.Width * b.Height];
                foreach (var p in b.Pieces)
                    foreach (var c in Bastion.Engine.Base.CellsOf(p))
                        if (c.X >= 0 && c.Y >= 0 && c.X < b.Width && c.Y < b.Height && pieceGrid[c.Y * b.Width + c.X] == null) pieceGrid[c.Y * b.Width + c.X] = p;
            }
            return pieceGrid[y * b.Width + x];
        }

        /// <summary>The pieces that stand tall enough to hide a soldier from a camera a storey up (but the buildings, by their tiles).</summary>
        static readonly HashSet<string> TallPieces = new HashSet<string> { "palisade", "wall", "gate", "tower", "floodlight" };

        Vector2Int screenWas;

        void LateUpdate()
        {
            // A fitted shot fits the new size (stage.ts resize).
            var camera = Camera;
            if (camera != null)
            {
                var size = new Vector2Int(camera.pixelWidth, camera.pixelHeight);
                if (size != screenWas)
                {
                    screenWas = size;
                    Refit();
                }
            }
            MoveCamera(Time.deltaTime);
            StepFade(Time.deltaTime);
        }
    }
}
