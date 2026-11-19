using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Effects;
using Bastion.Client.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The gear's pictures (Bastion.Client.GearPictures) for the runs' grids, rendered into Resources/GearPictures/
    /// &lt;kind&gt;_&lt;type&gt;.png (&lt;kind&gt;.png for mags, medicine and supplies), 128 px a cell (a rifle of 4×1, 512×128): each
    /// thing alone, laid along the picture with its muzzle, tip or head to the right, seen from its side a little turned
    /// (that end toward us) and from above (its volume), orthographic, filling its cells but for 10 px round it (8 % of a
    /// cell); a warm key from the upper left, a soft cool fill from the right, a rim from behind (its edges on a dark
    /// grid), the portraits' studio (Studio: on nothing, nothing else lit) and tone. The models (Things): the soldier's
    /// weapon (Anims.FoundModel → Anims.Weapon(art).fbx, FigureCatalog's; the sawn-off's pump shotgun cut short), the
    /// grenades and the molotov thrown (FxArt), the Armería's loose silencer (Settlement.Silencer), two Sidekick torsos
    /// without their skin for the vests, and Synty's props for the rest (Things; the choice in
    /// .dev-snapshots/arte/unity/runs/iconos/LEEME.md).
    /// BASTION_OBJETOS=a,b only those (their files' names: weapon_assaultRifle, mags…); BASTION_OUT=dir copies there too;
    /// BASTION_HOJAS=dir the contact sheets of every picture there is (hoja-oscura.png, hoja-clara.png at 52 px a cell, as
    /// the base shows them; -40 the battle's). To choose a model: BASTION_MUESTRAS=pack:model,… (or sk:part:preset;
    /// after one, @WxH its cells, then +u standing or +g as a gun's part), those as pictures into BASTION_OUT (required)
    /// and their sheets, nothing into the game.
    /// BASTION_VISTA=yaw,tilt and BASTION_LUZ=key,fill,rim,sky,exposure try another view or light.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.GearPicturesMake.Run -quit
    /// </summary>
    public static class GearPicturesMake
    {
        const string Folder = "Assets/Bastion/Client/Resources/" + GearPictures.Folder;
        const int Cell = GearPictures.CellSize;
        /// <summary>Clear round each picture (8 % of a cell), whatever its cells.</summary>
        static readonly int Margin = Mathf.RoundToInt(Cell * 0.08f);
        /// <summary>Drawn at this many times the picture's size and averaged down.</summary>
        const int Super = 4;
        /// <summary>The view, degrees: turned (its right end toward us) and tilted (its top toward us), as Tarkov's or XCOM 2's.</summary>
        static float yaw = 14, tilt = 22;
        /// <summary>What stands is turned further (a box shows two sides).</summary>
        const float StandingYaw = 32;
        static float key = 3.4f, fill = 1.6f, rim = 3f, sky = 2.6f, exposure = 1.3f;

        /// <summary>One picture: its file, its cells, its model and how it lies.</summary>
        sealed class Thing
        {
            public string file;
            public int w = 1, h = 1;
            public string source;
            public Func<GameObject> make;
            /// <summary>In its model's frame: what runs to the picture's right (none: its longest axis, towards its tip).</summary>
            public Vector3? right;
            /// <summary>In its model's frame: what looks at us (none: its thinnest axis other than that, its + side).</summary>
            public Vector3? facing;
            /// <summary>Where it is held (its tip is the far end from it), its model's frame.</summary>
            public Vector3 grip;
            /// <summary>A gun: its muzzle (+z) to the right and its right side (+x) to us.</summary>
            public bool gun;
            /// <summary>Standing as it stands (a box, a bottle, a magazine): its +x to the right, its −z to us, turned further.</summary>
            public bool upright;
            /// <summary>Its own view (degrees), if not the common one.</summary>
            public float? yaw, tilt;
            /// <summary>Cut short along its +z: kept from the first to the second, as parts of its length (its model's frame).</summary>
            public Vector2? cut;
        }

        /// <summary>Where the sawn-off's pump shotgun (SM_Wep_Shotgun_01) is cut, parts of its length: its stock off behind the grip, its barrel a little past the pump.</summary>
        static readonly Vector2 SawnOff = new Vector2(0.2f, 0.8f);

        /// <summary>The weapons (FoundWeaponIds and the knife) and their cells (H0-DISENO §2, Content.GearSize).</summary>
        static readonly (string id, int w, int h)[] Weapons =
        {
            ("machineGun", 4, 2), ("lightMachineGun", 4, 2),
            ("assaultRifle", 4, 1), ("pumpShotgun", 4, 1), ("precisionRifle", 4, 1), ("huntingRifle", 4, 1), ("scopedRifle", 4, 1),
            ("pistol", 2, 1), ("sawnOff", 2, 1),
            ("machete", 3, 1), ("bat", 3, 1), ("crowbar", 3, 1), ("chainsaw", 3, 1), ("katana", 3, 1), ("longsword", 3, 1), ("rake", 3, 1), ("stick", 3, 1),
            ("knife", 2, 1), ("pan", 2, 1),
        };

        public static void Run()
        {
            PortraitLab.EnsureTone();
            ReadTrials();
            string copies = Environment.GetEnvironmentVariable("BASTION_OUT");
            string sheets = Environment.GetEnvironmentVariable("BASTION_HOJAS");
            string samples = Environment.GetEnvironmentVariable("BASTION_MUESTRAS");
            if (samples != null)
            {
                if (copies == null) throw new ArgumentException("BASTION_MUESTRAS needs BASTION_OUT");
                Samples(samples, copies);
                return;
            }
            Directory.CreateDirectory(Folder);
            if (copies != null) Directory.CreateDirectory(copies);
            var only = Environment.GetEnvironmentVariable("BASTION_OBJETOS")?.Split(',').Select(s => s.Trim()).ToList();
            var things = Things().Where(t => only == null || only.Contains(t.file)).ToList();
            var made = new List<string>();
            Warm();
            foreach (var t in things)
            {
                var png = Picture(t);
                if (png == null) continue;
                string path = $"{Folder}/{t.file}.png";
                File.WriteAllBytes(path, png);
                if (copies != null) File.WriteAllBytes(Path.Combine(copies, t.file + ".png"), png);
                made.Add(path);
            }
            AssetDatabase.Refresh();
            foreach (string path in made) Import(path);
            Debug.Log($"[objetos] {made.Count} of {things.Count} pictures in {Folder}");
            if (sheets != null)
            {
                Directory.CreateDirectory(sheets);
                var all = Things().Select(t => (t.file, t.w, t.h, path: $"{Folder}/{t.file}.png")).Where(x => File.Exists(x.path)).ToList();
                Sheets(all, sheets, "hoja");
            }
        }

        /// <summary>As PiecePictures': straight alpha, no compression (its edges), clamped; mipmaps, as the grids show them at a third.</summary>
        static void Import(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void ReadTrials()
        {
            float[] Floats(string name) => Environment.GetEnvironmentVariable(name)?.Split(',').Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            var view = Floats("BASTION_VISTA");
            if (view != null && view.Length >= 2) (yaw, tilt) = (view[0], view[1]);
            var light = Floats("BASTION_LUZ");
            if (light != null && light.Length >= 5) (key, fill, rim, sky, exposure) = (light[0], light[1], light[2], light[3], light[4]);
        }

        /// <summary>Every picture the game has, in the sheets' order: the weapons, the vests, the belt's, the mods and the piles.</summary>
        static List<Thing> Things()
        {
            // (Each looked up when drawn: a new scene unloads what nothing holds.)
            FigureCatalog Catalog() => FigureCatalog.Load();
            var prefabs = MapLab.Prefabs();
            var list = new List<Thing>();
            Thing Prop(string file, string model, bool gun = false, bool upright = false) => new Thing { file = file, source = model, make = () => Copy(prefabs(model)), gun = gun, upright = upright };
            Thing Fx(string file, Func<FxArt, GameObject> of, string name) => new Thing { file = file, source = "FxArt." + name, upright = true, make = () =>
            {
                var fx = AssetDatabase.LoadAssetAtPath<FxArt>("Assets/Bastion/Client/Resources/FxArt.asset");
                if (fx == null) Debug.LogWarning("[objetos] no FxArt");
                return Copy(fx != null ? of(fx) : null);
            } };
            foreach (var (id, w, h) in Weapons)
            {
                // The model the soldier carries (UnitViews.ArmamentOf at tier 1); the knife and the pistol by their own art.
                var art = Anims.Weapon(Anims.FoundModel(id, 1) ?? id);
                list.Add(new Thing
                {
                    file = "weapon_" + id, w = w, h = h, source = art?.fbx,
                    make = () => Weapon(art != null ? Catalog()?.Weapon(art.fbx) : null),
                    gun = art != null && !art.melee,
                    grip = art?.grip != null && art.grip.Length >= 3 ? new Vector3(art.grip[0], art.grip[1], art.grip[2]) : Vector3.zero,
                    // The sawn-off is the pump shotgun in the hands (FoundModels): here sawn, its stock off behind the grip
                    // and its barrel past the pump, or it would be the long gun drawn small.
                    // (Not cut any more: since 18/11 the sawn-off is Apocalypse's own short one, whole; T22.)
                    cut = null,
                });
                if (id == "machineGun") list.AddRange(Vests());
            }
            // The belt's, standing: the grenades and the bottle thrown in the game, and a red case with a white cross.
            list.Add(Fx("item_fragGrenade", a => a.grenade, "grenade"));
            list.Add(Fx("item_molotov", a => a.molotov, "molotov"));
            list.Add(Fx("item_smokeGrenade", a => a.grenadeSmoke, "grenadeSmoke"));
            list.Add(Fx("item_flashbang", a => a.grenadeFlash, "grenadeFlash"));
            list.Add(Prop("item_firstAid", "apocalypse:SM_Prop_Medical_Container_01", upright: true));
            // The mods as a gun's parts (their muzzle's way +z): a sight with its caps, a wooden thumbhole stock (its butt to the left),
            // a drum magazine, the Armería's silencer.
            list.Add(Prop("mod_scope", "military:SM_Wep_Mod_Scope_01", gun: true));
            list.Add(Prop("mod_stock", "military:SM_Wep_Mod_B_Stock_02", gun: true));
            list.Add(Prop("mod_extMag", "military:SM_Wep_Mod_A_Mag_07", upright: true));
            list.Add(new Thing { file = "mod_silencer", source = Settlement.Silencer, make = () => Copy(Catalog()?.Weapon(Settlement.Silencer)), gun = true });
            // The piles: two magazines taped together, a pill bottle, a wooden crate (supplies: never a coin, Arturo).
            list.Add(Prop("mags", "apocalypse:SM_Chr_Attach_Mags_01", upright: true));
            list.Add(Prop("medicine", "apocalypse:SM_Item_Pills_01", upright: true));
            list.Add(Prop("supplies", "apocalypse:SM_Prop_Crate_01", upright: true));
            // The missions' own, as they lie on the floor (MapBuilder.Loose): the armoury's key in brass; the antiviral, the
            // syringe with its jar of pills small behind it (the syringe is what tells it from the medicine).
            list.Add(new Thing { file = "key", source = "police:SM_Prop_Handcuffs_Key_01", make = () =>
            {
                var key = Copy(prefabs("police:SM_Prop_Handcuffs_Key_01"));
                var brass = KitMaterials.New(KitMaterials.Plain, "Candado");
                if (key == null || brass == null) return key;
                brass.SetColor("_BaseColor", new Color(0.86f, 0.62f, 0.16f));
                foreach (var r in key.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(brass, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                return key;
            } });
            list.Add(new Thing { file = "antiviral", source = "apocalypse:SM_Item_Pills_01 + our syringe", upright = true, make = () =>
            {
                var jar = Copy(prefabs("apocalypse:SM_Item_Pills_01"));
                if (jar == null) return null;
                var both = new GameObject("antiviral");
                jar.transform.SetParent(both.transform, false);
                if (!Box(jar, out var j)) return both;
                // (The syringe is the picture, across it from corner to corner, the jar small behind its plunger's end:
                // another outline than the medicine's. Ours, of cylinders, not Synty's: at 40 px that one was a cigarette
                // (Producto, 19/11), so a wide plunger and flange, green in the barrel and a dark needle.)
                var syringe = new GameObject("syringe").transform;
                syringe.SetParent(both.transform, false);
                float length = j.size.y * 2.6f;
                Material Paint(Color c) { var m = KitMaterials.New(KitMaterials.Plain, "Jeringa"); if (m != null) m.SetColor("_BaseColor", c); return m; }
                void Part(Material m, float from, float to, float radius)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
                    c.transform.SetParent(syringe, false);
                    c.transform.localPosition = new Vector3(0, (from + to) / 2 * length, 0);
                    c.transform.localScale = new Vector3(radius * 2 * length, (to - from) / 2 * length, radius * 2 * length);
                    if (m != null) c.GetComponent<Renderer>().sharedMaterial = m;
                }
                Material steel = Paint(new Color(0.2f, 0.22f, 0.25f)), white = Paint(new Color(0.9f, 0.93f, 0.95f)), green = Paint(new Color(0.25f, 0.8f, 0.4f));
                // (From its plunger's end, 0, to its needle's tip, 1.)
                Part(white, 0, 0.035f, 0.12f);
                Part(white, 0.035f, 0.2f, 0.03f);
                Part(white, 0.2f, 0.235f, 0.14f);
                Part(white, 0.235f, 0.36f, 0.075f);
                Part(green, 0.36f, 0.7f, 0.075f);
                Part(white, 0.7f, 0.76f, 0.04f);
                Part(steel, 0.76f, 1, 0.018f);
                syringe.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(0.72f, 0.69f, 0).normalized);
                Box(syringe.gameObject, out var y);
                syringe.localPosition += new Vector3(j.max.x - y.center.x - y.size.x * 0.3f, j.min.y - y.min.y, j.min.z - y.max.z);
                return both;
            } });
            return list;
        }

        /// <summary>
        /// The vests (VestIds), 2×2: Sidekick torsos alone, without the skin (Sidekick's figures face +z), upright from the front.
        /// (Not Police Station's bulletproof vest, 17/11: it hangs on a coat hanger of the same mesh.)
        /// </summary>
        static IEnumerable<Thing> Vests()
        {
            yield return Torso("vest_vest", "SK_APOC_SURV_06_10TORS_HU01", "ApocalypseSurvivor_02");
            yield return Torso("vest_heavyVest", "SK_MDRN_POLC_09_10TORS_HU01", "ModernPolice_03");
        }

        static Thing Torso(string file, string part, string preset) => new Thing
        {
            file = file, w = 2, h = 2, source = part, make = () => Torso(part, preset),
            right = Vector3.left, facing = Vector3.forward, upright = true, yaw = -18, tilt = 8,
        };

        /// <summary>To choose: each of `keys` (a prop's key, or sk:part:preset; @WxH its cells) as a picture into `folder`, and their sheets.</summary>
        static void Samples(string keys, string folder)
        {
            Directory.CreateDirectory(folder);
            var prefabs = MapLab.Prefabs();
            var made = new List<(string file, int w, int h, string path)>();
            Warm();
            foreach (string raw in keys.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                string key = raw;
                int w = 1, h = 1;
                bool upright = key.EndsWith("+u"), gun = key.EndsWith("+g");
                if (upright || gun) key = key.Substring(0, key.Length - 2);
                int at = key.IndexOf('@');
                if (at >= 0)
                {
                    var cells = key.Substring(at + 1).Split('x');
                    w = int.Parse(cells[0]);
                    h = int.Parse(cells[1]);
                    key = key.Substring(0, at);
                }
                var t = new Thing { file = key.Replace(':', '_') + (upright ? "-u" : ""), w = w, h = h, source = key, upright = upright, gun = gun };
                if (key.StartsWith("sk:"))
                {
                    var parts = key.Split(':');
                    t = Torso(t.file, parts[1], parts.Length > 2 ? parts[2] : null);
                    t.w = w;
                    t.h = h;
                }
                else t.make = () => Copy(prefabs(key));
                var png = Picture(t);
                if (png == null) continue;
                string path = Path.Combine(folder, t.file + ".png");
                File.WriteAllBytes(path, png);
                made.Add((t.file, w, h, path));
            }
            Sheets(made, folder, "muestras");
        }

        static GameObject Copy(GameObject prefab) => prefab != null ? UnityEngine.Object.Instantiate(prefab) : null;

        /// <summary>A weapon's model as UnitViews.WeaponModel makes it: without Apocalypse's silencers and a sword's sheath.</summary>
        static GameObject Weapon(GameObject prefab)
        {
            var model = Copy(prefab);
            if (model == null) return null;
            foreach (var part in model.GetComponentsInChildren<Transform>(true).Where(t => t != model.transform && (t.name.Contains("Silencer") || t.name.Contains("Sheath"))).ToList())
                part.gameObject.SetActive(false);
            return model;
        }

        /// <summary>
        /// A Sidekick part alone (BibliotecaExport's way: Sidekick's material with the colour map of `preset`, the one that
        /// wears it), without the triangles on its skin's cells (T_SkinMask_Map, black on skin): a torso's neck and arms.
        /// </summary>
        static GameObject Torso(string part, string preset)
        {
            // (Sidekick's own, or those of its packs kept aside: UndeadFigure.Spare.)
            var folders = new[] { "Assets/Synty/SidekickCharacters", UndeadFigure.Spare.TrimEnd('/') }.Where(AssetDatabase.IsValidFolder).ToArray();
            string path = AssetDatabase.FindAssets(part + " t:Model", folders).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == part);
            var asset = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            var basic = Resources.Load<Material>("Materials/M_BaseMaterial");
            if (asset == null || basic == null)
            {
                Debug.LogWarning($"[objetos] {part}: {(asset == null ? "no model" : "no M_BaseMaterial")}");
                return null;
            }
            var model = UnityEngine.Object.Instantiate(asset);
            var material = new Material(basic);
            string mapName = $"T_{preset}ColorMap";
            string map = preset == null ? null : AssetDatabase.FindAssets(mapName + " t:Texture2D", new[] { "Assets/Synty/SidekickCharacters/Characters" }).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == mapName);
            if (map != null) material.SetTexture("_ColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(map));
            else Debug.LogWarning($"[objetos] {part}: no {mapName}");
            var skin = SkinMask();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();
                if (skin == null || r.sharedMesh == null) continue;
                var mesh = UnityEngine.Object.Instantiate(r.sharedMesh);
                var uv = mesh.uv;
                int dropped = 0;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var triangles = mesh.GetTriangles(s);
                    var kept = new List<int>(triangles.Length);
                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        var c = (uv[triangles[i]] + uv[triangles[i + 1]] + uv[triangles[i + 2]]) / 3;
                        int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(c.x, 1) * skin.width), 0, skin.width - 1);
                        int y = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(c.y, 1) * skin.height), 0, skin.height - 1);
                        if (skin.GetPixel(x, y).r < 0.5f)
                        {
                            dropped++;
                            continue;
                        }
                        kept.Add(triangles[i]);
                        kept.Add(triangles[i + 1]);
                        kept.Add(triangles[i + 2]);
                    }
                    mesh.SetTriangles(kept, s);
                }
                r.sharedMesh = mesh;
                Debug.Log($"[objetos] {part}: {dropped} triangles of skin off");
            }
            return model;
        }

        static Texture2D skinMask;

        /// <summary>Sidekick's skin mask, read from its file (a palette of 16×16 cells; black where the skin is).</summary>
        static Texture2D SkinMask()
        {
            if (skinMask != null) return skinMask;
            string path = AssetDatabase.FindAssets("T_SkinMask_Map t:Texture2D", new[] { "Assets/Synty/SidekickCharacters" }).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "T_SkinMask_Map");
            if (path == null) return null;
            skinMask = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            skinMask.LoadImage(File.ReadAllBytes(path));
            return skinMask;
        }

        /// <summary>(The first picture a batchmode run draws may come out without its light: one thrown away first.)</summary>
        static void Warm()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Mood.Show(Mood.Day);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.layer = Studio.Layer;
            var camera = Lens(cube.GetComponent<Renderer>().bounds, 1, 1, out _);
            var target = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            Shoot(camera, target);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(cube);
        }

        /// <summary>A thing's picture as a PNG (its cells at 128 px), or null.</summary>
        static byte[] Picture(Thing t)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // The day's light settings (no haze: Studio turns it off), for what reads them as globals.
            Mood.Show(Mood.Day);
            GameObject model = null;
            try
            {
                model = t.make?.Invoke();
                if (model == null)
                {
                    Debug.LogWarning($"[objetos] {t.file}: no model ({t.source})");
                    return null;
                }
                model.SetActive(true);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach (var g in model.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = g.GetLODs();
                    for (int i = 1; i < lods.Length; i++)
                        foreach (var r in lods[i].renderers)
                            if (r != null) r.enabled = false;
                    g.enabled = false;
                }
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) r.enabled = false;
                foreach (var tr in model.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Studio.Layer;
                if (t.cut is Vector2 cut) Cut(model, cut);
                if (!Box(model, out var local))
                {
                    Debug.LogWarning($"[objetos] {t.file}: nothing drawn ({t.source})");
                    return null;
                }
                Lay(model, t, local);
                Box(model, out var box);
                int width = t.w * Cell, height = t.h * Cell;
                // Once loose to find it, once fitted to its cells.
                var camera = Lens(box, width, height, out _);
                var target = new RenderTexture(width * Super, height * Super, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                Shoot(camera, target);
                var first = PiecePicturesMake.Read(target);
                if (!Extent(first, target.width, target.height, out var px))
                {
                    Debug.LogWarning($"[objetos] {t.file}: nothing in the picture ({t.source})");
                    return null;
                }
                float unit = camera.orthographicSize * 2 / target.height;
                var origin = new Vector2(camera.transform.position.x - camera.orthographicSize * camera.aspect, camera.transform.position.y - camera.orthographicSize);
                var seen = Rect.MinMaxRect(origin.x + px.xMin * unit, origin.y + px.yMin * unit, origin.x + px.xMax * unit, origin.y + px.yMax * unit);
                float scale = Mathf.Min((width - 2 * Margin) / seen.width, (height - 2 * Margin) / seen.height);
                camera.orthographicSize = height / 2f / scale;
                camera.transform.position = new Vector3(seen.center.x, seen.center.y, camera.transform.position.z);
                Shoot(camera, target);
                var second = PiecePicturesMake.Read(target);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                var pixels = Down(second, width, height, Super);
                Extent(pixels, width, height, out var fitted);
                Debug.Log(FormattableString.Invariant($"[objetos] {t.file}: {t.source}, {width}×{height}, drawn {fitted.xMin}–{fitted.xMax} × {fitted.yMin}–{fitted.yMax}"));
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                texture.Apply();
                var png = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                return png;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[objetos] {t.file}: {e}");
                return null;
            }
            finally
            {
                if (model != null) UnityEngine.Object.DestroyImmediate(model);
            }
        }

        /// <summary>
        /// The thing turned so that its `right` runs to the picture's right (+x) and its `facing` looks at us (−z: the camera
        /// looks along +z), then tilted about its length (its top to us) and turned (its right end to us), which keeps that
        /// length level in the picture (what stands, the other way round). `local`: its box as it stood, unturned.
        /// </summary>
        static void Lay(GameObject model, Thing t, Bounds local)
        {
            var size = local.size;
            int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int a = (longest + 1) % 3, b = (longest + 2) % 3;
            int thinnest = size[a] <= size[b] ? a : b;
            Vector3 Axis(int k) => k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward;
            // The tip: the end of its length further from where it is held.
            float tipWay = Mathf.Abs(local.max[longest] - t.grip[longest]) >= Mathf.Abs(local.min[longest] - t.grip[longest]) ? 1 : -1;
            var right = t.right ?? (t.gun ? Vector3.forward : t.upright ? Vector3.right : Axis(longest) * tipWay);
            var facing = Vector3.ProjectOnPlane(t.facing ?? (t.gun ? Vector3.right : t.upright ? Vector3.back : Axis(thinnest)), right);
            // (Along its length: the axis most across it.)
            if (facing.sqrMagnitude < 1e-6f) facing = Vector3.ProjectOnPlane(new[] { 0, 1, 2 }.Select(Axis).OrderBy(x => Mathf.Abs(Vector3.Dot(x, right))).First(), right);
            facing.Normalize();
            var up = Vector3.Cross(right.normalized, facing);
            var lie = Quaternion.Euler(0, 90, 0) * Quaternion.Inverse(Quaternion.LookRotation(right, up));
            // (Standing, turned about its height first, then seen from above as a camera would: its upright edges stay upright.)
            var turn = Quaternion.AngleAxis(t.yaw ?? (t.upright ? StandingYaw : yaw), Vector3.up);
            var tip = Quaternion.AngleAxis(t.tilt ?? tilt, Vector3.right);
            model.transform.rotation = (t.upright ? tip * turn : turn * tip) * lie;
        }

        /// <summary>
        /// A model cut short along its +z (its root's frame), `cut` the parts of its length kept: the triangles wholly out
        /// go, the points of those across a cut are pressed onto it (a barrel's long sides shortened, not gone).
        /// </summary>
        static void Cut(GameObject model, Vector2 cut)
        {
            var root = model.transform;
            var filters = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToList();
            var frames = filters.Select(f => root.worldToLocalMatrix * f.transform.localToWorldMatrix).ToList();
            var points = filters.Select((f, k) => f.sharedMesh.vertices.Select(v => frames[k].MultiplyPoint3x4(v)).ToArray()).ToList();
            float z0 = points.SelectMany(p => p).Min(p => p.z), z1 = points.SelectMany(p => p).Max(p => p.z);
            float from = Mathf.Lerp(z0, z1, cut.x), to = Mathf.Lerp(z0, z1, cut.y);
            for (int k = 0; k < filters.Count; k++)
            {
                var mesh = UnityEngine.Object.Instantiate(filters[k].sharedMesh);
                var p = points[k];
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var triangles = mesh.GetTriangles(s);
                    var kept = new List<int>(triangles.Length);
                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        float a = p[triangles[i]].z, b = p[triangles[i + 1]].z, c = p[triangles[i + 2]].z;
                        if (Mathf.Max(a, Mathf.Max(b, c)) < from || Mathf.Min(a, Mathf.Min(b, c)) > to) continue;
                        kept.Add(triangles[i]);
                        kept.Add(triangles[i + 1]);
                        kept.Add(triangles[i + 2]);
                    }
                    mesh.SetTriangles(kept, s);
                }
                var back = frames[k].inverse;
                mesh.vertices = p.Select(v => back.MultiplyPoint3x4(new Vector3(v.x, v.y, Mathf.Clamp(v.z, from, to)))).ToArray();
                mesh.RecalculateBounds();
                filters[k].sharedMesh = mesh;
            }
        }

        /// <summary>What a model draws, its box in the world (as it stands at the origin unturned: its own frame).</summary>
        static bool Box(GameObject model, out Bounds box)
        {
            box = new Bounds();
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (any) box.Encapsulate(r.bounds);
                else box = r.bounds;
                any = true;
            }
            return any && box.size.sqrMagnitude > 1e-10f;
        }

        /// <summary>An orthographic camera looking along +z at `box`, the picture's aspect, loose round it.</summary>
        static Camera Lens(Bounds box, int width, int height, out float depth)
        {
            depth = box.extents.magnitude + 1;
            var camera = new GameObject("Cámara de los objetos").AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = width / (float)height;
            camera.orthographicSize = Mathf.Max(box.extents.y, box.extents.x / camera.aspect) * 1.25f + 1e-3f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = depth * 2 + 1;
            camera.transform.SetPositionAndRotation(new Vector3(box.center.x, box.center.y, box.center.z - depth), Quaternion.identity);
            return camera;
        }

        static void Shoot(Camera camera, RenderTexture target) =>
            Studio.Shoot(camera, target, new[]
            {
                new Studio.Lamp(0xfff0dc, key, new Vector3(-1.1f, 1.5f, -1.2f)),
                new Studio.Lamp(0xb4ccff, fill, new Vector3(1.4f, 0.2f, -1f)),
                new Studio.Lamp(0xfff6ea, rim, new Vector3(0.35f, 0.9f, 1.6f)),
            }, Studio.Hex(0xe2eaff), Studio.Hex(0x4a4036), sky, Studio.Tone.Neutral, exposure);

        /// <summary>Where a picture is not clear, in its pixels (x right, y up; max exclusive).</summary>
        static bool Extent(Color[] pixels, int width, int height, out Rect extent)
        {
            int x0 = width, y0 = height, x1 = -1, y1 = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < 4 / 255f) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            extent = x1 < 0 ? default : Rect.MinMaxRect(x0, y0, x1 + 1, y1 + 1);
            return x1 >= 0;
        }

        /// <summary>A picture `k` times as big averaged down to width × height: in linear light, the colour weighted by its alpha.</summary>
        static Color[] Down(Color[] src, int width, int height, int k)
        {
            int sw = width * k;
            var dst = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < k; sy++)
                        for (int sx = 0; sx < k; sx++)
                        {
                            var c = src[(y * k + sy) * sw + x * k + sx];
                            if (c.a <= 0) continue;
                            var l = c.linear;
                            r += l.r * c.a;
                            g += l.g * c.a;
                            b += l.b * c.a;
                            a += c.a;
                        }
                    dst[y * width + x] = a > 1e-5f ? new Color(r / a, g / a, b / a, 1).gamma.WithAlpha(a / (k * k)) : Color.clear;
                }
            return dst;
        }

        static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ------------------------------------------------------------ the contact sheets

        /// <summary>The grids' look (UI's dark panels, and a light one to check the other side), each cell a little lighter, its lines darker.</summary>
        static readonly (string name, Color ground, Color cell, Color line)[] Grounds =
        {
            ("oscura", Hex(0x10151b), Hex(0x18202a), Hex(0x0b0f13)),
            ("clara", Hex(0xd8dbd4), Hex(0xe6e8e2), Hex(0xbfc3bb)),
        };

        static Color Hex(int hex) => Studio.Hex(hex);

        /// <summary>The pictures laid as in a grid of 12 cells across (in order, a row when the next doesn't fit), at 52 and at 40 px a cell, on each ground.</summary>
        static void Sheets(List<(string file, int w, int h, string path)> pictures, string folder, string name)
        {
            const int across = 12, pad = 12;
            var placed = new List<(string path, int x, int y, int w, int h)>();
            int cx = 0, cy = 0, row = 0;
            foreach (var p in pictures)
            {
                if (cx + p.w > across)
                {
                    cy += row;
                    cx = 0;
                    row = 0;
                }
                placed.Add((p.path, cx, cy, p.w, p.h));
                cx += p.w;
                row = Mathf.Max(row, p.h);
            }
            int rows = cy + row;
            var loaded = placed.Select(p => p.path).Distinct().ToDictionary(p => p, p =>
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                tex.LoadImage(File.ReadAllBytes(p));
                return tex;
            });
            foreach (int cell in new[] { 52, 40 })
                foreach (var (groundName, ground, cellColour, line) in Grounds)
                {
                    int width = across * cell + pad * 2, height = rows * cell + pad * 2;
                    var sheet = Enumerable.Repeat(ground, width * height).ToArray();
                    // (Rows top down: y from the top, flipped when written.)
                    void Set(int x, int y, Color c) => sheet[(height - 1 - y) * width + x] = c;
                    foreach (var p in placed)
                        for (int y = 0; y < p.h * cell; y++)
                            for (int x = 0; x < p.w * cell; x++)
                            {
                                bool edge = x % cell == 0 || y % cell == 0 || x % cell == cell - 1 || y % cell == cell - 1;
                                Set(pad + p.x * cell + x, pad + p.y * cell + y, edge ? line : cellColour);
                            }
                    foreach (var p in placed)
                    {
                        var tex = loaded[p.path];
                        var src = tex.GetPixels();
                        int tw = p.w * cell, th = p.h * cell;
                        for (int y = 0; y < th; y++)
                            for (int x = 0; x < tw; x++)
                            {
                                var c = Sample(src, tex.width, tex.height, (x + 0.5f) / tw, (y + 0.5f) / th, tex.width / (float)tw);
                                if (c.a <= 0) continue;
                                int sx = pad + p.x * cell + x, sy = pad + p.y * cell + y;
                                var under = sheet[(height - 1 - sy) * width + sx].linear;
                                var over = c.linear;
                                Set(sx, sy, new Color(over.r * c.a + under.r * (1 - c.a), over.g * c.a + under.g * (1 - c.a), over.b * c.a + under.b * (1 - c.a), 1).gamma);
                            }
                    }
                    var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    output.SetPixels(sheet);
                    output.Apply();
                    string file = Path.Combine(folder, $"{name}-{groundName}{(cell == 52 ? "" : "-" + cell)}.png");
                    File.WriteAllBytes(file, output.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(output);
                }
            foreach (var tex in loaded.Values) UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[objetos] sheets of {placed.Count} pictures in {folder}");
        }

        /// <summary>
        /// The picture round (u, v) (v from the top) over a footprint `step` of its pixels wide: the mean of 4×4 bilinear
        /// taps, in linear light weighted by alpha (as a texture shown smaller, without its mipmaps' blur).
        /// </summary>
        static Color Sample(Color[] src, int w, int h, float u, float v, float step)
        {
            const int k = 4;
            float r = 0, g = 0, b = 0, a = 0;
            for (int sy = 0; sy < k; sy++)
                for (int sx = 0; sx < k; sx++)
                {
                    float x = u * w + ((sx + 0.5f) / k - 0.5f) * step - 0.5f;
                    float y = (1 - v) * h + ((sy + 0.5f) / k - 0.5f) * step - 0.5f;
                    int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                    float fx = x - ix, fy = y - iy;
                    for (int j = 0; j < 4; j++)
                    {
                        int px = ix + (j & 1), py = iy + (j >> 1);
                        float weight = ((j & 1) != 0 ? fx : 1 - fx) * ((j >> 1) != 0 ? fy : 1 - fy);
                        if (px < 0 || py < 0 || px >= w || py >= h || weight <= 0) continue;
                        var c = src[py * w + px];
                        var l = c.linear;
                        r += l.r * c.a * weight;
                        g += l.g * c.a * weight;
                        b += l.b * c.a * weight;
                        a += c.a * weight;
                    }
                }
            return a > 1e-5f ? new Color(r / a, g / a, b / a, 1).gamma.WithAlpha(a / (k * k)) : Color.clear;
        }
    }
}
