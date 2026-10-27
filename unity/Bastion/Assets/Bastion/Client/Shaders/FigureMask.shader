// Where a figure is drawn, a bit of the stencil (64) for the fog to leave it be: 0.10.1's fog of war and its lit tiles'
// glow are its map's materials' (fogOfWar.ts patch), not the units'. One more material over a figure's meshes, drawing
// nothing, after the opaque (its depth there to test against); UnitViews.Masks keeps it on them.
Shader "Bastion/FigureMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest+49" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Cull Back
            Stencil
            {
                Ref 64
                WriteMask 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 Vertex(float4 position : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(position.xyz);
            }

            half4 Fragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
