// A drop, a flake or a wisp of blown snow (ClimaLab's particles): its soft round texture in its colour (times _Gain),
// unlit, blended over the picture, in the map's haze; none under a roof (Clima.shader's map of roofs), where a building
// is seen through the cutaway.
Shader "Bastion/ClimaGota"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Gain ("Gain", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // What passes by the camera fades out, from _ClimaNear.y metres off to gone at _ClimaNear.x (Weather.Follow: a
            // share of the way to the ground it looks at, 4 to 12 m at the least; those two without it): the storm's streaks
            // close to it were huge, over the squad. The far ones, that make the rain, as they were. The review of 10/11, L-13
            // (6 to 20 m, tried, took most of the rain away at the nearest camera and little more at the first one).
            #define NearFrom 4.0
            #define NearTo 12.0
            float4 _ClimaNear;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // How much brighter than its colour (a flake as bright as the lit snow under it).
            float _Gain;
            TEXTURE2D(_ClimaRoofs);
            float4 _ClimaSize;

            struct Attributes
            {
                float4 position : POSITION;
                half4 colour : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 colour : COLOR;
                float2 uv : TEXCOORD0;
                float fog : TEXCOORD1;
                float3 world : TEXCOORD2;
            };

            Varyings Vertex(Attributes a)
            {
                Varyings v;
                VertexPositionInputs at = GetVertexPositionInputs(a.position.xyz);
                v.position = at.positionCS;
                v.colour = a.colour;
                v.uv = a.uv;
                v.fog = ComputeFogFactor(at.positionCS.z);
                v.world = at.positionWS;
                return v;
            }

            half4 Fragment(Varyings v) : SV_Target
            {
                if (_ClimaSize.w > 0.5)
                {
                    float2 cell = float2(v.world.x, -v.world.z) / _ClimaSize.z;
                    float roof = SAMPLE_TEXTURE2D_LOD(_ClimaRoofs, sampler_PointClamp, cell / _ClimaSize.xy, 0).r;
                    if (roof > 0 && v.world.y / _ClimaSize.z < roof - 0.2) discard;
                }
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, v.uv) * v.colour;
                c.rgb *= _Gain;
                float2 near = _ClimaNear.y > 0 ? _ClimaNear.xy : float2(NearFrom, NearTo);
                c.a *= smoothstep(near.x, near.y, distance(v.world, GetCameraPositionWS()));
                c.rgb = MixFog(c.rgb, v.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
