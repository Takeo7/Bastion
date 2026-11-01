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

        /// <summary>The buildings `prefabs` in a row along x from `x`, their fronts at z = Back, facing the street; where the row ends.</summary>
        static float Lay(Transform root, string[] prefabs, float x)
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
                x += box.size.x + Gap;
            }
            return x - Gap;
        }

        static Bounds Bounds(GameObject g)
        {
            var renderers = g.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var box = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) box.Encapsulate(r.bounds);
            return box;
        }
    }
}
