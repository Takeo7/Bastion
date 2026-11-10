using System;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Profiling;

namespace Bastion.EditorTools
{
    /// <summary>What the soldiers' figures keep in memory (the review of 10/11, row 4: SoldierFigure keeps every one it made).</summary>
    public static partial class CombatLab
    {
        /// <summary>
        /// A while in Personalizar: BASTION_VUELTAS rounds (8 unless said), each a head or a build changed (the heads in
        /// turn, the builds in turn, the sexes in turn), so all the outfits of that sex made again apart in the background
        /// (Settlement.Prepare's SoldierFigure.Prepare), and the pointer over two of them (Settlement.Showcase: made, painted
        /// and let go). BASTION_MODO=hacer: all of them made at once instead (Make), each shown once. Logs after each round,
        /// the unused let go first, the figures kept (SoldierFigure's shelf), the meshes, textures and materials (how many,
        /// MB) and the memory Unity has (MB); then with a scene gone (an empty one, made and unloaded: what SoldierFigure
        /// does as the base goes); then with the shelf's figures destroyed (as the base's scene going destroys them). Then
        /// as many joined as a mission's squad (BASTION_ESCUADRA, 6 unless said), the battle's. Unity quits once done.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.MemoriaPersonalizar -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 3000 -shotFps 10 -shotSize 160x90 -shotOut &lt;folder&gt;
        /// </summary>
        public static void MemoriaPersonalizar()
        {
            Plain(u => false, 2, 4);
            int rounds = int.TryParse(Environment.GetEnvironmentVariable("BASTION_VUELTAS"), out int r) ? r : 8;
            int squad = int.TryParse(Environment.GetEnvironmentVariable("BASTION_ESCUADRA"), out int q) ? q : 6;
            var heads = Appearances.Heads.Keys.ToList();
            var builds = Appearances.Builds.Keys.ToList();
            sequence = async () =>
            {
                try
                {
                    await Awaitable.NextFrameAsync();
                    await Measure("al empezar");
                    for (int k = 0; k < rounds; k++)
                    {
                        string sex = k % 2 == 0 ? Sexes.F : Sexes.M;
                        var look = new Appearance { Sex = sex, Head = heads[k % heads.Count], Build = builds[k / 2 % builds.Count], Wardrobe = Appearances.Wardrobe };
                        var dresses = Dress.OutfitsFor(sex).Select(outfit =>
                        {
                            var copy = Bastion.Net.Json.Read<Appearance>(Bastion.Net.Json.Write(look));
                            copy.Character = outfit;
                            return Dress.Of(copy, "lab-memoria");
                        }).ToList();
                        bool all = Environment.GetEnvironmentVariable("BASTION_MODO") == "hacer";
                        int frames = 0;
                        if (!all)
                        {
                            SoldierFigure.Prepare(dresses, true);
                            for (; SoldierFigure.Waiting > 0 && frames < 5000; frames++) await Awaitable.NextFrameAsync();
                        }
                        int shown = 0;
                        foreach (var dress in all ? dresses : dresses.Take(2).ToList())
                        {
                            var figure = SoldierFigure.Make(dress, "Soldado", true);
                            if (figure == null) continue;
                            if (k == 0 && shown == 0) Detail(figure, "aparte");
                            shown++;
                            SquadTint.Paint(figure, look, null);
                            UnityEngine.Object.Destroy(figure);
                        }
                        await Awaitable.NextFrameAsync();
                        await Measure($"vuelta {k + 1} ({sex}, {look.Head}, {look.Build}: {dresses.Count} trajes, {frames} fotogramas, {shown} vistos)");
                    }
                    var empty = UnityEngine.SceneManagement.SceneManager.CreateScene("Vacía");
                    var unloading = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(empty);
                    while (unloading != null && !unloading.isDone) await Awaitable.NextFrameAsync();
                    await Measure("ida una escena");
                    var shelf = GameObject.Find("Soldados hechos");
                    if (shelf != null) foreach (Transform model in shelf.transform) UnityEngine.Object.Destroy(model.gameObject);
                    await Awaitable.NextFrameAsync();
                    await Measure("soltados los de la estantería");
                    for (int k = 0; k < squad; k++)
                    {
                        var look = new Appearance { Sex = k % 2 == 0 ? Sexes.F : Sexes.M, Head = heads[k % heads.Count], Wardrobe = Appearances.Wardrobe };
                        var figure = SoldierFigure.Make(Dress.Of(look, "lab-escuadra-" + k), "Soldado");
                        if (figure != null && k == 0) Detail(figure, "unido");
                        if (figure != null) UnityEngine.Object.Destroy(figure);
                    }
                    await Awaitable.NextFrameAsync();
                    await Measure($"{squad} unidos (los de una misión)");
                    Debug.Log("[memoria] done");
                    UnityEditor.EditorApplication.Exit(0);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }

        /// <summary>A figure's meshes: corners, blend shapes and size (MB), all together and the biggest ([memoria]).</summary>
        static void Detail(GameObject figure, string what)
        {
            var meshes = figure.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(x => x.sharedMesh).Where(m => m != null).Distinct().ToList();
            float Mb(Mesh m) => Profiler.GetRuntimeMemorySizeLong(m) / (1024f * 1024);
            string biggest = string.Join(", ", meshes.OrderByDescending(Mb).Take(3).Select(m => FormattableString.Invariant($"{m.name} {m.vertexCount} v, {m.blendShapeCount} formas, {Mb(m):0.00} MB")));
            Debug.Log(FormattableString.Invariant($"[memoria] una figura {what}: {meshes.Count} mallas, {meshes.Sum(m => m.vertexCount)} vértices, {meshes.Sum(m => m.blendShapeCount)} formas de mezcla, {meshes.Sum(Mb):0.00} MB, legibles {meshes.Count(m => m.isReadable)}; las mayores: {biggest}"));
        }

        /// <summary>The figures kept and the memory, once the unused are let go ([memoria]).</summary>
        static async Awaitable Measure(string what)
        {
            GC.Collect();
            var unloading = Resources.UnloadUnusedAssets();
            while (!unloading.isDone) await Awaitable.NextFrameAsync();
            const float MB = 1024 * 1024;
            (int n, float mb) Of<T>() where T : UnityEngine.Object
            {
                var all = Resources.FindObjectsOfTypeAll<T>();
                return (all.Length, all.Sum(o => Profiler.GetRuntimeMemorySizeLong(o)) / MB);
            }
            var shelf = GameObject.Find("Soldados hechos");
            var meshes = Of<Mesh>();
            var textures = Of<Texture2D>();
            var materials = Of<Material>();
            Debug.Log(FormattableString.Invariant($"[memoria] {what}: {(shelf != null ? shelf.transform.childCount : 0)} figuras guardadas; mallas {meshes.n} ({meshes.mb:0.0} MB), texturas {textures.n} ({textures.mb:0.0} MB), materiales {materials.n} ({materials.mb:0.0} MB); Unity {Profiler.GetTotalAllocatedMemoryLong() / MB:0} MB, reservado {Profiler.GetTotalReservedMemoryLong() / MB:0} MB, gráfica {Profiler.GetAllocatedMemoryForGraphicsDriver() / MB:0} MB, Mono {Profiler.GetMonoUsedSizeLong() / MB:0} MB"));
        }
    }
}
