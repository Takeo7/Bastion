// The special dead's atlas with its colours dulled (SoldierPalette.DullAtlas; Ana's #17 of 28/10 and #20 of 29/10): its
// skin and its clothes are one texture, so as a common dead's skin (SoldierPalette.DullFlesh) all of it, green to purple
// (hue 55°–335°, whole from 70° to 320°) to a third of its saturation, never under 0.15, and 90 % of its light; reds,
// oranges, yellows and browns as they are. In the texture's own (sRGB) values, as the palettes are. Drawn once an atlas
// into a texture of its own; in Resources, so the app keeps it.
Shader "Hidden/Bastion/DullAtlas"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float3 Hsv(float3 c)
            {
                float4 k = float4(0, -1.0 / 3, 2.0 / 3, -1);
                float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6 * d + 1e-10)), d / (q.x + 1e-10), q.x);
            }

            float3 Rgb(float3 c)
            {
                float3 p = abs(frac(c.xxx + float3(1, 2.0 / 3, 1.0 / 3)) * 6 - 3);
                return c.z * lerp(float3(1, 1, 1), saturate(p - 1), c.y);
            }

            float4 Fragment(v2f_img i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                float3 hsv = Hsv(saturate(LinearToGammaSpace(t.rgb)));
                float deg = hsv.x * 360;
                float w = saturate(min((deg - 55) / 15, (335 - deg) / 15));
                if (w > 0 && hsv.y > 0.15)
                {
                    hsv.y = max(0.15, hsv.y * (1 - 0.67 * w));
                    hsv.z *= 1 - 0.1 * w;
                }
                return float4(GammaToLinearSpace(Rgb(hsv)), t.a);
            }
            ENDCG
        }
    }
}
