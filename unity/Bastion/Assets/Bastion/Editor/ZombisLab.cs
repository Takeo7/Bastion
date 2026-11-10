using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The dead of 0.12.14 (Arturo, 9/11, of Biblioteca's list): City Zombies in the crowd, Horror Asylum's patients for
    /// who started in a clinic or a hospital, Sidekick's zombies in Apocalypse Zombies' parts no preset wore.
    /// </summary>
    public static partial class CombatLab
    {
        static readonly string[] ZombieParts =
        {
            "SK_ZOMB_BASE_02_10TORS_ZB01", "SK_ZOMB_BASE_04_10TORS_ZB01", "SK_ZOMB_BASE_05_10TORS_ZB01", "SK_ZOMB_BASE_08_10TORS_ZB01", "SK_ZOMB_BASE_09_10TORS_ZB01",
            "SK_ZOMB_BASE_01_17HIPS_ZB01", "SK_ZOMB_BASE_03_17HIPS_ZB01", "SK_ZOMB_BASE_06_17HIPS_ZB01", "SK_ZOMB_BASE_02_02HAIR_ZB01", "SK_ZOMB_BASE_05_02HAIR_ZB01",
            "SK_APOC_ZOMB_02_22AHED_ZB01", "SK_APOC_ZOMB_02_24ABAC_ZB01", "SK_APOC_ZOMB_11_24ABAC_ZB01", "SK_APOC_ZOMB_03_23AFAC_ZB01", "SK_APOC_ZOMB_05_23AFAC_ZB01",
            "SK_APOC_ZOMB_01_29ASHL_ZB01",
        };

        /// <summary>
        /// The palette cells each of Apocalypse Zombies' unworn parts paints from, and what each of the five zombies' colour
        /// maps has there (FF0000: nothing), to see what it would be painted in (UndeadFigure.RagCells).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.CombatLab.CeldasZombis -quit
        /// </summary>
        public static void CeldasZombis()
        {
            var catalog = FigureCatalog.Load();
            var maps = Enumerable.Range(1, 5).Select(k => SoldierPalette.Cells(catalog.ColourMap($"ApocalypseZombie_0{k}"))).ToArray();
            foreach (string part in ZombieParts)
            {
                string path = AssetDatabase.FindAssets(part + " t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == part);
                var mesh = path != null ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault() : null;
                if (mesh == null)
                {
                    Debug.Log($"[zombis] {part}: not found");
                    continue;
                }
                var cells = mesh.uv.Select(SoldierPalette.CellOf).Distinct().OrderBy(c => c).ToList();
                Debug.Log($"[zombis] {part} ({mesh.vertexCount} v, {path}): " + string.Join(" ", cells.Select(c => $"{c % 16},{c / 16}=" + string.Join("/", maps.Select(m => ColorUtility.ToHtmlStringRGB(m[c]))))));
            }
        }

        /// <summary>
        /// A crowd of the common dead in rows of twelve, two tiles apart, facing the camera, BASTION_FILA: multitud (24 in their
        /// own clothes, City Zombies' one in three by their id), ciudad (the crowd's 19 of City Zombies), harapos (Sidekick's
        /// 19 of the crowd, the six in Apocalypse Zombies' parts among them), pacientes (the 8 in a patient's gown), policias
        /// (the 5 in a policeman's uniform). Still a second, then two tiles towards the camera, each filmed every frame
        /// (BASTION_TIRAS: Film, close and from the game's camera). BASTION_SIN_MUERTOS_NUEVOS=1: as 0.12.13 drew them.
        ///   BASTION_FILA=pacientes BASTION_TIRAS=&lt;folder&gt; unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Zombis
        ///     -shotPlay -shotCamera "Shot camera" -shotFrames 40 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Zombis()
        {
            string fila = Environment.GetEnvironmentVariable("BASTION_FILA") ?? "multitud";
            // (Ids that City Zombies' share takes, or doesn't: z0, z1… as they fall.)
            IEnumerable<string> Ids(bool? city) => Enumerable.Range(0, 1000).Select(k => "z" + k).Where(id => city == null || UnitViews.FromCity(id) == city);
            var who = fila == "pacientes" ? Ids(null).Take(8).Select(id => (id, Outfits.Patient))
                : fila == "policias" ? Ids(null).Take(5).Select(id => (id, Outfits.Police))
                : fila == "ciudad" ? Ids(true).Take(19).Select(id => (id, (string)null))
                : fila == "harapos" ? Ids(false).Take(19).Select(id => (id, (string)null))
                : Ids(null).Take(24).Select(id => (id, (string)null));
            var list = who.ToList();
            var first = MapLab.Golden("city-7");
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => u.Id == deadId, 2, 2 + 2 * Math.Min(12, list.Count) - 2);
            var like = state.Units[0];
            state.Units = list.Select((w, k) =>
            {
                var u = Bastion.Net.Json.Read<Unit>(Bastion.Net.Json.Write(like));
                u.Id = w.Item1;
                u.Template = TemplateIds.Zombie;
                u.Outfit = w.Item2;
                u.RisenFrom = null;
                return Put(u, 2 + 2 * (k % 12), 14 - 4 * (k / 12), Math.PI / 2);
            }).ToList();
            var row = state.Units.Select(u => (u, u.Id)).ToList();
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.NextFrameAsync();
                    await Awaitable.NextFrameAsync();
                    Debug.Log("[zombis] " + fila + ": " + string.Join(", ", state.Units.Select(u =>
                    {
                        var figure = Figures.FeetOf(u.Id)?.GetComponentInChildren<Animator>();
                        int vertices = figure != null ? figure.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0) : 0;
                        return $"{u.Id} {figure?.name} {vertices} v";
                    })));
                    Reds();
                    var sticks = Sticks();
                    int frame = 0;
                    for (float t = 0; t < 1; t += Time.deltaTime)
                    {
                        Film(row, frame++, "quietos");
                        await Awaitable.NextFrameAsync();
                    }
                    int walking = row.Count;
                    foreach (var (u, _) in row) Walk(u);
                    async void Walk(Unit u)
                    {
                        await units.MoveAlong(u.Id, new[] { new Pos { X = u.Pos.X, Y = u.Pos.Y + 1 }, new Pos { X = u.Pos.X, Y = u.Pos.Y + 2 } }, 1);
                        walking--;
                    }
                    for (int k = 0; k < 40 && walking > 0; k++)
                    {
                        Film(row, frame++, "andan");
                        foreach (var s in sticks) s();
                        await Awaitable.NextFrameAsync();
                    }
                    Debug.Log($"[zombis] done, {frame} frames");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
            // (BASTION_ROJO=1, the 3D review's Z-1 b, 10/11: for each of the row, the cells its meshes paint from that are
            // drawn pure red (a cell its preset doesn't use: SoldierPalette.Unused) and those loud (saturation and light both
            // over 0.5), as drawn.)
            void Reds()
            {
                if (Environment.GetEnvironmentVariable("BASTION_ROJO") != "1") return;
                foreach (var u in state.Units)
                {
                    var feet = Figures.FeetOf(u.Id);
                    if (feet == null) continue;
                    var red = new List<string>();
                    var loud = new List<string>();
                    foreach (var r in feet.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var block = new MaterialPropertyBlock();
                        r.GetPropertyBlock(block);
                        // (The block's only if it has one: without, it gives a white one.)
                        var drawn = (block.HasTexture("_ColorMap") ? block.GetTexture("_ColorMap") : r.sharedMaterial != null && r.sharedMaterial.HasTexture("_ColorMap") ? r.sharedMaterial.GetTexture("_ColorMap") : null) as Texture2D;
                        if (drawn == null || r.sharedMesh == null) continue;
                        var cells = SoldierPalette.Cells(drawn);
                        foreach (int c in r.sharedMesh.uv.Select(SoldierPalette.CellOf).Distinct().OrderBy(c => c))
                        {
                            Color.RGBToHSV(cells[c], out float h, out float sat, out float v);
                            string cell = $"{c % 16},{c / 16}={ColorUtility.ToHtmlStringRGB(cells[c])}";
                            if (SoldierPalette.Unused(cells[c])) red.Add(cell);
                            else if (sat > 0.5f && v > 0.5f) loud.Add(cell);
                        }
                    }
                    Debug.Log($"[rojo] {u.Id} {feet.GetComponentInChildren<Animator>()?.name}: red {red.Count} ({string.Join(" ", red)}); loud {string.Join(" ", loud)}");
                }
            }
            // (BASTION_PALO=1, Z-1 c: for ApocalypseZombie_02, the points of its body standing more than 0.25 m behind its
            // chest and over its shoulders (the stick on its back) taken; then each frame of the walk, [palo]: the nearest of
            // them to its head's bone, m. Under 0.12, it goes through the head.)
            System.Collections.Generic.List<Action> Sticks()
            {
                var found = new System.Collections.Generic.List<Action>();
                if (Environment.GetEnvironmentVariable("BASTION_PALO") != "1") return found;
                foreach (var u in state.Units)
                {
                    var feet = Figures.FeetOf(u.Id);
                    var rig = feet != null ? feet.GetComponentInChildren<Animator>() : null;
                    if (rig == null || !rig.isHuman || !rig.name.Contains("ApocalypseZombie_02")) continue;
                    var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0).FirstOrDefault();
                    var head = rig.GetBoneTransform(HumanBodyBones.Head);
                    var chest = rig.GetBoneTransform(HumanBodyBones.UpperChest) ?? rig.GetBoneTransform(HumanBodyBones.Chest);
                    var shoulder = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    if (skin == null || head == null || chest == null || shoulder == null) continue;
                    var baked = new Mesh();
                    skin.BakeMesh(baked, true);
                    // (Baked with its scale: into the world by its place and turn alone.)
                    Matrix4x4 World() => Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    var m = World();
                    var points = baked.vertices;
                    var stick = Enumerable.Range(0, points.Length).Where(k =>
                    {
                        var w = m.MultiplyPoint3x4(points[k]);
                        return Vector3.Dot(w - chest.position, feet.forward) < -0.25f && w.y > shoulder.position.y;
                    }).ToArray();
                    Debug.Log($"[palo] {u.Id} {rig.name}: {stick.Length} points of {points.Length}");
                    if (stick.Length == 0) continue;
                    found.Add(() =>
                    {
                        skin.BakeMesh(baked, true);
                        var now = baked.vertices;
                        var mm = World();
                        float least = stick.Min(k => Vector3.Distance(mm.MultiplyPoint3x4(now[k]), head.position));
                        Debug.Log(FormattableString.Invariant($"[palo] {Time.time:0.000} {u.Id} nearest {least:0.000}"));
                    });
                }
                return found;
            }
        }
    }
}
