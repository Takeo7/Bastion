using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The Armería's corner by the caravan (0.10.1's clearingArt.ts campPlaces and fillArmory; Ana, 20/10 #29: a table and
    /// boxes with what there is, near empty at first): the bench, and on it what is bought and found (the grenades, smoke
    /// and flash ones, the first-aid kits, boxes of the special rounds, the improvements not yet mounted); the rack with
    /// the weapons found, the rest in a crate open beside it once the rack is full; an ammunition crate, its lid off. At
    /// an end of the caravan, else by one of its long sides, to either hand or further out; as near the caravan as the
    /// whole corner has room, clear of Soldados' soldier and of the squad's row and what their cameras see; of those, the
    /// one its own camera sees most clearly.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>Where the Armería's things go (tiles: across the caravan's end u, out from it v): the bench's, the toolbox's, the rack's, the crates'.</summary>
        static readonly (float u, float v)[] CornerSpots = { (0, 0), (0.8f, 0), (-0.8f, 0), (0.6f, 0.45f), (-1, -0.05f), (1, 0.3f), (1.3f, 0.65f), (-1.1f, 0.75f), (-1.55f, 0.85f) };
        static readonly float[] CornerShifts = { 0, 1.5f, -1.5f, 3, -3 };
        const float CornerReach = 8;
        /// <summary>How far the corner's things keep from Soldados' soldier and the way its camera looks at it, and from what the squad's row's camera sees (tiles), for a row of this many at least, and behind it.</summary>
        const float SoldierRoom = 1.2f, RowRoom = 1.5f, RowBehind = 4;
        const int RowGuess = 6;
        /// <summary>How high the bench's top is (tiles, at its own size: 0.8 of it as it stands).</summary>
        const float BenchTop = 0.58f;

        Vector2 armoryAt;
        Vector2 armoryOut = new Vector2(0, 1);
        Vector2 armoryDir = new Vector2(0.92f, 0.4f);
        float armoryDistance = 3.5f;
        bool armoryPlaced;
        Transform armoryCorner;
        Func<float, float, bool> armoryFree;

        /// <summary>What the Armería holds: the weapons found on the rack (FoundWeaponIds), the accessories bought (ItemIds → how many free), the improvements loose (WeaponModIds → copies).</summary>
        List<string> stockWeapons = new List<string>();
        Dictionary<string, int> stockItems = new Dictionary<string, int>();
        Dictionary<string, int> stockMods = new Dictionary<string, int>();
        string stocked;

        /// <summary>
        /// The Armería as the campaign has it (screen.ts armoryStock: the weapons found nobody carries, the accessories
        /// nobody has equipped, the improvements not mounted): its corner filled again if it changed.
        /// </summary>
        public void SetArmory(CampaignState campaign)
        {
            if (campaign == null) return;
            stockWeapons = (campaign.FoundWeapons ?? new List<FoundWeapon>()).Where(w => w.Holder == null).Select(w => w.Kind).ToList();
            stockItems = ItemIds.All.Select(id => (id, n: Campaign.ItemsFree(campaign, id))).Where(x => x.n > 0).ToDictionary(x => x.id, x => x.n);
            stockMods = campaign.WeaponMods != null ? new Dictionary<string, int>(campaign.WeaponMods) : new Dictionary<string, int>();
            if (Stock() == stocked || !armoryPlaced) return;
            FillArmory();
        }

        string Stock() => Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { stockWeapons, stockItems.OrderBy(kv => kv.Key), stockMods.OrderBy(kv => kv.Key) });

        /// <summary>The corner's place (campPlaces): `free` where nothing stands, `view` the clearest way to look at a spot.</summary>
        void PlaceArmory(BaseState b, Func<float, float, bool> free, Func<float, float, Vector2, float, (Vector2 dir, float dist, float cost)> view)
        {
            armoryPlaced = false;
            var caravan = b.Pieces.FirstOrDefault(p => p.Piece == PieceIds.Caravan);
            if (caravan == null) return;
            var cells = Bastion.Engine.Base.BastionFootprint(b);
            if (cells.Count == 0) return;
            int x0 = cells.Min(c => c.X), x1 = cells.Max(c => c.X), y0 = cells.Min(c => c.Y), y1 = cells.Max(c => c.Y);
            var mid = new Vector2((x0 + x1 + 1) / 2f, (y0 + y1 + 1) / 2f);
            var half = new Vector2((x1 - x0 + 1) / 2f, (y1 - y0 + 1) / 2f);
            var along = half.x >= half.y ? new Vector2(1, 0) : new Vector2(0, 1);
            var ways = new[] { along, -along, new Vector2(along.y, along.x), new Vector2(-along.y, -along.x) };
            // Clear of Soldados' soldier too, and of the way from its camera to it.
            bool InShot(float px, float pz)
            {
                float reach = soldierDistance + 1;
                float t = Mathf.Clamp((px - soldierAt.x) * soldierView.x + (pz - soldierAt.y) * soldierView.y, 0, reach);
                return Vector2.Distance(new Vector2(px, pz), soldierAt + soldierView * t) <= SoldierRoom;
            }
            // And of the squad's row by La Lata and what its camera sees of it, as many as it may be.
            int count = Mathf.Max(RowGuess, lineupKey.Length > 0 ? lineupEntries.Count : 0);
            var row = PlaceLineup(count);
            var spots = row?.spots ?? new List<Vector2>();
            var cam = row != null ? new Vector2(row.cam.x, row.cam.z) : mid;
            var beyond = new List<Vector2>();
            if (spots.Count > 0)
                foreach (var e in new[] { spots[0], spots[Mathf.Min(spots.Count - 1, count > LineupRow ? Mathf.CeilToInt(count / 2f) - 1 : count - 1)] })
                {
                    float d = Vector2.Distance(e, cam);
                    beyond.Add(e + (e - cam) / (d > 0 ? d : 1) * RowBehind);
                }
            bool InRow(float px, float pz) =>
                spots.Any(q => Vector2.Distance(new Vector2(px, pz), q) <= SoldierRoom) || (beyond.Count == 2 && NearTriangle(new Vector2(px, pz), cam, beyond[0], beyond[1], RowRoom));
            bool Room(float x, float z, Vector2 @out) =>
                CornerSpots.All(s =>
                {
                    float px = x + @out.x * s.v - @out.y * s.u, pz = z + @out.y * s.v + @out.x * s.u;
                    return free(px, pz) && !InShot(px, pz) && !InRow(px, pz);
                });
            bool found = false;
            float bestCost = 0;
            // Room for the whole corner; with none anywhere, for its bench at least.
            foreach (var fits in new Func<float, float, Vector2, bool>[] { Room, (x, z, _) => free(x, z) })
            {
                for (int k = 0; k < ways.Length; k++)
                {
                    var @out = ways[k];
                    float edge = @out.x != 0 ? half.x : half.y;
                    foreach (float shift in CornerShifts)
                        for (float reach = edge + 1.1f; reach <= edge + CornerReach + 1e-4f; reach += 0.5f)
                        {
                            float x = mid.x + @out.x * reach - @out.y * shift, z = mid.y + @out.y * reach + @out.x * shift;
                            if (!fits(x, z, @out)) continue;
                            foreach (int lean in new[] { 1, -1 })
                            {
                                var side = new Vector2(-@out.y * lean, @out.x * lean);
                                float length = Mathf.Sqrt(3.2f * 3.2f + 1.4f * 1.4f);
                                // As far as the shot may go back to keep the corner whole.
                                var v = view(x - @out.x * 0.2f, z - @out.y * 0.2f, (@out * 3.2f + side * 1.4f) / length, 5);
                                // Its own camera not among the squad, nor the squad before it.
                                var camera = new Vector2(x, z) + v.dir * v.dist;
                                bool crowded = spots.Any(q =>
                                {
                                    float t = Mathf.Clamp((q.x - x) * v.dir.x + (q.y - z) * v.dir.y, 0, v.dist);
                                    return Vector2.Distance(q, new Vector2(x, z) + v.dir * t) < 0.9f || Vector2.Distance(q, camera) < 1.2f;
                                });
                                float cost = v.cost + (crowded ? 30 : 0) + (k < 2 ? 0 : 1.5f) + Mathf.Abs(shift) * 0.3f + (reach - edge - 1.1f) * 0.2f;
                                if (found && cost >= bestCost) continue;
                                found = true;
                                bestCost = cost;
                                armoryAt = new Vector2(x, z);
                                armoryOut = @out;
                                armoryDir = v.dir;
                                armoryDistance = v.dist;
                            }
                            break;
                        }
                }
                if (found) break;
            }
            if (!found) return;
            armoryPlaced = true;
            armoryFree = free;
            FillArmory();
        }

        /// <summary>Whether (x, z) is in the triangle abc, or within `margin` of its edges (clearingArt.ts nearTriangle).</summary>
        static bool NearTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float margin)
        {
            float Cross(Vector2 s, Vector2 q) => (q.x - s.x) * (p.y - s.y) - (q.y - s.y) * (p.x - s.x);
            float d1 = Cross(a, b), d2 = Cross(b, c), d3 = Cross(c, a);
            if (!((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0))) return true;
            float Edge(Vector2 s, Vector2 q)
            {
                float len = Vector2.Distance(s, q);
                if (len <= 0) len = 1;
                float t = Mathf.Clamp01(Vector2.Dot(p - s, q - s) / (len * len));
                return Vector2.Distance(p, s + (q - s) * t);
            }
            return Mathf.Min(Edge(a, b), Edge(b, c), Edge(c, a)) <= margin;
        }

        /// <summary>The corner's things where they go (fillArmory), with what the Armería holds now.</summary>
        void FillArmory()
        {
            stocked = Stock();
            if (armoryCorner == null)
            {
                armoryCorner = new GameObject("La Armería").transform;
                armoryCorner.SetParent(transform, false);
            }
            foreach (Transform child in armoryCorner.Cast<Transform>().ToList()) DestroyNow(child.gameObject);
            if (!armoryPlaced) return;
            float x = armoryAt.x, z = armoryAt.y;
            var @out = armoryOut;
            var side = new Vector2(-@out.y, @out.x);
            float yaw = Mathf.Atan2(@out.x, @out.y);
            Vector2 Spot(float u, float v) => new Vector2(x + @out.x * v + side.x * u, z + @out.y * v + side.y * u);
            // A prop at (u, v) of the corner, `y` up, turned `turn` from the corner's own way and `k` its map size (tiles).
            void Put(string id, float u, float v, float turn = 0, float k = 1, float y = 0)
            {
                var p = Spot(u, v);
                if (!armoryFree(p.x, p.y)) return;
                var prop = MapBuilder.Prop(armoryCorner, id, prefabOf);
                if (prop == null) return;
                prop.transform.position = World(new Vector3(p.x, y, p.y));
                prop.transform.rotation = Quaternion.Euler(0, -(yaw + turn) * Mathf.Rad2Deg, 0);
                prop.transform.localScale = Vector3.one * k;
            }
            // Its long side across the camera's view, its board to the caravan.
            Put("bWorkbench", 0, 0, -Mathf.PI / 2, 0.8f);
            Put("bToolbox", 0.6f, 0.45f, 0.4f, 0.9f);
            Put("gunRack", -1.0f, -0.05f, Mathf.PI, 0.75f);
            Put("armAmmoCrate", 1.0f, 0.3f, 0.25f, 0.55f);
            Put("armAmmoCrateLid", 1.3f, 0.65f, 1.1f, 0.55f);
            // On the bench: in rows along its top, each thing as many times as there are (a few at most).
            float top = BenchTop * 0.8f;
            int Of(string id, int most) => Mathf.Min(most, stockItems.TryGetValue(id, out int n) ? n : 0);
            var onBench = Enumerable.Repeat("armGrenade", Of(ItemIds.FragGrenade, 4))
                .Concat(Enumerable.Repeat("armSmoke", Of(ItemIds.SmokeGrenade, 3)))
                .Concat(Enumerable.Repeat("armFlashbang", Of(ItemIds.Flashbang, 3)))
                .Concat(Enumerable.Repeat("clinicalKit", Of(ItemIds.FirstAid, 2)))
                .Concat(Enumerable.Repeat("armAmmo556", Of(ItemIds.ApRounds, 2)))
                .Concat(Enumerable.Repeat("armAmmo762", Of(ItemIds.TracerRounds, 2)))
                .ToList();
            for (int k = 0; k < onBench.Count; k++)
            {
                int row = k % 2, col = k / 2;
                Put(onBench[k], -0.45f + col * 0.13f + row * 0.06f, -0.06f + row * 0.14f, (k * 1.7f) % 1.2f - 0.6f, 1, top);
            }
            var catalog = FigureCatalog.Load();
            // A weapon's model (or an improvement's) at the figures' scale, under the corner.
            Transform Model(string fbx, float size)
            {
                var prefab = catalog?.Weapon(fbx);
                if (prefab == null) return null;
                var g = Instantiate(prefab, armoryCorner, false).transform;
                foreach (var c in g.GetComponentsInChildren<Collider>()) DestroyNow(c);
                foreach (var part in g.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Silencer") && t != g).ToList()) part.gameObject.SetActive(false);
                g.localScale = Vector3.one * Figures.CharacterScale * (size > 0 ? size : 1);
                return g;
            }
            // The improvements not yet mounted: a sight, a silencer, lying at the bench's end.
            var parts = new List<string>();
            if (stockMods.TryGetValue("scope", out int scopes) && scopes > 0) parts.Add(Scope);
            if (stockMods.TryGetValue("silencer", out int silencers) && silencers > 0) parts.Add(Silencer);
            for (int k = 0; k < parts.Count; k++)
            {
                var g = Model(parts[k], 1);
                if (g == null) continue;
                var p = Spot(0.4f - k * 0.14f, 0.05f);
                g.position = World(new Vector3(p.x, top + 0.02f, p.y));
                g.rotation = Quaternion.Euler(0, -(yaw + Mathf.PI / 2) * Mathf.Rad2Deg, 0);
            }
            // The weapons found: in the rack, standing, a little apart; the rest laid in a crate open by it.
            var found = stockWeapons.Select(kind => Anims.Weapon(Anims.FoundModel(kind))).Where(a => a != null).ToList();
            var rack = Spot(-1.0f, -0.05f);
            var outward = new Vector3(@out.x, 0, -@out.y);
            var across = new Vector3(side.x, 0, -side.y);
            if (armoryFree(rack.x, rack.y))
                for (int k = 0; k < Mathf.Min(3, found.Count); k++)
                {
                    var g = Model(found[k].fbx, found[k].size);
                    if (g == null) continue;
                    float t = (k - 1) * 0.15f;
                    g.position = World(new Vector3(rack.x + side.x * t + @out.x * 0.02f, 0.12f, rack.y + side.y * t + @out.y * 0.02f));
                    // Standing on its butt, its muzzle up and leant a little back to the caravan, its top out.
                    var barrel = Vector3.up * Mathf.Cos(0.12f) - outward * Mathf.Sin(0.12f);
                    g.rotation = Quaternion.LookRotation(barrel, outward);
                }
            if (found.Count > 3)
            {
                Put("lootWeapons", -1.1f, 0.75f, 0.2f, 0.8f);
                Put("lootWeaponsLid", -1.55f, 0.85f, 1.2f, 0.8f);
                for (int k = 3; k < Mathf.Min(6, found.Count); k++)
                {
                    var g = Model(found[k].fbx, found[k].size);
                    if (g == null) continue;
                    var p = Spot(-1.1f + (k - 4) * 0.12f, 0.75f);
                    g.position = World(new Vector3(p.x, 0.22f, p.y));
                    g.rotation = Quaternion.Euler(0, -(yaw + 0.2f + Mathf.PI / 2) * Mathf.Rad2Deg, 0);
                }
            }
            foreach (var r in armoryCorner.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>The loose improvements' models (syntyManifest.ts WEAPON_PARTS scope2, silencer2).</summary>
        public const string Scope = "SM_Wep_Mod_Attach_Scope_02", Silencer = "SM_Wep_Mod_Attach_Silencer_02";

        /// <summary>
        /// The Armería (armoryShot): its corner, from in front of the bench and a little to the side, close, the corner kept
        /// whole in what the panels leave free and in the middle of the screen across (screen.ts: centred).
        /// </summary>
        Shot ArmoryShot()
        {
            if (!armoryPlaced) return ClearingShot();
            float x = armoryAt.x, z = armoryAt.y;
            var target = new Vector3(x + armoryOut.x * 0.2f, 0.55f, z + armoryOut.y * 0.2f);
            // Along the clearest way to it, mostly from in front.
            var ahead = new Vector3(armoryOut.x * 0.5f + armoryDir.x * 0.5f, 0, armoryOut.y * 0.5f + armoryDir.y * 0.5f).normalized;
            var shot = new Shot { pos = target + ahead * 2.6f + new Vector3(0, 1.3f, 0), target = target, fov = 40 };
            // What stands by the bench (not a lid thrown further off): the box the screen keeps whole.
            var box = new Bounds();
            bool any = false;
            if (armoryCorner != null)
                foreach (var r in armoryCorner.GetComponentsInChildren<Renderer>())
                {
                    var c = Tiles(r.bounds.center);
                    if (Mathf.Sqrt((c.x - x) * (c.x - x) + (c.z - z) * (c.z - z)) >= 1.4f) continue;
                    var b = new Bounds(c, new Vector3(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) / MapSpace.Tile);
                    if (any) box.Encapsulate(b);
                    else box = b;
                    any = true;
                }
            if (!any) box = new Bounds(target, new Vector3(2.4f, 1.2f, 2.4f));
            var corners = new List<Vector3>();
            for (int k = 0; k < 8; k++) corners.Add(new Vector3((k & 1) != 0 ? box.max.x : box.min.x, (k & 2) != 0 ? box.max.y : box.min.y, (k & 4) != 0 ? box.max.z : box.min.z));
            shot.seen = corners;
            return Fitted(shot, corners, true);
        }
    }
}
