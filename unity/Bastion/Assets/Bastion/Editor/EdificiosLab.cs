using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// A street made of Synty's whole buildings, to weigh what the city could look like (Ana's #3, 1/11: «los edificios
    /// parecen la misma casa en distintos tamaños»; Arturo: first what the packs have and a proposal with pictures, before
    /// the generator is touched). Four stretches along one street, each a part of the city: the houses of the outskirts,
    /// the high street's shops, the centre's offices and flats, the industrial edge. At the buildings' scale (KIT_SCALE
    /// 1.2), fronts to the street (to the south: the game's camera looks from the south-east), in the game's light by day.
    /// Only what the project has imported already (Town, City, Apocalypse). BASTION_TRAMO=k the stretch framed (0 to 3;
    /// all of it from above if not said), BASTION_DIST tiles off (24, the game's).
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.EdificiosLab.Calle -shotOut calle.png -shotSize 1600x900
    /// </summary>
    public static class EdificiosLab
    {
        /// <summary>The buildings' scale (buildings.ts KIT_SCALE) and the gap between two along the street (m).</summary>
        const float Scale = 1.2f, Gap = 3f;
        /// <summary>The street: its middle on z = 0, this wide (m), its pavements, and how far back the fronts stand from its middle.</summary>
        const float Road = 9, Pavement = 3, Back = Road / 2 + Pavement;

        static readonly (string name, string[] prefabs)[] Stretches =
        {
            ("afueras", new[]
            {
                "PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_01", "PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_03",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_House_01", "PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_05",
                "PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_07", "PolygonApocalypse/Prefabs/Buildings/SM_Bld_House_02",
                "PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_09", "PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_11",
            }),
            ("calle comercial", new[]
            {
                "PolygonCity/Prefabs/Buildings/SM_Bld_Shop_01", "PolygonTown/Prefabs/Buildings/SM_Bld_Shop_01", "PolygonCity/Prefabs/Buildings/SM_Bld_Shop_02",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Cafe_01", "PolygonCity/Prefabs/Buildings/SM_Bld_Shop_03", "PolygonTown/Prefabs/Buildings/SM_Bld_Shop_02",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Shop_Small_01", "PolygonCity/Prefabs/Buildings/SM_Bld_Shop_04", "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Diner_01",
                "PolygonCity/Prefabs/Buildings/SM_Bld_Shop_05", "PolygonTown/Prefabs/Buildings/SM_Bld_Shop_03", "PolygonCity/Prefabs/Buildings/SM_Bld_Shop_06",
            }),
            ("centro", new[]
            {
                "PolygonCity/Prefabs/Buildings/SM_Bld_OfficeOld_Small_01", "PolygonCity/Prefabs/Buildings/SM_Bld_Apartment_01", "PolygonCity/Prefabs/Buildings/SM_Bld_OfficeOld_Large_01",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Commercial_Medium_01", "PolygonCity/Prefabs/Buildings/SM_Bld_Apartment_02", "PolygonCity/Prefabs/Buildings/SM_Bld_OfficeOld_Small_02",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Commercial_Large_01", "PolygonCity/Prefabs/Buildings/SM_Bld_Apartment_03",
            }),
            ("polígono", new[]
            {
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_AutoRepair_01", "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Warehouse_Brick_01",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Industrial_Small_01", "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Motel_01",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Industrial_Medium_01", "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Warehouse_Concrete_01",
                "PolygonApocalypse/Prefabs/Buildings/SM_Bld_Market_Medium_01",
            }),
        };

        public static void Calle()
        {
            MapLab.Lighting(true);
            var root = new GameObject("La calle").transform;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Flat(Color c) => new Material(lit) { color = c };
            // The ground: grass, the pavements, the road (a strip each, along x).
            float length = 0;
            var spans = new List<(float from, float to)>();
            foreach (var (_, prefabs) in Stretches)
            {
                float from = length;
                length = Lay(root, prefabs, length);
                spans.Add((from, length));
                length += Gap * 4;
            }
            void Strip(string name, float z0, float z1, Material m, float y)
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = name;
                strip.transform.SetParent(root, false);
                strip.transform.position = new Vector3(length / 2 - 10, y - 0.05f, (z0 + z1) / 2);
                strip.transform.localScale = new Vector3(length + 40, 0.1f, z1 - z0);
                strip.GetComponent<Renderer>().sharedMaterial = m;
                UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
            }
            Strip("Hierba", -60, 80, Flat(new Color(0.36f, 0.42f, 0.24f)), -0.02f);
            Strip("Acera", -Back, Back, Flat(new Color(0.62f, 0.6f, 0.56f)), 0.08f);
            Strip("Calzada", -Road / 2, Road / 2, Flat(new Color(0.25f, 0.25f, 0.27f)), 0.1f);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 1500;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 24;
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_TRAMO"), out int k) && k >= 0 && k < spans.Count)
                MapSpace.Aim(camera, new Vector3((spans[k].from + spans[k].to) / 2, 0, Back + 8), off);
            else
            {
                camera.fieldOfView = 30;
                camera.transform.position = new Vector3(length / 2, length * 0.75f, -length * 0.55f);
                camera.transform.LookAt(new Vector3(length / 2, 0, 12));
            }
            Debug.Log($"[edificios] {Stretches.Length} stretches, {length:0} m: " + string.Join(", ", spans.Select((s, i) => $"{Stretches[i].name} {s.from:0}–{s.to:0}")));
        }

        /// <summary>
        /// Synty's prefabs `BASTION_PREFABS` (paths under Assets/Synty, without .prefab, by commas; a folder and a name
        /// pattern, «PolygonShops/Prefabs/Buildings:SM_Bld_ShopFront_*», for all that match) in a row at the buildings'
        /// scale, their fronts to the south, to pick pieces from (phase 2 of #3, the shops). Each one's size in the log.
        /// BASTION_DIST how far the camera (metres, 14), BASTION_GIRO its turn from the south (degrees), BASTION_ALTO how
        /// high it looks down from (degrees, 20).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.EdificiosLab.Muestras -shotOut m.png -shotSize 1600x900
        /// </summary>
        public static void Muestras()
        {
            MapLab.Lighting(true);
            var root = new GameObject("Muestras").transform;
            var paths = new List<string>();
            foreach (string item in (Environment.GetEnvironmentVariable("BASTION_PREFABS") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = item.IndexOf(':');
                if (colon < 0)
                {
                    paths.Add(item.Trim());
                    continue;
                }
                string folder = item.Substring(0, colon).Trim();
                var pattern = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(item.Substring(colon + 1).Trim()).Replace("\\*", ".*") + "$");
                paths.AddRange(System.IO.Directory.GetFiles($"Assets/Synty/{folder}", "*.prefab").Select(System.IO.Path.GetFileNameWithoutExtension).Where(n => pattern.IsMatch(n)).OrderBy(n => n).Select(n => $"{folder}/{n}"));
            }
            float length = Lay(root, paths.ToArray(), 0, 1);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(length / 2, -0.05f, Back);
            ground.transform.localScale = new Vector3(length + 40, 0.1f, 60);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.55f, 0.54f, 0.5f) };
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 1500;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            camera.fieldOfView = 30;
            float Env(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : or;
            // Far enough for the whole row as wide as the picture, or as BASTION_DIST says.
            float far = Env("BASTION_DIST", Mathf.Max(14, (length + 2) / 2 / (Mathf.Tan(camera.fieldOfView / 2 * Mathf.Deg2Rad) * 16 / 9)));
            var look = new Vector3(length / 2, 2.5f, Back);
            camera.transform.position = look + Quaternion.Euler(Env("BASTION_ALTO", 20), Env("BASTION_GIRO", 0), 0) * new Vector3(0, 0, -far);
            camera.transform.LookAt(look);
        }

        /// <summary>
        /// Each house of the city maps (engine-cs/golden/step1, BASTION_MAP to name some) as it is drawn (Arturo's phase 1
        /// of #3): its place and size, its roof (hipped or gabled, pitch), the window frame and door it took, whether it
        /// has shutters and a garage door. To find one to frame in a capture.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.EdificiosLab.Casas -quit
        /// </summary>
        public static void Casas()
        {
            var names = Environment.GetEnvironmentVariable("BASTION_MAP")?.Split(',') ?? new[] { "city-1", "city-7", "city-5-night" };
            foreach (string name in names)
            {
                var s = MapLab.Golden(name);
                var plan = new BuildingPlan(s);
                var layout = WallLayout.Of(s, plan);
                foreach (var b in plan.Buildings.Where(b => b.kind == BuildingKinds.House || b.pitched != null))
                {
                    var mine = layout.Pieces.Where(p => p.building == b.index).Select(p => p.id).ToList();
                    var roof = BuildingSurfaces.HouseRoofOf(b);
                    string window = mine.FirstOrDefault(id => id.StartsWith("houseWindow") && id != "houseWindowWall") ?? "-";
                    string door = mine.FirstOrDefault(id => id.StartsWith("houseDoor") && id != "houseDoorWall") ?? "-";
                    Debug.Log(FormattableString.Invariant($"[casas] {name} #{b.index} ({b.x0},{b.y0})–({b.x1},{b.y1}) {b.kind}, {b.storeys} storeys, walk {b.walkableRoof}, pitched {b.pitched ?? "-"} (state {(s.Pitched != null ? "says" : "silent")}): {(roof.hipped ? "hipped" : "gabled")} {Mathf.Atan((float)roof.pitch) * Mathf.Rad2Deg:0}°, {window}, {door}, shutters {mine.Contains("houseShutters")}, garage {(layout.Pieces.FirstOrDefault(p => p.building == b.index && p.id == "houseGarage") is WallPiece g ? $"at {g.matrix.m03:0.0},{g.matrix.m23:0.0} facing {g.outward.x:0},{g.outward.y:0}" : "no")}"));
                }
            }
        }

        /// <summary>
        /// Each shop of the city maps (BASTION_MAP to name some) as the plan has it (phase 2 of #3): its place and size,
        /// storeys, kit, whether its roof is walked on, the side of its front (its sign's door) and what its sign says.
        /// To find one to frame in a capture. BASTION_PIEZAS=k also every wall piece of building k (where, which way). A
        /// generated map by biome:seed[:kind[:site]] (city:3:elimination:industrial), as MapLab's BASTION_GENERAR.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.EdificiosLab.Tiendas -quit
        /// </summary>
        public static void Tiendas()
        {
            var names = Environment.GetEnvironmentVariable("BASTION_MAP")?.Split(',') ?? new[] { "city-1", "city-7", "city-5-night" };
            foreach (string name in names)
            {
                var made = name.Split(':');
                var s = made.Length >= 2 ? MapLab.Generated(made[0], int.Parse(made[1]), made.Length > 2 ? made[2] : "elimination", made.Length > 3 ? made[3] : null) : MapLab.Golden(name);
                var plan = new BuildingPlan(s);
                var layout = WallLayout.Of(s, plan);
                // BASTION_TIPO: another kind of building (office, hall…), or * for all.
                string kind = Environment.GetEnvironmentVariable("BASTION_TIPO") ?? BuildingKinds.Shop;
                foreach (var b in plan.Buildings.Where(b => kind == "*" || b.kind == kind))
                {
                    var signs = layout.Props.Where(p => p.building == b.index && p.id.StartsWith("sign")).ToList();
                    string sign = signs.Count > 0 ? FormattableString.Invariant($"{signs[0].id} at {signs[0].matrix.m03:0.0},{signs[0].matrix.m23:0.0}") : "no sign";
                    int fronts = layout.Pieces.Count(p => p.building == b.index && (p.id == "shopWindow" || p.id == "shopDoor"));
                    // BASTION_PIEZAS=k: every piece of building k, its tile, storey, place and the way it faces.
                    if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_PIEZAS"), out int k) && k == b.index)
                        foreach (var p in layout.Pieces.Where(p => p.building == b.index))
                            Debug.Log(FormattableString.Invariant($"[piezas] #{b.index} {p.id} tile {p.tile % s.Width},{p.tile / s.Width} storey {p.level} at {p.matrix.m03:0.00},{p.matrix.m13:0.00},{p.matrix.m23:0.00} out {p.outward.x:0},{p.outward.y:0} along x {p.matrix.m00:0.00},{p.matrix.m20:0.00} thick {p.thick:0.00} square {p.square} tint {p.tint} roof {p.roof}"));
                    Debug.Log(FormattableString.Invariant($"[tiendas] {name} #{b.index} ({b.x0},{b.y0})–({b.x1},{b.y1}) {b.kind}, {b.storeys} storeys, kit {b.kit}, walk {b.walkableRoof}, leveled {b.leveled}, frame {b.frame}, market {(b.market != null ? b.market.ToString() : "-")}: {sign}, {fronts} front pieces, trade {WallLayout.ShopSign(s, b)}, facing {b.facing ?? "-"}, awning {BuildingLooks.ShopAwning(b) ?? "-"}"));
                }
            }
        }

        /// <summary>The buildings `prefabs` in a row along x from `x`, `gap` apart, their fronts at z = Back, facing the street; where the row ends.</summary>
        static float Lay(Transform root, string[] prefabs, float x, float gap = Gap)
        {
            foreach (var path in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Synty/{path}.prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"[edificios] missing {path}");
                    continue;
                }
                var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                made.transform.SetParent(root, false);
                made.transform.localScale = Vector3.one * Scale;
                // (Synty's buildings have their fronts to −z: turned round, to the street to the south.)
                made.transform.rotation = Quaternion.Euler(0, 180, 0);
                var box = Bounds(made);
                made.transform.position += new Vector3(x - box.min.x, -box.min.y, Back - box.min.z);
                Debug.Log($"[edificios] {path.Split('/').Last()}: {box.size.x:0.0} × {box.size.z:0.0} × {box.size.y:0.0} m at x {x:0}");
                x += box.size.x + gap;
            }
            return x - gap;
        }

        static Bounds Bounds(GameObject g)
        {
            var renderers = g.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var box = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) box.Encapsulate(r.bounds);
            return box;
        }

        /// <summary>
        /// Each piece's box in the wall's frame (tiles: x along it, z out of it), fitted and thickened as WallsView lays it
        /// (thick 1.75 across), its leaves apart (BASTION_PIEZAS_ID=id,id…; the brick kit's unless said): what a corner's or a
        /// door's gap is made of (Ana's #18 of 7/11).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.EdificiosLab.Grosor -quit
        /// </summary>
        public static void Grosor()
        {
            var ids = Environment.GetEnvironmentVariable("BASTION_PIEZAS_ID")?.Split(',') ?? new[] { "bunkerWall", "brickFace", "bunkerLintel", "brickLintel", "metalDoor", "sheetGrey" };
            var thicken = Matrix4x4.Scale(new Vector3(1, 1, 1.75f));
            foreach (string id in ids)
                foreach (var part in Pieces.Parts(id))
                {
                    if (part.mesh == null) continue;
                    var m = thicken * part.fit;
                    var b = part.mesh.bounds;
                    Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
                    for (int k = 0; k < 8; k++)
                    {
                        var c = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z));
                        lo = Vector3.Min(lo, c);
                        hi = Vector3.Max(hi, c);
                    }
                    Debug.Log(FormattableString.Invariant($"[grosor] {id} {part.name}{(part.leaf ? " (leaf)" : "")}: x {lo.x:0.000}…{hi.x:0.000}, y {lo.y:0.000}…{hi.y:0.000}, z {lo.z:0.000}…{hi.z:0.000}"));
                    // (Its planes across: how many of its vertices stand at each depth, to the centimetre, and their reach along.)
                    var planes = part.mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).GroupBy(v => Mathf.Round(v.z * 100) / 100)
                        .OrderBy(g => g.Key).Select(g => FormattableString.Invariant($"{g.Key:0.00} ×{g.Count()} (x {g.Min(v => v.x):0.00}…{g.Max(v => v.x):0.00})"));
                    Debug.Log("[grosor]   " + string.Join("; ", planes));
                }
        }
    }
}
