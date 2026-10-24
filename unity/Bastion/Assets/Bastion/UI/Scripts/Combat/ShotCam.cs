using System.Collections.Generic;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// Where the camera stands for the shot over the shoulder (shotCam.ts; the attack that kills, playtest 20/10 #11):
    /// behind the attacker's head, to its right, on the line of fire carried back, so the attacker fills the left third
    /// and the target is in the middle. Never in a wall, a slab, a car or a window: it comes in towards the head, goes
    /// over the other shoulder or follows the line of fire less steeply; and only where it sees the target. The web's
    /// lengths were in tiles: here times 1.5, in metres.
    /// </summary>
    public static class ShotCam
    {
        const float T = Board.Tile;
        /// <summary>How far from the attacker's head the camera stands, and the head over the chest the shot is aimed from.</summary>
        const float Reach = 1.8f * T;
        const float Head = 0.22f * T;
        /// <summary>Round from straight behind the head (rad, + to its right): the right shoulder, the left, nearer, then more beside.</summary>
        static readonly float[] Turns = { 0.34f, -0.34f, 0.17f, -0.17f, 0.6f, -0.6f };
        /// <summary>The attacker kept this near the middle (rad, across); the steepest it follows the line of fire up or down.</summary>
        const float Keep = 22 * Mathf.Deg2Rad;
        const float Steep = 40 * Mathf.Deg2Rad;
        /// <summary>It keeps this far from anything solid, and comes no nearer the head than Nearest, or there is no shot.</summary>
        const float Room = 0.22f * T;
        const float Nearest = 0.85f * T;
        /// <summary>Anyone else's body, for the camera to keep out of: this wide round its tile's middle, this tall; further than BodyNear, left out.</summary>
        const float Body = 0.3f * T;
        const float BodyTop = 1.5f * T;
        const float BodyNear = 3.5f * T;
        /// <summary>A window is seen through between its sill and its lintel (storeys over its floor).</summary>
        const float Sill = 0.38f;
        const float Lintel = 0.78f;
        /// <summary>The slabs, this thick under their top: a ceiling is solid that far down.</summary>
        const float Slab = 0.2f * T;
        /// <summary>Steps along the lines it checks, and how much of the line nearest the target it leaves out (its own cover).</summary>
        const float Step = 0.05f * T;
        const float SightStep = 0.1f * T;
        const float SightSpare = 0.6f * T;
        /// <summary>On the line of fire carried back, less steep, or higher over it (a parapet): how steep, how much higher.</summary>
        static readonly (float tilt, float lift)[] Rises = { (1, 0), (0.5f, 0), (0, 0), (1, 0.5f * T), (1, 1 * T) };

        /// <summary>Height above its floor of what stands on a tile, for the camera (storeys): a window is a wall with a hole in it.</summary>
        static readonly Dictionary<string, float> Solid = new Dictionary<string, float>
        {
            ["floor"] = 0, ["ladder"] = 0, ["doorOpen"] = 0, ["low"] = 0.4f, ["high"] = 0.8f, ["console"] = 0.8f,
            ["wall"] = 1, ["door"] = 1, ["window"] = 1, ["windowBroken"] = 1, ["roof"] = 1,
        };

        /// <summary>What the camera may not see the target through: walls, doors and roofs; cover only in the half nearer the camera.</summary>
        static readonly Dictionary<string, float> Opaque = With(Solid, ("low", 0), ("high", 0), ("console", 0), ("window", 0), ("windowBroken", 0));
        static readonly Dictionary<string, float> OpaqueNear = With(Solid, ("window", 0), ("windowBroken", 0));

        static Dictionary<string, float> With(Dictionary<string, float> from, params (string kind, float h)[] changes)
        {
            var d = new Dictionary<string, float>(from);
            foreach (var (kind, h) in changes) d[kind] = h;
            return d;
        }

        static float Height(Dictionary<string, float> heights, string kind) => kind != null && heights.TryGetValue(kind, out var h) ? h : 0;

        /// <summary>
        /// The shot from `from` (the attacker's chest, where it strikes or shoots from) at `to` (the target's chest), or null
        /// where there is no room for one: the camera stays as it is. Out of everyone's body but `skip`'s (the two of them).
        /// </summary>
        public static (Vector3 pos, Vector3 look)? OverShoulder(GameState s, Vector3 from, Vector3 to, params string[] skip)
        {
            var flat = new Vector3(to.x - from.x, 0, to.z - from.z);
            var run = flat.magnitude;
            if (run < 0.015f) return null;
            flat /= run;
            // The attacker's right, facing the target (x east, z north).
            var right = new Vector3(flat.z, 0, -flat.x);
            var head = from + Vector3.up * Head;
            var bodies = new List<Vector3>();
            foreach (var u in s.Units)
            {
                if (!u.Alive || System.Array.IndexOf(skip, u.Id) >= 0) continue;
                var feet = MapSpace.TileToWorld(s, u.Pos);
                if (new Vector2(feet.x - head.x, feet.z - head.z).magnitude > BodyNear) continue;
                bodies.Add(feet);
            }
            var pitch = Mathf.Clamp(Mathf.Atan2(to.y - head.y, run), -Steep, Steep);
            Vector3? best = null;
            var bestScore = float.NegativeInfinity;
            for (var n = 0; n < Turns.Length; n++)
            {
                var turn = Turns[n];
                foreach (var (tilt, lift) in Rises)
                {
                    var p = pitch * tilt;
                    var back = Mathf.Cos(turn) * Reach;
                    var want = head - flat * (Mathf.Cos(p) * back) + right * (Mathf.Sin(turn) * Reach);
                    want.y += lift - Mathf.Sin(p) * back;
                    var reach = Vector3.Distance(head, want);
                    var free = FreeLength(s, bodies, head, want) / reach;
                    if (free * reach < Nearest) continue;
                    var pos = Vector3.Lerp(head, want, free);
                    if (!Sees(s, pos, to)) continue;
                    // Room counts most; then the order of Turns, the line of fire, and the least height over it.
                    var score = free - n * 0.03f - (1 - tilt) * 0.02f - lift / T * 0.05f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = pos;
                    }
                }
            }
            if (best == null) return null;
            var at = best.Value;
            // From beside the attacker, the view turns from the target towards it, until it is Keep from the middle.
            var ahead = Mathf.Atan2(to.x - at.x, to.z - at.z);
            var off = Mathf.Repeat(Mathf.Atan2(head.x - at.x, head.z - at.z) - ahead + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
            var swing = Mathf.Sign(off) * Mathf.Max(0, Mathf.Abs(off) - Keep);
            var look = at + Quaternion.Euler(0, swing * Mathf.Rad2Deg, 0) * (to - at);
            return (at, look);
        }

        /// <summary>How far from `a` towards `b` the camera can go with room round it, out of the walls and of `bodies` (their feet).</summary>
        static float FreeLength(GameState s, List<Vector3> bodies, Vector3 a, Vector3 b)
        {
            var length = Vector3.Distance(a, b);
            var free = 0f;
            for (var d = Step; d <= length + 1e-6f; d += Step)
            {
                var p = Vector3.Lerp(a, b, Mathf.Min(1, d / length));
                if (Crowded(s, p)) break;
                var hit = false;
                foreach (var f in bodies)
                    if (new Vector2(p.x - f.x, p.z - f.z).magnitude < Body + Room && p.y > f.y - Room && p.y < f.y + BodyTop + Room)
                    {
                        hit = true;
                        break;
                    }
                if (hit) break;
                free = Mathf.Min(d, length);
            }
            return free;
        }

        /// <summary>Whether anything solid stands within Room of `p`.</summary>
        static bool Crowded(GameState s, Vector3 p)
        {
            return SolidAt(s, p, Solid) || SolidAt(s, p + Vector3.right * Room, Solid) || SolidAt(s, p - Vector3.right * Room, Solid)
                || SolidAt(s, p + Vector3.forward * Room, Solid) || SolidAt(s, p - Vector3.forward * Room, Solid)
                || SolidAt(s, p + Vector3.up * Room, Solid) || SolidAt(s, p - Vector3.up * Room, Solid);
        }

        /// <summary>Whether the camera at `pos` sees the target's chest `to`: nothing opaque between, its own cover left out.</summary>
        static bool Sees(GameState s, Vector3 pos, Vector3 to)
        {
            var length = Vector3.Distance(pos, to) - SightSpare;
            for (var d = SightStep; d < length; d += SightStep)
            {
                var p = Vector3.Lerp(pos, to, d / (length + SightSpare));
                if (SolidAt(s, p, d < (length + SightSpare) / 2 ? OpaqueNear : Opaque)) return false;
            }
            return true;
        }

        /// <summary>
        /// Whether world point `p` is inside something solid: below the ground, what stands on a tile up to its height (a
        /// wall on the ground up to its building's top), or a slab, a storey's floor and the ceiling under it. Out of the
        /// map, only under the ground (Ana, 23/10 #22: a soldier with their back to the map's edge had no shot at all).
        /// </summary>
        static bool SolidAt(GameState s, Vector3 p, Dictionary<string, float> heights)
        {
            var t = MapSpace.WorldToTile(p);
            if (!Grid.InBounds(s, t.x, t.y)) return p.y < 0;
            var i = Grid.TileIndex(s, t.x, t.y);
            var h = p.y / Board.Storey;
            var floor = s.Elev[i];
            if (h < floor) return true;
            var kind = s.Tiles[i];
            var levels = s.Levels;
            var tall = kind == "wall" && (levels == null || levels.Count == 0 || levels[0].Tiles[i] == null) ? Mathf.Max(1, Grid.StoreysAt(s, t.x, t.y)) : 1;
            if (h < floor + Height(heights, kind) * tall || Framed(kind, h - floor, heights)) return true;
            if (levels == null) return false;
            // The storey this height is on, and the slab just above (its ceiling) or just below (its floor).
            var k = Mathf.FloorToInt(h);
            foreach (var level in new[] { k, k + 1 })
            {
                var slab = level >= 1 && level - 1 < levels.Count ? levels[level - 1].Tiles[i] : null;
                if (slab != null && h > level - Slab / Board.Storey && h < level + 0.02f) return true;
            }
            var on = k >= 1 && k - 1 < levels.Count ? levels[k - 1].Tiles[i] : null;
            return on != null && (h < k + Height(heights, on) || Framed(on, h - k, heights));
        }

        /// <summary>Whether a height `up` storeys over its floor is in the wall round a window seen through (under its sill, over its lintel).</summary>
        static bool Framed(string kind, float up, Dictionary<string, float> heights)
        {
            return (kind == "window" || kind == "windowBroken") && Height(heights, kind) == 0 && up < 1 && (up < Sill || up > Lintel);
        }
    }
}
