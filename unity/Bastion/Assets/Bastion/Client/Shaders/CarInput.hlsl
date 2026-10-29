// The street's whole cars (Car.shader): the same inputs in every pass, as the SRP Batcher wants; what URP's own shadow
// and depth passes read (_BaseMap, _BaseColor, _Cutoff) among them.
#ifndef BASTION_CAR_INPUT
#define BASTION_CAR_INPUT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _Smoothness;
    half _Metallic;
    half _NormalAmount;
    half _Dull;
    half _Dust;
    half4 _DustColor;
CBUFFER_END

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

#endif
