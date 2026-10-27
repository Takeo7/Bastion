using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// What is left on the floor (0.10.1's dropped.ts, docs/FASE8.md §C: GameState.Dropped), one thing a tile, to be read
    /// from the tactical camera: a weapon lies on its side; a medicine is its first-aid box; food a carton with a can on
    /// it; anything else, and several things together, a bag. What a fallen soldier carried (`From`, playtest 18/10 #49)
    /// is their rucksack, tipped over, with their main weapon beside it, one for each of them on a tile. Each lies off the
    /// tile's middle (whoever stands there doesn't hide it), turned its own way, on a thin pale ring that tells it from the
    /// street's litter, and goes once it is picked up. On its storey, so the cutaway hides it with the rest.
    /// </summary>
    public sealed partial class MapBuilder
    {
        /// <summary>How far from the tile's middle a thing lies (tiles).</summary>
        const float DropOff = 0.24f;
        /// <summary>The ring under each (tiles): pale, a little short of white, over the floor.</summary>
        const float DropRingIn = 0.26f, DropRingOut = 0.3f;
        static readonly Color DropRingColour = new Color32(0xe9, 0xdf, 0xc4, 153);

        readonly List<GameObject> droppedArt = new List<GameObject>();
        string droppedDrawn;
        static Mesh dropRing;
        Material dropRingMaterial;

        /// <summary>The floor's things as the state has them now, drawn again when one is dropped or picked up.</summary>
        void DrawDropped()
        {
            var dropped = s.Dropped ?? new List<DroppedLoot>();
            string key = string.Join(",", dropped.Select(d => d.Id));
            if (key == droppedDrawn) return;
            droppedDrawn = key;
            foreach (var o in droppedArt) DestroyNow(o);
            droppedArt.Clear();
            // By tile, and on it each fallen soldier's things apart.
            var byTile = new Dictionary<string, (Pos pos, string id, List<Loot> things, string from)>();
            var order = new List<string>();
            foreach (var d in dropped)
            {
                string k = $"{d.Pos.X},{d.Pos.Y},{d.Pos.Z}" + (d.From != null ? ":" + d.From : "");
                if (byTile.TryGetValue(k, out var t)) t.things.Add(d.Loot);
                else
                {
                    byTile[k] = (d.Pos, d.From ?? d.Id, new List<Loot> { d.Loot }, d.From);
                    order.Add(k);
                }
            }
            foreach (var k in order)
            {
                var t = byTile[k];
                int level = Mathf.Clamp(t.pos.Z, 0, Storeys.Length - 1);
                var g = new GameObject("tirado " + t.id).transform;
                g.SetParent(Storeys.Length > 0 ? Storeys[level] : root, false);
                float way = DropHash(t.id, 1) * Mathf.PI * 2;
                g.position = MapSpace.TileToWorld(s, t.pos) + new Vector3(Mathf.Sin(way), 0, -Mathf.Cos(way)) * DropOff * MapSpace.Tile;
                string weapon = t.things.FirstOrDefault(l => l.Weapon != null)?.Weapon;
                if (t.from != null) Pack(g, weapon != null ? Anims.FoundModel(weapon) : null);
                else if (t.things.Count == 1 && t.things[0].Weapon != null) Laid(g, Anims.FoundModel(t.things[0].Weapon));
                else if (t.things.Count == 1 && t.things[0].Medicines != null) Things(g, ("lootMedicalKit", 0.9f, false, 0));
                else if (t.things.Count == 1 && t.things[0].Food != null) Things(g, ("lootFoodTop", 0.72f, false, 0), ("lootCan", 1.3f, true, 0.6f));
                else Things(g, ("dropBag", 1, false, 0));
                DropRing(g);
                // Turned once dressed: Prop fits a model by its box in the world, as if what holds it were square to it.
                g.rotation = Quaternion.Euler(0, -DropHash(t.id, 2) * 360, 0);
                droppedArt.Add(g.gameObject);
            }
        }

        /// <summary>dropped.ts hash: stable numbers from a thing's id, 0 to 1 (FNV-1a over its UTF-16 units).</summary>
        static float DropHash(string id, int k)
        {
            uint h = 0x811c9dc5u ^ (uint)k;
            foreach (char c in id) h = (h ^ c) * 0x01000193u;
            return (float)(h / (double)0xffffffffu);
        }

        /// <summary>
        /// A weapon's model on its side (rolled a quarter turn about its length), its middle `aside` tiles along the
        /// spot's x, resting on the floor, turned `turn` radians; at its own size, not the figures' (as 0.10.1 laid it:
        /// measured the same to the thousandth, as were the props, with CombatLab.Tirado).
        /// </summary>
        static void Laid(Transform g, string weapon, float aside = 0, float turn = 0)
        {
            var art = Anims.Weapon(weapon);
            var prefab = art != null ? FigureCatalog.Load()?.Weapon(art.fbx) : null;
            if (prefab == null) return;
            var gun = Object.Instantiate(prefab, g, false).transform;
            gun.name = "arma";
            foreach (var part in gun.GetComponentsInChildren<Transform>(true).Where(p => p.name.Contains("Silencer")).ToList()) part.gameObject.SetActive(false);
            foreach (var c in gun.GetComponentsInChildren<Collider>()) DestroyNow(c);
            gun.localRotation = Quaternion.Euler(0, -turn * Mathf.Rad2Deg, 90);
            gun.localScale = Vector3.one * (art.size > 0 ? art.size : 1);
            gun.localPosition = Vector3.zero;
            var box = BoundsIn(gun.gameObject, g);
            gun.localPosition = new Vector3(aside * MapSpace.Tile - box.center.x, -box.min.y + 0.005f * MapSpace.Tile, -box.center.z);
        }

        /// <summary>The rucksack tipped onto its back, a little askew; the weapon beside it.</summary>
        void Pack(Transform g, string weapon)
        {
            if (weapon != null) Laid(g, weapon, 0.17f, 0.25f);
            var pack = Prop(g, "fallenPack", prefabOf);
            if (pack == null) return;
            // (dropped.ts's turn (−1.35, 0.5, 0.12) in three's YXZ, its z the other way.)
            pack.transform.localRotation = Quaternion.Euler(1.35f * Mathf.Rad2Deg, -0.5f * Mathf.Rad2Deg, 0.12f * Mathf.Rad2Deg);
            var box = BoundsIn(pack, g);
            pack.transform.localPosition = new Vector3(-0.12f * MapSpace.Tile - box.center.x, -box.min.y, -box.center.z);
        }

        /// <summary>Props on the floor, each its size, one set on the one before if `on`, turned its own `turn` radians.</summary>
        void Things(Transform g, params (string id, float size, bool on, float turn)[] things)
        {
            float top = 0;
            foreach (var (id, size, on, turn) in things)
            {
                var placed = Prop(g, id, prefabOf);
                if (placed == null) continue;
                placed.transform.localScale = Vector3.one * size;
                placed.transform.localRotation = Quaternion.Euler(0, -turn * Mathf.Rad2Deg, 0);
                placed.transform.localPosition = new Vector3(0, on ? top : 0, 0);
                top = BoundsIn(placed, g).max.y;
            }
        }

        /// <summary>The pale ring under a tile's things, a hair over the floor.</summary>
        void DropRing(Transform g)
        {
            if (dropRing == null) dropRing = Ring(DropRingIn * MapSpace.Tile, DropRingOut * MapSpace.Tile, 40);
            if (dropRingMaterial == null)
            {
                dropRingMaterial = KitMaterials.New(KitMaterials.SeeThrough, "Aro de lo tirado");
                if (dropRingMaterial == null) return;
                dropRingMaterial.SetColor("_BaseColor", DropRingColour);
            }
            var ring = new GameObject("aro").transform;
            ring.SetParent(g, false);
            ring.localPosition = Vector3.up * 0.025f * MapSpace.Tile;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = dropRing;
            var r = ring.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = dropRingMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>A flat ring on the ground plane (three's RingGeometry turned flat), facing up.</summary>
        static Mesh Ring(float inner, float outer, int segments)
        {
            var points = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                points[2 * i] = d * inner;
                points[2 * i + 1] = d * outer;
                int j = (i + 1) % segments;
                // (Wound so the face looks up.)
                tris[6 * i] = 2 * i;
                tris[6 * i + 1] = 2 * j;
                tris[6 * i + 2] = 2 * i + 1;
                tris[6 * i + 3] = 2 * i + 1;
                tris[6 * i + 4] = 2 * j;
                tris[6 * i + 5] = 2 * j + 1;
            }
            var mesh = new Mesh { name = "Aro", vertices = points, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
