// The buildings' material inputs (Building.shader): the same in every pass, as the SRP Batcher wants; what URP's own
// shadow and depth passes read (_BaseMap, _BaseColor, _Cutoff) among them.
#ifndef BASTION_BUILDING_INPUT
#define BASTION_BUILDING_INPUT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half _Cutoff;
    half _Smoothness;
CBUFFER_END

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

#endif
