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
                        await Awaitable.NextFrameAsync();
                    }
                    Debug.Log($"[zombis] done, {frame} frames");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }
    }
}
