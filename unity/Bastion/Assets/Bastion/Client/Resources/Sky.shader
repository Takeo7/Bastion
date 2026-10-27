// The sky round every scene (sky.ts; Sky, UI's): the dome shaded from the haze at the horizon to the zenith with the
// sun's glow (_DOME); a band on the horizon cut from a strip of texture, the mountains or a town's roofs, as the land's
// light has it, snowed on in winter and hazed towards its foot (_BAND); the clouds, lit from above and shaded below (_CLOUD).
// Its colours are globals Sky sets with each mood. Drawn after the opaque world and before the transparent, only where
// nothing else is (no depth written, the depth test kept), at a share of the camera's far plane. In Resources, so the
// app has it without Shader.Find.
Shader "Bastion/Sky"
{
    Properties
    {
        _MainTex ("Strip", 2D) = "white" {}
        _Rows ("Rows (v from, to)", Vector) = (0, 1, 0, 0)
        _Repeat ("Times round", Float) = 1
        _Air ("Haze over it", Float) = 0.3
        _Foot ("Hazed foot (share of its height)", Float) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-400" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _DOME _BAND _CLOUD
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Rows;
                float _Repeat;
                float _Air;
                float _Foot;
                float _Cull;
            CBUFFER_END

            float3 _SkyHorizon, _SkyMiddle, _SkyZenith, _SkySunDir, _SkySun, _SkyLand, _SkyCloudLit, _SkyCloudShade;
            float _SkyGlow, _SkySnow;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float2 light : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                output.uv = input.uv;
                // The clouds: how up their side faces, and towards the sun (sky.ts cloudMaterial).
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.light = float2(input.normalOS.y, dot(normalize(normalWS), _SkySunDir));
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 c;
                #if defined(_DOME)
                    float3 d = normalize(input.direction);
                    float e = d.y;
                    c = lerp(_SkyHorizon, _SkyMiddle, smoothstep(0.0, 0.22, e));
                    c = lerp(c, _SkyZenith, smoothstep(0.18, 0.75, e));
                    float s = max(dot(d, _SkySunDir), 0.0);
                    c += _SkySun * (pow(s, 900.0) * 2.0 + pow(s, 10.0) * 0.35 * _SkyGlow);
                #elif defined(_BAND)
                    float4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(input.uv.x * _Repeat, lerp(_Rows.x, _Rows.y, input.uv.y)));
                    clip(t.a - 0.5);
                    c = t.rgb;
                    // Snow: the slopes greyed and lightened to a winter's white, the rock's shading kept.
                    float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                    c = lerp(c, float3(0.78, 0.84, 0.92) * (0.55 + l * 1.1), _SkySnow);
                    c *= _SkyLand;
                    c = lerp(c, _SkyHorizon, saturate(_Air + (1.0 - smoothstep(0.0, _Foot, input.uv.y)) * (1.0 - _Air)));
                #else
                    float k = saturate(input.light.x * 0.5 + 0.5 + max(input.light.y, 0.0) * 0.25);
                    c = lerp(_SkyCloudShade, _SkyCloudLit, k);
                #endif
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
