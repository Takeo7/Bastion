// The ground as continuous terrain (0.10.1's ground.ts SYNTY_GROUND; UI's): the tiles say what the floor is (GroundMap,
// a texel a tile), the look is worked out in the world's plane with Synty's tiling textures: broad patches that run
// across tiles, borders between paving, lawns and streets that wander (kerbs and gutters in town, grass over a dirt
// track's edge), cracks and road paint. In tiles as the 0.10.1 had it (p: the world's x and −z over 1.5 m), so the
// patterns fall where they fell there. Lit as URP's own; its depth and normals for the fog and the occlusion.
Shader "Bastion/Ground"
{
    Properties
    {
        _GroundMap ("Ground map (a texel a tile)", 2D) = "black" {}
        _Asphalt ("Asphalt", 2D) = "grey" {}
        _GrassTex ("Grass", 2D) = "green" {}
        _DirtTex ("Dirt", 2D) = "grey" {}
        _SnowTex ("Snow", 2D) = "white" {}
        _GroundSize ("Map size (tiles)", Vector) = (64, 64, 0, 0)
        _Biome ("Biome", Float) = 0
        _Tarmac ("Tarmac road", Float) = 0
        _Snow ("Snow", Float) = 0
        _Water ("Canal (side, far bank)", Vector) = (-1, 5, 0, 0)
        _Pave ("Paving", Color) = (0.741, 0.71, 0.675, 1)
        _Kerb ("Kerb", Color) = (0.827, 0.796, 0.757, 1)
        _AsphaltTint ("Asphalt tint", Color) = (0.788, 0.722, 0.69, 1)
        _Moss ("Moss", Color) = (0.424, 0.431, 0.243, 1)
        _Grime ("Grime", Color) = (0.337, 0.282, 0.251, 1)
        _Dust ("Dust", Color) = (0.612, 0.541, 0.447, 1)
        _Paint ("Road paint", Color) = (0.902, 0.886, 0.831, 1)
        _Yellow ("Yellow paint", Color) = (0.847, 0.667, 0.227, 1)
        _GrassTint ("Grass", Color) = (0.824, 0.855, 0.722, 1)
        _GrassDry ("Dry grass", Color) = (0.941, 0.863, 0.635, 1)
        _DirtTint ("Dirt", Color) = (1, 1, 1, 1)
        _Concrete ("Concrete", Color) = (0.604, 0.604, 0.588, 1)
        _Deck ("Deck", Color) = (0.541, 0.549, 0.557, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.05
        [HideInInspector] _BaseMap ("Base", 2D) = "white" {}
        [HideInInspector] _BaseColor ("Base colour", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "GroundInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GroundMap);
            TEXTURE2D(_Asphalt);
            TEXTURE2D(_GrassTex);
            TEXTURE2D(_DirtTex);
            TEXTURE2D(_SnowTex);
            SAMPLER(sampler_linear_repeat);
            // The baked contact occlusion (GroundOcclusion, global): r how much sky, g the height it was worked out for;
            // where it lies (x and z of its corner, its size, metres).
            TEXTURE2D(_GroundAO);
            SAMPLER(sampler_linear_clamp);
            float4 _GroundAOArea;

            // A tile is 1.5 m; how much the asphalt is lifted (its average is a 27 % grey: a whole apron read as black).
            #define TILE 1.5
            #define ASPHALT_GAIN 2.6
            #define CROSSWALK 4.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // GLSL's mod (the sign of y), not HLSL's fmod: past the map's edge the coordinates go negative.
            float2 Mod(float2 x, float y) { return x - y * floor(x / y); }
            float Mod(float x, float y) { return x - y * floor(x / y); }

            float sHash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float sNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(sHash(i), sHash(i + float2(1.0, 0.0)), f.x), lerp(sHash(i + float2(0.0, 1.0)), sHash(i + float2(1.0, 1.0)), f.x), f.y);
            }

            float sFbm(float2 p)
            {
                return sNoise(p) * 0.55 + sNoise(p * 2.07 + 11.3) * 0.3 + sNoise(p * 4.13 + 5.7) * 0.15;
            }

            float4 sTile(int2 t)
            {
                return LOAD_TEXTURE2D(_GroundMap, clamp(t, int2(0, 0), int2(_GroundSize.xy) - 1));
            }

            // 1 if tile t is of kind k on this level (k −1: open ground marked as a lawn).
            float sIs(int2 t, float k, float level)
            {
                float4 g = sTile(t);
                float kind = floor(g.r * 255.0 + 0.5);
                if (abs(g.g - level) > 0.002) return 0.0;
                if (k < -0.5) return kind < 0.5 && floor(g.b * 255.0 + 0.5) > 2.5 ? 1.0 : 0.0;
                return abs(kind - k) < 0.5 ? 1.0 : 0.0;
            }

            // How much point p is in tiles of kind k: 1 in their middle, blended between tile middles.
            float sMask(float2 p, float k, float level)
            {
                float2 q = p - 0.5;
                int2 i = int2(floor(q));
                float2 f = frac(q);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(sIs(i, k, level), sIs(i + int2(1, 0), k, level), f.x), lerp(sIs(i + int2(0, 1), k, level), sIs(i + int2(1, 1), k, level), f.x), f.y);
            }

            // Distance from p to the nearest tile of kind k round tile t (2 if none).
            float sNear(float2 p, int2 t, float k, float level)
            {
                float d = 2.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int2 n = t + int2(x, y);
                        if (sIs(n, k, level) < 0.5) continue;
                        d = min(d, length(max(abs(p - float2(n) - 0.5) - 0.5, 0.0)));
                    }
                }
                return d;
            }

            // Distance to the nearest border of a jittered cell pattern: thin lines are cracks.
            float sCrack(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float d1 = 9.0;
                float d2 = 9.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y);
                        float2 c = o + float2(sHash(i + o), sHash(i + o + 17.31)) - f;
                        float d = dot(c, c);
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }
                }
                return sqrt(d2) - sqrt(d1);
            }

            // Road paint along a street's middle (the tile says its axis and where across it the line is).
            float sLine(float2 p, int2 t, float4 own)
            {
                float axis = floor(own.b * 255.0 + 0.5);
                if (axis < 0.5 || axis > 2.5) return 0.0;
                float2 local = p - float2(t);
                float across = axis < 1.5 ? local.y : local.x;
                float along = axis < 1.5 ? p.x : p.y;
                return step(abs(across - own.a), 0.045) * step(frac(along / 1.75), 0.5) * smoothstep(0.2, 0.4, sNoise(p * 2.0 + 5.0));
            }

            // The ground of each biome at p (ground.ts SYNTY_GROUND_FRAGMENT): in town, paving with its slabs and a kerb
            // along the streets, lawns away from them, asphalt with a centre line, a gutter, patches and cracks; in the
            // wilds, grass in lush and dry patches, a dirt track whose edges wander, ploughed fields; in facilities,
            // concrete with joints and stains, asphalt yards with yellow lines. Nothing lines up with the tiles.
            half3 GroundColour(float2 p)
            {
                int2 t = int2(floor(p));
                float4 own = sTile(t);
                float k = floor(own.r * 255.0 + 0.5);
                // A canal's water (the ground beyond the map as the edge's tiles): no ground down to it, up to its far bank.
                if (_Water.x > -0.5 && abs(k - 7.0) < 0.5)
                {
                    float past = _Water.x < 0.5 ? -p.y : _Water.x < 1.5 ? p.x - _GroundSize.x : _Water.x < 2.5 ? p.y - _GroundSize.y : -p.x;
                    if (past < _Water.y) discard;
                }
                float level = own.g;
                float big = sFbm(p * 0.07);
                float mid = sFbm(p * 0.33 + 3.1);
                float2 warp = (float2(sNoise(p * 0.8), sNoise(p * 0.8 + 17.0)) - 0.5) * 0.7;
                // Synty's road texture is nearly black (Town lights it hard): lifted to a dark grey that overlays read on.
                half3 asphalt = SAMPLE_TEXTURE2D(_Asphalt, sampler_linear_repeat, p / 2.2).rgb * _AsphaltTint.rgb * ASPHALT_GAIN * (0.9 + 0.18 * big) * (0.97 + 0.06 * mid);
                half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_linear_repeat, p / 3.2).rgb * lerp(_GrassTint.rgb, _GrassDry.rgb, smoothstep(0.42, 0.72, big)) * (0.94 + 0.12 * mid);
                half3 dirt = SAMPLE_TEXTURE2D(_DirtTex, sampler_linear_repeat, p / 2.6).rgb * _DirtTint.rgb * (0.92 + 0.14 * big);
                half3 c;
                if (k > 2.5 && k < 3.5)
                {
                    // A deck or platform up a storey.
                    c = _Deck.rgb * (0.94 + 0.08 * mid);
                }
                else if (_Biome < 0.5)
                {
                    if (k < 0.5)
                    {
                        // Paving (slabs that never line up with the tiles), lawns away from the streets, a kerb along them.
                        // The slabs are oblong and laid in a running bond, their joints thin, smoothed and worn in patches.
                        float2 sp = p / float2(0.86, 0.58) + float2(0.31, 0.17);
                        sp.x += 0.5 * Mod(floor(sp.y), 2.0);
                        float2 sf = frac(sp);
                        float2 edge = min(sf, 1.0 - sf);
                        float2 seam = 1.0 - smoothstep(float2(0.025, 0.025), float2(0.025, 0.025) + 1.5 * fwidth(sp), edge);
                        float joint = max(seam.x, seam.y) * (0.3 + 0.7 * smoothstep(0.3, 0.7, sNoise(p * 0.9 + 7.0)));
                        half3 pave = _Pave.rgb * (0.965 + 0.05 * sHash(floor(sp))) * (1.0 - 0.1 * joint);
                        pave *= 1.0 - 0.14 * smoothstep(0.55, 0.8, big);
                        float lawn = smoothstep(0.42, 0.6, sMask(p + warp * 0.6, -1.0, level) + (mid - 0.5) * 0.25);
                        c = lerp(pave, grass, lawn);
                        float ds = sNear(p, t, 1.0, level);
                        // Grime in broad patches, and moss in clumps, more of it by the kerb.
                        c = lerp(c, c * _Grime.rgb * 2.2, smoothstep(0.55, 0.82, sFbm(p * 0.4 + 17.0)) * 0.35 * (1.0 - lawn));
                        // Dust blown over it in drifts, and the slabs cracked where the paving is oldest.
                        c = lerp(c, _Dust.rgb * (0.9 + 0.2 * sNoise(p * 3.0)), smoothstep(0.6, 0.78, sFbm(p * 0.3 + 71.0)) * 0.45 * (1.0 - lawn));
                        float split = 1.0 - smoothstep(0.02, 0.04, sCrack(p * 1.3 + 9.0));
                        c *= 1.0 - 0.22 * split * smoothstep(0.58, 0.72, sFbm(p * 0.25 + 13.0)) * (1.0 - lawn);
                        float mossy = smoothstep(0.66, 0.69, sFbm(p * 0.6 + 29.0) + (1.0 - smoothstep(0.0, 0.7, ds)) * 0.08);
                        c = lerp(c, _Moss.rgb * (0.8 + 0.4 * sNoise(p * 6.0)), mossy * 0.85 * (1.0 - lawn));
                        float kerb = 1.0 - smoothstep(0.105, 0.125, ds);
                        c = lerp(c, _Kerb.rgb * (0.82 + 0.18 * smoothstep(0.0, 0.03, ds)) * (0.96 + 0.06 * mid), kerb);
                    }
                    else if (k < 1.5 || k > 5.5)
                    {
                        // A street, or a yard's tarmac (no paint on it): repairs, a gutter by the kerb, the centre line, cracks.
                        c = asphalt;
                        c *= 1.0 - 0.09 * smoothstep(0.6, 0.62, sNoise(floor(p * 1.3) * 0.41 + 9.0) * 0.7 + sNoise(p * 0.5) * 0.3);
                        float dk = sNear(p, t, 0.0, level);
                        c *= lerp(0.8, 1.0, smoothstep(0.02, 0.22, dk));
                        // Oil and dirt, and weeds in the gutter.
                        c *= 1.0 - 0.16 * smoothstep(0.62, 0.8, sFbm(p * 0.7 + 41.0));
                        c = lerp(c, _Moss.rgb * (0.8 + 0.4 * sNoise(p * 6.0)), smoothstep(0.7, 0.74, sFbm(p * 0.9 + 5.0) + (1.0 - smoothstep(0.0, 0.25, dk)) * 0.2) * 0.8);
                        c = lerp(c, _Paint.rgb, sLine(p, t, own) * 0.9);
                        // Zebra crossing: bars along the street, across its width, worn in places.
                        float zebra = floor(own.b * 255.0 + 0.5);
                        if (k < 1.5 && zebra > CROSSWALK - 0.5)
                        {
                            float2 zl = p - float2(t);
                            float across = zebra < CROSSWALK + 0.5 ? p.y : p.x;
                            float inTile = zebra < CROSSWALK + 0.5 ? zl.x : zl.y;
                            float bar = step(0.5, frac(across * 2.2)) * step(0.12, inTile) * step(inTile, 0.88);
                            c = lerp(c, _Paint.rgb, bar * 0.88 * smoothstep(0.18, 0.4, sNoise(p * 2.6 + 9.0)));
                        }
                        // Cracks in the worn stretches (a good part of a town nobody mends), and dust blown into the road.
                        float crack = 1.0 - smoothstep(0.025, 0.045, sCrack(p * 0.9 + 3.0));
                        c *= 1.0 - 0.34 * crack * smoothstep(0.56, 0.7, sFbm(p * 0.22 + 61.0));
                        c = lerp(c, _Dust.rgb * 0.8 * (0.9 + 0.2 * sNoise(p * 3.0)), smoothstep(0.62, 0.8, sFbm(p * 0.35 + 83.0)) * 0.35);
                    }
                    else
                    {
                        c = _Pave.rgb;
                    }
                }
                else if (_Biome < 1.5)
                {
                    // Grass, a dirt track with wandering edges, ploughed fields.
                    c = grass;
                    // Tarmac: the yards (a forecourt, a car park, a sawmill's yard), and at the forest stop its road.
                    float m = sMask(p + warp * 0.2, 6.0, level) + _Tarmac * sMask(p + warp * 0.2, 1.0, level);
                    float road = smoothstep(0.42, 0.52, m);
                    if (_Tarmac < 0.5)
                    {
                        float track = smoothstep(0.36, 0.64, sMask(p + warp, 1.0, level) + (mid - 0.5) * 0.35);
                        c = lerp(c, dirt, track);
                    }
                    if (m > 0.001)
                    {
                        // On a gravel verge, worn, cracked and dusty; a centre line down the road where it runs straight.
                        c = lerp(c, dirt * 0.88, smoothstep(0.2, 0.38, m));
                        half3 tar = asphalt * (1.0 - 0.16 * smoothstep(0.62, 0.8, sFbm(p * 0.7 + 41.0)));
                        tar = lerp(tar, _Paint.rgb, sLine(p, t, own) * 0.85);
                        float crack = 1.0 - smoothstep(0.025, 0.045, sCrack(p * 0.9 + 3.0));
                        tar *= 1.0 - 0.32 * crack * smoothstep(0.54, 0.7, sFbm(p * 0.22 + 61.0));
                        tar = lerp(tar, _Dust.rgb * 0.8 * (0.9 + 0.2 * sNoise(p * 3.0)), smoothstep(0.6, 0.8, sFbm(p * 0.35 + 83.0)) * 0.4);
                        c = lerp(c, tar, road);
                    }
                    // Worn ground: bare patches here and there.
                    c = lerp(c, dirt * 0.95, smoothstep(0.7, 0.78, sFbm(p * 0.45 + 41.0)) * 0.8 * (1.0 - road));
                    float rows = 0.0;
                    float axis = 0.0;
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            int2 n = t + int2(x, y);
                            if (sIs(n, 5.0, level) < 0.5) continue;
                            rows += 1.0;
                            axis = floor(sTile(n).b * 255.0 + 0.5);
                        }
                    }
                    if (abs(k - 5.0) < 0.5 || rows > 1.5)
                    {
                        float across = axis < 1.5 ? p.y : p.x;
                        float ridge = smoothstep(0.2, 0.5, abs(frac(across * 2.5) - 0.5) * 2.0);
                        c = dirt * (0.72 + 0.3 * ridge) * (0.95 + 0.1 * mid);
                    }
                }
                else
                {
                    if (k < 0.5)
                    {
                        // Concrete slabs with joints off the grid, oil stains.
                        float2 sp = p / 2.4 + float2(0.37, 0.71);
                        float2 sf = frac(sp) * 2.4;
                        float joint = max(step(sf.x, 0.035), step(sf.y, 0.035));
                        c = _Concrete.rgb * (0.95 + 0.08 * sHash(floor(sp))) * (0.93 + 0.1 * big) * (1.0 - 0.2 * joint);
                        c *= 1.0 - 0.3 * smoothstep(0.72, 0.8, sFbm(p * 0.6 + 23.0));
                    }
                    else if (k < 1.5 || k > 5.5)
                    {
                        c = asphalt * 1.08;
                        float dk = sNear(p, t, 0.0, level);
                        // A yellow line along the yard's edge, and down its middle.
                        c = lerp(c, _Yellow.rgb, (1.0 - smoothstep(0.03, 0.045, abs(dk - 0.22))) * step(dk, 1.0) * smoothstep(0.2, 0.4, sNoise(p * 2.0 + 5.0)) * 0.85);
                        c = lerp(c, _Yellow.rgb, sLine(p, t, own) * 0.85);
                        float crack = 1.0 - smoothstep(0.025, 0.045, sCrack(p * 0.9 + 7.0));
                        c *= 1.0 - 0.28 * crack * smoothstep(0.66, 0.76, sFbm(p * 0.22 + 31.0));
                    }
                    else
                    {
                        c = _Concrete.rgb;
                    }
                }
                // Deep cover on open ground and roofs; slushy streets keep their darker routes and markings.
                if (_Snow > 0.5 && abs(k - 2.0) > 0.5)
                {
                    half3 snow = SAMPLE_TEXTURE2D(_SnowTex, sampler_linear_repeat, p / 3.0).rgb * half3(0.89, 0.95, 1.0);
                    float onRoad = sMask(p, 1.0, level) + sMask(p, 6.0, level);
                    float cover = lerp(0.97, 0.5 + 0.26 * smoothstep(0.28, 0.72, mid), saturate(onRoad));
                    c = lerp(c, snow * (0.94 + 0.08 * big), cover);
                }
                return c;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normal = normalize(input.normalWS);
                // In tiles, as the 0.10.1's world: x east, y south.
                float2 p = float2(input.positionWS.x, -input.positionWS.z) / TILE;
                half3 albedo = normal.y > 0.5 ? GroundColour(p) : _Pave.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // Contact occlusion (groundAO.ts contactPatch), as seen from the ground's own height (b: under a car's or a
                // bench's overhang too); the indirect light all of it, the direct most of it.
                float2 aoUv = (input.positionWS.xz - _GroundAOArea.xy) / max(_GroundAOArea.zw, 1e-3);
                half ground = SAMPLE_TEXTURE2D(_GroundAO, sampler_linear_clamp, aoUv).b;
                float onMap = step(0.0, aoUv.x) * step(aoUv.x, 1.0) * step(0.0, aoUv.y) * step(aoUv.y, 1.0);
                half occlusion = lerp(1.0, ground, 0.95 * onMap);
                half direct = lerp(1.0, occlusion, 0.85);
                albedo *= direct;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = _Smoothness;
                surface.occlusion = occlusion / max(direct, 1e-3);
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 colour = UniversalFragmentPBR(inputData, surface);
                colour.rgb = MixFog(colour.rgb, inputData.fogCoord);
                colour.a = 1;
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "GroundInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "GroundInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
