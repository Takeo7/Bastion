// The outline's flat-coloured shapes (Flat.shader): the same inputs in every pass, as the SRP Batcher wants; what URP's
// own shadow and depth passes read (_BaseMap, _BaseColor, _Cutoff) among them.
#ifndef BASTION_FLAT_INPUT
#define BASTION_FLAT_INPUT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _Smoothness;
    half _Metallic;
CBUFFER_END

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

#endif
