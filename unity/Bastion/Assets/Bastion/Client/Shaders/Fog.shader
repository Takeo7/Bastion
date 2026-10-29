// The fog of war (Bastion.Client.Map.Fog; 0.10.1's fogOfWar.ts), over the picture after it is lit: each pixel's place
// from the depth (and its facing from the normals), and its tile's zone from Fog's texture (red: in sight, green: seen,
// alpha: its floor), blended between tiles; a building's wall as the street it looks onto, its roof (and a wall over a
// lower roof beside it) as the street along it (the shells' texture: red its storeys, green the street along it in sight, blue seen). In sight, as it is; seen but out of sight, greyer and darker under a haze, with a low mist drifting over it;
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
            // (Not over the figures: FigureMask's bit.)
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
            // By night: lit tiles (the map's blue) glow warm (Fog: _FogLamp); the light a surface gets, to reckon its colour by (Mood).
            float _FogNight;
            float4 _FogLamp;
            float4 _LightSky;
            float4 _LightGround;
            float4 _LightSun;
            float4 _LightSunFrom;
            // The night's real lights (NightLights: where, 1/range² in w; their linear colour times intensity), counted in the
            // light a surface gets: else what a Bengala lights reads as white and its pool drowns the red in orange.
            float4 _NightLightAt[6];
            float4 _NightLightColour[6];

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
                    // Above the horizon it is the sky (Sky, UI's: drawn where nothing else is), out of the fog as 0.10.1's was.
                    if (ray.y >= -1e-3) return colour;
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
                // (And a wall where it rises over the roof of a lower one beside it, as the roof: else it took its own tile's,
                // and that is the inside's, unknown till a soldier goes in.)
                float beside = SAMPLE_TEXTURE2D(_FogShell, sampler_FogShell, front / _FogSize.xy).r * 255.0 / 32.0;
                float roof = built * max(step(storeys * 2.3 - 0.15, high), step(0.5, beside) * step(beside * 2.3 - 0.15, high));
                float face = (built - roof) * step(beside, 0.01);
                float4 ahead = SAMPLE_TEXTURE2D(_FogMap, sampler_FogMap, front / _FogSize.xy);
                seen = max(seen, max(smoothstep(0.2, 0.5, ahead.r + (mist - 0.5) * 0.12) * face, smoothstep(0.3, 0.7, home.g) * roof));
                known = max(known, max(smoothstep(0.12, 0.58, ahead.g + (mist - 0.5) * 0.4) * face, smoothstep(0.22, 0.78, home.b) * roof));
                // Nothing drawn there: nothing to be seen, whatever the edge's tile is.
                if (none)
                {
                    seen = 0;
                    known = 0;
                }
                // Night: a lit tile glows warm, so players read where they'd be seen (fogOfWar.ts: the lamp's light over the
                // surface's own colour, less where it is bright already, on what stands at its floor). How light a
                // material is: the picture over the light it gets (the sky's hemisphere, the moon and the real lights).
                if (_FogNight > 0.5 && !none)
                {
                    float floorAt = tile.a * 8.0 * 2.3;
                    float near = 1.0 - smoothstep(2.3 * 0.6, 2.3 * 1.2, high - floorAt);
                    // (The moon's shadow in it: in a wall's shade the picture is darker, not the surface, and its pool as bright.)
                    float moon = none ? 1.0 : MainLightRealtimeShadow(TransformWorldToShadowCoord(world));
                    float3 light = lerp(_LightGround.rgb, _LightSky.rgb, 0.5 * normal.y + 0.5) + _LightSun.rgb * saturate(dot(normal, _LightSunFrom.xyz)) * moon;
                    // (URP's point light: 1/d², faded out smooth by its range.)
                    for (int k = 0; k < 6; k++)
                    {
                        float3 to = _NightLightAt[k].xyz - world;
                        float d2 = max(dot(to, to), 1e-4);
                        float fade = saturate(1.0 - Sq(d2 * _NightLightAt[k].w));
                        light += _NightLightColour[k].rgb * (fade * fade / d2) * saturate(dot(normal, to * rsqrt(d2)));
                    }
                    // As a grey: the night's teal haze is in the picture, and would tint the pool green.
                    float hot = dot(colour.rgb, float3(0.2126, 0.7152, 0.0722));
                    float own = saturate(hot / max(dot(light, float3(0.2126, 0.7152, 0.0722)), 0.02));
                    colour.rgb += own * _FogLamp.rgb * tile.b * inside * near / (1.0 + 2.5 * hot);
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
