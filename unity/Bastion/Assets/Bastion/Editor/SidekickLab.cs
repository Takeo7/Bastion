using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Units;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The first look at Sidekick in Unity (docs/UNITY-ARTE.md), as setups for Shots (-shotSetup): the 0.10.1 soldier in
    /// survivor4's clothes with face1, hairLongM and beard2, put together at run time from those parts with the Sidekicks
    /// tool's API; beside it the zombie skZombie1, the ready prefab of ApocalypseZombie_01. On a plain floor, by day or
    /// by night, standing (their idles) or walking (in place). The game camera: 52° down, 40° of view, 24 tiles (36 m) off.
    /// </summary>
    public static class SidekickLab
    {
        public static void DayStill() => Build(true, false);
        public static void NightStill() => Build(false, false);
        public static void DayWalk() => Build(true, true);
        public static void NightWalk() => Build(false, true);

        /// <summary>
        /// Figures in a row, in the order of BASTION_LAB (soldier, survivor, zombie, comma separated), by day, standing: to
        /// see whether one changes how another is drawn.
        /// </summary>
        public static void Row()
        {
            Ground();
            Light(true);
            // BASTION_NOBATCH=1: without URP's SRP Batcher this once (put back as the editor quits).
            if (Environment.GetEnvironmentVariable("BASTION_NOBATCH") == "1" && GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                bool was = urp.useSRPBatcher;
                urp.useSRPBatcher = false;
                EditorApplication.quitting += () => urp.useSRPBatcher = was;
            }
            var names = (Environment.GetEnvironmentVariable("BASTION_LAB") ?? "soldier,survivor,zombie").Split(',');
            for (int k = 0; k < names.Length; k++)
            {
                GameObject figure;
                switch (names[k])
                {
                    case "soldier": figure = Soldier(); break;
                    case "survivor": figure = Prefab("ApocalypseSurvivors/ApocalypseSurvivor_04/ApocalypseSurvivor_04"); break;
                    case "zombie": figure = Prefab("ApocalypseZombies/ApocalypseZombie_01/ApocalypseZombie_01"); break;
                    default: throw new ArgumentException($"no figure {names[k]}");
                }
                Place(figure, (k - (names.Length - 1) / 2f) * 1.2f * Tile);
                Play(figure, Clip(names[k] == "zombie" ? "A_POLY_IDL_Sway_Drunk_Loop_Femn" : "A_Idle_Standing_Masc"));
            }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.fieldOfView = 40;
            camera.transform.position = new Vector3(0, 1.5f, -6.5f);
            camera.transform.LookAt(new Vector3(0, 1.1f, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
        }

        static GameObject Prefab(string path) =>
            (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/{path}.prefab"));

        /// <summary>What each figure of Check is drawn with: its renderers' materials and their colour maps, in the log.</summary>
        public static void Report()
        {
            Check();
            // A second soldier: what one costs once the parts and the database are warm.
            Soldier().name = "Soldado 2";
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                foreach (var m in renderer.sharedMaterials)
                    Debug.Log($"[sidekick] {renderer.transform.root.name}/{renderer.name}: material {m?.name} ({m?.shader?.name}), _ColorMap {(m != null && m.HasTexture("_ColorMap") ? m.GetTexture("_ColorMap")?.name : "-")}, mesh {renderer.sharedMesh?.name} uv0 {renderer.sharedMesh?.uv.Length}");
            }
        }

        /// <summary>The soldier put together at run time beside ApocalypseSurvivor_04's own prefab: whether the tool's build looks as Synty's.</summary>
        public static void Check()
        {
            Build(true, false);
            var survivor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/ApocalypseSurvivors/ApocalypseSurvivor_04/ApocalypseSurvivor_04.prefab"));
            survivor.name = "Superviviente 4";
            Place(survivor, -1.8f * Tile);
            Play(survivor, Clip("A_Idle_Standing_Masc"));
        }

        /// <summary>The 0.10.1 soldier of the comparison: its wardrobe pieces, skin tone and hair colour (wardrobe.ts Dress).</summary>
        static readonly (string piece, string id)[] SoldierDress = { ("outfit", "survivor4"), ("face", "face1"), ("hair", "hairLongM"), ("beard", "beard2") };
        const int SoldierSkin = 2;
        const int SoldierHair = 1;

        const string Characters = "Assets/Synty/SidekickCharacters/Characters";
        const string Clips = "Assets/Synty";
        /// <summary>The figures' size against Synty's (syntyManifest.ts CHARACTER_SCALE), and a tile in metres (KIT_TILE · KIT_SCALE).</summary>
        const float CharacterScale = 1.35f / 1.2f;
        const float Tile = 1.5f;

        static void Build(bool day, bool walk)
        {
            Ground();
            Light(day);

            var soldier = Soldier();
            Place(soldier, -0.6f * Tile);
            Play(soldier, Clip(walk ? "A_Walk_F_Masc" : "A_Idle_Standing_Masc"));

            var zombie = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/ApocalypseZombies/ApocalypseZombie_01/ApocalypseZombie_01.prefab"));
            zombie.name = "Zombi";
            Place(zombie, 0.6f * Tile);
            Play(zombie, Clip(walk ? "A_POLY_GBL_Walk_F_Neut" : "A_POLY_IDL_Sway_Drunk_Loop_Femn"));

            // The game's camera unless -shotFrom/-shotAt place it closer: south of what it looks at, looking north (+z;
            // docs/UNITY-ARTE.md §7).
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.fieldOfView = 40;
            float distance = 24 * Tile;
            float pitch = 52 * Mathf.Deg2Rad;
            camera.transform.position = new Vector3(0, Mathf.Sin(pitch) * distance, -Mathf.Cos(pitch) * distance);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = day ? new Color(0.72f, 0.78f, 0.86f) : new Color(0.03f, 0.05f, 0.08f);
        }

        /// <summary>
        /// The soldier, from its wardrobe pieces' parts, with the Sidekicks tool's runtime API (what a game build does as a
        /// mission loads), in a palette of its own composed as 0.10.1's (SoldierPalette).
        /// </summary>
        static GameObject Soldier()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var pieces = SoldierDress.Select(d => Wardrobe.Piece(d.piece, d.id)).ToList();
            var parts = pieces.SelectMany(p => p.parts).Select(Part).ToList();
            var outfit = pieces[0];
            var palette = SoldierPalette.Compose(outfit, outfit.parts.Select(n => Part(n).sharedMesh).ToList(), null, null, SoldierSkin, SoldierHair, ColorMap);
            double loaded = clock.Elapsed.TotalMilliseconds;
            var baseModel = Resources.Load<GameObject>("Meshes/SK_BaseModel");
            var material = new Material(Resources.Load<Material>("Materials/M_BaseMaterial"));
            material.SetTexture("_ColorMap", palette);
            var runtime = new SidekickRuntime(baseModel, material, null, new DatabaseManager())
            {
                // A man of the middle build: every body shape at none.
                BodyTypeBlendValue = -100,
                BodySizeSkinnyBlendValue = 0,
                BodySizeHeavyBlendValue = 0,
                MusclesBlendValue = -100,
            };
            double ready = clock.Elapsed.TotalMilliseconds;
            var soldier = runtime.CreateCharacter("Soldado", parts, true, true);
            Debug.Log($"[sidekick] soldier: parts and palette {loaded:0} ms, tool and database {ready - loaded:0} ms, put together {clock.Elapsed.TotalMilliseconds - ready:0} ms");
            return soldier;
        }

        /// <summary>A Sidekick preset's colour map (T_&lt;preset&gt;ColorMap), wherever its pack put it.</summary>
        static Texture2D ColorMap(string preset)
        {
            string guid = AssetDatabase.FindAssets($"T_{preset}ColorMap t:Texture2D", new[] { Characters })
                .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == $"T_{preset}ColorMap")
                ?? throw new ArgumentException($"no colour map for {preset}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }

        /// <summary>A figure at its size, `x` along the row, facing the camera (south).</summary>
        static void Place(GameObject figure, float x)
        {
            figure.transform.SetPositionAndRotation(new Vector3(x, 0, 0), Quaternion.Euler(0, 180, 0));
            figure.transform.localScale = Vector3.one * CharacterScale;
        }

        static SkinnedMeshRenderer Part(string name)
        {
            string guid = AssetDatabase.FindAssets($"{name} t:Model", new[] { "Assets/Synty/SidekickCharacters/Resources/Meshes" })
                .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name)
                ?? throw new ArgumentException($"no Sidekick part {name}");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            return model.GetComponentInChildren<SkinnedMeshRenderer>() ?? throw new ArgumentException($"{name} has no skinned mesh");
        }

        static AnimationClip Clip(string name)
        {
            string guid = AssetDatabase.FindAssets($"{name} t:Model", new[] { Clips })
                .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name)
                ?? throw new ArgumentException($"no clip {name}");
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"))
                ?? throw new ArgumentException($"{name} has no clip");
        }

        /// <summary>
        /// Its colour map on the renderer itself (a MaterialPropertyBlock), not left to the material: with URP's SRP Batcher
        /// on, two Sidekick characters drawn one after another came out both in one's colours (Unity 6.3, URP 17.3).
        /// BASTION_NOMPB=1 leaves it to the material, to see that again.
        /// </summary>
        static void OwnColours(GameObject figure)
        {
            if (Environment.GetEnvironmentVariable("BASTION_NOMPB") == "1") return;
            foreach (var renderer in figure.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var map = renderer.sharedMaterial != null ? renderer.sharedMaterial.GetTexture("_ColorMap") : null;
                if (map == null) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetTexture("_ColorMap", map);
                renderer.SetPropertyBlock(block);
            }
        }

        static void Play(GameObject figure, AnimationClip clip)
        {
            OwnColours(figure);
            var animator = figure.GetComponent<Animator>() ?? throw new ArgumentException($"{figure.name} has no Animator");
            figure.AddComponent<ClipPlayer>().Play(animator, clip);
        }

        static void Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Suelo";
            ground.transform.localScale = Vector3.one * 6;
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.72f, 0.69f, 0.63f) };
            lit.SetFloat("_Smoothness", 0.1f);
            ground.GetComponent<Renderer>().sharedMaterial = lit;
        }

        /// <summary>A first day and night, not yet the game's: a warm sun and a sky; a cold moon and a dark blue.</summary>
        static void Light(bool day)
        {
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>() ?? new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(day ? 48 : 35, day ? -35 : 140, 0);
            sun.color = day ? new Color(1f, 0.94f, 0.84f) : new Color(0.6f, 0.72f, 1f);
            sun.intensity = day ? 1.6f : 0.35f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = day ? new Color(0.62f, 0.68f, 0.78f) : new Color(0.07f, 0.1f, 0.16f);
            RenderSettings.ambientEquatorColor = day ? new Color(0.52f, 0.52f, 0.5f) : new Color(0.05f, 0.07f, 0.1f);
            RenderSettings.ambientGroundColor = day ? new Color(0.32f, 0.3f, 0.27f) : new Color(0.03f, 0.04f, 0.05f);
        }

        /// <summary>A clip on a figure, looped and in place, stepped by hand (IShotStep): a strip comes out the same every time.</summary>
        sealed class ClipPlayer : MonoBehaviour, IShotStep
        {
            PlayableGraph graph;

            public void Play(Animator animator, AnimationClip clip)
            {
                animator.applyRootMotion = false;
                graph = PlayableGraph.Create(name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "clip", animator);
                output.SetSourcePlayable(AnimationClipPlayable.Create(graph, clip));
                graph.Play();
                graph.Evaluate(0);
            }

            public void ShotStep(float dt) => graph.Evaluate(dt);

            void OnDestroy()
            {
                if (graph.IsValid()) graph.Destroy();
            }
        }
    }
}
