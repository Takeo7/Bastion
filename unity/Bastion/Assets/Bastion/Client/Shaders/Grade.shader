// The picture's last step (0.10.1's post.ts GradedOutputPass, after the bloom): the exposure and Khronos' PBR neutral
// tone mapping (three's NeutralToneMapping, which keeps the atlases' colours), then the grade on the display values as
// they would be in sRGB (saturation, a vibrance, contrast, split toning between the shade and the light, lifted blacks,
// the middle tones brightened, a soft vignette), back to linear for URP to write out. Off (the picture as it is) while
// no mood is set (Mood: _GradeOn 0), and on a camera that shows a picture as it was painted (_GradePlain 1 while it
// draws: UI's valley, as 0.10.1's geoscape MAP_MOOD). Graded or not, the 3D faded to black by _StageFade (the
// settlement's change into the Mapa and out of it: stage.css .stage-fade, a black layer under the panels, so on the
// display's values). A full-screen pass of the renderers, after URP's post (GradeMake); URP's own tone mapping is none
// and its grading HDR, so what comes in is still the lit picture.
Shader "Bastion/Grade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "Grade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _GradeOn;
            float _GradeExposure;
            // Saturation, contrast, vignette.
            float4 _GradeLook;
            float4 _GradeShadowTint;
            float4 _GradeHighlightTint;
            float4 _GradeLift;
            // The «vivid» level over it: saturation, vibrance, contrast, lift; and the gain.
            float4 _GradeVivid;
            float _GradeGain;
            float _StageFade;
            float _GradePlain;

            float3 Neutral(float3 color)
            {
                const float startCompression = 0.8 - 0.04;
                const float desaturation = 0.15;
                float x = min(color.r, min(color.g, color.b));
                float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
                color -= offset;
                float peak = max(color.r, max(color.g, color.b));
                if (peak < startCompression) return color;
                float d = 1.0 - startCompression;
                float newPeak = 1.0 - d * d / (peak + d - startCompression);
                color *= newPeak / peak;
                float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
                return lerp(color, newPeak.xxx, g);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // (On display values, as the CSS layer's opacity: linear, to the 2.2.)
                if (_GradeOn < 0.5 || _GradePlain > 0.5) return half4(source.rgb * pow(saturate(1.0 - _StageFade), 2.2), source.a);
                float3 c = LinearToSRGB(saturate(Neutral(source.rgb * _GradeExposure)));
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                // The more vivid colours, none in the darkest: the fog of war's and the night's shade stay as they are.
                float vivid = smoothstep(0.08, 0.24, luma);
                c = lerp(luma.xxx, c, _GradeLook.x * lerp(1.0, _GradeVivid.x, vivid));
                float chroma = max(max(c.r, c.g), c.b) - min(min(c.r, c.g), c.b);
                c = lerp(luma.xxx, c, 1.0 + _GradeVivid.y * vivid * 4.0 * chroma * (1.0 - chroma));
                c = (c - 0.5) * _GradeLook.y + 0.5;
                c += _GradeVivid.z * vivid * (c - 0.3) * (1.0 - c);
                c *= lerp(_GradeShadowTint.rgb, _GradeHighlightTint.rgb, smoothstep(0.12, 0.7, luma));
                float3 lifted = _GradeLift.rgb * lerp(1.0, _GradeVivid.w, vivid);
                c = lifted + c * (1.0 - lifted);
                c += _GradeGain * vivid * c * (1.0 - c);
                float2 d = uv - 0.5;
                c *= 1.0 - _GradeLook.z * dot(d, d) * 2.0;
                c *= saturate(1.0 - _StageFade);
                return half4(SRGBToLinear(saturate(c)), source.a);
            }
            ENDHLSL
        }
    }
}
