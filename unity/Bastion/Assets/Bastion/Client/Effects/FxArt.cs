using UnityEngine;

namespace Bastion.Client.Effects
{
    /// <summary>
    /// What the effects are drawn with (0.10.1's syntyManifest.ts FX_MODELS, FX_TEXTURES): Particle FX's shapes and
    /// sprites, and the two shaders. Resources/FxArt.asset, made by Bastion.EditorTools.FxArtMake.Run; without it the
    /// effects draw with Unity's own shapes and a soft glow.
    /// </summary>
    public sealed class FxArt : ScriptableObject
    {
        /// <summary>Bastion/FxUnlit: light (added), sprites and decals (blended), flames (opaque).</summary>
        public Shader unlit;
        /// <summary>URP's Lit: smoke, dust, gore, chips, the shell and the stone.</summary>
        public Shader lit;

        public Mesh smokePuff;
        public Mesh goreChunk1;
        public Mesh goreChunk2;
        public Mesh goreChunk3;
        public Mesh flame;
        public Mesh medCross;
        public Mesh woodChip;

        public Texture2D polyDisc;
        public Texture2D sparkle;
        public Texture2D shockRing;
        public Texture2D groundCrack;
        public Texture2D lightning;
        /// <summary>The blade's arc.</summary>
        public Texture2D semiCircle;
    }
}
