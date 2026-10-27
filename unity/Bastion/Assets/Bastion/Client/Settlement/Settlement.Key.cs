using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.Client
{
    /// <summary>
    /// The soldiers' own light on the clearing (0.10.1's clearingArt.ts FACE_LIGHT, FACE_SKY, FACE_KEY; synty.ts UNIT_FACE,
    /// UNIT_SKY; the one Arturo chose on 23/10, #13): a warm white key from above the camera and to its left as it looks,
    /// 2.4 as three's, on the soldiers alone (a rendering layer of their own), so a face with the sun behind it reads and
    /// the body has its light and shade; and their sky 0.6 of the scene's (a light probe of their own, the ambient scaled).
    /// Only on the clearing: not the portraits', nor the battle's.
    /// </summary>
    public sealed partial class Settlement : IShotStep
    {
        /// <summary>Captures in edit mode step it by hand (Shots): the key where the camera has it.</summary>
        public void ShotStep(float dt)
        {
            if (!Application.isPlaying) StepFaceKey();
        }

        const float FaceLight = 2.4f, FaceSky = 0.6f;
        static readonly Color FaceKey = new Color32(0xff, 0xf1, 0xe2, 0xff);
        /// <summary>The rendering layer the key lights (the soldiers on the clearing), beside the default one.</summary>
        public const uint FaceLayer = 1u << 1;
        /// <summary>synty.ts: the key's way in the camera's frame (three's view space: z towards the viewer).</summary>
        static readonly Vector3 FaceFrom = new Vector3(-0.55f, 0.6f, 0.6f).normalized;

        Light faceKey;
        readonly HashSet<Renderer> faceLit = new HashSet<Renderer>();
        SphericalHarmonicsL2 faceSkyWas;
        MaterialPropertyBlock faceBlock;

        /// <summary>`figure` (a soldier on the clearing) in the key's layer, its sky FaceSky of the scene's.</summary>
        void FaceLit(GameObject figure)
        {
            if (figure == null) return;
            faceBlock ??= new MaterialPropertyBlock();
            var sky = Sky();
            foreach (var r in figure.GetComponentsInChildren<Renderer>(true))
            {
                r.renderingLayerMask |= FaceLayer;
                r.lightProbeUsage = LightProbeUsage.CustomProvided;
                Probe(r, sky);
                faceLit.Add(r);
            }
        }

        /// <summary>What isn't a soldier under `root` out of the key's layer: Synty's prefabs come in every layer (La Lata's glass caught it).</summary>
        void UnFace(Transform root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (!faceLit.Contains(r)) r.renderingLayerMask &= ~FaceLayer;
        }

        static SphericalHarmonicsL2 Sky() => RenderSettings.ambientProbe * (FaceKeyOff ? 1 : FaceSky);

        void Probe(Renderer r, SphericalHarmonicsL2 sky)
        {
            r.GetPropertyBlock(faceBlock);
            faceBlock.CopySHCoefficientArraysFrom(new List<SphericalHarmonicsL2> { sky });
            r.SetPropertyBlock(faceBlock);
        }

        /// <summary>For the labs: the key off and their sky the scene's (to tell what it does).</summary>
        public static bool FaceKeyOff;

        /// <summary>The key where the camera has it, each frame; the soldiers' sky again when the scene's changes (Mood).</summary>
        void StepFaceKey()
        {
            var camera = Camera;
            if (camera == null) return;
            if (faceKey == null)
            {
                faceKey = new GameObject("Luz de los soldados").AddComponent<Light>();
                faceKey.transform.SetParent(transform, false);
                faceKey.type = LightType.Directional;
                faceKey.color = FaceKey;
                // (Mood: three's intensity over π.)
                faceKey.intensity = FaceLight / Mathf.PI;
                faceKey.shadows = LightShadows.None;
                // (URP reads a light's layers from its own data, not Light.renderingLayerMask: there, Default, it lit everything.)
                var data = faceKey.GetUniversalAdditionalLightData();
                data.renderingLayers = FaceLayer;
                faceKey.renderingLayerMask = (int)FaceLayer;
            }
            faceKey.enabled = !FaceKeyOff;
            // (Unity's camera looks along its +z: three's z towards the viewer is its −z.)
            var from = camera.transform.rotation * new Vector3(FaceFrom.x, FaceFrom.y, -FaceFrom.z);
            faceKey.transform.rotation = Quaternion.LookRotation(-from, Vector3.up);
            var sky = Sky();
            if (sky == faceSkyWas) return;
            faceSkyWas = sky;
            faceLit.RemoveWhere(r => r == null);
            faceBlock ??= new MaterialPropertyBlock();
            foreach (var r in faceLit) Probe(r, sky);
        }
    }
}
