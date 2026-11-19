using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// La llave's locked door (G23.1; Gameplay's PARA-ARTE of 19/11: the 0.13.4 had the lock in UI's pointer alone): on each
    /// door of MissionState.Locked (Looting.LockedAt) while it is a door still, a red steel door of ours, whole (the
    /// cutaway doesn't cut it: it has to be found from the game's camera), barred, a brass padlock on it, on both its
    /// faces (Arturo, cuadro 91: «la puerta roja blindada»; no pack has a padlock). Gone once it is unlocked (the node out
    /// of Locked: the house's own leaf then, open) or no door any more; there still, shut, if the wall beside it is blown
    /// through (cuadro 91).
    /// </summary>
    public sealed partial class MapBuilder
    {
        readonly List<GameObject> locksArt = new List<GameObject>();
        string locksDrawn;
        static Material lockBrass, lockSteel, lockDoor, lockPanel;

        /// <summary>The locked doors as the state has them now, drawn again when one is unlocked or goes (DrawDropped calls it: at each change of the map).</summary>
        void DrawLocks()
        {
            var doors = new List<Pos>();
            if (s.Mission?.Locked != null)
                for (int k = 0; k < Storeys.Length; k++)
                {
                    var tiles = TilesOf(k);
                    for (int i = 0; i < tiles.Count; i++)
                    {
                        if (tiles[i] != TileKinds.Door) continue;
                        var at = new Pos { X = i % s.Width, Y = i / s.Width, Z = k };
                        if (Looting.LockedAt(s, at)) doors.Add(at);
                    }
                }
            string key = string.Join(",", doors.Select(d => $"{d.X}.{d.Y}.{d.Z}"));
            if (key == locksDrawn && locksArt.All(o => o != null)) return;
            locksDrawn = key;
            foreach (var o in locksArt) DestroyNow(o);
            locksArt.Clear();
            if (doors.Count == 0) return;
            lockBrass ??= Coloured("Candado", new Color(0.86f, 0.62f, 0.16f));
            lockSteel ??= Coloured("Cerrojo", new Color(0.3f, 0.31f, 0.34f));
            foreach (var d in doors)
            {
                // The door lies along its wall: across the way the walls beside it run.
                bool alongX = WallAt(d.Z, d.X - 1, d.Y) || WallAt(d.Z, d.X + 1, d.Y);
                var g = new GameObject($"candado {d.X},{d.Y}").transform;
                g.SetParent(Storeys.Length > 0 ? Storeys[d.Z] : root, false);
                g.position = MapSpace.TileToWorld(s, d);
                g.rotation = Quaternion.Euler(0, alongX ? 0 : 90, 0);
                // A steel door, red, whole whatever the cutaway does to the walls: its frame, the leaf with two sunk panels
                // and a band across it, the bar and the padlock, on both faces.
                float face = 0.1f * WallLayout.Thick * MapSpace.Tile / 2 + 0.05f, wide = 0.98f * MapSpace.Tile, tall = 2.15f;
                lockDoor ??= Coloured("Puerta blindada", new Color(0.52f, 0.15f, 0.12f));
                lockPanel ??= Coloured("Puerta blindada, panel", new Color(0.4f, 0.11f, 0.09f));
                foreach (float x in new[] { -1f, 1f }) Block(g, lockSteel, new Vector3(x * (wide / 2 + 0.05f), tall / 2 + 0.05f, 0), new Vector3(0.12f, tall + 0.1f, face * 2 + 0.08f));
                Block(g, lockSteel, new Vector3(0, tall + 0.06f, 0), new Vector3(wide + 0.22f, 0.12f, face * 2 + 0.08f));
                Block(g, lockDoor, new Vector3(0, tall / 2, 0), new Vector3(wide, tall, face * 2 - 0.04f));
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float y in new[] { 0.55f, 1.62f }) Block(g, lockPanel, new Vector3(0, y, side * (face - 0.03f)), new Vector3(wide * 0.74f, 0.72f, 0.03f));
                    Block(g, lockSteel, new Vector3(0, 1.09f, side * face), new Vector3(wide + 0.1f, 0.1f, 0.06f));
                    foreach (float x in new[] { -0.38f, 0.38f }) foreach (float y in new[] { 0.12f, 2.03f }) Block(g, lockSteel, new Vector3(x * wide, y, side * (face - 0.01f)), new Vector3(0.07f, 0.07f, 0.03f));
                    // Its padlock, hung from the bar.
                    float body = 0.4f, high = 1.05f, out2 = side * (face + 0.07f);
                    Block(g, lockBrass, new Vector3(0, high - body * 0.75f, out2), new Vector3(body, body, 0.12f));
                    foreach (float x in new[] { -body * 0.3f, body * 0.3f }) Block(g, lockSteel, new Vector3(x, high - 0.04f, out2), new Vector3(body * 0.13f, body * 0.65f, 0.05f));
                    Block(g, lockSteel, new Vector3(0, high + body * 0.25f, out2), new Vector3(body * 0.73f, body * 0.13f, 0.05f));
                }
                locksArt.Add(g.gameObject);
            }
        }

        static Material Coloured(string name, Color colour)
        {
            var m = KitMaterials.New(KitMaterials.Plain, name);
            if (m != null) m.SetColor("_BaseColor", colour);
            return m;
        }

        static GameObject Block(Transform parent, Material material, Vector3 at, Vector3 size)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyNow(o.GetComponent<Collider>());
            o.name = "pieza";
            o.transform.SetParent(parent, false);
            o.transform.localPosition = at;
            o.transform.localScale = size;
            var r = o.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return o;
        }
    }
}
