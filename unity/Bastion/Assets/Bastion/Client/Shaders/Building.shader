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
        // Pieces: the storey, a cap (1).
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
        // How high it is drawn up to (m), how far it has faded, whether it is a cap.
        float3 cut : TEXCOORD6;
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
        output.outer = input.outer.rgb;
        output.inner = input.inner;
        output.cut = float3(top, fade, input.more.y);
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
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);
                #if defined(_GLASS)
                    // The windows' panes (buildings.ts glassMaterial): pale blue, see-through, a dark glow of their own.
                    surface.metallic = 0.3;
                    surface.alpha = _BaseColor.a;
                    surface.emission = _EmissionColor.rgb;
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
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
