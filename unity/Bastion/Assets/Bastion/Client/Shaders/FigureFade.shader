// A figure coming into sight or going out of the camera's way (Bastion.Client.Units.UnitViews.Fade; 0.10.1's dyeFade in
// synty.ts): its body drawn with this in its materials' place while it fades, an ordered 4 × 4 pattern deciding which
// fragments show yet (_Fade, 0 none to 1 all), lit plainly from its own colour map (the sun with its shadows, the sky).
// No shadow of its own meanwhile, as 0.10.1 cast none until it was all there. _Fade comes per renderer.
Shader "Bastion/FigureFade"
{
    Properties
    {
        _BaseMap ("Colour map", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Fade;
        CBUFFER_END

        // synty.ts dyeFade: which of the 16 cells of the pattern show by now.
        void Dither(float4 position)
        {
            uint2 cell = (uint2)fmod(position.xy, 4.0);
            static const float order[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            if ((order[cell.x + cell.y * 4] + 0.5) / 16.0 > _Fade) discard;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 world : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.position.xyz);
                output.position = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                Dither(input.position);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(input.normal);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                half3 light = sun.color * saturate(dot(n, sun.direction)) * sun.shadowAttenuation + SampleSH(n);
                return half4(albedo * light, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            float4 Vertex(float4 position : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(position.xyz);
            }

            half Fragment(float4 position : SV_POSITION) : SV_Target
            {
                Dither(position);
                return position.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
            };

            Varyings Vertex(float4 position : POSITION, float3 normal : NORMAL)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position.xyz);
                output.normal = TransformObjectToWorldNormal(normal);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                Dither(input.position);
                return half4(NormalizeNormalPerPixel(input.normal), 0);
            }
            ENDHLSL
        }
    }
}
