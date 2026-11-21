using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The raiders' camp (Saqueadores; Producto, 20/11: .dev-snapshots/arte/unity/runs/saqueadores/), a look to choose from
    /// before the engine lays it: on a generated map, a clearing made by hand (its tiles and decor cleared), the camping's
    /// own tents, fire, chairs and sleeping bag by its marks (MapBuilder.CampScenery, CampFire), and by hand what is new:
    /// the fence of low cover with gaps (sandbags, sheets and pallets, tyres: Apocalypse's) and the stash, a few crates to
    /// search in their three states. The raiders by the fire, a soldier and a dead one beside it, for the size.
    ///   BASTION_GENERAR=wilds,3 BASTION_CAMPO=20,20 BASTION_CERCO=sacos|chatarra|mezcla BASTION_ALIJO=cerrado|lleno|vacio|mezcla
    ///   BASTION_DIST=24 BASTION_GIRO=45 BASTION_NOCHE=1
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.SaqueadoresLab.Campamento -shotCamera "Shot camera" …
    /// </summary>
    public static class SaqueadoresLab
    {
        /// <summary>The clearing (tiles), the fence a tile in from its edge.</summary>
        const int Wide = 13, Deep = 11;

        /// <summary>The clearing Campamento last made, in the world (x, z).</summary>
        static Rect clearing;

        static string Env(string name, string or) => Environment.GetEnvironmentVariable(name) ?? or;

        public static void Campamento()
        {
            var made = Env("BASTION_GENERAR", "wilds,3").Split(',');
            var state = MapLab.Generated(made[0], int.Parse(made[1]), made.Length > 2 ? made[2] : "elimination", made.Length > 3 ? made[3] : null);
            var at = Env("BASTION_CAMPO", $"{state.Width / 2 - Wide / 2},{state.Height / 2 - Deep / 2}").Split(',').Select(int.Parse).ToArray();
            int ox = at[0], oy = at[1];
            clearing = new Rect(ox * MapSpace.Tile, -(oy + Deep) * MapSpace.Tile, Wide * MapSpace.Tile, Deep * MapSpace.Tile);
            bool night = Env("BASTION_NOCHE", "0") == "1";
            string fence = Env("BASTION_CERCO", "mezcla"), stash = Env("BASTION_ALIJO", "mezcla");
            Debug.Log($"[saqueadores] map {state.Width}×{state.Height}, the clearing from {ox},{oy}, {Wide}×{Deep}; fence {fence}, stash {stash}");
            int I(int x, int y) => (oy + y) * state.Width + ox + x;
            // The clearing: open ground, nothing on it.
            for (int y = 0; y < Deep; y++)
                for (int x = 0; x < Wide; x++)
                {
                    state.Tiles[I(x, y)] = TileKinds.Floor;
                    if (state.Decor != null && I(x, y) < state.Decor.Count) state.Decor[I(x, y)] = 0;
                    if (state.Elev != null && I(x, y) < state.Elev.Count) state.Elev[I(x, y)] = state.Elev[I(0, 0)];
                }
            if (night) state.Mission.Night = true;
            // The camping's marks: two dome tents and a long one (their blocks low cover), the fire, chairs, a sleeping bag.
            var site = new InteriorSite { Kind = InteriorKinds.Camping, X = ox, Y = oy, W = Wide, H = Deep };
            void Mark(string kind, int x, int y, int facing = 2) => site.Dressing.Add(new InteriorDetail { Kind = kind, X = ox + x, Y = oy + y, Facing = facing });
            void Block(int x0, int y0, int w, int h)
            {
                for (int y = y0; y < y0 + h; y++)
                    for (int x = x0; x < x0 + w; x++) state.Tiles[I(x, y)] = TileKinds.Low;
            }
            Block(2, 2, 2, 2);
            Mark(CampDetailKinds.TentDome, 2, 2, 2);
            Block(9, 2, 2, 2);
            Mark(CampDetailKinds.TentDome, 9, 2, 3);
            Block(2, 6, 3, 2);
            Mark(CampDetailKinds.TentLong, 2, 6, 1);
            Mark(CampDetailKinds.FirePit, 6, 5);
            Mark(CampDetailKinds.CampChair, 5, 4, 1);
            Mark(CampDetailKinds.CampChair, 7, 4, 3);
            Mark(CampDetailKinds.CampChair, 6, 6, 0);
            Mark(CampDetailKinds.SleepingBag, 7, 7, 1);
            (state.Interiors ??= new List<InteriorSite>()).Add(site);
            // (The map's own lamps on the clearing go with what stood there.)
            state.Lights?.RemoveAll(l => l.Tile.X >= ox && l.Tile.X < ox + Wide && l.Tile.Y >= oy && l.Tile.Y < oy + Deep);
            var fireTile = new Pos { X = ox + 6, Y = oy + 5 };
            (state.Lights ??= new List<Bastion.Engine.Light>()).Add(new Bastion.Engine.Light { Id = "hoguera", Tile = fireTile, Radius = 5, Tiles = Los.LightTiles(state, fireTile, 5, Content.Night.LampHeight) });
            // The fence, a tile in: gaps for two ways in and a few where it was never finished.
            var gaps = new HashSet<(int, int)> { (5, 9), (6, 9), (11, 4), (11, 5), (4, 1), (8, 1), (1, 6) };
            var ring = new List<(int x, int y, int yaw)>();
            for (int x = 1; x <= 11; x++)
            {
                ring.Add((x, 1, 0));
                ring.Add((x, 9, 0));
            }
            for (int y = 2; y <= 8; y++)
            {
                ring.Add((1, y, 90));
                ring.Add((11, y, 90));
            }
            ring.RemoveAll(r => gaps.Contains((r.x, r.y)));
            foreach (var r in ring) state.Tiles[I(r.x, r.y)] = TileKinds.Low;
            // BASTION_CERCO=marcas: the fence and the stash by the engine's marks (Gameplay's contract of 20/11), as the game
            // will draw them (MapBuilder.CampScenery), nothing by hand.
            bool marks = fence == "marcas";
            if (marks)
                foreach (var r in ring)
                {
                    bool corner = (r.x == 1 || r.x == 11) && (r.y == 1 || r.y == 9);
                    if (corner) Mark(CampDetailKinds.BarricadeCorner, r.x, r.y, r.x == 11 && r.y == 1 ? 0 : r.x == 11 ? 1 : r.y == 9 ? 2 : 3);
                    else Mark(CampDetailKinds.Barricade, r.x, r.y, r.y == 1 ? 0 : r.x == 11 ? 1 : r.y == 9 ? 2 : 3);
                }
            // The stash: four crates together, by the east tent.
            var crates = new[] { (8, 6), (9, 6), (9, 7), (8, 7) };
            foreach (var (x, y) in crates) state.Tiles[I(x, y)] = TileKinds.Low;
            if (marks) foreach (var (x, y) in crates) Mark(CampDetailKinds.StashCrate, x, y, x == 8 ? 3 : 1);
            // Who is there: the raiders by the fire, a soldier at the way in, a dead one outside.
            state.Units.RemoveAll(u => u.Team != Teams.Xcom);
            Unit Put(string id, string template, int x, int y)
            {
                var u = Setup.MakeUnit(id, template, id, null, new Pos { X = ox + x, Y = oy + y }, null);
                state.Units.Add(u);
                return u;
            }
            Put("saq1", TemplateIds.Trooper, 5, 5);
            Put("saq2", TemplateIds.Officer, 7, 5);
            Put("saq3", TemplateIds.Lancer, 6, 3);
            Put("z1", TemplateIds.Zombie, 7, 10);
            var squad = state.Units.Where(u => u.Team == Teams.Xcom).ToList();
            for (int k = 0; k < squad.Count; k++) squad[k].Pos = new Pos { X = ox + 5 + k % 2, Y = oy + 10 + k / 2 * 100 };
            state.Units.RemoveAll(u => u.Team == Teams.Xcom && u.Pos.Y >= state.Height);

            Bastion.Client.NightLights.StateForLab = state;
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, night));
            var view = MapLab.Map(state, "saqueadores");
            var prefabs = MapLab.Prefabs();
            var root = new GameObject("Campamento").transform;
            root.SetParent(view.transform, false);

            // `key`'s model on tile (x, y): `along` tiles long (its longer side), no taller than `tall` metres, on the ground.
            GameObject Fit(string key, float x, float y, float yaw, float along, float tall, float lift = 0)
            {
                var model = prefabs(key);
                if (model == null)
                {
                    Debug.LogWarning($"[saqueadores] {key}: none");
                    return null;
                }
                var o = (GameObject)UnityEngine.Object.Instantiate(model, root);
                o.name = key;
                o.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var box = Box(o);
                float k = along * MapSpace.Tile / Mathf.Max(box.size.x, box.size.z);
                float ky = Mathf.Min(k, tall / box.size.y);
                o.transform.localScale = Vector3.Scale(o.transform.localScale, new Vector3(k, tall > 0 ? ky : k, k));
                // (Long along x: one long along z turned a quarter.)
                o.transform.rotation = Quaternion.Euler(0, yaw + (box.size.z > box.size.x ? 90 : 0), 0);
                box = Box(o);
                var spot = MapSpace.TileToWorld(state, new Pos { X = ox, Y = oy }) + new Vector3(x * MapSpace.Tile, lift, -y * MapSpace.Tile);
                o.transform.position += new Vector3(spot.x - box.center.x, spot.y - box.min.y, spot.z - box.center.z);
                box = Box(o);
                Debug.Log(FormattableString.Invariant($"[saqueadores] {key} on {x:0.0},{y:0.0}: {box.size.x:0.00} × {box.size.y:0.00} × {box.size.z:0.00} m at {box.center.x:0.0},{box.center.y:0.0},{box.center.z:0.0}, {o.GetComponentsInChildren<Renderer>().Count(r => r.enabled)} drawn"));
                return o;
            }

            const string A = "apocalypse:SM_Prop_";
            string[] junk = { A + "Barricade_Corrugated_01", A + "Barricade_Pallet_01", A + "Barricade_Metal_01", A + "Barricade_DiamondPlate_01", A + "Barricade_01", A + "Barricade_04" };
            foreach (var r in marks ? new List<(int x, int y, int yaw)>() : ring)
            {
                float h = MapArt.Hash(r.x, r.y, 31);
                bool corner = (r.x == 1 || r.x == 11) && (r.y == 1 || r.y == 9);
                bool bags = fence == "sacos" || fence == "mezcla" && MapArt.Hash((r.x + r.y) / 3, r.yaw, 7) < 0.5f;
                float yaw = corner ? (r.x == r.y || r.x + r.y == 20 ? 135 : 45) : r.yaw;
                if (bags) Fit(A + "Sandbag_Wall_02", r.x, r.y, yaw + (h < 0.5f ? 180 : 0), 1.08f, 0);
                else Fit(junk[Mathf.Min(junk.Length - 1, (int)(h * junk.Length))], r.x, r.y, yaw + (h * 7 % 1 < 0.5f ? 180 : 0) + (h * 13 % 1 - 0.5f) * 14, 1.02f, 1.15f);
                // (Tyres at the foot of some of the sheets.)
                if (!bags && h * 5 % 1 < 0.35f) Fit(A + "Tire_Pile_01", r.x + (r.yaw == 0 ? 0.1f : 0.32f), r.y + (r.yaw == 0 ? 0.32f : 0.1f), h * 360, 0.5f, 0);
            }

            // The stash's crates: Apocalypse's orange one on a pallet. Shut; open and full (its lid against it, boxes and
            // tins in it); open and empty (its lid on the ground).
            for (int n = 0; n < (marks ? 0 : crates.Length); n++)
            {
                var (x, y) = crates[n];
                string how = stash == "mezcla" ? new[] { "cerrado", "lleno", "vacio", "cerrado" }[n] : stash;
                float turn = (MapArt.Hash(x, y, 5) - 0.5f) * 24;
                Fit(A + "Pallet_01", x, y, turn, 0.92f, 0);
                var crate = Fit(A + "Crate_Open_03", x, y, turn, 0.8f, 0, 0.14f);
                if (crate == null) continue;
                var lid = crate.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.Contains("Lid"));
                if (how == "cerrado" || lid == null) continue;
                if (how == "lleno")
                {
                    // Its lid up on its back edge.
                    lid.localRotation = Quaternion.Euler(-78, 0, 0) * lid.localRotation;
                    lid.localPosition += new Vector3(0, 0.22f, -0.12f);
                    Fit("apocalypse:SM_Prop_CardboardBox_02", x - 0.14f, y, turn + 12, 0.36f, 0, 0.36f);
                    Fit("apocalypse:SM_Prop_Ammo_Box_01", x + 0.17f, y + 0.07f, turn - 20, 0.3f, 0, 0.4f);
                    Fit("apocalypse:SM_Prop_MedicalBox_02", x + 0.13f, y - 0.13f, turn + 30, 0.28f, 0, 0.4f);
                }
                else
                {
                    // Its lid thrown on the ground beside it.
                    lid.SetParent(root, true);
                    var b = Box(lid.gameObject);
                    lid.rotation = Quaternion.Euler(0, turn + 35, 0) * lid.rotation;
                    lid.position += new Vector3(0.55f * MapSpace.Tile, MapSpace.TileToWorld(state, new Pos { X = ox, Y = oy }).y + 0.02f - b.min.y, -0.5f * MapSpace.Tile);
                }
            }

            var units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
            units.Sync(state);
            foreach (var u in state.Units) units.Reveal(u.Id);
            units.MasksNow();

            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            float F(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : or;
            var look = Env("BASTION_MIRA", "6,5").Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            MapSpace.Aim(camera, MapSpace.TileToWorld(state, new Pos { X = ox, Y = oy }) + new Vector3(look[0] * MapSpace.Tile, 0, -look[1] * MapSpace.Tile), F("BASTION_DIST", 24), F("BASTION_GIRO", 45));
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, night));
        }

        /// <summary>
        /// What the camp costs to draw (Producto, 20/11): Campamento's (BASTION_CERCO=marcas), the game's camera on it at 24
        /// tiles, at each quality level, 1920×1080, with the camp's scenery and its guards drawn and without (their
        /// renderers off), in turns three times, the least of each, ms a frame. In the editor, on this machine's GPU.
        ///   BASTION_CERCO=marcas unity/tools/unity.sh -executeMethod Bastion.EditorTools.SaqueadoresLab.Coste -quit
        /// </summary>
        public static void Coste()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            Campamento();
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            double Frames()
            {
                void One()
                {
                    camera.Render();
                    RenderTexture.active = target;
                    probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    probe.Apply(false);
                    RenderTexture.active = null;
                }
                for (int k = 0; k < 10; k++) One();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < 150; k++) One();
                return watch.Elapsed.TotalMilliseconds / 150;
            }
            // The camp's: what CampScenery drew (the packs' prefabs whole, by their names), the fire, the guards.
            var camp = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => clearing.Contains(new Vector2(r.bounds.center.x, r.bounds.center.z)) && r.bounds.size.x < 12 && r.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("SM_Prop_") || t.name.StartsWith("SM_Bld_") || t.name == "Hoguera" || t.name.StartsWith("saq"))).ToList();
            Debug.Log($"[coste] the camp: {camp.Count} renderers, {camp.Sum(r => r is MeshRenderer && r.GetComponent<MeshFilter>()?.sharedMesh != null ? r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3 : r is SkinnedMeshRenderer s && s.sharedMesh != null ? s.sharedMesh.triangles.Length / 3 : 0)} triangles");
            int was = QualitySettings.GetQualityLevel();
            foreach (string level in QualitySettings.names)
            {
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, level), true);
                double with = double.MaxValue, without = double.MaxValue;
                for (int turn = 0; turn < 3; turn++)
                {
                    foreach (var r in camp) r.enabled = true;
                    with = Math.Min(with, Frames());
                    foreach (var r in camp) r.enabled = false;
                    without = Math.Min(without, Frames());
                }
                foreach (var r in camp) r.enabled = true;
                Debug.Log(FormattableString.Invariant($"[coste] {level}: {with:0.00} ms a frame with the camp, {without:0.00} without: {with - without:+0.00;-0.00}"));
            }
            QualitySettings.SetQualityLevel(was, true);
            camera.targetTexture = null;
        }

        static Bounds Box(GameObject o)
        {
            var renderers = o.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(o.transform.position, Vector3.one * 0.01f);
            var box = renderers[0].bounds;
            foreach (var r in renderers) box.Encapsulate(r.bounds);
            return box;
        }
    }
}
