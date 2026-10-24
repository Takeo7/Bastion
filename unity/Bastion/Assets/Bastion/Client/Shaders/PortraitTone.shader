// A portrait's tone (0.10.1's portraits.ts renderer: three's ACESFilmicToneMapping at an exposure of 1.1), from the
// lit picture (linear, HDR) to the one shown, its alpha kept (the bust on nothing). Graphics.Blit's: _MainTex.
Shader "Hidden/Bastion/PortraitTone"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "black" {}
        _Exposure ("Exposure", Float) = 1.1
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

            float4 Frag(Varyings input) : SV_Target
            {
                float4 c = tex2D(_MainTex, input.uv);
                // What has no figure stays clear; at its edges (drawn over a clear black, so their colour is premultiplied),
                // the figure's own colour, its alpha straight (as UI shows a texture).
                float3 rgb = c.a > 0 ? c.rgb / max(c.a, 1e-4) : 0;
                return float4(ACESFilmic(rgb), c.a);
            }
            ENDHLSL
        }
    }
}
