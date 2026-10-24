// The ground's material inputs (Ground.shader): the same in every pass, as the SRP Batcher wants; what URP's own depth
// passes read (_BaseMap, _BaseColor, _Cutoff) among them.
#ifndef BASTION_GROUND_INPUT
#define BASTION_GROUND_INPUT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    // The map in tiles; the biome (0 town, 1 wilds, 2 facility); tarmac on the forest road; snow; a canal along a side
    // (x: its side, DIRS4, −1 none; y: how far past the edge its far bank is).
    float4 _GroundSize;
    float _Biome;
    float _Tarmac;
    float _Snow;
    float4 _Water;
    // ground.ts SYNTY_LOOK: flat colours, and tints over the tiling textures (linear).
    half4 _Pave;
    half4 _Kerb;
    half4 _AsphaltTint;
    half4 _Moss;
    half4 _Grime;
    half4 _Dust;
    half4 _Paint;
    half4 _Yellow;
    half4 _GrassTint;
    half4 _GrassDry;
    half4 _DirtTint;
    half4 _Concrete;
    half4 _Deck;
    half _Smoothness;
CBUFFER_END

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

#endif
