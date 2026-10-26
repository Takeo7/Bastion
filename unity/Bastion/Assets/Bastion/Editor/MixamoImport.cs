using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Mixamo's clips for the combat (docs/UNITY-ARTE.md §2) into Assets/Mixamo, off GitHub like Assets/Synty: copied
    /// from assets-src (X Bot, no skin, 30 fps), each named by the game's id, humanoid on the avatar of X Bot's T-pose,
    /// in place (the game moves and turns the unit), the throws mirrored to the other hand. Run it on each machine:
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MixamoImport.Run
    /// </summary>
    public static class MixamoImport
    {
        const string Folder = "Assets/Mixamo";

        /// <summary>The clips by the game's id: their file under assets-src, and whether they go mirrored.</summary>
        static readonly (string id, string file, bool mirror)[] Clips =
        {
            ("rifleIdle", "mixamo/Rifle Idle.fbx", false),
            ("rifleLowered", "mixamo/Rifle Idle 4.fbx", false),
            ("rifleIdleLook", "mixamo/Rifle Idle 3.fbx", false),
            ("rifleAim", "mixamo/Rifle Aiming Idle.fbx", false),
            ("rifleFire", "mixamo/Firing Rifle.fbx", false),
            ("rifleReload", "mixamo/Reloading.fbx", false),
            ("rifleKneel", "mixamo/Rifle Kneel Idle.fbx", false),
            ("rifleKneelAim", "mixamo/Idle Crouching Aiming.fbx", false),
            ("rifleWalk", "mixamo/Rifle Walk.fbx", false),
            ("rifleRun", "mixamo/Rifle Run.fbx", false),
            ("rifleSprint", "mixamo/Sprint Forward.fbx", false),
            ("rifleTurnL45", "mixamo/Rifle Turn.fbx", false),
            ("rifleTurnR45", "mixamo/Rifle Turn 2.fbx", false),
            ("rifleTurnL90", "mixamo/Rifle Turn 6.fbx", false),
            ("rifleTurnR90", "mixamo/Rifle Turn 8.fbx", false),
            ("rifleTurnL180", "mixamo/Rifle Turn 5.fbx", false),
            ("rifleTurnR180", "mixamo/Rifle Turn 3.fbx", false),
            ("pistolRest", "mixamo/Pistol Aim.fbx", false),
            ("pistolAim", "mixamo/Pistol Idle 2.fbx", false),
            ("pistolFire", "mixamo/Shooting.fbx", false),
            ("tossGrenade", "mixamo/Toss Grenade.fbx", true),
            ("throwStone", "mixamo/Throw Object.fbx", true),
            ("knifeStab", "mixamo/Stabbing 3.fbx", false),
            ("macheteSwing", "mixamo/Standing Melee Attack Horizontal.fbx", false),
            ("zombieNeckBite", "mixamo-30fps/Zombie Neck Bite.fbx", false),
            ("zombieHeadbutt", "mixamo-30fps/Zombie Headbutt.fbx", false),
            ("zombieFeed", "mixamo-30fps/Zombie Biting.fbx", false),
            ("zombieFeedFromStanding", "mixamo-30fps/Zombie Biting 2.fbx", false),
            // The dead's feeding (syntyManifest.ts FEED_CLIPS): down onto its knees by the fallen, and up again.
            ("kneelDown", "mixamo-30fps/Kneeling Down.fbx", false),
            ("kneelUp", "mixamo-30fps/Standing.fbx", false),
            // Boarding La Lata (clearingArt.ts board, boardCar; syntyManifest.ts CAMP_SPANS): into the 4×4 by a left door
            // (mirrored, a right one), and up a step into the others.
            ("enterCar", "mixamo/Entering Car.fbx", false),
            ("enterCarR", "mixamo/Entering Car.fbx", true),
            ("stepUp", "mixamo/Ascending Stairs.fbx", false),
            // The camp (clearingLife.ts; syntyManifest.ts CAMP_LOOPS, CAMP_MOVES): sat on a camp chair, on the ground,
            // talking; sitting down and getting up; knelt warming the hands by the fire (in, stayed in and out: CAMP_PARTS);
            // squatting.
            ("sitChair", "mixamo/Sitting Idle 3.fbx", false),
            ("sitChairStill", "mixamo/Sitting Idle 5.fbx", false),
            ("sitBreathe", "mixamo/Sitting Idle.fbx", false),
            ("sitFloor", "mixamo/Sitting Idle 2.fbx", false),
            ("sitReclined", "mixamo/Sitting Idle 4.fbx", false),
            ("sitCrossed", "mixamo/Sitting.fbx", false),
            ("sitTalk", "mixamo/Sitting Talking.fbx", false),
            ("sitTalk2", "mixamo/Sitting Talking 2.fbx", false),
            ("standToSit", "mixamo/Stand To Sit.fbx", false),
            ("sitToStand", "mixamo/Sit To Stand.fbx", false),
            ("warmHands", "mixamo/Kneel.fbx", false),
            ("squat", "mixamo/Crouch Idle.fbx", false),
        };

        public static void Run()
        {
            try
            {
                Import();
            }
            catch (Exception e)
            {
                Debug.LogError("[mixamo] " + e);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        static void Import()
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../assets-src"));
            Directory.CreateDirectory(Folder);
            // The T-pose first: every clip takes its avatar.
            string tpose = $"{Folder}/T-Pose.fbx";
            File.Copy(Path.Combine(source, "mixamo/T-Pose.fbx"), tpose, true);
            AssetDatabase.ImportAsset(tpose, ImportAssetOptions.ForceSynchronousImport);
            var model = (ModelImporter)AssetImporter.GetAtPath(tpose);
            model.animationType = ModelImporterAnimationType.Human;
            model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            model.importAnimation = false;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.SaveAndReimport();
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(tpose);
            if (avatar == null || !avatar.isHuman) throw new InvalidOperationException("the T-pose gave no humanoid avatar");

            foreach (var (id, file, mirror) in Clips)
            {
                string path = $"{Folder}/{id}.fbx";
                File.Copy(Path.Combine(source, file), path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var clips = new List<ModelImporterClipAnimation>();
                foreach (var clip in importer.defaultClipAnimations)
                {
                    clip.name = id;
                    clip.mirror = mirror;
                    // In place: the root's turn, height and travel stay in the pose; the game moves the unit itself.
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                    clips.Add(clip);
                }
                importer.clipAnimations = clips.ToArray();
                importer.SaveAndReimport();
            }
            Debug.Log($"[mixamo] {Clips.Length} clips in {Folder}, on {tpose}'s avatar");
        }
    }
}
