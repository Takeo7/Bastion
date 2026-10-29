// The target being picked, marked on its body (Bastion.Client.Units.UnitViews.Highlight): one more material over the
// body's meshes. A red line round it, 1.5 pixels wide (_Line: its back faces pushed out on the screen), and nothing over
// its colours (Ana's #12, Gameplay 29/10: 0.10.1's mark, the whole body taken towards red, lost the dead's look). For
// the lab, 0.10.1's other two (synty.ts dyeMark), off: the colour taken towards red (_Tint, its 0.45: the diffuse ×
// (1, 0.32, 0.26)), multiplied over what is drawn; and a red glow added, stronger at the edges as they turn away (_Base +
// _Rim × (1 − n·v)^_Edge; its 0.1 + 1.2 × (1 − n·v)²). _Mark is the mark now (0 none, 1 all), set from outside.
Shader "Bastion/Mark"
{
    Properties
    {
        _Mark ("Mark", Range(0, 1)) = 1
        _Tint ("Taken towards red", Range(0, 1)) = 0
        _Base ("Glow everywhere", Range(0, 1)) = 0
        _Rim ("Glow at the edges", Range(0, 3)) = 0
        _Edge ("How close to the edge", Range(1, 8)) = 2
        _Line ("Line round it (pixels)", Range(0, 6)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _Mark;
            half _Tint;
            half _Base;
            half _Rim;
            half _Edge;
            half _Line;
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
                return half4(lerp(half3(1, 1, 1), half3(1, 0.32, 0.26), _Tint * _Mark), 1);
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
                return half4(half3(1, 0.09, 0.05) * _Mark * (_Base + _Rim * pow(edge, _Edge)), 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Line"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex LineVertex
            #pragma fragment Fragment

            Varyings LineVertex(Attributes input)
            {
                Varyings output = Vertex(input);
                // The back faces out along their normal as the screen sees it, _Line pixels whatever the distance.
                float2 out2 = mul((float3x3)UNITY_MATRIX_VP, output.normal).xy;
                float len = length(out2);
                if (len > 1e-5) output.position.xy += out2 / len * _Line * 2 / _ScreenParams.xy * output.position.w;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                clip(_Line - 0.01);
                return half4(1, 0.16, 0.08, _Mark);
            }
            ENDHLSL
        }
    }
}
