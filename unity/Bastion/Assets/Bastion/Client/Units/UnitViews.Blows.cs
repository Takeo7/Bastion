using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.Client.Units
{
    /// <summary>
    /// Where the blows come from and what they do to the body (unitViews.ts struck, blastAt, blowAt; syntyFigures.ts
    /// deathFor, hitFor): the last blow at a unit is kept a while (who shot at it, what bit or cut it, a blast near it),
    /// and its hit and its death are the ones that go with that blow. It falls away from where it came from, doubles up
    /// round one at arm's length, takes one to the head for a critical. It falls where there is room for the body, else
    /// the death that reaches least, tucked into its tile if none has room.
    /// </summary>
    public sealed partial class UnitViews
    {
        sealed class Blow
        {
            public Vector3 from;
            public bool melee;
            public bool head;
            public float at;
        }

        /// <summary>unitViews.ts BLOW_SECONDS: how long a blow is what a death or a hit goes by.</summary>
        const float BlowSeconds = 8;
        /// <summary>syntyFigures.ts FITS: how far off the way away from the blow a death may fall (radians).</summary>
        const float Fits = 0.9f;
        /// <summary>syntyFigures.ts CRAMPED_REACH: a death with no room lies with its head this far from its tile's middle at most (tiles).</summary>
        const float CrampedReach = 0.42f;
        /// <summary>syntyManifest.ts SYNTY_SCALE: tiles a metre of a Synty model (the deaths' reach is measured on the model).</summary>
        const float SyntyScale = 1 / 1.5f;

        /// <summary>unitViews.ts BULKY: what takes a floor tile whole; no body falls across it.</summary>
        static readonly HashSet<string> Bulky = new HashSet<string>
        {
            "shelf", "counter", "table", "sofa", "bed", "desk", "wardrobe", "cabinet", "rack", "crates", "barricade", "tank", "machine", "forklift",
            "drums", "pallets", "hospitalBed", "medicalShelf", "gurney", "medicalCabinet", "medicalCart", "receptionDesk", "waitingSeats", "examTable",
            "xray", "bedsideTable", "clinicalSink", "sterilizer", "ultrasound", "waitingTable", "waterDispenser", "toilet", "consultDesk", "bench", "fridge",
            "stove", "bathtub", "washbasin", "tvStand", "wc", "lootWeapons", "lootMedical", "lootAmmo", "lootFood", "statue", "kiosk", "planter", "bin",
        };

        /// <summary>unitViews.ts struck: a shot or a stroke at `target` from `from`, on whoever stands nearest it (within 0.6 tiles).</summary>
        void Struck(Vector3 target, Vector3 from, bool melee)
        {
            View hit = null;
            float best = 0.6f * MapSpace.Tile * 0.6f * MapSpace.Tile;
            foreach (var v in views.Values)
            {
                if (v.dead) continue;
                var d = v.root.position - target;
                d.y = 0;
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    hit = v;
                }
            }
            if (hit != null) hit.blow = new Blow { from = from, melee = melee, at = Time.time };
        }

        /// <summary>A blow at `v` from `from` (a bite, a blade at it).</summary>
        /// <summary>For the labs: unit `id` struck from `from` (its death and its hit then by it), as a blow at arm's length or not.</summary>
        public void StruckFrom(string id, Vector3 from, bool melee = false)
        {
            if (views.TryGetValue(id, out var v)) StruckBy(v, from, melee);
        }

        static void StruckBy(View v, Vector3 from, bool melee)
        {
            if (v != null) v.blow = new Blow { from = from, melee = melee, at = Time.time };
        }

        /// <summary>A blast at `at` (a grenade, a burst, a fireball): whoever it kills falls away from it (unitViews.ts blastAt: those within 6 tiles).</summary>
        public void BlastAt(Vector3 at)
        {
            float reach = 6 * MapSpace.Tile;
            foreach (var v in views.Values)
                if (!v.dead && (v.root.position - at).sqrMagnitude < reach * reach) v.blow = new Blow { from = at, at = Time.time };
        }

        /// <summary>The blow at `v` still to go by, or null.</summary>
        static Blow LastBlow(View v) => v.blow != null && Time.time - v.blow.at < BlowSeconds ? v.blow : null;

        /// <summary>
        /// The way a blow from `from` comes to `v` in its own frame (unitViews.ts blowAt: 0 ahead, π/2 from its left; three's
        /// yaw, z the map's south: atan2 of x and of −z here), or null if it comes from where it stands.
        /// </summary>
        static float? WayOf(View v, Vector3 from)
        {
            var d = from - v.root.position;
            if (Mathf.Abs(d.x) < 1e-5f && Mathf.Abs(d.z) < 1e-5f) return null;
            var f = v.root.forward;
            return Wrap(Mathf.Atan2(d.x, -d.z) - Mathf.Atan2(f.x, -f.z));
        }

        static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));

        /// <summary>
        /// syntyFigures.ts deathFor: `v`'s death for its last blow. Those that fall away from it (within Fits; at arm's length
        /// the doubling up one too; for a shot to the head one to the head if any), else the nearest that way; one of them with
        /// room, picked by its id; else whichever has room and reaches least; else the one that reaches least, `cramped`.
        /// </summary>
        Death DeathFor(View v, out bool cramped)
        {
            cramped = false;
            var deaths = Anims.Deaths(v.build);
            if (deaths.Count == 0) return null;
            var last = LastBlow(v);
            float? from = last != null ? WayOf(v, last.from) : null;
            float scale = Figures.CharacterScale * v.size;
            bool Room(int k) => RoomToFall(v, deaths[k].falls, deaths[k].reach * SyntyScale * scale);
            int Least(IEnumerable<int> ks) => ks.Aggregate((a, b) => deaths[b].reach < deaths[a].reach ? b : a);
            var all = Enumerable.Range(0, deaths.Count).ToList();
            var fits = all;
            if (from is float way)
            {
                float away = way + Mathf.PI;
                float Off(int k) => Mathf.Abs(Wrap(deaths[k].falls - away));
                fits = all.Where(k => Off(k) < Fits || (last.melee && deaths[k].close)).ToList();
                if (last.head && fits.Any(k => deaths[k].head)) fits = fits.Where(k => deaths[k].head).ToList();
                if (fits.Count == 0) fits = new List<int> { all.Aggregate((a, b) => Off(b) < Off(a) ? b : a) };
            }
            var roomy = fits.Where(Room).ToList();
            if (roomy.Count > 0) return deaths[roomy[PickIndex(roomy.Count, v.id)]];
            var anywhere = all.Where(Room).ToList();
            if (anywhere.Count > 0) return deaths[Least(anywhere)];
            cramped = true;
            return deaths[Least(all)];
        }

        /// <summary>
        /// unitViews.ts blowAt's room: whether a body falling `falls` from `v`'s facing, its head coming down `reach` tiles
        /// off, lies on open floor: every tile on the way a quarter of a tile at a time, the shoulders' width across, with no
        /// cover between and nothing standing or lying there.
        /// </summary>
        bool RoomToFall(View v, float falls, float reach)
        {
            var s = state;
            var at = v.place;
            if (s == null || at == null) return true;
            var f = v.root.forward;
            float yaw = Mathf.Atan2(f.x, -f.z);
            float dx = Mathf.Sin(yaw + falls), dy = Mathf.Cos(yaw + falls);
            var checkedTiles = new Dictionary<int, bool>();
            for (float d = 0.25f; d < reach + 0.25f; d += 0.25f)
            {
                foreach (float side in new[] { -0.2f, 0, 0.2f })
                {
                    float along = Mathf.Min(d, reach);
                    var tile = new Pos { X = Mathf.FloorToInt(at.X + 0.5f + dx * along + dy * side), Y = Mathf.FloorToInt(at.Y + 0.5f + dy * along - dx * side), Z = at.Z };
                    if (tile.X == at.X && tile.Y == at.Y) continue;
                    int key = tile.Y * s.Width + tile.X;
                    if (!checkedTiles.TryGetValue(key, out bool ok))
                        checkedTiles[key] = ok = Grid.InBounds(s, tile.X, tile.Y) && Grid.CoverAt(s, at, new Vec2 { X = tile.X, Y = tile.Y }) == 0 && ClearFor(tile, v.id, at);
                    if (!ok) return false;
                }
            }
            return true;
        }

        /// <summary>unitViews.ts clearFor (bodies too): floor at the same height as `level`, nothing bulky on it, nobody standing or lying on it.</summary>
        bool ClearFor(Pos p, string except, Pos level)
        {
            var s = state;
            if (s == null || p.X < 0 || p.Y < 0 || p.X >= s.Width || p.Y >= s.Height) return false;
            string kind = Grid.KindAt(s, p);
            if (!Grid.CanStandOn(kind) || kind == TileKinds.Door || kind == TileKinds.Ladder) return false;
            if (Mathf.Abs(MapSpace.FloorAt(s, p) - MapSpace.FloorAt(s, level)) > 1e-3f) return false;
            int i = p.Y * s.Width + p.X;
            var decor = p.Z > 0 ? (s.Levels != null && p.Z - 1 < s.Levels.Count ? s.Levels[p.Z - 1].Decor : null) : s.Decor;
            if (decor != null && i < decor.Count && Bastion.Engine.Decor.DecorOf(decor[i]) is var look && look != null && Bulky.Contains(look.Kind)) return false;
            bool Here(Pos q) => q != null && q.X == p.X && q.Y == p.Y && q.Z == p.Z;
            foreach (var u in s.Units) if (u.Alive && u.Id != except && !u.Evacuated && Here(u.Pos)) return false;
            foreach (var o in views.Values) if (o.dead && o.id != except && o.root.gameObject.activeInHierarchy && Here(o.place)) return false;
            return true;
        }

        /// <summary>
        /// syntyFigures.ts hitFor: `v`'s hit for its last blow at `strength` (above 1 a critical, a shot to the head). At arm's
        /// length it doubles up; else a hard one or one to the head for a critical, one of the others else: the one whose blow
        /// comes from nearest its way, any of them (by its id) for a blow from nowhere known.
        /// </summary>
        Hit HitFor(View v, float strength)
        {
            var hits = Anims.Hits(v.build);
            if (hits.Count == 0) return null;
            var last = LastBlow(v);
            if (last != null) last.head = strength > 1;
            if (last != null && last.melee && hits.FirstOrDefault(h => h.close) is Hit close) return close;
            bool hard = strength > 1;
            var pool = hits.Where(h => hard ? h.hard || h.head : !h.hard).ToList();
            if (pool.Count == 0) pool = hits.ToList();
            float? from = last != null ? WayOf(v, last.from) : null;
            if (!(from is float way)) return pool[PickIndex(pool.Count, v.id)];
            return pool.Aggregate((a, b) => Mathf.Abs(Wrap(b.from - way)) < Mathf.Abs(Wrap(a.from - way)) ? b : a);
        }

        /// <summary>syntyFigures.ts die's tuck: a death with no room drawn in towards where it stood, as far as its head goes past CrampedReach, over its fall.</summary>
        async Awaitable Tuck(View v, Death death, float seconds, System.Threading.CancellationToken ct)
        {
            float scale = Figures.CharacterScale * v.size;
            float tuck = Mathf.Max(0, death.reach * SyntyScale - CrampedReach / scale) * scale * MapSpace.Tile;
            if (tuck <= 0 || v.body == null) return;
            // (Its fall's way in the figure's frame, falls from ahead towards its left: Unity's −x.)
            var back = -new Vector3(-Mathf.Sin(death.falls), 0, Mathf.Cos(death.falls)) * tuck;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = 1 - (1 - t / seconds) * (1 - t / seconds);
                if (v.body == null) return;
                v.body.localPosition = back * k;
                await Awaitable.NextFrameAsync(ct);
            }
            if (v.body != null) v.body.localPosition = back;
        }
    }
}
