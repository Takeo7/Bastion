// The buildings (0.10.1's buildings.ts materials), lit as URP's own: two kinds of surface.
//  - The kit pieces (WallsView): the pack's atlas, dyed as wallCutPatch did: along the top of Town's atlas the plaster
//    (left) takes the building's inner colour and the siding (right) its outer one, the trims between keep their white
//    (in the piece's own atlas, whichever pack). Both faces drawn; the back of a wall, seen through the cut, a flat dark.
//    The cutaway (BuildingCut.hlsl): on the storey in view the walls facing the camera drop to CUT_LOW, their caps (a
//    flat section of the wall) with them; above it everything fades, dithered.
//  - Floors, roofs and stairs (_SWATCH; BuildingSurfaces): a patch of Town's atlas (TEXCOORD1, glTF's uv: v down)
//    repeated over the world's plane (TEXCOORD0, in periods), times the colour it is given (swatchPatch), with the
//    derivatives of the continuous plane so there is no seam where it wraps. What fades (roofs, storeys, decks) fades
//    with its building's cut (roofFadePatch).
//  - The windows' panes (_GLASS: transparent, the material's colour and glow), cut as their walls.
// The colours per vertex are linear (three's), and can be over 1: multipliers of the patch's own. The depth passes cut
// as the colour does (the fog and the SSAO read them); the shadows are the whole building's, as in 0.10.1.
Shader "Bastion/Building"
{
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        [Toggle(_SWATCH)] _Swatch ("Swatches (floors, roofs)", Float) = 0
        [Toggle(_GLASS)] _Glass ("Glass (the windows' panes)", Float) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }

    HLSLINCLUDE
    #include "BuildingInput.hlsl"
    #include "BuildingCut.hlsl"

    struct BuildingAttributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        // Pieces: the atlas's uv. Swatches: the place on the world's plane, in periods of the patch.
        float2 uv : TEXCOORD0;
        // Swatches: the patch (x, y, width, height in glTF's uv).
        float4 swatch : TEXCOORD1;
        // Pieces: the outer colour; swatches: the colour.
        float4 outer : TEXCOORD2;
        // Pieces: the inner colour. Swatches: the building (+1) it fades with, its storey, whether it fades, weathered.
        float4 inner : TEXCOORD3;
        // Pieces: the building (+1), the way out (x, z in 0.10.1's frame; ON_ROOF), the floor (m).
        float4 cut : TEXCOORD4;
        // Pieces: the storey, a cap (1); a pane lit by night (1).
        float4 more : TEXCOORD5;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct BuildingVaryings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 swatch : TEXCOORD1;
        float3 outer : TEXCOORD2;
        float4 inner : TEXCOORD3;
        float3 positionWS : TEXCOORD4;
        float3 normalWS : TEXCOORD5;
        // How high it is drawn up to (m), how far it has faded, whether it is a cap, a pane lit by night.
        float4 cut : TEXCOORD6;
        half fogFactor : TEXCOORD7;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    BuildingVaryings Vertex(BuildingAttributes input)
    {
        BuildingVaryings output = (BuildingVaryings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float top = 1e4, fade = 0;
        #if defined(_SWATCH)
            if (input.inner.z > 0.5) fade = CutAbove(input.inner.x, input.inner.y);
        #else
            PieceCut(input.cut, input.more.x, input.more.y, positionWS, top, fade);
        #endif
        output.positionWS = positionWS;
        output.positionCS = TransformWorldToHClip(positionWS);
        // Gone: out of the view altogether, so it costs nothing.
        if (fade > 0.995) output.positionCS = float4(2, 2, 2, 1);
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.uv = input.uv;
        output.swatch = input.swatch;
        #if !defined(_SWATCH)
            // (A piece has no patch: its way out instead, for the clinics' walls, MedicalFinish.)
            output.swatch = float4(input.cut.y, input.cut.z, 0, 0);
        #endif
        output.outer = input.outer.rgb;
        output.inner = input.inner;
        output.cut = float4(top, fade, input.more.y, input.more.z);
        output.fogFactor = ComputeFogFactor(output.positionCS.z);
        return output;
    }

    float WeatherHash(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
    }

    float WeatherNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(WeatherHash(i), WeatherHash(i + float2(1, 0)), f.x), lerp(WeatherHash(i + float2(0, 1)), WeatherHash(i + float2(1, 1)), f.x), f.y);
    }

    // A roof's weathering (buildings.ts weatherPatch), on what faces up: grime in broad patches and streaks, a little moss
    // where it's worst; on 0.10.1's world plane, in tiles (z south).
    half3 Weather(half3 albedo, float3 positionWS, float3 normalWS)
    {
        if (normalWS.y <= 0.9) return albedo;
        float2 wp = float2(positionWS.x, -positionWS.z) / 1.5;
        float broad = WeatherNoise(wp * 0.3) * 0.6 + WeatherNoise(wp * 0.9 + 7.0) * 0.3 + WeatherNoise(wp * 3.1) * 0.1;
        albedo *= 1.0 - 0.24 * smoothstep(0.48, 0.8, broad);
        albedo *= 0.94 + 0.1 * WeatherNoise(wp * float2(1.2, 6.0) + 3.0);
        float moss = smoothstep(0.74, 0.78, broad + WeatherNoise(wp * 2.4) * 0.12);
        return lerp(albedo, half3(0.2, 0.22, 0.1) * (0.8 + 0.4 * WeatherNoise(wp * 7.0)), moss * 0.7);
    }

    // A clinic's own surfaces (0.10.1's medicalFinish.ts): a map of its tiles (_Medical, global: r its wear, 30 + 210 ×
    // it of 255, g its storeys) scopes them to the clinics' insides, on every storey. Worked out on 0.10.1's world, in
    // tiles (x east, y up, z south). _MedicalSize: the map's width and height in tiles, and 1 if there is a clinic at all.
    TEXTURE2D(_Medical);
    SAMPLER(sampler_point_clamp);
    float4 _MedicalSize;

    float MedHash(float2 p)
    {
        return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
    }

    float MedNoise(float2 p)
    {
        float2 i = floor(p), f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(MedHash(i), MedHash(i + float2(1, 0)), f.x), lerp(MedHash(i + float2(0, 1)), MedHash(i + 1.0), f.x), f.y);
    }

    // Whether `p` (0.10.1's world) is in a clinic, under its top storey, and how worn it is.
    bool Medical(float3 p, out float wear)
    {
        wear = 0;
        if (_MedicalSize.z < 0.5) return false;
        float2 uv = p.xz / _MedicalSize.xy;
        if (any(uv < 0) || any(uv >= 1)) return false;
        float4 medical = SAMPLE_TEXTURE2D_LOD(_Medical, sampler_point_clamp, uv, 0);
        if (medical.r <= 0.01 || p.y >= medical.g * 255.0 * 2.3 - 0.08) return false;
        wear = saturate((medical.r * 255.0 - 30.0) / 210.0);
        return true;
    }

    // medicalFinish.ts FLOOR: pale tiles about 65 cm square, dust, tracked-in grime, and with wear cracks and chips.
    half3 MedicalFloor(float3 p, float wear)
    {
        float2 uv = p.xz / 0.44;
        float2 cell = floor(uv), f = frac(uv);
        float r = MedHash(cell);
        float2 edge = min(f, 1.0 - f);
        float aa = max(fwidth(uv.x), fwidth(uv.y));
        float grout = 1.0 - smoothstep(0.012, 0.023 + aa * 0.6, min(edge.x, edge.y));
        float3 tile = lerp(float3(0.64, 0.65, 0.60), float3(0.79, 0.78, 0.70), r);
        float dust = MedNoise(p.xz * 0.75 + 21.0) * MedNoise(uv * 1.8);
        tile *= 1.0 - dust * (0.10 + wear * 0.65);
        float grime = smoothstep(0.43, 0.82, MedNoise(p.xz * 1.15 + p.y * 3.1 + 63.0));
        grime *= 0.60 + MedNoise(p.xz * 9.0) * 0.40;
        tile = lerp(tile, float3(0.31, 0.30, 0.22), grime * (0.28 + wear * 0.30));
        float damaged = step(r, wear * wear * 0.38);
        float crack = abs(f.y - (0.18 + f.x * 0.48 + MedNoise(float2(f.x * 12.0, r * 71.0)) * 0.13));
        tile = lerp(tile, float3(0.17, 0.15, 0.13), damaged * (1.0 - smoothstep(0.005, 0.013 + aa * 0.5, crack)));
        float chip = step(min(edge.x, edge.y) + MedNoise(f * 16.0 + cell) * 0.15, wear * 0.13) * damaged;
        tile = lerp(tile, float3(0.27, 0.24, 0.19), chip);
        return lerp(tile, float3(0.23, 0.23, 0.21), grout * 0.78);
    }

    // medicalFinish.ts WALL: a dark dado, a maroon band over it, pale paint above, a skirting; damp, streaks, and with
    // wear chips of brick showing through.
    half3 MedicalWall(float3 p, float3 n, float wear)
    {
        float h = fmod(p.y, 2.3);
        float2 along = abs(n.x) > 0.5 ? float2(p.z, p.y) : p.xy;
        float3 paint = h < 0.80 ? float3(0.36, 0.34, 0.31) : float3(0.76, 0.75, 0.67);
        if (h > 0.77 && h < 0.97) paint = float3(0.25, 0.065, 0.055);
        if (h < 0.095) paint = float3(0.16, 0.17, 0.16);
        float dirt = MedNoise(along * float2(1.9, 3.2) + 8.0);
        paint *= 0.93 + dirt * 0.07 - wear * MedNoise(along * 7.0) * 0.17;
        float damp = (1.0 - smoothstep(0.05, 0.48, h)) * MedNoise(along * float2(5.0, 1.7) + 47.0);
        paint = lerp(paint, float3(0.25, 0.26, 0.19), damp * (0.35 + wear * 0.25));
        float streaks = smoothstep(0.70, 0.92, MedNoise(float2(along.x * 21.0, 17.0))) * (1.0 - smoothstep(0.1, 1.15, h));
        paint *= 1.0 - streaks * (0.10 + wear * 0.12);
        float wornPatch = MedNoise(along * 1.4 + 31.0) + MedNoise(along * 9.0) * 0.18;
        float missing = step(1.14 - wear * 0.055, wornPatch) * step(0.2, wear);
        float2 brickUv = along / float2(0.29, 0.14);
        brickUv.x += fmod(floor(brickUv.y), 2.0) * 0.5;
        float2 joint = min(frac(brickUv), 1.0 - frac(brickUv));
        float3 brick = lerp(float3(0.28, 0.13, 0.08), float3(0.43, 0.23, 0.13), MedHash(floor(brickUv)));
        brick = lerp(brick, float3(0.39, 0.36, 0.30), 1.0 - smoothstep(0.025, 0.055, min(joint.x, joint.y)));
        float rim = step(1.115 - wear * 0.055, wornPatch) * step(0.2, wear);
        paint = lerp(paint, float3(0.48, 0.44, 0.35), rim);
        return lerp(paint, brick, missing);
    }

    void Clip(BuildingVaryings input)
    {
        if (input.positionWS.y > input.cut.x || CutDither(input.positionCS.xy) < input.cut.y) discard;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature_local _SWATCH
            #pragma shader_feature_local_fragment _GLASS
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // 1 by night (Mood): the lit windows glow.
            float _Night;
            // The baked contact occlusion over the map (UI's GroundOcclusion, global): r how much sky, g the height it was
            // worked out for (m); where it lies (x and z of its corner, its size, m).
            TEXTURE2D(_GroundAO);
            SAMPLER(sampler_linear_clamp);
            float4 _GroundAOArea;
            // 1 leaves it out (a lab's: MapLab BASTION_SIN_AO, to tell what the occlusion draws).
            float _GroundAOOff;

            half4 Fragment(BuildingVaryings input, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Clip(input);
                half3 albedo;
                #if defined(_SWATCH)
                    // The patch's place, in glTF's uv (v down) and then Unity's (v up).
                    float2 scale = input.swatch.zw;
                    float2 at = input.swatch.xy + frac(input.uv) * scale;
                    float2 dx = ddx(input.uv) * scale;
                    float2 dy = ddy(input.uv) * scale;
                    albedo = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, float2(at.x, 1.0 - at.y), float2(dx.x, -dx.y), float2(dy.x, -dy.y)).rgb * input.outer;
                    if (input.inner.w > 0.5) albedo = Weather(albedo, input.positionWS, normalize(input.normalWS));
                #elif defined(_GLASS)
                    albedo = half3(1, 1, 1);
                #else
                    albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                    // The top quarter of the atlas (glTF's v < 0.25).
                    if (input.uv.y > 0.75) albedo *= input.uv.x < 0.4167 ? input.inner.rgb : (input.uv.x > 0.506 ? input.outer : half3(1, 1, 1));
                    if (!front) albedo = half3(0.16, 0.15, 0.14);
                    // A wall's cap: the section of a solid wall, a plain grey a little of the building's inside colour.
                    if (input.cut.z > 0.5) albedo = half3(0.34, 0.32, 0.3) * lerp(half3(1, 1, 1), input.inner.rgb, 0.35);
                #endif
                albedo *= _BaseColor.rgb;
                #if !defined(_GLASS)
                {
                    // A clinic's floor (facing up, at a storey's floor) or its walls' inner faces (a wall in the building,
                    // or an outer one's face turned in), as 0.10.1's medicalFinish.ts patched the floor and piece materials.
                    float3 p = float3(input.positionWS.x, input.positionWS.y, -input.positionWS.z) / 1.5;
                    float3 n = normalize(input.normalWS);
                    n.z = -n.z;
                    float wear;
                    if (Medical(p, wear))
                    {
                        #if defined(_SWATCH)
                            if (n.y > 0.8 && fmod(p.y + 0.01, 2.3) < 0.06) albedo = MedicalFloor(p, wear);
                        #else
                            float2 out2 = input.swatch.xy;
                            if (front && abs(n.y) < 0.5 && input.cut.z < 0.5 && (dot(out2, out2) < 0.01 || dot(n.xz, out2) < -0.15)) albedo = MedicalWall(p, n, wear);
                        #endif
                    }
                }
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                float3 normal = normalize(input.normalWS);
                inputData.normalWS = front ? normal : -normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                #if defined(_SWATCH)
                    // A floor, a raised ground or a solid block's top (what doesn't fade with the cut: buildings.ts
                    // contactPatch) sits in the occlusion of what stands round it, as the ground does: a room darker by its
                    // walls and furniture, its open floor lighter (groundAO.ts: the sky light by all of it, the sun's by 85 %).
                    if (input.inner.z < 0.5 && inputData.normalWS.y > 0.7)
                    {
                        float4 cao = SAMPLE_TEXTURE2D(_GroundAO, sampler_linear_clamp, (input.positionWS.xz - _GroundAOArea.xy) / max(_GroundAOArea.zw, 1e-3));
                        float here = 1.0 - smoothstep(0.15 * 1.5, 0.6 * 1.5, abs(input.positionWS.y - cao.g));
                        float occlusion = lerp(1.0, cao.r, 0.95 * here * (1 - _GroundAOOff));
                        float direct = lerp(1.0, occlusion, 0.85);
                        surface.albedo *= direct;
                        surface.occlusion = occlusion / max(direct, 1e-3);
                    }
                #endif
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);
                #if defined(_GLASS)
                    // The windows' panes (buildings.ts glassMaterial): pale blue, see-through, a dark glow of their own.
                    surface.metallic = 0.3;
                    surface.alpha = _BaseColor.a;
                    surface.emission = _EmissionColor.rgb;
                    // By night some windows are lit, warm and nearly opaque (buildings.ts glassMaterial's aGlow).
                    float lit = input.cut.w * _Night;
                    surface.emission += half3(1.0, 0.6, 0.26) * 2.4 * lit;
                    surface.alpha = lerp(surface.alpha, 0.92, lit);
                #endif

                half4 colour = UniversalFragmentPBR(inputData, surface);
                colour.rgb = MixFog(colour.rgb, inputData.fogCoord);
                #if !defined(_GLASS)
                    colour.a = 1;
                #endif
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowFragment
            #pragma shader_feature_local _SWATCH
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            // As three casts them (0.10.1: a material's shadowSide, the side it doesn't draw): a surface of one side (a floor,
            // a storey's slab, a roof's top) only by its faces turned away from the light, so a flat roof casts none and
            // the sun gets into a building cut open, while a closed one (a roof's lip, a block) casts whole; the pieces,
            // drawn both ways there, by both.
            half4 ShadowFragment(Varyings input, bool front : SV_IsFrontFace) : SV_TARGET
            {
                #if defined(_SWATCH)
                    if (front) discard;
                #endif
                return ShadowPassFragment(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment DepthFragment
            #pragma shader_feature_local _SWATCH
            #pragma multi_compile_instancing

            half DepthFragment(BuildingVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Clip(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment NormalsFragment
            #pragma shader_feature_local _SWATCH
            #pragma multi_compile_instancing

            half4 NormalsFragment(BuildingVaryings input, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Clip(input);
                float3 normalWS = NormalizeNormalPerPixel(front ? input.normalWS : -input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
