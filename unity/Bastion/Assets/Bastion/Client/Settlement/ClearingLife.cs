using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Bastion.Client.Map;
using Bastion.Engine;
using BigInteger = System.Numerics.BigInteger;
using Trig = Bastion.Client.Map.HomeFurnishing.Trig;

namespace Bastion.Client
{
    // 0.10.1's clearingLife.ts: the clearing with life (Arturo, 22/10 #1). The soldiers at the base out about it while the
    // Asentamiento is open, at their ease, their guns slung. Some stand about (arms folded, hands on hips, a hand at the
    // chin, or the plain idle with its looks round); two stand talking face to face, now one and now the other saying
    // something with a gesture while the other nods; one walks from one place to another between the buildings and stays
    // a while at each. They keep off the pieces, the pines, the Bastion, La Lata and whatever else the clearing says
    // (`taken`), and walk off what is being placed (KeepClear). A handful at most (LifeMax).
    // And the camp (22/10, Gameplay): round the fire, sat on its camp chairs or on the ground, one knelt warming the hands;
    // one leant on La Lata; the one walking about goes and sits on a free chair or leans on La Lata a while; those standing
    // about now and then drink from a cup or eat a ration bar. Sitting down and getting up, leaning and coming off, are
    // clips of their own (the figure's CampMove), so nobody jumps from one to the other.
    // The same people in the same places as 0.10.1 for the same soldiers, to the bit: its seeded random off the list's
    // JSON (LifeEntry.Key is JSON.stringify's), sines and cosines V8's (Trig), atan2 and hypot Js.cs's, three's math in
    // doubles. Math.random and performance.now come in (the constructor's `random` and `now`). The figures are Unity's,
    // behind ICampFigure; positions and turns are 0.10.1's (tiles: x east, z south; yaw three's, from +z towards +x).
    // No Mathf, no scene: it runs in a plain dotnet harness (bport/life).

    /// <summary>
    /// A soldier out about the clearing (0.10.1's clearingArt.ts LineupEntry, the fields the life reads; screen.ts
    /// lifeEntries makes them): its template, its player's colour (#rrggbb), its looks, its gear and its campaign id. A
    /// null field is one not there (JSON.stringify leaves it out), but Arms.Primary and Arms.Melee, written as null. Not
    /// to be changed once given to ClearingLife.Set.
    /// </summary>
    public sealed class LifeEntry
    {
        public string Template;
        public string Colour;
        public Appearance Appearance;
        public LifeGear Gear;
        public string Seed;

        /// <summary>
        /// Its JSON as 0.10.1's JSON.stringify(entry) wrote it (the same figure for the same key, and the life's seed off
        /// them): template, color, appearance, gear, seed, in that order; the looks' fields in defaultAppearance's and
        /// normalizeAppearance's order, the gear's in gearFor's, the arms' in soldierArms'; numbers and strings as
        /// JavaScript writes them.
        /// </summary>
        public string Key
        {
            get
            {
                var sb = new StringBuilder();
                Write(sb);
                return sb.ToString();
            }
        }

        internal void Write(StringBuilder sb)
        {
            var o = new JsonObject(sb);
            o.String("template", Template);
            o.String("color", Colour);
            if (Appearance != null)
            {
                var a = o.Object("appearance");
                a.String("primary", Appearance.Primary);
                a.String("secondary", Appearance.Secondary);
                a.String("visor", Appearance.Visor);
                a.String("head", Appearance.Head);
                a.String("build", Appearance.Build);
                a.String("sex", Appearance.Sex);
                a.String("helmet", Appearance.Helmet);
                a.String("torso", Appearance.Torso);
                a.String("arms", Appearance.Arms);
                a.String("legs", Appearance.Legs);
                a.String("character", Appearance.Character);
                a.End();
            }
            if (Gear != null)
            {
                var g = o.Object("gear");
                g.Number("weapon", Gear.Weapon);
                g.Number("armor", Gear.Armor);
                if (Gear.Arms != null)
                {
                    var arms = Gear.Arms;
                    var m = g.Object("arms");
                    m.StringOrNull("primary", arms.Primary);
                    m.String("primaryId", arms.PrimaryId);
                    m.Number("condition", arms.Condition);
                    if (arms.Mods != null)
                    {
                        m.Name("mods");
                        sb.Append('[');
                        for (int i = 0; i < arms.Mods.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            if (arms.Mods[i] == null) sb.Append("null");
                            else ClearingLife.Quote(sb, arms.Mods[i]);
                        }
                        sb.Append(']');
                    }
                    m.StringOrNull("melee", arms.Melee);
                    m.String("meleeId", arms.MeleeId);
                    m.End();
                }
                g.End();
            }
            o.String("seed", Seed);
            o.End();
        }

        /// <summary>An object being written: its fields in turn, a comma between them.</summary>
        readonly struct JsonObject
        {
            readonly StringBuilder sb;
            readonly int start;

            public JsonObject(StringBuilder sb)
            {
                this.sb = sb;
                sb.Append('{');
                start = sb.Length;
            }

            public void Name(string name)
            {
                if (sb.Length > start) sb.Append(',');
                ClearingLife.Quote(sb, name);
                sb.Append(':');
            }

            public void String(string name, string value)
            {
                if (value == null) return;
                Name(name);
                ClearingLife.Quote(sb, value);
            }

            public void StringOrNull(string name, string value)
            {
                Name(name);
                if (value == null) sb.Append("null");
                else ClearingLife.Quote(sb, value);
            }

            public void Number(string name, double? value)
            {
                if (value == null) return;
                Name(name);
                sb.Append(ClearingLife.JsNumber(value.Value));
            }

            public JsonObject Object(string name)
            {
                Name(name);
                return new JsonObject(sb);
            }

            public void End() => sb.Append('}');
        }
    }

    /// <summary>A soldier's gear (0.10.1's figures.ts Gear, as screen.ts gearFor makes it): the weapon's and the armour's tiers (1–3), and its own weapons (the engine's soldierArms).</summary>
    public sealed class LifeGear
    {
        public int? Weapon;
        public int? Armor;
        public Arms Arms;
    }

    /// <summary>
    /// A figure out about the clearing, as 0.10.1's clearingLife.ts drove its rig (syntyFigures.ts SyntyFigure, figures.ts
    /// FigureRig): what each call asked of it. ClearingLife calls them in 0.10.1's order, every frame SetPosition, SetStance,
    /// Camp, HoldItem and ApplyPose for each, after what that frame's life does. Ids are 0.10.1's (syntyManifest.ts: Anims
    /// has their clips).
    /// </summary>
    public interface ICampFigure
    {
        /// <summary>Where it stands (rig.root.position.set(x, 0, z)): tiles in 0.10.1's world (x east, z south), on the ground.</summary>
        void SetPosition(double x, double z);

        /// <summary>sling(on): its weapons all on it, the hands free (at ease on the clearing); false, the one drawn in the hand again. Called with true as it comes out.</summary>
        void Sling(bool on);

        /// <summary>
        /// setGait(motion, speed): what it plays moving (ApplyPose's run 1) and how fast, its clip's own speed times `speed`
        /// (gaits.ts clipSpeed: its feet keep pace with ClearingLife's walk, 1.15 tiles a second). Here always 'walk'.
        /// </summary>
        void SetGait(string motion, double speed);

        /// <summary>
        /// campMove(move): plays the camp's move `move` once (syntyManifest.ts CAMP_MOVES: 'sitDown', 'standUp', 'leanLeftIn',
        /// 'leanLeftOut', 'leanRightIn', 'leanRightOut', 'kneelDown', 'kneelUp', 'drinkIn', 'drinkOut', 'sip', 'eatIn',
        /// 'eatOut', 'bite') over what it does, at its own speed, holding its last frame once played; the same move again
        /// goes on with it (it is called every frame while it plays). Returns how far through it is, 0 to 1 (1 without its
        /// clips: the life takes it as played). null lets go of it (eased out: the hips back over the feet as it goes);
        /// what it returns then is not read.
        /// </summary>
        double CampMove(string move);

        /// <summary>
        /// camp(loop, on, now): stays in the camp's loop `loop` (CAMP_LOOPS: 'sitChair', 'sitChairStill', 'sitBreathe',
        /// 'sitCrossed', 'sitTalk', 'sitFloor', 'warmHands', 'leanLeft', 'leanRight', 'drink', 'eat') instead of how it
        /// stands; null, as it stands. `on`: standing, it does the camp's things now and then (CAMP_VARIATIONS: stretches,
        /// yawns, looks at the time or the sky); false, none of them (those talking). `now`: into the loop at once, without
        /// easing (where the move into it left the body). Called every frame; a change only when loop or on change.
        /// </summary>
        void Camp(string loop, bool on, bool now);

        /// <summary>gesture(g): a gesture once over how it stands (GESTURES: 'point', 'shrug', 'calm', 'chin', 'nod'); whether it began (false while another plays, or without clips).</summary>
        bool Gesture(string gesture);

        /// <summary>
        /// holdItem(id, upright): holds the camp's thing `id` in the right hand ('campCup', 'campBar': CAMP_ITEMS), where a
        /// gun's grip goes, its weapons slung; `upright` kept level whatever way the fist turns (the cup); null, nothing.
        /// Called every frame.
        /// </summary>
        void HoldItem(string item, bool upright);

        /// <summary>setStance(stance): how it stands waiting (STANCES: 'armsFolded', 'handsOnHips', 'thoughtful'); null the plain idle with its looks round. Called every frame.</summary>
        void SetStance(string stance);

        /// <summary>
        /// applyPose({ ...IDLE_POSE, yaw, phase, run }, time): turned `yaw` about the vertical (three's: from +z towards +x),
        /// standing (run 0) or walking (run 1: SetGait's clip), `phase` its own offset into its idle's clips, at `time`
        /// (seconds, the frame's: what its clips run on). Every other pose value 0. Called every frame, last.
        /// </summary>
        void ApplyPose(double yaw, double phase, double run, double time);
    }

    /// <summary>The soldiers out about the base's clearing at their ease, and the camp's life (0.10.1's clearingLife.ts ClearingLife).</summary>
    public sealed class ClearingLife
    {
        /// <summary>How many at most, and how far from the clearing's middle they keep (tiles).</summary>
        public const int LifeMax = 6;
        const double LifeReach = 9;
        /// <summary>A walk's pace (tiles a second), and how long one stays at either end of its walk (seconds, from and to); sat, longer.</summary>
        public const double Walk = 1.15;
        static readonly (double from, double to) Stay = (4, 9);
        static readonly (double from, double to) Sat = (9, 16);
        /// <summary>How far apart two talking stand (tiles), and the seconds between one's gestures and the other's.</summary>
        const double TalkApart = 1.05;
        static readonly (double from, double to) TalkEvery = (2.2, 4.5);
        /// <summary>How fast one turns to where it goes (radians a second), and how near it is turned enough to sit down.</summary>
        const double Turn = 5;
        const double Turned = 0.12;
        /// <summary>Seconds one sat on the ground takes to get up before walking off (no clip for it: eased).</summary>
        const double Rise = 0.8;
        /// <summary>What the ones standing about stand in, and what the talkers do.</summary>
        static readonly string[] Stands = { "armsFolded", "handsOnHips", null, "thoughtful", "armsFolded", null };
        static readonly string[] Says = { "point", "shrug", "calm", "chin", "point" };
        /// <summary>
        /// Round a fire: how far a camp chair's sitter sits ahead of its middle (its hips over the seat), and how far ahead
        /// of that it stands to sit down and gets up to (Mixamo's sitting down and getting up move the hips that far).
        /// </summary>
        const double SeatAhead = 0;
        const double SitShift = 0.32;
        /// <summary>How far out on the ground the others sit, and the one knelt warming the hands kneels; how far apart they keep.</summary>
        const double FireRing = 1.15;
        const double FireKneel = 1;
        const double FireApart = 0.9;
        /// <summary>The places round a fire tried for those on the ground (SeatRound), as many round it.</summary>
        const int RoundSteps = 24;
        /// <summary>How far out behind one on the ground by a fire is kept clear too: the back and the gun slung on it.</summary>
        const double FireBack = 0.3;
        /// <summary>
        /// The camera looks from 45° and in 90° steps from there: two round a fire in a line with one of those looks are one
        /// behind the other. How far apart across a look they are enough (tiles), and how much that weighs against how far
        /// round apart they are.
        /// </summary>
        const double ViewsFrom = Math.PI / 4;
        const double SideBy = 0.6;
        const double ViewsWeigh = 3;
        /// <summary>How far round each fire the rest keep off (those round it sit within it).</summary>
        const double FireCircle = 1.7;
        /// <summary>How near the Camposanto's tiles a fire's circle may come (tiles): nobody sits warming the hands among the graves.</summary>
        const double GravesOff = 0.6;
        /// <summary>How far from a fire's middle one stands at least to sit down by it (its stones and flames).</summary>
        const double FireClear = 0.6;
        static readonly string[] OnChair = { "sitChair", "sitChairStill", "sitBreathe", "sitCrossed" };
        /// <summary>(Not reclined: lying back with the feet to the stones it read as a body.)</summary>
        static readonly string[] OnGround = { "sitFloor" };
        /// <summary>
        /// Those standing about with no way of standing of their own drink from a metal cup or eat a ration bar now and then
        /// (CAMP_ITEMS): the seconds between, how long they keep at it, and between one sip or bite and the next.
        /// </summary>
        static readonly (double from, double to) HabitEvery = (10, 24);
        static readonly (double from, double to) HabitFor = (8, 14);
        static readonly (double from, double to) HabitSip = (2.5, 5);
        static readonly Dictionary<string, (string into, string @out, string once)> HabitMoves = new Dictionary<string, (string, string, string)>
        {
            ["drink"] = ("drinkIn", "drinkOut", "sip"),
            ["eat"] = ("eatIn", "eatOut", "bite"),
        };
        /// <summary>syntyManifest.ts CAMP_ITEMS: what each habit holds.</summary>
        static readonly Dictionary<string, string> CampItems = new Dictionary<string, string> { ["drink"] = "campCup", ["eat"] = "campBar" };
        /// <summary>Leant on La Lata: a shoulder to it (the back would put the slung gun through it), the left (1) or the right (−1).</summary>
        static (string loop, string into, string @out) Leans(int side) => side == 1 ? ("leanLeft", "leanLeftIn", "leanLeftOut") : ("leanRight", "leanRightIn", "leanRightOut");

        /// <summary>
        /// gaits.ts clipSpeed(template, { motion: 'walk', seconds: 1 / Walk }): the speed the walk plays at for the feet to
        /// keep pace (TILES_PER_METRE, SYNTY_SCALE × CHARACTER_SCALE; the walk's metres a second, the dead's or the living's).
        /// </summary>
        static double WalkClipSpeed(string template)
        {
            const double tilesPerMetre = 1 / 1.5 * (1.35 / 1.2);
            bool dead = template == "zombie" || template == "xenoid" || template == "mec" || template == "sectoid" || template == "bloater";
            return 1 / (tilesPerMetre * (dead ? 1.78 : 1.46) * (1 / Walk));
        }

        // ------------------------------------------------------------------ the camp

        /// <summary>A point on the ground (tiles: x east, z south). Which one it is counts, as 0.10.1's objects did (===): never changed.</summary>
        public class Vec
        {
            public readonly double x, z;

            public Vec(double x, double z)
            {
                this.x = x;
                this.z = z;
            }
        }

        /// <summary>A camp chair: where it stands and its turn (three's yaw).</summary>
        public sealed class Seat : Vec
        {
            public readonly double yaw;

            public Seat(double x, double z, double yaw) : base(x, z) => this.yaw = yaw;
        }

        /// <summary>A place along La Lata to lean at: facing along it (yaw), the bus on its left (side 1) or its right (−1).</summary>
        public sealed class Wall : Vec
        {
            public readonly double yaw;
            public readonly int side;

            public Wall(double x, double z, double yaw, int side) : base(x, z)
            {
                this.yaw = yaw;
                this.side = side;
            }
        }

        /// <summary>A box in a prop's own frame (three's Box3: min and max), in tiles; empty, min above max.</summary>
        public readonly struct PropBox
        {
            public readonly double minX, minY, minZ, maxX, maxY, maxZ;

            public PropBox(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
            {
                this.minX = minX;
                this.minY = minY;
                this.minZ = minZ;
                this.maxX = maxX;
                this.maxY = maxY;
                this.maxZ = maxZ;
            }

            /// <summary>Box3.isEmpty.</summary>
            public bool IsEmpty => maxX < minX || maxY < minY || maxZ < minZ;

            /// <summary>A new Box3: nothing in it (a prop not loaded).</summary>
            public static readonly PropBox Empty = new PropBox(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
        }

        /// <summary>
        /// Something else standing about the camp, for none to sit in it: its box (the prop's, in its own frame) and the
        /// inverse of the matrix that places it (three's Matrix4 elements, column by column), and that matrix's largest
        /// scale (getMaxScaleOnAxis).
        /// </summary>
        public sealed class Thing
        {
            public readonly double[] inverse;
            public readonly PropBox box;
            public readonly double scale;

            public Thing(double[] inverse, PropBox box)
            {
                this.inverse = inverse;
                this.box = box;
                var te = inverse;
                double x = te[0] * te[0] + te[1] * te[1] + te[2] * te[2];
                double y = te[4] * te[4] + te[5] * te[5] + te[6] * te[6];
                double z = te[8] * te[8] + te[9] * te[9] + te[10] * te[10];
                scale = Math.Sqrt(Math.Max(Math.Max(x, y), z));
            }
        }

        /// <summary>
        /// The clearing's camp (0.10.1's clearingLife.ts Camp, clearingArt.ts campOf): its fires, the chairs by them, what
        /// else stands about them, and the places along La Lata one can lean at.
        /// </summary>
        public sealed class Camp
        {
            public readonly List<Vec> fires = new List<Vec>();
            public readonly List<Seat> seats = new List<Seat>();
            public readonly List<Thing> things = new List<Thing>();
            public readonly List<Wall> walls = new List<Wall>();

            /// <summary>
            /// clearingArt.ts campOf's pieces: every piece of `s` (clearingState's: the base as its siege has it, nobody on
            /// it) drawn (BasePieces.Placements) with the props measured by their boxes (Measure); each fire pit a fire, each
            /// camp chair a seat (its matrix decomposed: where it stands, its turn's yaw 'YXZ'), every other prop with a box
            /// a thing. `box`: a prop's parts' box in its own frame, as loaded (tiles; PropBox.Empty if it isn't). The
            /// matrices are BasePieces' floats, decomposed and inverted in doubles as three did.
            /// </summary>
            public static Camp Of(GameState s, Func<string, PropBox> box)
            {
                var camp = new Camp();
                foreach (var p in BasePieces.Placements(s, (id) => Measure(box(id))))
                {
                    var te = Elements(p.matrix);
                    if (p.id == "bFirePit") camp.fires.Add(new Vec(te[12], te[14]));
                    else if (p.id == "bCampChair") camp.seats.Add(new Seat(te[12], te[14], YawOf(te)));
                    else
                    {
                        var b = box(p.id);
                        if (!b.IsEmpty) camp.things.Add(new Thing(Invert(te), b));
                    }
                }
                return camp;
            }

            /// <summary>What campOf measures a prop by (PieceArt top and size): its box's top (`max.y || 0.3`), its size ((1, 1, 1) empty).</summary>
            public static PropMeasure Measure(PropBox b)
            {
                double top = b.maxY == 0 || double.IsNaN(b.maxY) ? 0.3 : b.maxY;
                return b.IsEmpty ? new PropMeasure(top, 1, 1, 1) : new PropMeasure(top, b.maxX - b.minX, b.maxY - b.minY, b.maxZ - b.minZ);
            }

            /// <summary>clearingArt.ts LEAN_OFF, LEAN_ALONG: how far out of La Lata's side one leans (tiles), and where along it (of its length, from its middle).</summary>
            const double LeanOff = 0.46;
            static readonly double[] LeanAlong = { -0.1, 0.1 };

            /// <summary>
            /// clearingArt.ts campOf's La Lata: places along its long sides to lean at, a shoulder to it, a tenth of its
            /// length from its middle either way, but where the squad's row stands (`row`, its places: none nearer than 1.4).
            /// La Lata parked at (x, z), turned `yaw` (its root's rotation.y), `length` and `width` long and wide; only while
            /// it is in sight and not away.
            /// </summary>
            public void AddBusSides(double x, double z, double yaw, double length, double width, IEnumerable<Vec> row) =>
                AddBusSides(x, z, yaw, length, (k, side) => width / 2 + LeanOff, row);

            /// <summary>
            /// The same, how far out from its middle across one leaning stands given by `reach(k, side)` (`k` tiles along it,
            /// on its `side`) instead of half its width and LeanOff: Unity's bus, its plates and doorways, and the body's own
            /// reach leant (Settlement.Life).
            /// </summary>
            public void AddBusSides(double x, double z, double yaw, double length, Func<double, int, double> reach, IEnumerable<Vec> row)
            {
                double dirX = Trig.Sin(yaw), dirZ = Trig.Cos(yaw);
                double acrossX = dirZ, acrossZ = -dirX;
                var spots = row?.ToList() ?? new List<Vec>();
                foreach (int side in new[] { 1, -1 })
                {
                    foreach (double along in LeanAlong)
                    {
                        double k = along * length;
                        double @out = side * reach(k, side);
                        double px = x + dirX * k + acrossX * @out, pz = z + dirZ * k + acrossZ * @out;
                        if (spots.Any((q) => Js.Hypot(q.x - px, q.z - pz) < 1.4)) continue;
                        // Facing along it with it on the left (side 1, `across` to its left) or the right.
                        double face = Js.Atan2(dirX, dirZ) + (along > 0 ? 0 : Math.PI);
                        double leftX = Trig.Cos(face), leftZ = -Trig.Sin(face);
                        int wallSide = leftX * acrossX * side + leftZ * acrossZ * side < 0 ? 1 : -1;
                        walls.Add(new Wall(px, pz, face, wallSide));
                    }
                }
            }

            /// <summary>A matrix's floats as three's elements (column by column), in doubles.</summary>
            static double[] Elements(UnityEngine.Matrix4x4 m) => new double[]
            {
                m.m00, m.m10, m.m20, m.m30, m.m01, m.m11, m.m21, m.m31, m.m02, m.m12, m.m22, m.m32, m.m03, m.m13, m.m23, m.m33,
            };

            /// <summary>`m.decompose(at, turn, scale)` and then `new Euler().setFromQuaternion(turn, 'YXZ').y`.</summary>
            static double YawOf(double[] te)
            {
                double qx = 0, qy = 0, qz = 0, qw = 1;
                double det = te[0] * (te[5] * te[10] - te[9] * te[6]) - te[4] * (te[1] * te[10] - te[9] * te[2]) + te[8] * (te[1] * te[6] - te[5] * te[2]);
                if (det != 0)
                {
                    double sx = Math.Sqrt(te[0] * te[0] + te[1] * te[1] + te[2] * te[2]);
                    double sy = Math.Sqrt(te[4] * te[4] + te[5] * te[5] + te[6] * te[6]);
                    double sz = Math.Sqrt(te[8] * te[8] + te[9] * te[9] + te[10] * te[10]);
                    if (det < 0) sx = -sx;
                    double invSX = 1 / sx, invSY = 1 / sy, invSZ = 1 / sz;
                    // Quaternion.setFromRotationMatrix of the rotation scaled out.
                    double m11 = te[0] * invSX, m12 = te[4] * invSY, m13 = te[8] * invSZ;
                    double m21 = te[1] * invSX, m22 = te[5] * invSY, m23 = te[9] * invSZ;
                    double m31 = te[2] * invSX, m32 = te[6] * invSY, m33 = te[10] * invSZ;
                    double trace = m11 + m22 + m33;
                    if (trace > 0)
                    {
                        double s = 0.5 / Math.Sqrt(trace + 1.0);
                        qw = 0.25 / s;
                        qx = (m32 - m23) * s;
                        qy = (m13 - m31) * s;
                        qz = (m21 - m12) * s;
                    }
                    else if (m11 > m22 && m11 > m33)
                    {
                        double s = 2.0 * Math.Sqrt(1.0 + m11 - m22 - m33);
                        qw = (m32 - m23) / s;
                        qx = 0.25 * s;
                        qy = (m12 + m21) / s;
                        qz = (m13 + m31) / s;
                    }
                    else if (m22 > m33)
                    {
                        double s = 2.0 * Math.Sqrt(1.0 + m22 - m11 - m33);
                        qw = (m13 - m31) / s;
                        qx = (m12 + m21) / s;
                        qy = 0.25 * s;
                        qz = (m23 + m32) / s;
                    }
                    else
                    {
                        double s = 2.0 * Math.Sqrt(1.0 + m33 - m11 - m22);
                        qw = (m21 - m12) / s;
                        qx = (m13 + m31) / s;
                        qy = (m23 + m32) / s;
                        qz = 0.25 * s;
                    }
                }
                // makeRotationFromQuaternion (compose at the origin, scale 1), then Euler's 'YXZ' y.
                double x2 = qx + qx, y2 = qy + qy, z2 = qz + qz;
                double xx = qx * x2, xz = qx * z2;
                double yy = qy * y2, yz = qy * z2, zz = qz * z2;
                double wx = qw * x2, wy = qw * y2;
                double r11 = 1 - (yy + zz), r31 = xz - wy, r13 = xz + wy, r23 = yz - wx, r33 = 1 - (xx + yy);
                return Math.Abs(r23) < 0.9999999 ? Js.Atan2(r13, r33) : Js.Atan2(-r31, r11);
            }

            /// <summary>Matrix4.invert (gl-matrix's), into a new array; all zeros if it has none.</summary>
            static double[] Invert(double[] te)
            {
                double n11 = te[0], n21 = te[1], n31 = te[2], n41 = te[3];
                double n12 = te[4], n22 = te[5], n32 = te[6], n42 = te[7];
                double n13 = te[8], n23 = te[9], n33 = te[10], n43 = te[11];
                double n14 = te[12], n24 = te[13], n34 = te[14], n44 = te[15];
                double t1 = n11 * n22 - n21 * n12;
                double t2 = n11 * n32 - n31 * n12;
                double t3 = n11 * n42 - n41 * n12;
                double t4 = n21 * n32 - n31 * n22;
                double t5 = n21 * n42 - n41 * n22;
                double t6 = n31 * n42 - n41 * n32;
                double t7 = n13 * n24 - n23 * n14;
                double t8 = n13 * n34 - n33 * n14;
                double t9 = n13 * n44 - n43 * n14;
                double t10 = n23 * n34 - n33 * n24;
                double t11 = n23 * n44 - n43 * n24;
                double t12 = n33 * n44 - n43 * n34;
                double det = t1 * t12 - t2 * t11 + t3 * t10 + t4 * t9 - t5 * t8 + t6 * t7;
                var o = new double[16];
                if (det == 0) return o;
                double detInv = 1 / det;
                o[0] = (n22 * t12 - n32 * t11 + n42 * t10) * detInv;
                o[1] = (n31 * t11 - n21 * t12 - n41 * t10) * detInv;
                o[2] = (n24 * t6 - n34 * t5 + n44 * t4) * detInv;
                o[3] = (n33 * t5 - n23 * t6 - n43 * t4) * detInv;
                o[4] = (n32 * t9 - n12 * t12 - n42 * t8) * detInv;
                o[5] = (n11 * t12 - n31 * t9 + n41 * t8) * detInv;
                o[6] = (n34 * t3 - n14 * t6 - n44 * t2) * detInv;
                o[7] = (n13 * t6 - n33 * t3 + n43 * t2) * detInv;
                o[8] = (n12 * t11 - n22 * t9 + n42 * t7) * detInv;
                o[9] = (n21 * t9 - n11 * t11 - n41 * t7) * detInv;
                o[10] = (n14 * t5 - n24 * t3 + n44 * t1) * detInv;
                o[11] = (n23 * t3 - n13 * t5 - n43 * t1) * detInv;
                o[12] = (n22 * t8 - n12 * t10 - n32 * t7) * detInv;
                o[13] = (n11 * t10 - n21 * t8 + n31 * t7) * detInv;
                o[14] = (n24 * t2 - n14 * t4 - n34 * t1) * detInv;
                o[15] = (n13 * t4 - n23 * t2 + n33 * t1) * detInv;
                return o;
            }
        }

        // ------------------------------------------------------------------ who is about

        /// <summary>What one out about the clearing does: stands about, talks with another, walks, sits round the fire, leans on La Lata.</summary>
        public enum Kind { Stand, Talk, Walk, Fire, Lean }

        /// <summary>
        /// A place at the camp: what one does there (its loop), where it stays and facing which way, where it stands to get
        /// into it and gets up to, the moves into and out of it (none: eased), what it holds there and the move it plays now
        /// and then (a sip, a bite).
        /// </summary>
        public sealed class Spot
        {
            public readonly string loop;
            public readonly Vec at;
            public readonly double yaw;
            public readonly Vec stand;
            public readonly string into, @out;
            public readonly string item, once;

            public Spot(string loop, Vec at, double yaw, Vec stand, string into, string @out, string item = null, string once = null)
            {
                this.loop = loop;
                this.at = at;
                this.yaw = yaw;
                this.stand = stand;
                this.into = into;
                this.@out = @out;
                this.item = item;
                this.once = once;
            }
        }

        /// <summary>A move into its place or out of it playing (`into`), or a sip or a bite over what it holds (`once`).</summary>
        public sealed class Act
        {
            public readonly string move;
            public readonly bool into, once;

            public Act(string move, bool into, bool once = false)
            {
                this.move = move;
                this.into = into;
                this.once = once;
            }
        }

        /// <summary>One out about the clearing (clearingLife.ts Idler). The life's own: read it, never change it.</summary>
        public sealed class Idler
        {
            public ICampFigure figure;
            /// <summary>Its entry's key (LifeEntry.Key): the same figure when the list comes again.</summary>
            public string key;
            public Kind kind;
            /// <summary>Where it is, which way it faces (three's yaw), and where it goes (null: it stays).</summary>
            public Vec at;
            public double yaw;
            public Vec goal;
            /// <summary>Where it faces once there (a talker, its partner; null, as it came).</summary>
            public Vec face;
            public string stance;
            public double phase;
            /// <summary>Its place at the camp (sat, knelt, leant, drinking: one round the fire, the one leant, the walker's end there).</summary>
            public Spot spot;
            /// <summary>In its place's loop now; on its way into it (turning to sit); the move into or out of it playing.</summary>
            public bool settled, toSit;
            public Act act;
            /// <summary>Standing about, what it does now and then ('drink', 'eat'), the seconds until then, and until the next sip or bite.</summary>
            public string habit;
            public double habitWait, sipWait;
            /// <summary>Seconds left getting up without a move, and what it does once up.</summary>
            public double rise;
            public Action then;
            /// <summary>A walker's two ends (the second its place at the camp, if it has one), the one it goes to now, and how long it stays before it sets off again.</summary>
            public Vec[] ends;
            public int end;
            public double stay;
            /// <summary>A talker's partner.</summary>
            public Idler partner;
        }

        List<Idler> idlers = new List<Idler>();
        string key = "";
        /// <summary>The list as last given (the key's): set again when the camp moves (0.10.1 parsed it back from the key).</summary>
        List<LifeEntry> entries = new List<LifeEntry>();
        BaseState @base;
        /// <summary>The tiles kept clear (what is being placed), as tile indices.</summary>
        HashSet<int> clear = new HashSet<int>();
        /// <summary>What the clearing says is taken (La Lata, the Armería's corner, the soldier in Soldados), by a tile's middle.</summary>
        Func<double, double, bool> taken = (x, z) => false;
        /// <summary>The Bastion's footprint grown from the caravan (tile indices), and the Camposanto's tiles.</summary>
        HashSet<int> foot = new HashSet<int>();
        List<(int x, int y)> graves = new List<(int x, int y)>();
        Camp camp = new Camp();
        string campKey = "";

        readonly Func<LifeEntry, ICampFigure> build;
        readonly Action<ICampFigure> drop;
        readonly Func<double> random;
        readonly Func<double> now;

        /// <summary>
        /// `build` makes a figure for an entry (0.10.1's buildUnit); `drop`, if given, is told of one no longer about (the
        /// list changed and it isn't in it, or the clearing was left): Unity's to put away. `random` is what 0.10.1 had
        /// from Math.random ([0, 1)); `now`, performance.now() (milliseconds).
        /// </summary>
        public ClearingLife(Func<LifeEntry, ICampFigure> build, Action<ICampFigure> drop, Func<double> random, Func<double> now)
        {
            this.build = build;
            this.drop = drop;
            this.random = random;
            this.now = now;
        }

        /// <summary>Those out about the clearing now, in the list's order.</summary>
        public IReadOnlyList<Idler> Idlers => idlers;

        /// <summary>The list's key (JSON.stringify of it, as given to Set; "" before any).</summary>
        public string Key => key;

        /// <summary>The camp as last given (SetCamp).</summary>
        public Camp CampNow => camp;

        /// <summary>The camp as the clearing now draws it (ClearingArtView); the soldiers are placed again if its fires, chairs or walls moved.</summary>
        public void SetCamp(Camp camp)
        {
            string Round(Vec p) => ToFixed1(p.x) + "," + ToFixed1(p.z);
            string campKey = string.Join(" ", camp.fires.Concat(camp.seats).Concat(camp.walls).Select(Round)) + "|" + camp.things.Count.ToString(CultureInfo.InvariantCulture);
            this.camp = camp;
            if (campKey == this.campKey) return;
            this.campKey = campKey;
            string key = this.key;
            this.key = "";
            if (key != "") Set(entries);
        }

        /// <summary>The clearing as it stands, and what else on it is taken (by a tile's middle, x and z in tiles: La Lata, the corners of its shots).</summary>
        public void SetBase(BaseState @base, Func<double, double, bool> taken)
        {
            this.@base = @base;
            this.taken = taken;
            foot = new HashSet<int>(Base.BastionFootprint(@base).Select((c) => c.Y * @base.Width + c.X));
            graves = @base.Pieces.Where((p) => p.Piece == PieceIds.Memorial).SelectMany((p) => Base.CellsOf(p)).Select((c) => (c.X, c.Y)).ToList();
            // Those now standing on something go elsewhere.
            foreach (var d in idlers) if (!Stays(d, 0)) MoveOff(d);
        }

        /// <summary>The soldiers out about the clearing (an empty list clears them); the same list again does nothing.</summary>
        public void Set(IReadOnlyList<LifeEntry> entries)
        {
            var list = entries.Take(LifeMax).ToList();
            var sb = new StringBuilder("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                list[i].Write(sb);
            }
            string key = sb.Append(']').ToString();
            if (key == this.key) return;
            this.key = key;
            this.entries = list;
            var before = idlers;
            Populate(list, key, before);
            // (0.10.1 took them all out of its group and put back those kept: the rest are gone.)
            if (drop == null) return;
            var gone = new List<ICampFigure>();
            foreach (var d in before)
                if (!idlers.Any((o) => ReferenceEquals(o.figure, d.figure)) && !gone.Any((f) => ReferenceEquals(f, d.figure))) gone.Add(d.figure);
            foreach (var f in gone) drop(f);
        }

        /// <summary>Who is out for `list` (its key `key`), each doing what and where, its figure kept from `before` if it was out.</summary>
        void Populate(List<LifeEntry> list, string key, List<Idler> before)
        {
            var old = new Dictionary<string, ICampFigure>();
            foreach (var d in before) old[d.key] = d.figure;
            idlers = new List<Idler>();
            if (@base == null || list.Count == 0) return;
            var r = Random(HashOf(key));
            var kinds = KindsFor(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                string who = entry.Key;
                var figure = old.TryGetValue(who, out var f) ? f : build(entry);
                figure.Sling(true);
                figure.SetGait("walk", WalkClipSpeed(entry.Template));
                figure.CampMove(null);
                var kind = kinds[i];
                string stance = kind == Kind.Stand ? Stands[i % Stands.Length] : kind == Kind.Talk ? (i == 0 ? null : "armsFolded") : null;
                double yaw = r() * Math.PI * 2;
                double phase = r() * 10;
                idlers.Add(new Idler { figure = figure, key = who, kind = kind, at = new Vec(0, 0), yaw = yaw, stance = stance, phase = phase });
            }
            var talkers = idlers.Where((d) => d.kind == Kind.Talk).ToList();
            if (talkers.Count == 2)
            {
                talkers[0].partner = talkers[1];
                talkers[1].partner = talkers[0];
            }
            SeatRound(r);
            // Those standing about as they are drink or eat now and then, by turns.
            int habits = 0;
            foreach (var d in idlers)
            {
                if (d.kind != Kind.Stand || d.stance != null) continue;
                d.habit = habits++ % 2 != 0 ? "eat" : "drink";
                d.habitWait = Between(r, HabitEvery) * 0.5;
            }
            foreach (var d in idlers)
            {
                // Those already in their places are sat there from the first frame, not sitting down.
                if (d.settled) d.figure.Camp(d.spot.loop, true, true);
                else Place(d, r, true);
            }
        }

        /// <summary>
        /// Who does what, for `n` out: round the fire (one; two from three on; three from five on), one leant on La Lata from
        /// two on, one walking from three on, a pair talking from six, the rest standing about. With no fire nor wall: a pair
        /// talking from three on, one walking from two on.
        /// </summary>
        List<Kind> KindsFor(int n)
        {
            if (CircleFire() == null && camp.walls.Count == 0)
                return Enumerable.Range(0, n).Select((i) => n >= 3 && i < 2 ? Kind.Talk : n >= 2 && i == (n >= 3 ? 2 : 1) ? Kind.Walk : Kind.Stand).ToList();
            var @out = new List<Kind>();
            void Add(Kind kind, int count)
            {
                for (int k = 0; k < count && @out.Count < n; k++) @out.Add(kind);
            }
            Add(Kind.Fire, CircleFire() != null ? (n >= 5 ? 3 : n >= 3 ? 2 : 1) : 0);
            Add(Kind.Lean, camp.walls.Count > 0 && n >= 2 ? 1 : 0);
            Add(Kind.Walk, n >= 3 ? 1 : 0);
            if (n - @out.Count >= 2 && n >= 6) Add(Kind.Talk, 2);
            Add(Kind.Stand, n);
            return @out;
        }

        /// <summary>
        /// Those round the fire (the first one's): on its chairs first, then on the ground about it facing it, one of them
        /// knelt nearest; and the one leant on La Lata. Each already there, in its place. Who can't be put there stands about.
        /// </summary>
        void SeatRound(Func<double> r)
        {
            var fire = CircleFire();
            var sitters = idlers.Where((o) => o.kind == Kind.Fire).ToList();
            // (The chairs themselves too: one on the ground behind a chair was in the back of who sat on it.)
            var taken = camp.seats.Select((s) => new Vec(s.x, s.z)).ToList();
            void Settle(Idler d, Spot spot)
            {
                d.spot = spot;
                d.at = new Vec(spot.at.x, spot.at.z);
                d.yaw = spot.yaw;
                d.stance = null;
                d.settled = true;
                taken.Add(spot.at);
            }
            int k = 0;
            if (fire != null)
            {
                foreach (var c in camp.seats.Where((s) => Js.Hypot(s.x - fire.x, s.z - fire.z) < 2.5).ToList())
                {
                    if (k >= sitters.Count) break;
                    var d = sitters[k];
                    // (Two on chairs: the second talks to the first.)
                    Settle(d, ChairSpot(c, k == 1 ? "sitTalk" : OnChair[(int)Math.Floor(r() * OnChair.Length)]));
                    k++;
                }
                // The rest on the ground round it, the last left over knelt, warming the hands: each on a side of their own,
                // as far round from the others as the free places let them.
                var ground = sitters.Skip(k).ToList();
                bool Kneels(int j) => j == ground.Count - 1 && k + j > 0;
                double start = r() * Math.PI * 2;
                Vec Round(double a, double ring) => new Vec(fire.x + Trig.Sin(a) * ring, fire.z + Trig.Cos(a) * ring);
                var places = new List<List<RoundPlace>>();
                for (int j = 0; j < ground.Count; j++)
                {
                    double ring = Kneels(j) ? FireKneel : FireRing;
                    var free = new List<RoundPlace>();
                    for (int step = 0; step < RoundSteps; step++)
                    {
                        double a = start + step * Math.PI * 2 / RoundSteps;
                        var c = new RoundPlace(a, Round(a, ring));
                        if (GroundFree(c.at, taken) && GroundFree(Round(c.a, ring + FireBack), new List<Vec>())) free.Add(c);
                    }
                    places.Add(free);
                }
                var there = sitters.Take(k).Select((d) => new RoundPlace(Js.Atan2(d.at.x - fire.x, d.at.z - fire.z), d.at)).ToList();
                var round = SpreadRound(places, there, fire);
                for (int j = 0; j < ground.Count; j++)
                {
                    var c = round[j];
                    if (c == null) continue;
                    bool kneel = Kneels(j);
                    var d = ground[j];
                    Settle(d, new Spot(kneel ? "warmHands" : OnGround[(k + j) % OnGround.Length], c.at, Js.Atan2(fire.x - c.at.x, fire.z - c.at.z), c.at, kneel ? "kneelDown" : null, kneel ? "kneelUp" : null));
                }
            }
            foreach (var d in idlers.Where((o) => o.kind == Kind.Lean).ToList())
            {
                var wall = camp.walls.FirstOrDefault((w) => GroundFree(w, taken));
                if (wall != null) Settle(d, WallSpot(wall));
            }
            foreach (var d in idlers) if ((d.kind == Kind.Fire || d.kind == Kind.Lean) && !d.settled) d.kind = Kind.Stand;
        }

        /// <summary>The fire to sit round: the first whose circle keeps off the Camposanto (the yard's, unless it was built beside it).</summary>
        Vec CircleFire()
        {
            double reach = FireRing + 0.5 + GravesOff;
            return camp.fires.FirstOrDefault((f) => !graves.Any((c) => NearTile(f, c, reach)));
        }

        /// <summary>
        /// Sitting on camp chair `c` in `loop`: its hips over the seat, standing a step ahead of it to sit down and getting up
        /// there; facing the fire it is by (the chair is a stool: any way round), or a little aside where that would have it
        /// stand in the fire to sit down.
        /// </summary>
        Spot ChairSpot(Seat c, string loop)
        {
            var fire = camp.fires.FirstOrDefault((f) => Js.Hypot(f.x - c.x, f.z - c.z) < 2.5);
            Spot SpotAt(double yaw)
            {
                double aheadX = Trig.Sin(yaw), aheadZ = Trig.Cos(yaw);
                var at = new Vec(c.x + aheadX * SeatAhead, c.z + aheadZ * SeatAhead);
                return new Spot(loop, at, yaw, new Vec(at.x + aheadX * SitShift, at.z + aheadZ * SitShift), "sitDown", "standUp");
            }
            if (fire == null) return SpotAt(c.yaw);
            double toward = Js.Atan2(fire.x - c.x, fire.z - c.z);
            foreach (double aside in new[] { 0, 0.6, -0.6, 1.1, -1.1, Math.PI / 2, -Math.PI / 2 })
            {
                var s = SpotAt(toward + aside);
                if (Js.Hypot(s.stand.x - fire.x, s.stand.z - fire.z) > FireClear) return s;
            }
            return SpotAt(toward + Math.PI / 2);
        }

        /// <summary>Leaning at `w` (a shoulder to the wall), into it and off it with its moves.</summary>
        static Spot WallSpot(Wall w)
        {
            var lean = Leans(w.side);
            return new Spot(lean.loop, new Vec(w.x, w.z), w.yaw, new Vec(w.x, w.z), lean.into, lean.@out);
        }

        /// <summary>A place at the camp nobody has: a chair by a fire, else a place along La Lata.</summary>
        Spot FreeSpot(Func<double> r)
        {
            var used = idlers.Where((d) => d.spot != null).Select((d) => d.spot.at).ToList();
            bool Free(Vec p) => !used.Any((u) => Js.Hypot(u.x - p.x, u.z - p.z) < FireApart);
            // (Not one by the Camposanto's graves.)
            var chairs = camp.seats.Where((c) => Free(ChairSpot(c, "sitChair").at) && !graves.Any((g) => NearTile(c, g, GravesOff + 0.8))).ToList();
            if (chairs.Count > 0) return ChairSpot(chairs[(int)Math.Floor(r() * chairs.Count)], OnChair[(int)Math.Floor(r() * OnChair.Length)]);
            var walls = camp.walls.Where((w) => Free(w) && GroundFree(w, new List<Vec>())).ToList();
            return walls.Count > 0 ? WallSpot(walls[(int)Math.Floor(r() * walls.Count)]) : null;
        }

        /// <summary>Whether one can sit at `at` by the fire: nothing of the camp there (a prop's box with a margin), no pine, none of `taken` near.</summary>
        bool GroundFree(Vec at, List<Vec> taken)
        {
            var b = @base;
            if (b == null) return false;
            double x = Math.Floor(at.x);
            double z = Math.Floor(at.z);
            if (x < 1 || z < 1 || x >= b.Width - 1 || z >= b.Height - 1) return false;
            if (Base.TreeAt(b, (int)x, (int)z) || clear.Contains((int)z * b.Width + (int)x)) return false;
            if (graves.Any((c) => NearTile(at, c, GravesOff))) return false;
            if (taken.Any((t) => Js.Hypot(t.x - at.x, t.z - at.z) < FireApart)) return false;
            foreach (var t in camp.things)
            {
                // p.set(at.x, 0.3, at.z).applyMatrix4(t.inverse)
                var e = t.inverse;
                double px = at.x, py = 0.3, pz = at.z;
                double w = 1 / (e[3] * px + e[7] * py + e[11] * pz + e[15]);
                double qx = (e[0] * px + e[4] * py + e[8] * pz + e[12]) * w;
                double qy = (e[1] * px + e[5] * py + e[9] * pz + e[13]) * w;
                double qz = (e[2] * px + e[6] * py + e[10] * pz + e[14]) * w;
                // (About a body's half width round it, in the prop's own measure.)
                double m = 0.28 * t.scale;
                var box = t.box;
                if (qx > box.minX - m && qx < box.maxX + m && qz > box.minZ - m && qz < box.maxZ + m && qy > box.minY - m && qy < box.maxY + m) return false;
            }
            return true;
        }

        /// <summary>What is being placed (its tiles; null nothing): whoever is on it, or about to walk through it, goes elsewhere.</summary>
        public void KeepClear(IEnumerable<Vec2> cells)
        {
            int w = @base?.Width ?? 0;
            var next = new HashSet<int>((cells ?? Enumerable.Empty<Vec2>()).Select((c) => c.Y * w + c.X));
            if (next.Count == clear.Count && next.All((i) => clear.Contains(i))) return;
            clear = next;
            if (next.Count == 0) return;
            foreach (var d in idlers) if (!Stays(d, 1)) MoveOff(d);
        }

        /// <summary>Whether `d` can stay where it is and go where it is going (`margin` tiles round free): one at the camp, what is placed off it.</summary>
        bool Stays(Idler d, int margin)
        {
            int w = @base?.Width ?? 0;
            bool Unplaced(Vec p) => !clear.Contains((int)Math.Floor(p.z) * w + (int)Math.Floor(p.x));
            if (d.settled || d.act != null || d.toSit) return Unplaced(d.at) && (d.spot == null || Unplaced(d.spot.at));
            // A walker's end at the camp is in the yard or by La Lata (off the ground the rest keep to): only what is placed counts there.
            bool Ok(Vec p) => d.spot != null && d.ends != null && ReferenceEquals(p, d.ends[1]) ? Unplaced(p) : OkAt(p, margin);
            return Ok(d.at) && (d.goal == null || Ok(d.goal)) && (d.ends == null || d.ends.All((e) => Ok(e) && Way(d.at, e, d.spot)));
        }

        /// <summary>Every frame: they walk, turn, sit down and get up, stay a while and talk. `dt` and `time` in seconds.</summary>
        public void Tick(double dt, double time)
        {
            foreach (var d in idlers)
            {
                var rig = d.figure;
                int moving = 0;
                if (d.act != null)
                {
                    // Into its place or out of it: the move to its end, standing where it stands for it.
                    double done = rig.CampMove(d.act.move);
                    if (done >= 1) EndAct(d);
                }
                else if (d.rise > 0)
                {
                    d.rise -= dt;
                    if (d.rise <= 0) Risen(d);
                }
                else if (d.goal != null)
                {
                    double dx = d.goal.x - d.at.x;
                    double dz = d.goal.z - d.at.z;
                    double far = Js.Hypot(dx, dz);
                    double step = Walk * dt;
                    if (far <= step)
                    {
                        d.at = d.goal;
                        d.goal = null;
                        d.stay = StayFor(d);
                        // At its place at the camp: it turns to sit down or lean there.
                        if (d.kind == Kind.Walk && d.spot != null && d.end == 1) d.toSit = true;
                    }
                    else
                    {
                        d.at = new Vec(d.at.x + dx / far * step, d.at.z + dz / far * step);
                        d.yaw = TurnToward(d.yaw, Js.Atan2(dx, dz), Turn * dt);
                        moving = 1;
                    }
                }
                else if (d.toSit && d.spot != null)
                {
                    d.yaw = TurnToward(d.yaw, d.spot.yaw, Turn * dt * 0.6);
                    if (Math.Abs(Js.Atan2(Trig.Sin(d.spot.yaw - d.yaw), Trig.Cos(d.spot.yaw - d.yaw))) < Turned)
                    {
                        d.toSit = false;
                        StartAct(d, true);
                    }
                }
                else
                {
                    if (d.face != null) d.yaw = TurnToward(d.yaw, Js.Atan2(d.face.x - d.at.x, d.face.z - d.at.z), Turn * dt * 0.6);
                    // A walker stays a while (sat or leant, if it went to the camp), then gets up and sets off for its other end.
                    if (d.kind == Kind.Walk && d.ends != null)
                    {
                        d.stay -= dt;
                        if (d.stay <= 0)
                        {
                            void Go()
                            {
                                d.end = 1 - d.end;
                                d.goal = d.ends[d.end];
                            }
                            if (d.settled) GetUp(d, Go);
                            else Go();
                        }
                    }
                    // Standing about: now and then it gets out its cup or its ration bar, has a sip or a bite every so often, and puts it away.
                    if (d.kind == Kind.Stand && d.habit != null) HabitTick(d, dt);
                    // Two talking: one says something with a gesture, the other nods; then the other way round.
                    if (d.kind == Kind.Talk && d.partner != null && d.partner.goal == null)
                    {
                        d.stay -= dt;
                        if (d.stay <= 0 && d.partner.stay > 0.6)
                        {
                            string says = Says[(int)Math.Floor((time * 7 + d.phase) % Says.Length)];
                            if (rig.Gesture(says))
                            {
                                d.partner.figure.Gesture("nod");
                                d.stay = Between(random, TalkEvery);
                                d.partner.stay = Math.Max(d.partner.stay, 1.2);
                            }
                        }
                    }
                }
                rig.SetPosition(d.at.x, d.at.z);
                rig.SetStance(moving != 0 || d.spot != null ? null : d.stance);
                // At the camp: sat, knelt or leant once in its place; standing about, the camp's things now and then (not two talking).
                rig.Camp(moving != 0 || !d.settled ? null : d.spot.loop, d.kind != Kind.Talk, false);
                rig.HoldItem(d.spot?.item != null && (d.settled || d.act != null) ? d.spot.item : null, d.spot?.loop == "drink");
                rig.ApplyPose(d.yaw, d.phase, moving, time);
            }
        }

        /// <summary>A habit's turn (drinking, eating): out with it when its wait is over, a sip or a bite now and then, away again after a while.</summary>
        void HabitTick(Idler d, double dt)
        {
            string habit = d.habit;
            if (!d.settled)
            {
                d.habitWait -= dt;
                if (d.habitWait > 0 || d.goal != null || d.spot != null) return;
                var moves = HabitMoves[habit];
                d.spot = new Spot(habit, new Vec(d.at.x, d.at.z), d.yaw, new Vec(d.at.x, d.at.z), moves.into, moves.@out, CampItems[habit], moves.once);
                d.stay = Between(random, HabitFor);
                d.sipWait = Between(random, HabitSip);
                StartAct(d, true);
                return;
            }
            if (d.spot?.item == null) return;
            d.stay -= dt;
            d.sipWait -= dt;
            if (d.stay <= 0)
            {
                GetUp(d, () =>
                {
                    d.spot = null;
                    d.habitWait = Between(random, HabitEvery);
                });
            }
            else if (d.sipWait <= 0 && d.spot.once != null)
            {
                d.act = new Act(d.spot.once, true, true);
                d.sipWait = Between(random, HabitSip);
            }
        }

        /// <summary>Into its place (`into`) or out of it: its move if it has one, else eased (the loop eases in, or it rises a moment).</summary>
        void StartAct(Idler d, bool into)
        {
            var spot = d.spot;
            string move = into ? spot.into : spot.@out;
            if (move != null)
            {
                d.act = new Act(move, into);
                return;
            }
            if (into)
            {
                d.at = new Vec(spot.at.x, spot.at.z);
                d.yaw = spot.yaw;
                d.settled = true;
            }
            else
            {
                d.settled = false;
                d.rise = Rise;
            }
        }

        /// <summary>Its move into its place or out of it played: there, in the loop at once (where the move left the body), or up where it gets up to.</summary>
        void EndAct(Idler d)
        {
            var rig = d.figure;
            var spot = d.spot;
            bool into = d.act.into, once = d.act.once;
            d.act = null;
            // A sip or a bite over what it holds: back to holding it.
            if (once)
            {
                rig.CampMove(null);
                return;
            }
            rig.CampMove(null);
            rig.Camp(into ? spot.loop : null, true, true);
            if (into)
            {
                d.at = new Vec(spot.at.x, spot.at.z);
                d.settled = true;
            }
            else
            {
                d.at = new Vec(spot.stand.x, spot.stand.z);
                d.settled = false;
                Risen(d);
            }
        }

        /// <summary>Up from its place: what it was to do then.</summary>
        static void Risen(Idler d)
        {
            var then = d.then;
            d.then = null;
            then?.Invoke();
        }

        /// <summary>Gets up from its place (its move, or eased), then `then`.</summary>
        void GetUp(Idler d, Action then)
        {
            d.then = then;
            StartAct(d, false);
        }

        /// <summary>Leaves the clearing: nobody about.</summary>
        public void ClearAll() => Set(new List<LifeEntry>());

        double StayFor(Idler d)
        {
            if (d.kind == Kind.Walk) return d.spot != null && d.end == 1 ? Between(random, Sat) : Between(random, Stay);
            return d.kind == Kind.Talk ? 1 + random() * 2 : 0;
        }

        /// <summary>Where `d` goes: a free place (two side by side for a pair, an end at either side of a walk); `now`, already there.</summary>
        void Place(Idler d, Func<double> r, bool now)
        {
            if (d.kind == Kind.Talk && d.partner != null)
            {
                bool lead = idlers.IndexOf(d) < idlers.IndexOf(d.partner);
                if (!lead) return;
                var pair = SpotPair(r, (p) =>
                {
                    double a = r() * Math.PI * 2;
                    var q = new Vec(p.x + Trig.Sin(a) * TalkApart, p.z + Trig.Cos(a) * TalkApart);
                    return OkAt(q, 0) ? q : null;
                });
                if (pair == null) return;
                foreach (var (who, there, other) in new[] { (d, pair[0], pair[1]), (d.partner, pair[1], pair[0]) })
                {
                    who.face = other;
                    if (now)
                    {
                        who.at = there;
                        who.yaw = Js.Atan2(other.x - there.x, other.z - there.z);
                    }
                    else who.goal = there;
                    who.stay = StayFor(who);
                }
                return;
            }
            if (d.kind == Kind.Walk)
            {
                // A free place at the camp (a chair by the fire, La Lata) and another a good few tiles off, the way free between.
                var atCamp = FreeSpot(r);
                var toCamp = atCamp != null ? SpotPair(r, (p) => Js.Hypot(atCamp.stand.x - p.x, atCamp.stand.z - p.z) > 4 && Way(p, atCamp.stand, atCamp) ? atCamp.stand : null) : null;
                var ends = toCamp ?? SpotPair(r, (p) =>
                {
                    for (int k = 0; k < 12; k++)
                    {
                        var q = Free(r);
                        if (q != null && Js.Hypot(q.x - p.x, q.z - p.z) > 4 && Way(p, q, null)) return q;
                    }
                    return null;
                });
                if (ends == null) return;
                d.spot = atCamp != null && ReferenceEquals(ends[1], atCamp.stand) ? atCamp : null;
                d.ends = ends;
                d.end = 0;
                if (now) d.at = ends[0];
                else d.goal = ends[0];
                d.stay = r() * Stay.to;
                return;
            }
            var spot = Free(r);
            if (spot == null) return;
            if (now) d.at = spot;
            else d.goal = spot;
        }

        /// <summary>A free place and another found from it (`then`), tried a few times.</summary>
        Vec[] SpotPair(Func<double> r, Func<Vec, Vec> then)
        {
            for (int k = 0; k < 24; k++)
            {
                var p = Free(r);
                var q = p != null ? then(p) : null;
                if (p != null && q != null) return new[] { p, q };
            }
            return null;
        }

        /// <summary>A free place about the clearing's middle, away from the others.</summary>
        Vec Free(Func<double> r)
        {
            var b = @base;
            if (b == null) return null;
            double cx = b.Width / 2.0;
            double cz = b.Height / 2.0;
            for (int k = 0; k < 40; k++)
            {
                double a = r() * Math.PI * 2;
                double far = Math.Sqrt(r()) * LifeReach;
                double x = Math.Floor(cx + Trig.Sin(a) * far) + 0.5 + (r() - 0.5) * 0.4;
                double z = Math.Floor(cz + Trig.Cos(a) * far) + 0.5 + (r() - 0.5) * 0.4;
                var p = new Vec(x, z);
                if (!OkAt(p, 1)) continue;
                if (idlers.Any((o) => Js.Hypot(o.at.x - p.x, o.at.z - p.z) < 1.6 || (o.goal != null && Js.Hypot(o.goal.x - p.x, o.goal.z - p.z) < 1.6))) continue;
                return p;
            }
            return null;
        }

        /// <summary>Whether one can stand at `p` (`margin` tiles round it free too: not against a wall or a pine), off the fires' circles.</summary>
        bool OkAt(Vec p, int margin)
        {
            var b = @base;
            if (b == null) return false;
            if (camp.fires.Any((f) => Js.Hypot(p.x - f.x, p.z - f.z) < FireCircle)) return false;
            for (int dz = -margin; dz <= margin; dz++)
            {
                for (int dx = -margin; dx <= margin; dx++)
                {
                    // (A whole tile round: the caravan and the tents stand out past their tiles.)
                    double x = Math.Floor(p.x + dx);
                    double z = Math.Floor(p.z + dz);
                    if (x < 1 || z < 1 || x >= b.Width - 1 || z >= b.Height - 1) return false;
                    int ix = (int)x, iz = (int)z;
                    int i = iz * b.Width + ix;
                    if (Base.TreeAt(b, ix, iz) || Base.PieceAt(b, ix, iz) != null || foot.Contains(i) || clear.Contains(i)) return false;
                    if (taken(x + 0.5, z + 0.5)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether the way from `a` to `b` is free all along; to a place at the camp (`to`), the last of it (in the yard, by
        /// La Lata) clear of the camp's things instead.
        /// </summary>
        bool Way(Vec a, Vec b, Spot to)
        {
            double n = Math.Ceiling(Js.Hypot(b.x - a.x, b.z - a.z) * 2);
            for (int k = 1; k < n; k++)
            {
                var p = new Vec(a.x + (b.x - a.x) * k / n, a.z + (b.z - a.z) * k / n);
                if (OkAt(p, 0)) continue;
                if (to != null && Js.Hypot(p.x - b.x, p.z - b.z) < 3 && GroundFree(p, idlers.Where((d) => d.settled).Select((d) => d.at).ToList())) continue;
                return false;
            }
            return true;
        }

        /// <summary>Off what is being placed, or what now stands where it was: somewhere else, walking there (getting up first).</summary>
        void MoveOff(Idler d)
        {
            var r = Random(HashOf(d.key) ^ ToUint32(Math.Floor(now())));
            if (d.settled || d.act != null)
            {
                if (d.act != null && !d.act.into) return;
                void Go()
                {
                    if (d.kind == Kind.Fire || d.kind == Kind.Lean) d.kind = Kind.Stand;
                    d.spot = null;
                    d.ends = null;
                    Place(d, r, false);
                }
                if (d.act != null && !d.act.once)
                {
                    // Halfway into it: out of it again at once.
                    d.act = null;
                    d.settled = false;
                    d.figure.CampMove(null);
                    Go();
                    return;
                }
                // (A sip or a bite cut short: it puts the thing away as ever.)
                if (d.act != null)
                {
                    d.act = null;
                    d.figure.CampMove(null);
                }
                GetUp(d, Go);
                return;
            }
            d.toSit = false;
            if (d.kind == Kind.Walk)
            {
                d.ends = null;
                d.spot = null;
                Place(d, r, false);
                return;
            }
            if (d.kind == Kind.Talk && d.partner != null)
            {
                var lead = idlers.IndexOf(d) < idlers.IndexOf(d.partner) ? d : d.partner;
                lead.partner.goal = null;
                lead.goal = null;
                Place(lead, r, false);
                return;
            }
            Place(d, r, false);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A number between `range`'s two, by `r`.</summary>
        static double Between(Func<double> r, (double from, double to) range) => range.from + r() * (range.to - range.from);

        /// <summary>Whether `p` is within `reach` tiles of tile `c` (of its square, not its middle).</summary>
        static bool NearTile(Vec p, (int x, int y) c, double reach)
        {
            double dx = Math.Max(Math.Max(c.x - p.x, 0), p.x - (c.x + 1));
            double dz = Math.Max(Math.Max(c.y - p.z, 0), p.z - (c.y + 1));
            return Js.Hypot(dx, dz) < reach;
        }

        /// <summary>A place round a fire: its angle round it (atan2 of x and z off it), and where.</summary>
        sealed class RoundPlace
        {
            public readonly double a;
            public readonly Vec at;

            public RoundPlace(double a, Vec at)
            {
                this.a = a;
                this.at = at;
            }
        }

        /// <summary>
        /// One of its free `places` round `fire` to each on the ground (null for who can't have one), together: as many as
        /// can be, then the widest the narrowest angle between any two round it (with those already `there`, on its chairs)
        /// and the most apart across the camera's looks (ViewsFrom) any two and the fire are; none nearer another than
        /// FireApart.
        /// </summary>
        static RoundPlace[] SpreadRound(List<List<RoundPlace>> places, List<RoundPlace> there, Vec fire)
        {
            double Apart(double a, double b) => Math.Abs(Js.Atan2(Trig.Sin(a - b), Trig.Cos(a - b)));
            // How far apart across the looks two are (the nearer of the two ways the camera looks along: from either end the same).
            var looks = new[] { ViewsFrom, ViewsFrom + Math.PI / 2 }.Select((y) => (x: Trig.Sin(y), z: Trig.Cos(y))).ToArray();
            double Across(Vec p, Vec q) => Math.Min(Math.Abs(looks[0].x * (q.z - p.z) - looks[0].z * (q.x - p.x)), Math.Abs(looks[1].x * (q.z - p.z) - looks[1].z * (q.x - p.x)));
            var best = new RoundPlace[places.Count];
            double bestScore = -1;
            var chosen = new List<RoundPlace>();
            void Pick(int j, List<RoundPlace> round)
            {
                if (j == places.Count)
                {
                    double narrowest = Math.PI;
                    double side = SideBy;
                    for (int i = 0; i < round.Count; i++)
                    {
                        side = Math.Min(side, Across(fire, round[i].at));
                        for (int k = i + 1; k < round.Count; k++)
                        {
                            narrowest = Math.Min(narrowest, Apart(round[i].a, round[k].a));
                            side = Math.Min(side, Across(round[i].at, round[k].at));
                        }
                    }
                    double score = chosen.Count((c) => c != null) * 10 + narrowest + ViewsWeigh * side;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = chosen.ToArray();
                    }
                    return;
                }
                bool any = false;
                foreach (var c in places[j])
                {
                    if (chosen.Any((o) => o != null && Js.Hypot(o.at.x - c.at.x, o.at.z - c.at.z) < FireApart)) continue;
                    any = true;
                    chosen.Add(c);
                    Pick(j + 1, new List<RoundPlace>(round) { c });
                    chosen.RemoveAt(chosen.Count - 1);
                }
                if (!any)
                {
                    chosen.Add(null);
                    Pick(j + 1, round);
                    chosen.RemoveAt(chosen.Count - 1);
                }
            }
            Pick(0, there);
            return best;
        }

        /// <summary>`yaw` turned towards `goal` by at most `step` (radians), the short way round.</summary>
        static double TurnToward(double yaw, double goal, double step)
        {
            double d = Js.Atan2(Trig.Sin(goal - yaw), Trig.Cos(goal - yaw));
            return yaw + Math.Max(-step, Math.Min(step, d));
        }

        /// <summary>FNV-1a over the text's UTF-16 units (clearingLife.ts hashOf): the same people in the same places each time the clearing is drawn again.</summary>
        static uint HashOf(string text)
        {
            uint h = 2166136261;
            foreach (char c in text) h = unchecked((h ^ c) * 16777619u);
            return h;
        }

        /// <summary>clearingLife.ts random(seed): a small seeded random in [0, 1), on 32 bits as JavaScript's ^, imul and >>> 0 work.</summary>
        static Func<double> Random(uint seed) => () =>
        {
            seed = unchecked((seed ^ (seed >> 15)) * 2246822519u + 0x9e3779b9u);
            return seed / 4294967296.0;
        };

        /// <summary>JavaScript's ToInt32 (a number taken to 32 bits, as ^ does), its bits as a uint.</summary>
        static uint ToUint32(double x) => double.IsNaN(x) || double.IsInfinity(x) ? 0 : unchecked((uint)(long)Math.Truncate(x % 4294967296.0));

        // ------------------------------------------------------------------ JavaScript's text

        /// <summary>JSON.stringify of a string: `"` and `\` escaped, the control characters short or as \u00xx, a lone surrogate as \udxxx.</summary>
        internal static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) sb.Append(c).Append(s[++i]);
                        else if (char.IsSurrogate(c)) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>
        /// A number as JSON.stringify writes it (Number::toString): the fewest digits that read back as it, the nearest of
        /// those (ties to the even one), as V8's; written plain from 1e-6 to 1e21, else with an exponent; NaN and the
        /// infinities null. Worked out exactly (Burger and Dybvig's free-format digits), not by the runtime's formatting:
        /// Mono's "R" is not the shortest.
        /// </summary>
        internal static string JsNumber(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            if (v == 0) return "0";
            var sb = new StringBuilder();
            if (v < 0)
            {
                sb.Append('-');
                v = -v;
            }
            string digits = ShortestDigits(v, out int n);
            int k = digits.Length;
            if (k <= n && n <= 21) sb.Append(digits).Append('0', n - k);
            else if (0 < n && n <= 21) sb.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
            else if (-6 < n && n <= 0) sb.Append("0.").Append('0', -n).Append(digits);
            else
            {
                int e = n - 1;
                sb.Append(digits[0]);
                if (k > 1) sb.Append('.').Append(digits, 1, k - 1);
                sb.Append('e').Append(e < 0 ? '-' : '+').Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>A positive finite double's shortest digits (0.d1d2… × 10^n is it).</summary>
        static string ShortestDigits(double v, out int n)
        {
            long bits = BitConverter.DoubleToInt64Bits(v);
            int be = (int)((bits >> 52) & 0x7FF);
            long fm = bits & 0xFFFFFFFFFFFFFL;
            long f = be == 0 ? fm : fm | 1L << 52;
            int e = be == 0 ? -1074 : be - 1075;
            // Its neighbours' halfway points count as it when its mantissa is even (they read back to it).
            bool even = (f & 1) == 0;
            BigInteger r, s, mp, mm;
            if (e >= 0)
            {
                var be2 = BigInteger.One << e;
                if (f != 1L << 52)
                {
                    r = f * be2 * 2;
                    s = 2;
                    mp = be2;
                    mm = be2;
                }
                else
                {
                    r = f * be2 * 4;
                    s = 4;
                    mp = be2 * 2;
                    mm = be2;
                }
            }
            else if (be <= 1 || f != 1L << 52)
            {
                r = new BigInteger(f) * 2;
                s = BigInteger.One << (1 - e);
                mp = 1;
                mm = 1;
            }
            else
            {
                r = new BigInteger(f) * 4;
                s = BigInteger.One << (2 - e);
                mp = 2;
                mm = 1;
            }
            n = 0;
            while (even ? r + mp >= s : r + mp > s)
            {
                s *= 10;
                n++;
            }
            while (even ? (r + mp) * 10 < s : (r + mp) * 10 <= s)
            {
                r *= 10;
                mp *= 10;
                mm *= 10;
                n--;
            }
            var sb = new StringBuilder();
            while (true)
            {
                r *= 10;
                mp *= 10;
                mm *= 10;
                int d = (int)BigInteger.DivRem(r, s, out r);
                bool low = even ? r <= mm : r < mm;
                bool high = even ? r + mp >= s : r + mp > s;
                if (!low && !high)
                {
                    sb.Append((char)('0' + d));
                    continue;
                }
                if (low && high)
                {
                    int c = (r * 2).CompareTo(s);
                    if (c > 0 || (c == 0 && d % 2 == 1)) d++;
                }
                else if (high) d++;
                sb.Append((char)('0' + d));
                return sb.ToString();
            }
        }

        /// <summary>Number.prototype.toFixed(1), exactly: the nearest tenth to the double's own value (of two as near, the larger), "0.0" for −0.</summary>
        internal static string ToFixed1(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (double.IsInfinity(x)) return x < 0 ? "-Infinity" : "Infinity";
            if (Math.Abs(x) >= 1e21) return JsNumber(x);
            string sign = x < 0 ? "-" : "";
            if (x < 0) x = -x;
            long bits = BitConverter.DoubleToInt64Bits(x);
            int be = (int)((bits >> 52) & 0x7FF);
            long fm = bits & 0xFFFFFFFFFFFFFL;
            var f = new BigInteger(be == 0 ? fm : fm | 1L << 52);
            int e = be == 0 ? -1074 : be - 1075;
            BigInteger tenths;
            if (e >= 0) tenths = (f << e) * 10;
            else
            {
                var den = BigInteger.One << -e;
                tenths = (f * 20 + den) / (den * 2);
            }
            string m = tenths.ToString(CultureInfo.InvariantCulture);
            if (m.Length < 2) m = "0" + m;
            return sign + m.Substring(0, m.Length - 1) + "." + m.Substring(m.Length - 1);
        }
    }
}
