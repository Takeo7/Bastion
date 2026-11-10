// Rain and snow over the picture (ClimaLab, Arturo 1/11: tries to choose from, not in the game), after the opaque things
// are drawn and before the transparent ones (the drops and flakes go over it): each pixel's place and facing from the
// depth and the normals, as the fog's. Wet: darker, the more the more it faces up, with a sheen of the sky; puddles in
// patches on the flat, low ground, the sky in them and rings where drops fall; snow on what faces up, in drifts, lit as
// the light a surface gets (Mood's), in the sun's shadows; a lightning's flash. Not over the figures (FigureMask's bit).
Shader "Bastion/Clima"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "Clima"
            Stencil
            {
                Ref 64
                ReadMask 64
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            // (The sun's shadows by their cascades, always: the pass may not get the keyword URP sets for the opaque things.)
            #define _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Where on the sun's shadow map a point is. With a single cascade («Bajo»'s) URP leaves the cascades' spheres as
            // they were (it sets them only for more) and the matrices after the first as no-ops, so the cascade found was
            // none, and the snow lit in the shadows too (5/11): then the first's, always.
            float4 ShadowCoordOf(float3 world)
            {
                if (all(_MainLightWorldToShadow[1][0].xyz == 0)) return float4(mul(_MainLightWorldToShadow[0], float4(world, 1.0)).xyz, 0.0);
                return TransformWorldToShadowCoord(world);
            }

            // How wet (0–1), how much of the flat ground is puddles (0–1), drops a second on each spot of a puddle, how
            // much snow (0–1: 1 all of what faces up), the clock (s), a lightning's flash (0–1).
            float _ClimaWet;
            float _ClimaPuddles;
            float _ClimaRipples;
            float _ClimaSnow;
            float _ClimaTime;
            float _ClimaFlash;
            // The sky a wet face and a puddle show, and the snow's colour (linear).
            float4 _ClimaSky;
            float4 _ClimaSnowTint;
            // Under a roof (no rain nor snow there, seen through the cutaway): each tile's roof height in tiles (0: open
            // sky), the map's width and height (tiles), a tile (m), on.
            TEXTURE2D(_ClimaRoofs);
            float4 _ClimaSize;
            // Where a puddle can lie (Weather.Paved: 1 paved or trodden, 0 grass and water), smooth across a tile.
            TEXTURE2D(_ClimaPaved);
            // The light a surface gets (Mood).
            float4 _LightSky;
            float4 _LightGround;
            float4 _LightSun;
            float4 _LightSunFrom;

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // Rings spreading where drops fall on a puddle at `p` (metres): two grids of cells about 0.45 m across, each
            // cell's drop at its own place and time, its ring widening and fading.
            float Rings(float2 p, float t)
            {
                float sum = 0;
                for (int layer = 0; layer < 2; layer++)
                {
                    float2 q = p * 2.2 + layer * 17.3;
                    float2 cell = floor(q);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            float2 c = cell + float2(dx, dy);
                            float h = Hash(c + layer * 7.1);
                            float phase = frac(t * _ClimaRipples * (0.8 + 0.4 * h) + h * 13.7);
                            float2 at = c + float2(Hash(c + 3.1), Hash(c + 5.7));
                            float d = length(q - at) - phase * 0.9;
                            sum += exp(-(d * 14.0) * (d * 14.0)) * (1.0 - phase);
                        }
                    }
                }
                return saturate(sum);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                #if UNITY_REVERSED_Z
                bool none = depth <= 0.0;
                #else
                bool none = depth >= 1.0;
                #endif
                // The sky (or nothing): only the flash.
                if (none) return half4(colour.rgb * (1.0 + _ClimaFlash * 1.5), colour.a);
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 normal = SampleSceneNormals(uv);
                float3 view = normalize(GetCameraPositionWS() - world);
                float up = smoothstep(0.25, 0.9, normal.y);
                float3 c = colour.rgb;
                // Indoors (on a tile under a roof, lower than it) it stays dry, only the flash comes in.
                float2 cell = float2(world.x, -world.z) / _ClimaSize.z;
                if (_ClimaSize.w > 0.5)
                {
                    float roof = SAMPLE_TEXTURE2D_LOD(_ClimaRoofs, sampler_PointClamp, cell / _ClimaSize.xy, 0).r;
                    if (roof > 0 && world.y / _ClimaSize.z < roof - 0.2) return half4(c * (1.0 + _ClimaFlash * 0.25), colour.a);
                }

                // Wet: darker, the more the more it faces up; a sheen of the sky, more the more grazing the look.
                c *= lerp(1.0, lerp(0.86, 0.6, up), _ClimaWet);
                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(normal, view)), 5.0);
                c += _ClimaSky.rgb * fresnel * _ClimaWet * up * 0.5;

                // Puddles on the ground (flat and low), in patches: the sky in them and rings where the drops fall.
                if (_ClimaPuddles > 0)
                {
                    float2 p = world.xz;
                    float patch = Noise(p * 0.32) * 0.65 + Noise(p * 0.9 + 7.3) * 0.35;
                    float flat = smoothstep(0.96, 0.995, normal.y) * (1.0 - smoothstep(0.25, 0.6, world.y));
                    float edge = 1.0 - _ClimaPuddles * 0.35;
                    // (Not on grass: the review of 10/11, L-12.)
                    float firm = _ClimaSize.w > 0.5 ? SAMPLE_TEXTURE2D_LOD(_ClimaPaved, sampler_LinearClamp, cell / _ClimaSize.xy, 0).r : 1.0;
                    float puddle = smoothstep(edge, edge + 0.04, patch) * flat * firm;
                    if (puddle > 0)
                    {
                        float3 r = reflect(-view, normal);
                        // (Dark water: what it shows is the sky, dimmed; the rings catch a little more of it.)
                        float3 sky = _ClimaSky.rgb * (0.3 + 0.15 * saturate(r.y));
                        float rings = _ClimaRipples > 0 ? Rings(p, _ClimaTime) : 0;
                        float3 water = lerp(c * 0.5, sky, 0.55) + sky * rings * 0.35;
                        c = lerp(c, water, puddle);
                    }
                }

                // Snow on what faces up, thinner here and there (no holes: soft holes read as blurred stains); lit as a
                // surface is (Mood's light), in the sun's shadows.
                if (_ClimaSnow > 0)
                {
                    float patchy = Noise(world.xz * 0.9) * 0.6 + Noise(world.xz * 3.1 + 3.1) * 0.4;
                    float cover = smoothstep(0.5, 0.85, normal.y) * saturate(_ClimaSnow * 1.3 - 0.25 + (patchy - 0.5) * 0.5);
                    float shadow = MainLightRealtimeShadow(ShadowCoordOf(world));
                    float3 light = lerp(_LightGround.rgb, _LightSky.rgb, 0.5 * normal.y + 0.5) + _LightSun.rgb * saturate(dot(normal, _LightSunFrom.xyz)) * shadow;
                    c = lerp(c, _ClimaSnowTint.rgb * light, cover);
                }

                // A lightning's flash: everything lit up from the sky for an instant, what faces up the most.
                c += _ClimaFlash * float3(0.75, 0.8, 1.0) * ((0.35 + 0.65 * up) * c + 0.08);
                return half4(c, colour.a);
            }
            ENDHLSL
        }
    }
}
