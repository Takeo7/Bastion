// By night a figure's cool moonlit rim (synty.ts dyeRim, under unitNight): one more material over the body's meshes,
// added. (0.42, 0.62, 0.8) × (1 − n·v)² × 0.3, and a breath of its own colour, 0.07 of its colour map, both × _Rim (1 the
// squad, 0.22 the enemy and the dead: syntyFigures.ts ENEMY_RIM), set per renderer from outside (UnitViews.Rim). The
// colour map is the copy's, or the renderer's own block's (a soldier's palette: SidekickLab.OwnColours). (0.10.1's was
// (1 − n·v)³ × 0.5: as much light over a round figure, in a band twice as wide and a less white edge; the cube's band
// was thinner than a pixel at the game's distance, motes on a black figure, or white wire close. The review of 10/11, L-03.)
// Far off it goes over to 0.03 + (1 − n·v) × 0.06, the same light over a round figure (0.03 + 0.06 × 1/3 = 0.3 × 1/6)
// spread nearly evenly, from RimNear to RimFar metres from the camera. At the battle's camera (36 m, a figure some 40 px tall) the body's facets
// come to a pixel or two, and those edge on lit at the band's peak: the light-blue motes on a dark figure were all the rim
// (the 3D review's L-03, 10/11: none with it off, MapLab's BASTION_SONDA_NOCHE=sinborde). And its own colour there 0.12,
// not 0.07: from that camera the squad's bodies, unmottled, were nearly the dark ground's (29 against 25 of 255), only
// their heads read.
Shader "Bastion/NightRim"
{
    Properties
    {
        _ColorMap ("Colour map", 2D) = "white" {}
        _Rim ("Rim", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ColorMap);
            SAMPLER(sampler_ColorMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorMap_ST;
                half _Rim;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            // (Metres from the camera: the band as it was up to RimNear, the even light from RimFar.)
            #define RimNear 15.0
            #define RimFar 30.0

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.position.xyz);
                output.position = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normal);
                float3 v = normalize(GetWorldSpaceViewDir(input.world));
                half edge = 1 - saturate(dot(n, v));
                half far = saturate((distance(input.world, GetCameraPositionWS()) - RimNear) / (RimFar - RimNear));
                half band = lerp(edge * edge * 0.3, 0.03 + edge * 0.06, far);
                half3 own = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, input.uv).rgb;
                return half4((half3(0.42, 0.62, 0.8) * band + own * lerp(0.07, 0.12, far)) * _Rim, 0);
            }
            ENDHLSL
        }
    }
}
