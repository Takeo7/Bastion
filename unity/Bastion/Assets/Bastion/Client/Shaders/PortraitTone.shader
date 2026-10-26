// A studio picture's tone (Studio.Shoot: 0.10.1's offscreen renderers, three's ACESFilmicToneMapping, _Mode 0, or its
// NeutralToneMapping, _Mode 1, at _Exposure), from the lit picture (linear, HDR) to the one shown, its alpha kept (the
// subject on nothing). Graphics.Blit's: _MainTex.
Shader "Hidden/Bastion/PortraitTone"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "black" {}
        _Exposure ("Exposure", Float) = 1.1
        _Mode ("ACES 0, Neutral 1", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure;
            float _Mode;

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            // three's tonemapping_pars_fragment: the RRT and ODT fit, between its input and output matrices.
            float3 RRTAndODTFit(float3 v)
            {
                float3 a = v * (v + 0.0245786) - 0.000090537;
                float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
                return a / b;
            }

            float3 ACESFilmic(float3 color)
            {
                const float3x3 input = float3x3(0.59719, 0.35458, 0.04823, 0.07600, 0.90834, 0.01566, 0.02840, 0.13383, 0.83777);
                const float3x3 output = float3x3(1.60475, -0.53108, -0.07367, -0.10208, 1.10813, -0.00605, -0.00327, -0.07276, 1.07602);
                color *= _Exposure / 0.6;
                color = mul(input, color);
                color = RRTAndODTFit(color);
                color = mul(output, color);
                return saturate(color);
            }

            // three's NeutralToneMapping (Khronos PBR Neutral).
            float3 Neutral(float3 color)
            {
                const float start = 0.8 - 0.04;
                const float desaturation = 0.15;
                color *= _Exposure;
                float x = min(color.r, min(color.g, color.b));
                float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
                color -= offset;
                float peak = max(color.r, max(color.g, color.b));
                if (peak < start) return saturate(color);
                float d = 1.0 - start;
                float newPeak = 1.0 - d * d / (peak + d - start);
                color *= newPeak / peak;
                float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
                return saturate(lerp(color, newPeak, g));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 c = tex2D(_MainTex, input.uv);
                // What has no figure stays clear; at its edges (drawn over a clear black, so their colour is premultiplied),
                // the figure's own colour, its alpha straight (as UI shows a texture).
                float3 rgb = c.a > 0 ? c.rgb / max(c.a, 1e-4) : 0;
                return float4(_Mode > 0.5 ? Neutral(rgb) : ACESFilmic(rgb), c.a);
            }
            ENDHLSL
        }
    }
}
