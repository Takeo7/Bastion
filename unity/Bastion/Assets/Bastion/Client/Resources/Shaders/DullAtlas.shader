// The special dead's atlas with its colours dulled (SoldierPalette.DullAtlas; Ana's #17 of 28/10, #20 of 29/10 and #6 of
// 1/11): its skin and its clothes are one texture. City Zombies' (_Flesh 0), their skin a greyed beige already: green to
// purple (hue 55°–335°, whole from 70° to 320°) to a third of its saturation, never under 0.15, and 90 % of its light,
// and anything loud (saturation and light both over 0.45, whole from 0.6; or light, over 0.5, whole from 0.65, and
// some coloured, saturation over 0.3, whole from 0.45) to 60 % of its saturation and 90 % of its light. Boss Zombies' (_Flesh 1) the same but for their skin's swatch, pink, teal or olive by the atlas (the last of the
// strip's columns, the same in all of them: the Blobber's face and arms, some of a dress), as a common dead's skin
// (SoldierPalette.DullFlesh), green to pink (hue 55°–355°, whole from 70° to 345°) made grey, pale or sickly: their
// clothes keep their colours, dulled as City's. Horror Asylum's (_Living, its rect: 9/11), the living in their hospital
// clothes: so, and the skin's swatch made a dead man's whatever its hue (the pinks, browns and lips of the living too),
// a sallow grey (45°, at most 0.14 of saturation, half its own) a twentieth lighter. In the texture's own (sRGB) values,
// as the palettes are. Drawn once an atlas into a texture of its own; in Resources, so the app keeps it.
Shader "Hidden/Bastion/DullAtlas"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Flesh ("Boss Zombies' skin swatch", Float) = 0
        _Living ("A living one's skin swatch: u0, v0, u1, v1 (none: 0)", Vector) = (0, 0, 0, 0)
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
            float _Flesh;
            float4 _Living;

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
                // (The swatch: u 0.712–0.729, v 0.818–0.888 of PolygonZombieBoss_0X_Y, 2048 px.)
                float2 inside = step(float2(0.712, 0.818), i.uv) * step(i.uv, float2(0.729, 0.888));
                float2 living = step(_Living.xy, i.uv) * step(i.uv, _Living.zw);
                if (_Living.z > 0 && living.x * living.y > 0)
                    return float4(GammaToLinearSpace(Rgb(float3(45.0 / 360, min(hsv.y * 0.5, 0.14), min(1, hsv.z * 1.05)))), t.a);
                if (_Flesh > 0.5 && inside.x * inside.y > 0)
                {
                    // SoldierPalette.DullFlesh, the pinks too: the greens and olives sallow, the blues grey, the purples and pinks pale.
                    float k = saturate(min((deg - 55) / 15, (355 - deg) / 10));
                    float3 dead = deg < 170 ? float3(45.0 / 360, min(hsv.y, 0.14), min(1, hsv.z * 1.2))
                        : deg < 260 ? float3(35.0 / 360, 0.05, min(1, hsv.z * 1.2)) : float3(25.0 / 360, 0.07, min(1, hsv.z * 1.35));
                    return float4(GammaToLinearSpace(lerp(Rgb(hsv), Rgb(dead), k)), t.a);
                }
                float w = saturate(min((deg - 55) / 15, (335 - deg) / 15));
                if (w > 0 && hsv.y > 0.15)
                {
                    hsv.y = max(0.15, hsv.y * (1 - 0.67 * w));
                    hsv.z *= 1 - 0.1 * w;
                }
                float loud = max(saturate((min(hsv.y, hsv.z) - 0.45) / 0.15), saturate((hsv.y - 0.3) / 0.15) * saturate((hsv.z - 0.5) / 0.15));
                hsv.y *= 1 - 0.4 * loud;
                hsv.z *= 1 - 0.1 * loud;
                return float4(GammaToLinearSpace(Rgb(hsv)), t.a);
            }
            ENDCG
        }
    }
}
