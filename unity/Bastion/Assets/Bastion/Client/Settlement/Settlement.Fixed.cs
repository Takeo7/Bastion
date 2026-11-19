using System;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The runs' clearing (Arturo, 16/11: the base only to come back to, its screens over the clearing in 3D, with nothing
    /// built on it nor walked about in it): one base, always the same, drawn from the base's data the engine keeps for it
    /// (Gameplay, 16/11: BaseState, Base.Pieces, MapSource.OfBase and its map). The clearing lab's (SettlementLab, as Arturo
    /// and Ana have seen it in its tests): the Bastion with its caravan and its fire, the radio, the barracks, the workshop,
    /// the garden and the memorial, a palisade with its gate, sandbags, logs, a floodlight and a tower; no snare nor turret,
    /// there being no siege. And its open shot: the fire with those sat round it (FireShot).
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>The seed of the fixed base's clearing (its pines, its paths): the lab's.</summary>
        const int FixedSeed = 3;

        /// <summary>
        /// The runs' base, made again at each call (cheap; nothing shares it): the engine's fresh one with the lab's pieces round
        /// its middle, the pines felled there first.
        /// </summary>
        public static BaseState FixedBase()
        {
            var @base = Bastion.Engine.Base.FreshBase(new Rng(FixedSeed));
            int c = @base.Width / 2;
            for (int y = c - 9; y <= c + 9; y++)
                for (int x = c - 11; x <= c + 11; x++)
                    Bastion.Engine.Base.FellTree(@base, x, y);
            void Put(string id, int x, int y, int rot = 0, int level = 1)
            {
                var p = Bastion.Engine.Base.PlacePiece(@base, id, x, y, rot);
                if (p.Level != 0) p.Level = level;
            }
            Put("radio", c - 9, c - 8, 0, 2);
            Put("quarters", c - 5, c - 8, 0, 3);
            Put("workshop", c + 4, c - 8, 1, 1);
            Put("garden", c - 9, c + 3, 0, 2);
            Put("memorial", c + 1, c + 4);
            for (int y = c - 4; y <= c + 1; y++) Put(y == c - 2 ? "gate" : "palisade", c - 10, y);
            Put("sandbags", c - 6, c - 3);
            Put("sandbags", c - 5, c - 3);
            Put("logs", c + 4, c - 3);
            Put("tower", c + 7, c - 3);
            Put("floodlight", c - 7, c);
            return @base;
        }

        /// <summary>The runs' clearing shown (Show with FixedBase): the same base each time, so made again only the first.</summary>
        public void ShowFixed(bool snow = false)
        {
            // The halt by night or under its weather, if the stage just played was («site/modifier», UI's: night, storm,
            // snowfall…): the engine's Weathers by name; any other, a fair day.
            haltModifier = Halt != null && Halt.Contains("/") ? Halt.Substring(Halt.IndexOf('/') + 1) : null;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Show(Halt != null ? HaltBase() : FixedBase(), snow);
            if (clock.ElapsedMilliseconds > 5) Debug.Log($"[alto] the {(Halt != null ? "halt" : "base")} made in {clock.ElapsedMilliseconds} ms");
        }

        string haltModifier;

        /// <summary>The fire of the open shot (tiles: its middle), if the clearing has one.</summary>
        Vector2 fireAt;
        bool fireFound;

        /// <summary>
        /// Where the open shot looks (CampPlaces): the fire those out about the clearing sit round (ClearingLife), from the
        /// clearest way out from it, the caravan behind them if it can be.
        /// </summary>
        void PlaceFire(BaseState b)
        {
            fireFound = false;
            if (b == null || built == null) return;
            var caravan = b.Pieces.FirstOrDefault(p => p.Piece == PieceIds.Caravan);
            var fires = built.PiecesPlaced.Where(p => p.id == "bFirePit").Select(p => (Vector3)p.matrix.GetColumn(3)).ToList();
            if (caravan == null || fires.Count == 0) return;
            var cells = Bastion.Engine.Base.BastionFootprint(b);
            if (cells.Count == 0) return;
            var mid = new Vector2((cells.Min(c => c.X) + cells.Max(c => c.X) + 1) / 2f, (cells.Min(c => c.Y) + cells.Max(c => c.Y) + 1) / 2f);
            // The one they sit round (ClearingLife.CircleFire): the first whose circle keeps off the Camposanto (the first
            // shot took the fire by the graves, with nobody at it and La Lata before it).
            var graves = b.Pieces.Where(p => p.Piece == PieceIds.Memorial).SelectMany(p => Bastion.Engine.Base.CellsOf(p)).ToList();
            bool ByGraves(Vector3 f) => graves.Any(c => Mathf.Max(Mathf.Max(c.X - f.x, 0), f.x - (c.X + 1)) < GravesReach && Mathf.Max(Mathf.Max(c.Y - f.z, 0), f.z - (c.Y + 1)) < GravesReach);
            var circle = fires.Where(f => !ByGraves(f)).ToList();
            if (circle.Count == 0) return;
            var fire = circle[0];
            fireAt = new Vector2(fire.x, fire.z);
            var away = fireAt - mid;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : new Vector2(0, 1);
            // The ways out from it, all round, the clearest first (CampPlaces' ViewOf leaves low buildings out: the first shot
            // stood in the workshop's roof): the fewest pieces, pines and La Lata on the ground under the way, as far as the
            // shot may stand (FireReach); the caravan behind them breaking ties. FireShot stands on the first.
            fireWays.Clear();
            for (int k = 0; k < 24; k++)
            {
                float a = k * Mathf.PI * 2 / 24;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float behind = 2 * (1 - Vector2.Dot(dir, away));
                fireWays.Add((dir, behind, behind + Lane(b, fireAt + dir * FireReach)));
            }
            fireWays.Sort((p, q) => p.cost.CompareTo(q.cost));
            fireFound = true;
        }

        /// <summary>The ways out from the fire (PlaceFire): each its unit, its caravan-behind cost and its whole cost straight out, clearest first.</summary>
        readonly System.Collections.Generic.List<(Vector2 dir, float behind, float cost)> fireWays = new System.Collections.Generic.List<(Vector2, float, float)>();

        /// <summary>What stands in the way from the fire (past its circle) to `to` (tiles, on the ground), InTheWay's a quarter of a tile at a time.</summary>
        float Lane(BaseState b, Vector2 to)
        {
            var d = to - fireAt;
            float length = d.magnitude, cost = 0;
            if (length < 1e-3f) return 0;
            d /= length;
            for (float t = 1.4f; t <= length + 0.75f; t += 0.25f) cost += InTheWay(b, fireAt + d * t);
            return cost;
        }

        /// <summary>How far the open shot may stand from the fire (tiles), all clear.</summary>
        const float FireReach = 15;

        /// <summary>What stands on the ground at `p` (tiles) in the way of a camera a few storeys up: a piece 10, La Lata 10, a pine 3.</summary>
        float InTheWay(BaseState b, Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.y);
            if (x < 0 || z < 0 || x >= b.Width || z >= b.Height) return 3;
            if (PieceOn(x, z) != null) return 10;
            if (busParked)
            {
                var o = p - busAt;
                if (Mathf.Abs(Vector2.Dot(o, busDir)) <= busHalf + 0.5f && Mathf.Abs(o.x * busDir.y - o.y * busDir.x) <= busHalfWidth + 0.5f) return 10;
            }
            return Bastion.Engine.Base.TreeAt(b, x, z) ? 3 : 0;
        }

        /// <summary>
        /// How far down the open shot looks (degrees), and the box round the fire it keeps in what the panels leave free
        /// (tiles each way from the fire, and up): those sat round it (ClearingLife's circle) and some ground.
        /// </summary>
        const float FirePitch = 24, FireBox = 2.4f, FireTall = 1.6f;
        /// <summary>ClearingLife's FireRing + 0.5 + GravesOff: how near the graves a fire is too near to sit round.</summary>
        const float GravesReach = 1.15f + 0.5f + 0.6f;

        /// <summary>
        /// The runs' open shot (UI, 16/11: behind the base with nothing open, and behind Mejoras): the fire with those sat
        /// round it from out in the clearing, a little up (FirePitch down), the tents behind them, in the middle of what the
        /// panels leave free; the clearing from above (ClearingShot) if it has no fire.
        /// </summary>
        public Shot FireShot()
        {
            if (!fireFound || fireWays.Count == 0) return ClearingShot();
            var target = new Vector3(fireAt.x, 0.6f, fireAt.y);
            var corners = new System.Collections.Generic.List<Vector3>();
            for (int k = 0; k < 8; k++)
                corners.Add(new Vector3(fireAt.x + ((k & 1) != 0 ? FireBox : -FireBox), (k & 2) != 0 ? FireTall : 0, fireAt.y + ((k & 4) != 0 ? FireBox : -FireBox)));
            // Not Fitted: fitted beside a panel the camera went sideways off the clear way, and the workshop's roof or La Lata
            // came into the shot. It stays on the clearest way out (PlaceFire), as near as the box round the fire fills what
            // the panels leave free across, and turns so that the fire is in the middle of that.
            var camera = Camera;
            var size = camera != null ? SizeOf(camera) : new Vector2(16, 9);
            float w = Mathf.Max(1, size.x), h = Mathf.Max(1, size.y);
            var inset = camera != null && FitShots ? InsetsOn(camera) : (0f, 0f, 0f, 0f);
            float left = inset.Item4, right = inset.Item2;
            if (left + right > w / 2) left = right = 0;
            float free = (w - left - right) / w, middle = (left + (w - right)) / 2 / w;
            const float fov = 38;
            float across = Mathf.Tan(fov / 2 * Mathf.Deg2Rad) * w / h;
            float pitch = FirePitch * Mathf.Deg2Rad;
            float slant = Mathf.Clamp(FireBox * FireSpan / (free * across), FireNear, FireReach);
            var dir = fireWays[0].dir;
            var pos = target + new Vector3(dir.x, 0, dir.y) * (slant * Mathf.Cos(pitch)) + Vector3.up * (slant * Mathf.Sin(pitch));
            // Turned so that the fire is at the free part's middle, as far off the screen's middle as that is.
            float turn = Mathf.Atan((middle * 2 - 1) * across) * Mathf.Rad2Deg;
            var fromW = World(pos);
            var lookW = fromW + Quaternion.AngleAxis(-turn, Vector3.up) * (World(target) - fromW);
            return new Shot { pos = pos, target = Tiles(lookW), fov = fov, seen = corners };
        }

        /// <summary>
        /// The open shot's box across the free part of the screen: its width (twice FireBox) this share of it (FireSpan over
        /// two: a little more than the box, those sat round the fire in it with some ground), and how near and far it stands
        /// (tiles, along its view).
        /// </summary>
        const float FireSpan = 2.1f, FireNear = 6;

        /// <summary>The open shot is on (fitted again when the panels move).</summary>
        bool FireShown => view == Views.Facilities;
    }
}
