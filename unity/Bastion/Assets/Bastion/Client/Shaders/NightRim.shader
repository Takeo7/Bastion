// By night a figure's cool moonlit rim (synty.ts dyeRim, under unitNight): one more material over the body's meshes,
// added. (0.42, 0.62, 0.8) × (1 − n·v)³ × 0.5, and a breath of its own colour, 0.07 of its colour map, both × _Rim (1 the
// squad, 0.22 the enemy and the dead: syntyFigures.ts ENEMY_RIM), set per renderer from outside (UnitViews.Rim). The
// colour map is the copy's, or the renderer's own block's (a soldier's palette: SidekickLab.OwnColours).
Shader "Bastion/NightRim"
{
    Properties
    {
        _ColorMap ("Colour map", 2D) = "white" {}
        _Rim ("Rim", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ColorMap);
            SAMPLER(sampler_ColorMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorMap_ST;
                half _Rim;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.position.xyz);
                output.position = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normal);
                float3 v = normalize(GetWorldSpaceViewDir(input.world));
                half edge = 1 - saturate(dot(n, v));
                half3 own = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, input.uv).rgb;
                return half4((half3(0.42, 0.62, 0.8) * edge * edge * edge * 0.5 + own * 0.07) * _Rim, 0);
            }
            ENDHLSL
        }
    }
}
