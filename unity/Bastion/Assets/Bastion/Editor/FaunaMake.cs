using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The animals' animator (Fauna; Arturo's #3 of 1/11, «los perros no tienen animaciones»): the Dog Pack's prefabs name a
    /// controller that isn't in the project, and the pack's own (Animation_Dog.controller) names clips that aren't either,
    /// its walk and run among them, so the dogs and the wild ones stood in their bind pose. Ours, in Resources (so it is in
    /// the build), of the pack's clips that are: standing, walking and running by Movement_f (0, 0.5, 1); a gesture now and
    /// then while it stands (Gesto with Accion: Fauna.Gestures), back to standing once played, or at once if it moves; and
    /// sitting or lying down, getting up again when Accion changes or it moves (the states it can't walk in tagged Suelo).
    /// And SIMPLE - Forest Animals' material (Arturo allowed its stags, does, boars and rabbits, 1/11) and SIMPLE - Cats'
    /// (its cats, Ana's #11): theirs are the built-in pipeline's Standard, pink under URP; ours URP's Lit with their
    /// textures, in Resources too.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.FaunaMake.Run -quit
    /// </summary>
    public static class FaunaMake
    {
        const string Path = "Assets/Bastion/Client/Resources/" + Bastion.Client.Map.Fauna.ControllerName + ".controller";
        const string Clips = "Assets/Synty/PolygonDog/Animations/";

        const string Folder = "Assets/Bastion/Client/Resources/";

        /// <summary>SIMPLE's animals' material (`name` in Resources, Folder): URP's Lit with their `texture`, a little gloss as Synty's.</summary>
        static void Simple(string name, string texturePath, string package)
        {
            string path = Folder + name + ".mat";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"[fauna] no {texturePath}: unity/tools/importar-synty.sh {package}");
                return;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            Debug.Log($"[fauna] {path}");
        }

        static AnimationClip Clip(string file)
        {
            string path = $"{Clips}{file}.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) Debug.LogError($"[fauna] no clip in {path}");
            return clip;
        }

        public static void Run()
        {
            Simple(Bastion.Client.Map.Fauna.SimpleMaterial, "Assets/Synty/SimpleForestAnimal/Textures/SimpleForestAnimalsTexture.png", "SIMPLE_Forest_Animals_Unity_2020_3_v1_02");
            Simple(Bastion.Client.Map.Fauna.CatMaterial, "Assets/Synty/SimpleCats/Textures/SimpleCats.png", "SIMPLE_Cats_Unity_2020_3_v1_0_1");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Path) != null) AssetDatabase.DeleteAsset(Path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Path);
            controller.AddParameter("Movement_f", AnimatorControllerParameterType.Float);
            controller.AddParameter("Accion", AnimatorControllerParameterType.Int);
            controller.AddParameter("Gesto", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;

            // Standing, walking, running.
            var walk = controller.CreateBlendTreeInController("Andar", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Movement_f";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Locomotion/_POLYGON_Dog_Locomotion_Standing"), 0);
            tree.AddChild(Clip("Locomotion/_POLYGON_Dog_Locomotion_Walking"), 0.5f);
            tree.AddChild(Clip("Locomotion/_POLYGON_Dog_Locomotion_Running"), 1);
            walk.tag = "Mueve";
            machine.defaultState = walk;

            AnimatorStateTransition To(AnimatorState from, AnimatorState to, float duration, float exit = -1)
            {
                var t = from.AddTransition(to);
                t.duration = duration;
                t.hasExitTime = exit >= 0;
                if (exit >= 0) t.exitTime = exit;
                return t;
            }
            AnimatorState State(string name, string clip, string tag, int row)
            {
                var state = machine.AddState(name, new Vector3(500, 60 * row, 0));
                state.motion = Clip(clip);
                state.tag = tag;
                return state;
            }

            // A gesture: in from standing, played once, back to standing (or at once, if it moves).
            foreach (var (accion, name, clip) in Bastion.Client.Map.Fauna.Gestures)
            {
                var state = State(name, clip, "Mueve", accion);
                var into = To(walk, state, 0.25f);
                into.AddCondition(AnimatorConditionMode.If, 0, "Gesto");
                into.AddCondition(AnimatorConditionMode.Equals, accion, "Accion");
                into.AddCondition(AnimatorConditionMode.Less, 0.1f, "Movement_f");
                To(state, walk, 0.25f, 0.95f);
                To(state, walk, 0.15f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Movement_f");
            }

            // Down and up again: sitting (Accion 7) and lying (8).
            void Down(int accion, string name, string down, string idle, string up, int row)
            {
                var going = State($"{name}: baja", down, "Suelo", row);
                var still = State(name, idle, "Suelo", row + 1);
                var rising = State($"{name}: sube", up, "Suelo", row + 2);
                var into = To(walk, going, 0.2f);
                into.AddCondition(AnimatorConditionMode.If, 0, "Gesto");
                into.AddCondition(AnimatorConditionMode.Equals, accion, "Accion");
                into.AddCondition(AnimatorConditionMode.Less, 0.1f, "Movement_f");
                To(going, still, 0.1f, 1);
                To(still, rising, 0.15f).AddCondition(AnimatorConditionMode.NotEqual, accion, "Accion");
                To(still, rising, 0.15f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Movement_f");
                To(rising, walk, 0.15f, 1);
            }
            Down(Bastion.Client.Map.Fauna.Sit, "Sentado", "Transitions/_POLYGON_Dog_Transition_Stand_ToSit", "Sit/_POLYGON_Dog_Sitting", "Transitions/_POLYGON_Dog_Transition_Sit_ToStand", 8);
            Down(Bastion.Client.Map.Fauna.Lie, "Tumbado", "Transitions/_POLYGON_Dog_Transition_Stand_ToSleep", "Sleep/_POLYGON_Dog_Sleep_Idle", "Transitions/_POLYGON_Dog_Transition_Sleep_ToStand", 12);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[fauna] {Path}: {machine.states.Length} states, " + string.Join(", ", machine.states.Select(s => $"{s.state.name} ({(s.state.motion != null ? s.state.motion.name : "none")})")));
        }
    }
}
