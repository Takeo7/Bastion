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
            // (Sidekick's zombies in Apocalypse Zombies' parts no preset wears: Ragged.)
            ("skZombieRag1", "ApocalypseZombie_01", true), ("skZombieRag2", "ApocalypseZombie_02", false), ("skZombieRag3", "ApocalypseZombie_03", false),
            ("skZombieRag4", "ApocalypseZombie_04", false), ("skZombieRag5", "ApocalypseZombie_05", false), ("skZombieRag6", "ApocalypseZombie_01", true),
        };

        /// <summary>
        /// Apocalypse Zombies' sixteen parts no preset wears (Arturo, 9/11, of Biblioteca's list: torn shirts and trousers, a
        /// skirt, two hairs, an axe in the head and one in the shoulder, knives in the back, a torn rucksack, a muzzle and
        /// a gas mask), on Sidekick's five zombies: by part type, what each wears instead of its preset's (null: nothing of
        /// that type). Their cells the preset's, or another zombie's where it paints none (RagCells).
        /// </summary>
        static readonly Dictionary<string, (string type, string part)[]> Ragged = new Dictionary<string, (string, string)[]>
        {
            ["skZombieRag1"] = new[] { ("Torso", "SK_ZOMB_BASE_08_10TORS_ZB01"), ("Hips", "SK_ZOMB_BASE_06_17HIPS_ZB01"), ("Hair", "SK_ZOMB_BASE_05_02HAIR_ZB01"), ("AttachmentHead", null) },
            ["skZombieRag2"] = new[] { ("Torso", "SK_ZOMB_BASE_02_10TORS_ZB01"), ("Hips", "SK_ZOMB_BASE_01_17HIPS_ZB01"), ("AttachmentBack", "SK_APOC_ZOMB_11_24ABAC_ZB01"), ("AttachmentShoulderLeft", "SK_APOC_ZOMB_01_29ASHL_ZB01") },
            ["skZombieRag3"] = new[] { ("Torso", "SK_ZOMB_BASE_04_10TORS_ZB01"), ("Hips", "SK_ZOMB_BASE_03_17HIPS_ZB01"), ("Hair", "SK_ZOMB_BASE_02_02HAIR_ZB01"), ("AttachmentHead", null), ("AttachmentFace", "SK_APOC_ZOMB_05_23AFAC_ZB01") },
            ["skZombieRag4"] = new[] { ("Torso", "SK_ZOMB_BASE_05_10TORS_ZB01"), ("Hips", "SK_ZOMB_BASE_01_17HIPS_ZB01"), ("AttachmentHead", "SK_APOC_ZOMB_02_22AHED_ZB01"), ("AttachmentBack", "SK_APOC_ZOMB_02_24ABAC_ZB01") },
            ["skZombieRag5"] = new[] { ("Torso", "SK_ZOMB_BASE_09_10TORS_ZB01"), ("Hips", "SK_ZOMB_BASE_03_17HIPS_ZB01"), ("AttachmentHead", null), ("AttachmentFace", "SK_APOC_ZOMB_03_23AFAC_ZB01") },
            ["skZombieRag6"] = new[] { ("Torso", "SK_ZOMB_BASE_04_10TORS_ZB01"), ("Hips", "SK_ZOMB_BASE_01_17HIPS_ZB01"), ("Hair", "SK_ZOMB_BASE_02_02HAIR_ZB01"), ("AttachmentHead", null), ("AttachmentBack", null), ("AttachmentShoulderLeft", null) },
        };

        /// <summary>Whether a body is one of Ragged's.</summary>
        public static bool IsRagged(string id) => id != null && Ragged.ContainsKey(id);

        /// <summary>
        /// The cells of Ragged's parts no zombie's colour map paints ('x,y' of 16 × 16, from the top left, as hex): the torn
        /// rucksack's canvas and strap, the gas mask's filter and its glass; and the rucksack's cloth, a dull olive, which the
        /// first zombie's paints a loud green.
        /// </summary>
        static readonly Dictionary<int, string> RagCells = new Dictionary<int, string>
        {
            [7 * 16 + 8] = "5b5446", [8 * 16 + 10] = "3b3631", [6 * 16 + 6] = "45463f", [7 * 16 + 6] = "8b8d80", [6 * 16 + 7] = "5d5b48",
        };

        /// <summary>
        /// Cells one body has painted over once its palette is filled, whatever its preset has there ('x,y' as RagCells'):
        /// the 3D review's Z-1 (10/11), skZombieCop1's back. ModernPolice_01 carries on it a pig in a police cap strapped on
        /// like a rucksack (SK_MDRN_POLC_05_24ABAC_HU01), pink in the preset's own cells 7,5, 7,6, 15,6 and 15,7 (B4827E,
        /// A46762, 936B79, A2615C: CombatLab.MuertoPaleta), which DullDead leaves pink (under its saturation): in the
        /// torn rucksack's canvas and strap (RagCells'), it reads from the game's camera as a pack.
        /// </summary>
        static readonly Dictionary<string, Dictionary<int, string>> OwnCells = new Dictionary<string, Dictionary<int, string>>
        {
            ["skZombieCop1"] = new Dictionary<int, string> { [5 * 16 + 7] = "5b5446", [6 * 16 + 7] = "3b3631", [6 * 16 + 15] = "5b5446", [7 * 16 + 15] = "3b3631" },
        };

        /// <summary>
        /// BASTION_Z1_ESPALDA=fuera, when the bodies are baked (FigureCatalogMake): skZombieCop1 without that back piece at
        /// all, the other way Z-1 could go (for Arte to set the two side by side; not the game's).
        /// </summary>
        static bool BackOff(string id, string partType) =>
            id == "skZombieCop1" && partType == "AttachmentBack" && System.Environment.GetEnvironmentVariable("BASTION_Z1_ESPALDA") == "fuera";

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
            // (One of Ragged's: its own parts in place of its preset's, and those of a type its preset hasn't.)
            Ragged.TryGetValue(id, out var swaps);
            var rows = preset["parts"].Select(row => ((string)row[0], (string)row[1])).ToList();
            if (swaps != null) rows.AddRange(swaps.Where(s => s.part != null && !rows.Any(r => r.Item2 == s.type)).Select(s => (s.part, s.type)));
            foreach (var (name, partType) in rows)
            {
                string partName = name;
                if (string.IsNullOrEmpty(partName) || Never.Contains(partType) || BackOff(id, partType)) continue;
                if (swaps != null && swaps.Any(s => s.type == partType))
                {
                    partName = swaps.First(s => s.type == partType).part;
                    if (partName == null) continue;
                }
                else if (Undead.TryGetValue(partType ?? "", out var dead)) partName = dead(n);
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
                // The zombies' parts paint from cells the living preset may not use (pure red: skZombieCop2's beard showed it
                // until 0.12.14), and Ragged's from cells their zombie may not: RagCells', else the zombie's, else another's.
                var others = new[] { dead }.Concat(Enumerable.Range(1, 5).Select(k => catalog.ColourMap($"ApocalypseZombie_0{k}")).Where(m => m != null).Select(SoldierPalette.Cells)).ToList();
                for (int c = 0; c < 256; c++)
                {
                    if (!SoldierPalette.Unused(cells[c])) continue;
                    if (RagCells.TryGetValue(c, out string hex) && ColorUtility.TryParseHtmlString("#" + hex, out var paint)) cells[c] = paint;
                    else if (others.FirstOrDefault(m => !SoldierPalette.Unused(m[c])) is Color32[] other) cells[c] = other[c];
                }
                // (And its own cells painted over: OwnCells, Z-1.)
                if (OwnCells.TryGetValue(id, out var painted))
                    foreach (var kv in painted)
                        if (ColorUtility.TryParseHtmlString("#" + kv.Value, out var paint)) cells[kv.Key] = paint;
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
