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
        /// <summary>
        /// The smoke, the dust and the gas (Fx.Puffs; the 3D review of 10/11, F-01 and «Puffs», Arturo's choice C: see-through,
        /// as Particle FX's own smoke draws). Not `smoke`: that is the black smoke's prefab.
        /// </summary>
        public Texture2D smokeSprite;

        /// <summary>A map's fires (Map.FireView, Arturo 1/11): Particle FX's flames, small, middling and big; its embers; its black smoke.</summary>
        public GameObject fireSmall;
        public GameObject fire;
        /// <summary>A pool's flames (the 3D review's L-05, 10/11): Particle FX's small fire of flame shapes, not FX_Fire_01's balls.</summary>
        public GameObject fireLick;
        public GameObject fireBig;
        public GameObject embers;
        public GameObject smoke;
        /// <summary>A molotov's bottle (Arturo, 1/11: thrown as a grenade, its pool of petrol burning): Apocalypse's, its rag lit by a small fire (Fx.Molotov).</summary>
        public GameObject molotov;
        /// <summary>
        /// The grenades thrown (the review of 9/11, Arturo in the box: POLYGON Military's, in the hand and in the air; till
        /// then nothing in the hand and a green ball flying): the frag, the smoke, the flash (Fx.Thrown).
        /// </summary>
        public GameObject grenade, grenadeSmoke, grenadeFlash;
        /// <summary>The medicine given (the review of 9/11, Gameplay: «la venda o la jeringa»): Gang Warfare's syringe, in the hand that treats (UnitViews.Treat).</summary>
        public GameObject syringe;
    }
}
