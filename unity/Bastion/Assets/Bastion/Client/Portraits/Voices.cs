using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The radio's voices (portraits.ts VOICES): who each speaker is drawn as (POLYGON's characters, syntyManifest.ts voice*),
    /// with a headset or not, their clothes dyed (baked in the editor by Bastion.EditorTools.VoiceDyesMake into Resources/Voices:
    /// the body split in what is dyed and the rest, and their atlases), how turned, lit only from behind or not.
    /// </summary>
    public sealed class Voices
    {
        public Func<GameObject> Make;
        public float turn = -0.3f;
        public bool backlit;
        public bool female;

        /// <summary>A voice as 0.10.1 has it: the character's prefab, a headset, the torso's and arms' dye, the skin's.</summary>
        public sealed class Look
        {
            public string prefab;
            public bool headset;
            public string dye;
            public string skin;
            public float turn = -0.3f;
            public bool backlit;
            public bool female;
        }

        public const string Headset = "SM_Chr_Attachment_Headset_02";
        public const string Folder = "Voices";

        /// <summary>
        /// portraits.ts VOICES. Morse: sixty, grey hair, her radio headphones, a wine cardigan · Teo: bald, white moustache,
        /// his good jacket · Nwosu: dark curls, glasses, a white coat · Simão: a Cuervos deserter in a field jacket, his
        /// scarred side shown · la Voz: a radio host's headset, ivory and composed · Halvorsen: platinum hair, a charcoal
        /// suit · the Coro: a veil against a green glow, nobody you could name.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            [SpeakerIds.Morse] = new Look { prefab = "SM_Chr_Boss_Female_01", headset = true, dye = "#7a2e3a", female = true },
            [SpeakerIds.Teo] = new Look { prefab = "SM_Chr_Boss_Male_01" },
            [SpeakerIds.Nwosu] = new Look { prefab = "SM_Chr_Business_Female_03", dye = "#e8e6df", skin = "#6b4330", female = true },
            [SpeakerIds.Simon] = new Look { prefab = "Character_Male_Jacket", turn = -0.48f },
            [SpeakerIds.Voz] = new Look { prefab = "SM_Chr_Business_Female_04", headset = true, dye = "#e9e4d8", female = true },
            [SpeakerIds.Halvorsen] = new Look { prefab = "Character_BusinessWoman", dye = "#2b2f35", female = true },
            [SpeakerIds.Coro] = new Look { prefab = "SM_Chr_Nun_Female_01", turn = 0.35f, backlit = true, female = true },
        };

        /// <summary>Every prefab the voices are made of (FigureCatalogMake puts them in the catalog).</summary>
        public static IEnumerable<string> Prefabs => Looks.Values.Select(l => l.prefab).Append(Headset).Distinct();

        /// <summary>The voice of `speaker` (SpeakerIds); null, none.</summary>
        public static Voices Of(string speaker)
        {
            if (speaker == null || !Looks.TryGetValue(speaker, out var look)) return null;
            return new Voices { Make = () => Figure(speaker, look), turn = look.turn, backlit = look.backlit, female = look.female };
        }

        /// <summary>The character alone (the prefab's other bodies gone), its headset on, its clothes dyed.</summary>
        static GameObject Figure(string speaker, Look look)
        {
            var catalog = FigureCatalog.Load();
            var prefab = catalog?.Character(look.prefab);
            if (prefab == null) return null;
            var figure = UnityEngine.Object.Instantiate(prefab);
            figure.name = look.prefab;
            // The pack's prefab holds every body of its rig, all but one off.
            foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => !r.gameObject.activeSelf).ToList()) Gone(r.gameObject);
            var animator = figure.GetComponentInChildren<Animator>();
            if (look.headset && animator != null && animator.isHuman && catalog.Character(Headset) is GameObject set)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var headset = UnityEngine.Object.Instantiate(set, head, false);
                headset.name = "auriculares";
                headset.transform.localPosition = Vector3.zero;
                headset.transform.localRotation = Quaternion.identity;
            }
            // Its dyes (VoiceDyesMake): the body split in what is dyed and the rest, each with its atlas.
            var dyed = Resources.Load<Texture2D>(Folder + "/" + speaker);
            var rest = Resources.Load<Texture2D>(Folder + "/" + speaker + "-piel");
            var split = Resources.Load<Mesh>(Folder + "/" + speaker);
            var body = figure.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault();
            if (body != null && dyed != null)
            {
                var material = body.sharedMaterial;
                if (split != null)
                {
                    body.sharedMesh = split;
                    body.materials = new[] { Worn(material, dyed, speaker), rest != null ? Worn(material, rest, speaker) : material };
                }
                else body.materials = new[] { Worn(material, dyed, speaker) };
            }
            return figure;
        }

        /// <summary>A copy of `material` wearing `atlas`.</summary>
        static Material Worn(Material material, Texture atlas, string speaker)
        {
            var copy = new Material(material) { name = material.name + " (" + speaker + ")" };
            foreach (string property in AtlasProperties)
                if (copy.HasProperty(property) && copy.GetTexture(property) != null) copy.SetTexture(property, atlas);
            return copy;
        }

        /// <summary>Where a character's material keeps its atlas: Synty's own shader's, URP's, the built-in's.</summary>
        static readonly string[] AtlasProperties = { "_Albedo_Map", "_BaseMap", "_MainTex" };

        /// <summary>A character material's atlas, or null.</summary>
        public static Texture AtlasOf(Material material)
        {
            if (material == null) return null;
            foreach (string property in AtlasProperties)
                if (material.HasProperty(property) && material.GetTexture(property) is Texture t) return t;
            return material.mainTexture;
        }

        static void Gone(GameObject o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
