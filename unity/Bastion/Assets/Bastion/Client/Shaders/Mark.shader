// The target being picked, marked on its body (Bastion.Client.Units.UnitViews.Highlight; synty.ts dyeMark): one more
// material over the body's meshes, drawn twice. First the colour taken towards red, as 0.10.1 took the diffuse
// (× (1, 0.32, 0.26) by 0.45 of the mark), multiplied over what is drawn; then a red glow added, stronger at the
// edges as they turn away (0.1 + 1.2 × (1 − n·v)²). _Mark is the mark now (0 none, 1 all), set from outside.
Shader "Bastion/Mark"
{
    Properties
    {
        _Mark ("Mark", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _Mark;
        CBUFFER_END

        struct Attributes
        {
            float4 position : POSITION;
            float3 normal : NORMAL;
        };

        struct Varyings
        {
            float4 position : SV_POSITION;
            float3 normal : TEXCOORD0;
            float3 world : TEXCOORD1;
        };

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            output.world = TransformObjectToWorld(input.position.xyz);
            output.position = TransformWorldToHClip(output.world);
            output.normal = TransformObjectToWorldNormal(input.normal);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Tint"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                return half4(lerp(half3(1, 1, 1), half3(1, 0.32, 0.26), 0.45 * _Mark), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normal);
                float3 v = normalize(GetWorldSpaceViewDir(input.world));
                half edge = 1 - saturate(dot(n, v));
                return half4(half3(1, 0.09, 0.05) * _Mark * (0.1 + 1.2 * edge * edge), 0);
            }
            ENDHLSL
        }
    }
}
