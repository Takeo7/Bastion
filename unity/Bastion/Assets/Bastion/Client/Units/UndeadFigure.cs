using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The living made dead (syntyManifest.ts CHARACTERS skZombieCivil, skZombieCop, skZombieSurv; convert.ts UNDEAD and
    /// convert.py sidekick): a living Sidekick preset, a civilian, a policeman or a survivor in their clothes, its head,
    /// face and bare body the zombies' (picked by its id), on the preset's own build, painted from its own colour map
    /// but the body's cells (skin, eyes, hair) from one of the five zombies'. Put together by Sidekick's runtime as the
    /// soldiers are (SoldierFigure), one mesh, 75–290 ms each: baked in the editor (FigureCatalogMake, the catalog's
    /// presets by their id): in the game only the bakes are drawn (Gameplay 26/10: the Sidekick parts no wardrobe names
    /// stay out of the app, so there is nothing to put one together from there).
    /// </summary>
    public static class UndeadFigure
    {
        /// <summary>0.10.1's: its id (the pick of the parts and of the zombie's palette goes by it), the living preset, its build.</summary>
        public static readonly (string id, string preset, bool female)[] Bodies =
        {
            ("skZombieCivil1", "ModernCivilian_01", true), ("skZombieCivil2", "ModernCivilian_02", true), ("skZombieCivil3", "ModernCivilian_03", true),
            ("skZombieCivil4", "ModernCivilian_04", false), ("skZombieCivil5", "ModernCivilian_05", true), ("skZombieCivil6", "ModernCivilian_06", false),
            ("skZombieCop1", "ModernPolice_01", false), ("skZombieCop2", "ModernPolice_05", false), ("skZombieCop3", "ModernPolice_07", false),
            ("skZombieCop4", "ModernPolice_04", true), ("skZombieSurv1", "ApocalypseSurvivor_04", false), ("skZombieSurv2", "ApocalypseSurvivor_01", true),
        };

        /// <summary>convert.ts SIDEKICK_NEVER: the teeth and the tongue never show.</summary>
        static readonly HashSet<string> Never = new HashSet<string> { "Teeth", "Tongue" };

        /// <summary>convert.ts UNDEAD: what of a living preset the zombies' takes the place of, by part type (n: its id's number).</summary>
        static readonly Dictionary<string, System.Func<uint, string>> Undead = new Dictionary<string, System.Func<uint, string>>
        {
            ["Head"] = n => Zb(1, "01HEAD"),
            ["EyeLeft"] = n => Zb(1, "05EYEL"),
            ["EyeRight"] = n => Zb(1, "06EYER"),
            ["EarLeft"] = n => Zb(1, "07EARL"),
            ["EarRight"] = n => Zb(1, "08EARR"),
            ["Nose"] = n => Zb((int)(n % 6) + 1, "35NOSE"),
            ["EyebrowLeft"] = n => Zb((int)(n % 8) + 1, "03EBRL"),
            ["EyebrowRight"] = n => Zb((int)(n % 8) + 1, "04EBRR"),
            ["Hair"] = n => Zb((int)(n % 10) + 1, "02HAIR"),
            ["FacialHair"] = n => Zb((int)(n % 4) + 1, "09FCHR"),
        };

        static string Zb(int k, string code) => $"SK_ZOMB_BASE_{k:00}_{code}_ZB01";

        /// <summary>convert.ts idNumber: `h * 31 + c` over the id's characters from 7, on 32 bits.</summary>
        static uint IdNumber(string id)
        {
            uint h = 7;
            foreach (char c in id) h = unchecked(h * 31 + c);
            return h;
        }

        static DatabaseManager database;
        static Newtonsoft.Json.Linq.JObject presets;

        static Newtonsoft.Json.Linq.JObject Presets()
        {
            if (presets == null && Resources.Load<TextAsset>("Undead") is TextAsset file) presets = Newtonsoft.Json.Linq.JObject.Parse(file.text);
            return presets;
        }
        static Transform shelf;

        /// <summary>
        /// Sidekick's parts, and where those no wardrobe piece names are kept out of Resources, so that they stay out of the app
        /// (FigureCatalogMake.Trim): in the editor the bake still finds them there.
        /// </summary>
        public const string Meshes = "Assets/Synty/SidekickCharacters/Resources/Meshes/", Spare = "Assets/Synty/Extra/SidekickSpare/Meshes/";

        /// <summary>A part's model, from Resources or, in the editor, from where FigureCatalogMake.Trim put it; null if neither.</summary>
        static GameObject PartModel(SidekickPart part)
        {
            if (part == null) return null;
            var model = part.GetPartModel();
#if UNITY_EDITOR
            if (model == null) model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(part.Location.Replace('\\', '/').Replace(Meshes, Spare));
#endif
            return model;
        }

        /// <summary>A body made now, kept by no one (FigureCatalogMake bakes them: in the game they are its presets, ready made).</summary>
        public static GameObject Fresh(string id) => Build(id);

        static GameObject Build(string id)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var body = Bodies.FirstOrDefault(b => b.id == id);
            var catalog = FigureCatalog.Load();
            if (body.id == null || catalog == null) return null;
            if (database == null)
            {
                database = new DatabaseManager();
                database.GetDbConnection();
            }
            // Its parts and build as its .sk file has them (FigureCatalogMake: Resources/Undead.json).
            var presets = Presets();
            if (presets == null || !(presets[body.preset] is Newtonsoft.Json.Linq.JObject preset))
            {
                Debug.LogWarning($"[figures] no {body.preset} in Resources/Undead.json (Bastion.EditorTools.FigureCatalogMake.Run)");
                return null;
            }
            uint n = IdNumber(id);
            var parts = new List<SkinnedMeshRenderer>();
            foreach (var row in preset["parts"])
            {
                string partName = (string)row[0], partType = (string)row[1];
                if (string.IsNullOrEmpty(partName) || Never.Contains(partType)) continue;
                if (Undead.TryGetValue(partType ?? "", out var dead)) partName = dead(n);
                // A bare arm, hand or leg the zombies' bare one, where there is one.
                else if (partName.StartsWith("SK_HUMN_BASE_"))
                {
                    string bare = Regex.Replace(Regex.Replace(partName, @"^SK_HUMN_BASE_\d\d_", "SK_ZOMB_BASE_01_"), "_HU01$", "_ZB01");
                    if (SidekickPart.SearchForByName(database, bare) != null) partName = bare;
                }
                var part = PartModel(SidekickPart.SearchForByName(database, partName));
                var renderer = part != null ? part.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (renderer == null)
                {
                    Debug.LogWarning($"[figures] {id}: no Sidekick part {partName}");
                    continue;
                }
                parts.Add(renderer);
            }
            if (parts.Count == 0) return null;
            // Its own colour map, the body's cells one of the zombies' (convert.py sidekick: bodyPalette, BODY_CELLS).
            var own = catalog.ColourMap(body.preset);
            var zombie = catalog.ColourMap($"ApocalypseZombie_0{n % 5 + 1}");
            Texture2D palette = own;
            if (own != null && zombie != null)
            {
                var cells = SoldierPalette.Cells(own);
                var dead = SoldierPalette.Cells(zombie);
                for (int c = 0; c < 256; c++) if (SoldierPalette.IsBody(c)) cells[c] = dead[c];
                palette = SoldierPalette.Texture(cells);
                palette.name = id + " palette";
            }
            // The preset's build (its sliders, ±100: convert.ts body: heavy or skinny by its size, buff by its muscle).
            float type = (float)preset["type"], size = (float)preset["size"], muscle = (float)preset["muscle"];
            var baseModel = Resources.Load<GameObject>("Meshes/SK_BaseModel");
            var material = new Material(Resources.Load<Material>("Materials/M_BaseMaterial"));
            if (palette != null) material.SetTexture("_ColorMap", palette);
            var runtime = new SidekickRuntime(baseModel, material, null, database)
            {
                BodyTypeBlendValue = type,
                BodySizeSkinnyBlendValue = Mathf.Max(0, -size),
                BodySizeHeavyBlendValue = Mathf.Max(0, size),
                MusclesBlendValue = Mathf.Max(0, muscle),
            };
            var model = runtime.CreateCharacter(id, parts, true, true);
            SoldierFigure.OwnColours(model);
            if (shelf == null)
            {
                shelf = new GameObject("Muertos hechos").transform;
                shelf.gameObject.hideFlags = HideFlags.HideInHierarchy;
            }
            model.transform.SetParent(shelf, false);
            model.SetActive(false);
            Debug.Log($"[figures] {id}: {body.preset} made dead, {parts.Count} parts, the zombie's palette ApocalypseZombie_0{n % 5 + 1}, in {clock.Elapsed.TotalMilliseconds:0} ms");
            return model;
        }
    }
}
