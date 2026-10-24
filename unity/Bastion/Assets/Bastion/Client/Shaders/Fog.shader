// The fog of war (Bastion.Client.Map.Fog; 0.10.1's fogOfWar.ts), over the picture after it is lit: each pixel's place
// from the depth (and its facing from the normals), and its tile's zone from Fog's texture (red: in sight, green: seen,
// alpha: its floor), blended between tiles; a building's wall as the street it looks onto, its roof as the street along
// it (the shells' texture: red its storeys, green the street along it in sight, blue seen). In sight, as it is; seen but out of sight, greyer and darker under a haze, with a low mist drifting over it;
// never seen, a dark mist where only the tallest shapes show. The same numbers as 0.10.1's, in its tiles. Off (the
// picture as it is) while no map has a fog (_FogSize.w 0). A full-screen pass of the renderers (FogFeatureMake).
Shader "Bastion/Fog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "Fog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_FogMap);
            SAMPLER(sampler_FogMap);
            TEXTURE2D(_FogShell);
            SAMPLER(sampler_FogShell);
            // The map's width and height (tiles), a tile (metres), on.
            float4 _FogSize;
            float4 _FogUnknown;
            float4 _FogHaze;
            float4 _FogMistTint;
            float _FogMist;
            float _FogTime;

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

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_FogSize.w < 0.5) return colour;
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                // Where nothing is drawn (past the map, the sky): the ground there, out of the map, so in the dark (0.10.1:
                // beyond the edge is the mist's).
                #if UNITY_REVERSED_Z
                bool none = depth <= 0.0;
                #else
                bool none = depth >= 1.0;
                #endif
                if (none)
                {
                    float3 eye = GetCameraPositionWS();
                    float3 ray = normalize(world - eye);
                    world = eye + ray * (ray.y < -1e-3 ? -eye.y / ray.y : 1e4);
                }
                // In 0.10.1's tiles: east along x, south along the map's rows; up in tiles too.
                float2 cell = float2(world.x, -world.z) / _FogSize.z;
                float high = world.y / _FogSize.z;
                // The way it faces, across: a wall's own tile a little into it, and the tile it looks onto (FACE_OUT).
                float3 normal = none ? float3(0, 1, 0) : SampleSceneNormals(uv);
                float2 facing = float2(normal.x, -normal.z);
                float2 own = cell - facing * 0.3;
                float2 front = cell + facing * 0.6;
                float4 tile = SAMPLE_TEXTURE2D(_FogMap, sampler_FogMap, own / _FogSize.xy);
                float2 outside = max(max(-cell, cell - _FogSize.xy), 0.0);
                float inside = 1.0 - smoothstep(0.0, 6.0, length(outside));
                float mist = Noise(cell * 0.44 + float2(_FogTime * 0.05, _FogTime * 0.03));
                float seen = smoothstep(0.2, 0.5, tile.r + (mist - 0.5) * 0.12) * inside;
                float known = smoothstep(0.12, 0.58, tile.g + (mist - 0.5) * 0.4) * inside;
                // A building: its roof (at its top or above) as the street along it; a wall that looks out of it as the tile it
                // looks onto.
                float4 home = SAMPLE_TEXTURE2D(_FogShell, sampler_FogShell, own / _FogSize.xy);
                float storeys = home.r * 255.0 / 32.0;
                float built = step(0.5, storeys) * (1.0 - step(0.001, length(outside)));
                float roof = built * step(storeys * 2.3 - 0.15, high);
                float face = (built - roof) * step(SAMPLE_TEXTURE2D(_FogShell, sampler_FogShell, front / _FogSize.xy).r, 0.01);
                float4 ahead = SAMPLE_TEXTURE2D(_FogMap, sampler_FogMap, front / _FogSize.xy);
                seen = max(seen, max(smoothstep(0.2, 0.5, ahead.r + (mist - 0.5) * 0.12) * face, smoothstep(0.3, 0.7, home.g) * roof));
                known = max(known, max(smoothstep(0.12, 0.58, ahead.g + (mist - 0.5) * 0.4) * face, smoothstep(0.22, 0.78, home.b) * roof));
                // Nothing drawn there: nothing to be seen, whatever the edge's tile is.
                if (none)
                {
                    seen = 0;
                    known = 0;
                }
                // Seen, out of sight: greyer, darker, under a haze that drifts with the mist (FOG_GREY, FOG_DARK, FOG_HAZE, FOG_DRIFT).
                half3 grey = dot(colour.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 fogged = lerp(colour.rgb, grey, 0.55) * 0.62 + _FogHaze.rgb * (0.1 + 0.12 * mist);
                half3 c = lerp(fogged, colour.rgb, seen);
                // Over it, the low mist: thickest at the ground, thinning by a soldier's head (MIST_HEIGHT), in wisps.
                float ground = tile.a * 8.0 * 2.3;
                float low = 1.0 - smoothstep(0.0, 2.2, high - ground);
                float wisp = Noise(cell * 0.18 + float2(_FogTime * 0.04, -_FogTime * 0.025)) * 0.6 + Noise(cell * 0.5 + float2(-_FogTime * 0.07, _FogTime * 0.05)) * 0.4;
                float veil = _FogMist * known * (1.0 - seen) * (0.3 + 0.7 * low) * (0.5 + wisp);
                c = lerp(c, _FogMistTint.rgb, clamp(veil, 0.0, 0.85));
                // Never seen: the dark mist, the tallest shapes faintly through it.
                half3 dark = _FogUnknown.rgb * (0.55 + 1.1 * mist) * (1.0 + (0.15 + 0.35 * (1.0 - inside)) * saturate(high / 5.0));
                c = lerp(dark, c, known);
                return half4(c, colour.a);
            }
            ENDHLSL
        }
    }
}
