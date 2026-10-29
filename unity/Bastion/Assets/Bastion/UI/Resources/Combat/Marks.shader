// The marks on the map (MoveOverlay): unlit vertex colours, given as sRGB like the web's hex colours, over a
// texture (white by default). A line's vertices carry, in TEXCOORD1, the line's direction (xyz) and its width in
// pixels signed by the side (w): it is widened on screen, the same width near and far, as three.js's Line2 did.
// A line's segments have no caps: a pixel or two wide, their joints don't show.
// What it is drawn over (depth test), how it blends (a shade blends by the minimum: tileLooks.ts `shade`) and the
// render queue are the material's.
Shader "Bastion/Marks"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend op", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Marks"
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            BlendOp [_BlendOp]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 wide : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // The HUD's pixel scale (GameSettings.PixelScale: with the screen, never past its density); 0, unset.
            float _BastionPixelScale;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
            CBUFFER_END

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float4 clip = TransformWorldToHClip(world);
                if (v.wide.w != 0)
                {
                    // Across the line on screen, half its width each way: w pixels are 2w / size in clip space, over two. The
                    // pixels are the HUD's (HudPanel: 1440×900, matched half by width and half by height), so on a screen
                    // twice the size, a Retina one, the line is twice as wide, as the web's CSS pixels were.
                    float4 ahead = TransformWorldToHClip(world + TransformObjectToWorldDir(v.wide.xyz, false));
                    float2 size = _ScreenParams.xy;
                    float px = v.wide.w * (_BastionPixelScale > 0 ? _BastionPixelScale : sqrt(size.x / 1440.0 * size.y / 900.0));
                    float2 along = ahead.xy / ahead.w * size - clip.xy / clip.w * size;
                    float2 dir = dot(along, along) > 1e-8 ? normalize(along) : float2(1, 0);
                    clip.xy += float2(-dir.y, dir.x) * (px / size) * clip.w;
                }
                o.positionCS = clip;
                o.color = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                o.color.rgb = SRGBToLinear(o.color.rgb);
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color * _Color;
            }
            ENDHLSL
        }
    }
}
