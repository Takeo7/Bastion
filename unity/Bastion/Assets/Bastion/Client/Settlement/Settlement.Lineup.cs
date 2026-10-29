using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The squad standing by La Lata (0.10.1's clearingArt.ts setLineup, placeLineup, padPosition, lineupShot; the
    /// Escuadra and the debrief): in a row along the bus's side or across the track at its tail, whichever its camera sees
    /// most clearly; one commander's half and then the other's, two rows past eight; each on a ring of their player's
    /// colour (an empty place's grey), with their weapon, each waiting another way; the wounded slumped, the fallen a
    /// cross and a candle. La Lata's doors turned to them.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>
        /// One place of the row (clearingArt.ts LineupEntry): who stands there (a campaign soldier's id), their player's
        /// colour (#rrggbb), and how they came back (the debrief: "ok", "wounded", "dead"; a soldier left behind, "dead").
        /// </summary>
        public sealed class LineupEntry
        {
            public string SoldierId;
            public string Colour;
            public string Status = "ok";
        }

        const int LineupRow = 8;
        const float LineupStep = 0.95f, LineupGap = 0.5f;

        /// <summary>Where a lineup of `count` stands (tiles) and its camera.</summary>
        sealed class LineupPlace
        {
            public int count;
            public bool mirror, halves;
            public List<Vector2> spots;
            public Vector2 toward;
            public Vector3 cam, target;
        }

        List<LineupEntry> lineupEntries = new List<LineupEntry>();
        CampaignState lineupCampaign;
        string lineupKey = "";
        bool lineupMirror, lineupHalves = true;
        LineupPlace place;
        Transform lineupRoot;

        sealed class Stander
        {
            public Transform root;
            public Animator animator;
            public FigureAnimator anim;
            public Transform gun;
            /// <summary>Whether that is the pistol, its only gun (held at rest by the thigh: UnitViews.HoldPistol), not a two-handed one.</summary>
            public bool pistol;
            public Quaternion? grip;
            public float yaw;
            /// <summary>Its build's clips (masc, femn); whether its gun is slung on its back (getting in: Sling), and where on which bone.</summary>
            public string build = "masc";
            public bool slung;
            public Transform slingBone;
            public Vector3 slingAt;
            public Quaternion slingTurn;
        }

        readonly List<Stander> standers = new List<Stander>();

        /// <summary>
        /// The row (null entries: an empty place), in order, left to right as its camera sees them, the first ceil(n / 2)
        /// and then the rest; `mirror`, right to left (the second player's screen: each sees their own on their left);
        /// `halves` false, one row with no step in its middle (a commander alone). The soldiers as `campaign` has them
        /// (their looks, the Armería's weapon). Built again only if it changed; empty clears it. As screen.ts lineup: the
        /// Escuadra's from Campaign.SquadPads, the debrief's from the report's squad.
        /// </summary>
        public void SetLineup(CampaignState campaign, IReadOnlyList<LineupEntry> entries, bool mirror = false, bool halves = true)
        {
            entries = entries ?? new List<LineupEntry>();
            string key = entries.Count == 0 ? "" : Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { mirror, halves, entries.Select(e => e == null ? null : Who(campaign, e)) });
            if (key == lineupKey) return;
            // (Cleared from outside, after boarding: the row made again from nothing, its figures moved or hidden.)
            bool fresh = lineupKey == "";
            lineupKey = key;
            lineupEntries = entries.ToList();
            lineupCampaign = campaign;
            if (mirror != lineupMirror || halves != lineupHalves) place = null;
            lineupMirror = mirror;
            lineupHalves = halves;
            // The pads and graves go; each soldier standing stays by who they are (clearingArt.ts lineupByWho, playtest 21/10 #2:
            // made again, the whole row hopped back into its idle at every change), unless the row starts afresh.
            var before = new Dictionary<string, Stander>(lineupByWho);
            lineupByWho.Clear();
            standers.Clear();
            if (fresh)
            {
                foreach (var s in before.Values) s.anim?.Dispose();
                before.Clear();
            }
            if (lineupRoot != null)
                foreach (Transform child in lineupRoot.Cast<Transform>().ToList())
                    if (!before.Values.Any(s => s.root == child)) DestroyNow(child.gameObject);
            // Its tab's shot for the row as it now is (UI may show the tab before it gives the row).
            if (view == Views.Squad || view == Views.Debrief) needsFrame = true;
            // (Those in it no longer about the camp: the same soldier was in both, clearingArt.ts relife.)
            LifeView();
            if (entries.Count == 0)
            {
                foreach (var s in before.Values) DropStander(s);
                return;
            }
            if (lineupRoot == null)
            {
                lineupRoot = new GameObject("La fila").transform;
                lineupRoot.SetParent(transform, false);
            }
            var row = PlaceLineup(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var at = row.spots[i];
                float facing = Mathf.Atan2(row.cam.x - at.x, row.cam.z - at.y);
                Pad(at, entry != null ? entry.Colour : null);
                if (entry == null) continue;
                var s = campaign?.Soldiers?.FirstOrDefault(x => x.Id == entry.SoldierId);
                if (entry.Status == "dead" || s == null)
                {
                    var cross = Grave(lineupRoot, World(new Vector3(at.x, 0, at.y)), facing);
                    continue;
                }
                string who = Who(campaign, entry);
                float yaw = facing + (i % 3 - 1) * 0.12f;
                if (before.TryGetValue(who, out var kept) && kept.root != null)
                {
                    before.Remove(who);
                    kept.root.position = World(new Vector3(at.x, 0, at.y));
                    kept.yaw = yaw;
                    standers.Add(kept);
                    lineupByWho[who] = kept;
                    continue;
                }
                var made = Stand(s, campaign, i, at, yaw, entry.Status == "wounded", entry.Colour);
                if (made != null) lineupByWho[who] = made;
            }
            // Those no longer in it go.
            foreach (var s in before.Values) DropStander(s);
            StepLineup(1.2f);
            if (needsFrame)
            {
                needsFrame = false;
                Reframe();
            }
        }

        bool needsFrame;

        /// <summary>Who stands on a place, as their figure is made: the soldier, their colour, how they came back, their looks and their gun.</summary>
        static string Who(CampaignState c, LineupEntry e)
        {
            var s = c?.Soldiers?.FirstOrDefault(x => x.Id == e.SoldierId);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { e.SoldierId, e.Colour, e.Status, s?.Appearance, s != null ? Campaign.SoldierArms(c, s)?.Primary : null });
        }

        /// <summary>The row's figures by who each is (Who), kept from one row to the next.</summary>
        readonly Dictionary<string, Stander> lineupByWho = new Dictionary<string, Stander>();

        /// <summary>A place's ring on the ground (0.36 to 0.42 tiles), in its player's colour or an empty place's grey.</summary>
        void Pad(Vector2 at, string colour)
        {
            var ring = new GameObject("sitio");
            ring.transform.SetParent(lineupRoot, false);
            ring.transform.position = World(new Vector3(at.x, 0.03f, at.y));
            ring.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            var r = ring.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var c = colour != null && ColorUtility.TryParseHtmlString(colour, out var parsed) ? parsed : new Color(0x3a / 255f, 0x46 / 255f, 0x52 / 255f);
            r.sharedMaterial = RingMaterial(c);
        }

        static Mesh ring;

        static Mesh RingMesh()
        {
            if (ring != null) return ring;
            const int n = 28;
            float inner = 0.36f * MapSpace.Tile, outer = 0.42f * MapSpace.Tile;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int k = 0; k <= n; k++)
            {
                float a = k * Mathf.PI * 2 / n;
                v.Add(new Vector3(Mathf.Cos(a) * inner, 0, Mathf.Sin(a) * inner));
                v.Add(new Vector3(Mathf.Cos(a) * outer, 0, Mathf.Sin(a) * outer));
                if (k == n) continue;
                int b = k * 2;
                t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            ring = new Mesh { name = "Sitio de la fila", vertices = v.ToArray(), triangles = t.ToArray() };
            ring.RecalculateNormals();
            ring.RecalculateBounds();
            return ring;
        }

        static readonly Dictionary<Color, Material> ringMaterials = new Dictionary<Color, Material>();

        /// <summary>clearingArt.ts: a basic material of the colour, see-through at 0.75, both sides (KitMaterials.SeeThrough).</summary>
        static Material RingMaterial(Color c)
        {
            if (ringMaterials.TryGetValue(c, out var m) && m != null) return m;
            m = KitMaterials.New(KitMaterials.SeeThrough, "Sitio");
            if (m == null) return null;
            m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.75f));
            return ringMaterials[c] = m;
        }

        /// <summary>
        /// How far down a wounded soldier stands in the row (clearingArt.ts tick: crouch 0.4 over the idle; Synty's
        /// «slumped» stance stood them up straight, Ana 28/10). With a rifle, its kneel 0.4 more over the legs, which three's
        /// mixer shares out with the rest (0.8 of 1.4 down) and its lean bends further: 0.7 kneeling, set against 0.10.1's.
        /// </summary>
        const float WoundedCrouch = 0.4f, WoundedKneel = 0.7f;

        /// <summary>A soldier on their place, their weapon in hand, waiting the `index`th way (restAs), slumped if wounded.</summary>
        Stander Stand(CampaignSoldier s, CampaignState campaign, int index, Vector2 at, float yaw, bool wounded, string band)
        {
            var stander = Standing(s, campaign, index, World(new Vector3(at.x, 0, at.y)), yaw, wounded, band, lineupRoot);
            if (stander != null) standers.Add(stander);
            return stander;
        }

        /// <summary>That soldier standing at `world` (metres) under `parent`, the lineup's or the posts' (Settlement.Posts); null without a figure.</summary>
        Stander Standing(CampaignSoldier s, CampaignState campaign, int index, Vector3 world, float yaw, bool wounded, string band, Transform parent)
        {
            var look = s.Appearance;
            string named = look == null || string.IsNullOrEmpty(look.Sex) ? Content.SexOfName(s.Name) : null;
            var dress = Dress.Of(look, s.Id, named);
            GameObject figure;
            try
            {
                figure = SoldierFigure.Make(dress, s.Name, true);
                SquadTint.Paint(figure, look, band);
                FaceLit(figure);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[settlement] {s.Id}: no figure ({e.Message})");
                return null;
            }
            if (figure == null) return null;
            var holder = new GameObject(s.Name).transform;
            holder.SetParent(parent, false);
            holder.position = world;
            figure.transform.SetParent(holder, false);
            figure.transform.localScale = Vector3.one * Figures.CharacterScale;
            string build = dress.sex == "f" ? "femn" : "masc";
            var stander = new Stander { root = holder, yaw = yaw, build = build };
            stander.animator = figure.GetComponentInChildren<Animator>();
            var catalog = FigureCatalog.Load();
            if (stander.animator != null && stander.animator.isHuman && catalog != null)
            {
                stander.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // The Armería's gun, or the pistol where there is none (syntyFigures.ts armamentOf), at rest by the thigh.
                string primary = Campaign.SoldierArms(campaign, s)?.Primary;
                stander.gun = primary == null ? UnitViews.Pistol(holder, catalog) : UnitViews.Gun(Anims.Weapon(Anims.FoundModel(primary)), holder, catalog, out _);
                stander.pistol = primary == null && stander.gun != null;
                bool armed = stander.gun != null && !stander.pistol;
                var rests = Anims.RifleRests;
                string rest = rests.Count > 0 ? rests[((index % rests.Count) + rests.Count) % rests.Count] : "rifleIdle";
                // How it stands: with the pistol its way of waiting (PISTOL_RESTS), else its idle; wounded, part way down to
                // the crouch's idle, kneeling with the rifle (UnitViews: the armed crouch is rifleKneel).
                string still = stander.pistol ? Anims.Stance(build, UnitViews.PistolStance(rest)) : null;
                string ArmedId(string m) => !armed ? null : m == "crouchIdle" ? "rifleKneel" : Anims.Armed("rifle", m) is string a && a != "own" ? a : null;
                stander.anim = new FigureAnimator(stander.animator, m => catalog.Clip(ArmedId(m) ?? (m == "idle" && still != null ? still : Anims.Motion(build, m, false))), null, m => ArmedId(m) != null);
                if (armed) stander.anim.Rest(catalog.Clip(rest), 0);
                if (wounded) stander.anim.Crouch = armed ? WoundedKneel : WoundedCrouch;
                if (stander.pistol) stander.anim.ArmRest(catalog.Clip("pistolRest"), UnitViews.PistolHold, 0);
            }
            return stander;
        }

        /// <summary>The row's breath: each in its pose, facing its camera, its gun in its hands.</summary>
        void StepLineup(float dt)
        {
            foreach (var s in standers) StepStander(s, dt);
        }

        /// <summary>One standing: its turn (three's yaw), its clips on by `dt`, its gun in its hands or on its back.</summary>
        static void StepStander(Stander s, float dt)
        {
            if (s.root == null) return;
            s.root.rotation = Quaternion.Euler(0, Mathf.Atan2(Mathf.Sin(s.yaw), -Mathf.Cos(s.yaw)) * Mathf.Rad2Deg, 0);
            if (s.anim == null) return;
            if (dt <= 0) s.anim.Step(0);
            for (float left = dt; left > 1e-5f; left -= 0.1f) s.anim.Step(Mathf.Min(0.1f, left));
            if (s.slung) KeepSlung(s);
            else if (s.pistol) UnitViews.HoldPistol(s.animator, s.gun, s.root, 0, ref s.grip);
            else UnitViews.Hold(s.animator, s.gun, s.root, false, ref s.grip);
        }

        /// <summary>Where place `i` of a lineup of `n` stands (Unity's world, on the ground: for the cards under them).</summary>
        public Vector3 PadWorld(int i, int n)
        {
            if (n <= 0 || Base == null) return Vector3.zero;
            var spots = PlaceLineup(n).spots;
            var at = i >= 0 && i < spots.Count ? spots[i] : Vector2.zero;
            return World(new Vector3(at.x, 0, at.y));
        }

        /// <summary>That place on `camera`'s screen (pixels, from its bottom left, as Camera.WorldToScreenPoint); null behind it.</summary>
        public Vector2? PadScreen(int i, int n, Camera camera)
        {
            camera = camera != null ? camera : Camera;
            if (camera == null) return null;
            var p = camera.WorldToScreenPoint(PadWorld(i, n));
            return p.z > 0 ? new Vector2(p.x, p.y) : (Vector2?)null;
        }

        /// <summary>
        /// The squad's row by La Lata, from out in the clearing (lineupShot), kept whole in what the panels leave free and
        /// in the middle of the screen across (screen.ts framedLineup): each place's width over the heads, and on the
        /// ground before them the room their cards take.
        /// </summary>
        Shot LineupShot()
        {
            int n = Mathf.Max(1, lineupKey.Length > 0 ? lineupEntries.Count : 4);
            var row = PlaceLineup(n);
            var shot = new Shot { pos = row.cam, target = row.target, fov = 38, seen = row.spots.SelectMany(p => new[] { new Vector3(p.x, 0.3f, p.y), new Vector3(p.x, 1.3f, p.y) }).ToList() };
            var toward = new Vector3(shot.pos.x - shot.target.x, 0, shot.pos.z - shot.target.z).normalized;
            var side = new Vector3(-toward.z, 0, toward.x);
            var points = new List<Vector3>();
            foreach (var p in row.spots)
                foreach (float s in new[] { -0.7f, 0.7f })
                {
                    var q = new Vector3(p.x, 0, p.y) + side * s;
                    points.Add(new Vector3(q.x, 2.3f, q.z));
                    points.Add(q + toward * 1.8f);
                }
            if (view == Views.Debrief) return Closer(shot, row.spots, DebriefStep(n), 210, true);
            return Closer(Fitted(shot, points, true), row.spots, PlacePixels, 120, false);
        }

        /// <summary>
        /// How wide each place of the debrief's row is on screen (px at 1440 across), as 0.10.1 framed it (UI, 25/10: 132
        /// with 8, 144 with 6, 155 with 4): its cards are taller than the Escuadra's (what a soldier learnt) and stand
        /// under each place, on the panel's top.
        /// </summary>
        static float DebriefStep(int n)
        {
            int front = n > LineupRow ? Mathf.CeilToInt(n / 2f) : n;
            return front >= 8 ? 132 : front == 7 ? 138 : front == 6 ? 144 : front == 5 ? 150 : 155;
        }

        /// <summary>How wide on screen each place of the row is at least (px at 1440 across, and as much of another width): its card's (UI, 25/10: the debrief's 8 overlapped, the figures tiny).</summary>
        const float PlacePixels = 110;

        /// <summary>
        /// The row's shot brought in until each place is `placePixels` across (at 1440: as much of another width), if
        /// fitting it left them narrower (`always`: whatever it was), its places then in the middle across and their feet
        /// `roomPixels` (at 900 high) over the panels below: the room their cards take under them.
        /// </summary>
        Shot Closer(Shot shot, List<Vector2> spots, float placePixels, float roomPixels, bool always)
        {
            var camera = Camera;
            if (!FitShots || camera == null || spots == null || spots.Count < 2) return shot;
            var size = SizeOf(camera);
            float w = size.x, h = size.y;
            if (w <= 0 || h <= 0) return shot;
            float want = placePixels * w / 1440;
            var inset = InsetsOn(camera);
            float free0 = inset.bottom / h * 2 - 1, cardRoom = roomPixels / 900 * 2;
            float left = inset.left + 12, right = inset.right + 12;
            left = right = Mathf.Max(left, right);
            float middleX = ((left / w * 2 - 1) + (1 - right / w * 2)) / 2;
            var pads = spots.Select(p => World(new Vector3(p.x, 0, p.y))).ToList();
            var target = World(shot.target);
            var back = World(shot.pos) - target;
            float distance = back.magnitude;
            back /= Mathf.Max(distance, 1e-4f);
            var was = (camera.transform.position, camera.transform.rotation, camera.fieldOfView);
            camera.fieldOfView = shot.fov;
            camera.aspect = w / h;
            bool moved = false;
            for (int round = 0; round < 10; round++)
            {
                camera.transform.position = target + back * distance;
                camera.transform.LookAt(target);
                var screen = pads.Select(p => camera.WorldToViewportPoint(p)).ToList();
                // The narrowest step between neighbours (the row's halves have a gap between them).
                float step = float.PositiveInfinity;
                for (int i = 0; i + 1 < screen.Count; i++) step = Mathf.Min(step, Mathf.Abs(screen[i + 1].x - screen[i].x) * w);
                // (Behind the camera, or on top of each other: nothing to go by.)
                if (!(step > 1e-3f) || float.IsInfinity(step) || screen.Any(s => s.z <= 0)) break;
                if (!moved && !always && step >= want * 0.98f) break;
                if (always || step < want * 0.98f) distance *= step / want;
                moved = true;
                // In the middle across; on the panels below with a card's room.
                float x0 = screen.Min(s => s.x * 2 - 1), x1 = screen.Max(s => s.x * 2 - 1), y0 = screen.Min(s => s.y * 2 - 1);
                float half = Mathf.Tan(shot.fov / 2 * Mathf.Deg2Rad) * distance;
                target += camera.transform.right * (((x0 + x1) / 2 - middleX) * half * camera.aspect);
                target += camera.transform.up * ((y0 - (free0 + cardRoom)) * half);
            }
            camera.transform.SetPositionAndRotation(was.position, was.rotation);
            camera.fieldOfView = was.fieldOfView;
            camera.ResetAspect();
            var closer = new Shot { pos = Tiles(target + back * distance), target = Tiles(target), fov = shot.fov, seen = shot.seen };
            return moved && Finite(closer.pos) && Finite(closer.target) ? closer : shot;
        }

        /// <summary>
        /// Where a lineup of `count` stands, and its camera: along either side of La Lata (its doors turned to them) or
        /// across the track behind it; each seen from straight out to 50° aside, near enough to fill the frame or a little
        /// further. The one where the fewest pines and buildings come between the camera and the row, and nothing stands
        /// under the row, the camera least turned. Without La Lata, in front of the caravan.
        /// </summary>
        LineupPlace PlaceLineup(int count)
        {
            if (place != null && place.count == count && place.mirror == lineupMirror && place.halves == lineupHalves) return place;
            var b = Base;
            bool parked = Lata != null && Lata.Shown;
            float heading = parked ? Lata.Heading : 0;
            var dir = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
            var side = new Vector2(dir.y, -dir.x);
            var lot = parked || b == null ? null : Bastion.Engine.Base.BastionLot(b);
            var at = parked ? new Vector2(Lata.At.x, Lata.At.z) : lot != null ? new Vector2(lot.X + lot.W / 2f, lot.Y + lot.H + 1.5f) : b != null ? new Vector2(b.Width / 2f, b.Height / 2f + 5) : Vector2.zero;
            // (The bus it starts with by the size 0.10.1 had for it when the camp was placed, before its model was built:
            // Park's 7 by 1.7.)
            float length = parked ? (Lata.Vehicle == 3 ? 7 : Lata.Length) : 0;
            float reach = parked ? (Lata.Vehicle == 3 ? 1.7f : Lata.Width) / 2 + 0.95f : 0;
            var footprint = new HashSet<int>(b != null && Bastion.Engine.Base.BastionStage(b) > 0 ? Bastion.Engine.Base.BastionFootprint(b).Select(c => c.Y * b.Width + c.X) : Enumerable.Empty<int>());
            float Height(float x, float z, bool crowns = true)
            {
                if (b == null) return 0;
                int tx = Mathf.FloorToInt(x), tz = Mathf.FloorToInt(z);
                if (tx < 0 || tz < 0 || tx >= b.Width || tz >= b.Height) return 0;
                if (Bastion.Engine.Base.TreeAt(b, tx, tz)) return 4.5f;
                // A pine's crown spreads over the tiles round its trunk, from a storey or so up.
                if (crowns)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = tx + dx, nz = tz + dz;
                            if (nx >= 0 && nz >= 0 && nx < b.Width && nz < b.Height && Bastion.Engine.Base.TreeAt(b, nx, nz)) return 2.5f;
                        }
                if (footprint.Contains(tz * b.Width + tx)) return 3;
                var p = PieceOn(tx, tz);
                if (p == null || p.Hp <= 0) return 0;
                var def = Bastion.Engine.Base.Pieces[p.Piece];
                return def.Kind == PieceKinds.Building ? (def.Tile == "low" ? 0.8f : 3) : TallPieces.Contains(p.Piece) ? 2.2f : 0.6f;
            }
            int rows = count > LineupRow ? 2 : 1;
            int front = rows == 2 ? Mathf.CeilToInt(count / 2f) : count;
            float gap = lineupHalves && count > 1 ? LineupGap : 0;
            float halfWidth = (rows == 1 ? (count - 1) / 2f * LineupStep + gap / 2 : (front - 1) / 2f * LineupStep + LineupStep / 2) + 0.7f;
            // Along either side (also moved up towards the nose, away from the Bastion's lot behind it), or across the
            // track at its tail.
            var candidates = new List<(Vector2 centre, Vector2 toward, int k, float shift)>();
            foreach (float shift in new[] { 0, 0.6f, 1.2f })
            {
                candidates.Add((at + side * reach + dir * shift, side, 0, shift));
                candidates.Add((at - side * reach + dir * shift, -side, 1, shift));
            }
            candidates.Add((at - dir * (length / 2 + 1.3f), -dir, 2, 0));
            LineupPlace best = null;
            float bestCost = float.PositiveInfinity;
            foreach (var c in candidates)
            {
                // Left to right as the camera sees them (right to left, mirrored); the second row a step behind, between the first's.
                var right = lineupMirror ? new Vector2(-c.toward.y, c.toward.x) : new Vector2(c.toward.y, -c.toward.x);
                var spots = new List<Vector2>();
                for (int i = 0; i < count; i++)
                {
                    float u, v = 0;
                    if (rows == 1) u = (i - (count - 1) / 2f) * LineupStep + (i < Mathf.CeilToInt(count / 2f) ? -gap / 2 : gap / 2);
                    else
                    {
                        int back = i >= front ? 1 : 0;
                        int n = back == 1 ? count - front : front;
                        u = ((back == 1 ? i - front : i) - (n - 1) / 2f) * LineupStep + back * (LineupStep / 2);
                        v = -back * 0.95f;
                    }
                    spots.Add(c.centre + right * u + c.toward * v);
                }
                // Nobody stands on anything built, a pine, the Bastion or its yard (its lot), nor off the clearing.
                float standing = (c.k == 2 ? 0.5f : 0) + c.shift * 2;
                foreach (var p in spots)
                    if (Height(p.x, p.y, false) > 0 || (b != null && (Bastion.Engine.Base.OnBastionLot(b, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)) || p.x < 0 || p.y < 0 || p.x >= b.Width || p.y >= b.Height)))
                        standing += 20;
                float near = Mathf.Max(5.5f, halfWidth / 0.42f);
                var middle = c.centre - c.toward * ((rows - 1) * 0.45f);
                foreach (float a in new[] { 0, 0.3f, -0.3f, 0.6f, -0.6f, 0.9f, -0.9f })
                {
                    var d = new Vector2(c.toward.x * Mathf.Cos(a) - c.toward.y * Mathf.Sin(a), c.toward.x * Mathf.Sin(a) + c.toward.y * Mathf.Cos(a));
                    foreach (float dist in new[] { near, near + 1.5f, near + 3 })
                        foreach (float lift in new[] { 1, 1.5f, 2 })
                        {
                            // From higher up, over the pines, rather than turned.
                            float y = (1.8f + dist * 0.3f) * lift;
                            var cam = middle + d * dist;
                            // Seen from straight out unless the way is blocked.
                            float cost = standing + Mathf.Abs(a) * 25 + (dist - near) * 0.4f + (lift - 1) * 3;
                            if (Height(cam.x, cam.y) > y - 0.6f) cost += 60;
                            // What comes between the camera and the row's middle, and (half as much: at the frame's edge) its ends.
                            foreach (var (q, weight) in new[] { (middle, 6f), (middle + right * halfWidth, 3f), (middle - right * halfWidth, 3f) })
                                for (float t = 0.8f; t < dist; t += 0.4f)
                                {
                                    float f = t / dist;
                                    if (Height(q.x + (cam.x - q.x) * f, q.y + (cam.y - q.y) * f) > 0.9f + (y - 0.9f) * f) cost += weight;
                                }
                            if (cost < bestCost)
                            {
                                bestCost = cost;
                                best = new LineupPlace { count = count, mirror = lineupMirror, halves = lineupHalves, spots = spots, toward = c.toward, cam = new Vector3(cam.x, y, cam.y), target = new Vector3(middle.x, 0.5f, middle.y) };
                            }
                        }
                }
            }
            // La Lata's doors to the squad.
            if (parked && best != null && count > 0)
            {
                var mid = Vector2.Lerp(best.spots[0], best.spots[count - 1], 0.5f);
                Lata.Park(Lata.At, heading, new Vector3(mid.x, 0, mid.y), true, 0, false);
            }
            return place = best;
        }
    }
}
