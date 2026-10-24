// The valley's painting and what has happened to it (geoscape.ts GROUND_FRAGMENT of the 0.10.1, the same to the
// constant): Atalaya's works burnt out (charred, a few embers, smoke drifting east), the Bastion's clearing in the pines
// (a meadow, its cross of tracks, the caravan), and the Marea over it all: where the activity has reached, the ground bled
// of colour and stained violet under a drifting veil; at its edge a glow and a bright line a few pixels wide whatever
// the size. In Resources, so the game finds it (ValleyMap). Colours are linear, as the painting is read; drawn plain (no
// tone mapping, no grade), as the 0.10.1 drew the map.
Shader "Bastion/Valley"
{
    Properties
    {
        _MainTex ("Painting", 2D) = "white" {}
        _Field ("Marea field (activity at which it reaches)", 2D) = "white" {}
        _Level ("Activity", Float) = 0
        _Pixel ("Screen pixels per UI pixel", Float) = 1
        _Size ("Valley size (x, z)", Vector) = (30, 20, 0, 0)
        _Atalaya ("Atalaya's works (x, z, rx, rz)", Vector) = (0, 0, 1, 1)
        _Bastion ("The Bastion (x, z)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Valley"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Field);
            SAMPLER(sampler_Field);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Level;
                float _Pixel;
                float4 _Size;
                float4 _Atalaya;
                float4 _Bastion;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // The 0.10.1's map units: x east, z south (Unity's −z), so its noise falls the same.
                float2 map : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.map = float2(world.x, -world.z);
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1.0, 0.0)), u.x), lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            float Box(float2 p, float2 size, float r)
            {
                float2 q = abs(p) - size + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 vMap = input.map;
                float time = _Time.y;
                float2 half_ = _Size.xy * 0.5;
                // The painting's top row is the north (v = 0): Unity's textures start at the bottom.
                float2 uv = (vMap + half_) / _Size.xy;
                float3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(uv.x, 1.0 - uv.y)).rgb;

                // Atalaya, burnt out: its works charred, soot in patches, a few embers glowing, smoke off east on the wind.
                float2 ra = (vMap - _Atalaya.xy) / _Atalaya.zw;
                float ruin = smoothstep(1.0, 0.6, length(ra) + (Noise(vMap * 2.6) - 0.5) * 0.4);
                float soot = Noise(vMap * 5.0 + 2.0) * 0.6 + Noise(vMap * 13.0) * 0.4;
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                float3 scorched = lerp(c * float3(0.55, 0.45, 0.3), lum.xxx * float3(0.3, 0.27, 0.24), 0.5);
                float3 charred = lerp(lum.xxx * float3(0.32, 0.3, 0.28), float3(0.008, 0.007, 0.006), smoothstep(0.4, 0.75, soot) * 0.8);
                c = lerp(c, lerp(scorched, charred, smoothstep(0.12, 0.35, lum)), ruin);
                float ember = smoothstep(0.93, 0.985, Noise(vMap * 24.0 + 4.0)) * ruin * (0.55 + 0.45 * sin(time * 2.1 + vMap.x * 11.0 + vMap.y * 7.0));
                c += float3(1.0, 0.38, 0.1) * ember * 0.75;
                float2 wind = normalize(float2(1.0, 0.35));
                float2 off = vMap - _Atalaya.xy;
                float down = dot(off, wind);
                float across = dot(off, float2(-wind.y, wind.x));
                float plume = smoothstep(-0.4, 0.3, down) * exp(-down / 3.2) * exp(-across * across / (0.25 + max(down, 0.0) * 0.35));
                float puff = Noise(off * 1.6 - float2(1.0, 0.35) * time * 0.12) * 0.65 + Noise(off * 3.7 - float2(1.0, 0.35) * time * 0.2 + 5.0) * 0.35;
                c = lerp(c, float3(0.25, 0.25, 0.24), saturate(plume * smoothstep(0.3, 0.8, puff)) * 0.55);

                // The Bastion: a meadow cut out of the pines, its tracks out to the four sides, the caravan in the middle.
                float2 rb = vMap - _Bastion.xy;
                float d = length(rb * float2(1.0, 1.12)) + (Noise(vMap * 9.0) - 0.5) * 0.12;
                float ring = smoothstep(0.24, 0.32, d) * smoothstep(0.62, 0.4, d) * smoothstep(0.35, 0.62, Noise(vMap * 16.0 + 3.0));
                c = lerp(c, float3(0.012, 0.045, 0.014) * (0.8 + 0.4 * Noise(vMap * 40.0)), ring * 0.75);
                float glade = smoothstep(0.3, 0.22, d);
                c = lerp(c, float3(0.36, 0.38, 0.11) * (0.9 + 0.2 * Noise(vMap * 30.0)), glade * 0.9);
                float track = max(1.0 - smoothstep(0.012, 0.024, abs(rb.x)), 1.0 - smoothstep(0.012, 0.024, abs(rb.y))) * smoothstep(0.5, 0.22, length(rb));
                c = lerp(c, float3(0.33, 0.21, 0.11), track * 0.85);
                float shade = Box(rb - float2(0.012, 0.014), float2(0.05, 0.026), 0.012);
                c *= 1.0 - 0.45 * (1.0 - smoothstep(0.0, 0.012, shade));
                float caravan = Box(rb, float2(0.05, 0.026), 0.012);
                c = lerp(c, float3(0.52, 0.12, 0.025), 1.0 - smoothstep(0.0, 0.004, caravan));
                c = lerp(c, float3(0.8, 0.74, 0.62), 1.0 - smoothstep(0.0, 0.004, Box(rb - float2(-0.012, -0.003), float2(0.009, 0.008), 0.002)));

                // Where it reaches: the field read a little askew, its edge wavering the same everywhere.
                float2 warp = (float2(Noise(vMap * 0.8 + 3.1), Noise(vMap * 0.8 + 9.7)) - 0.5) * 0.7 + (float2(Noise(vMap * 2.7 + 1.3), Noise(vMap * 2.7 + 5.9)) - 0.5) * 0.22;
                float2 fuv = (vMap + warp + half_) / _Size.xy;
                float edge = SAMPLE_TEXTURE2D(_Field, sampler_Field, fuv).r - _Level;
                // Pixels from the edge (UI ones), negative inside.
                float px = edge / max(fwidth(edge), 1e-5) / _Pixel;
                float2 drift = vMap * 0.7 + float2(time * 0.05, time * 0.018);
                float veil = Noise(drift) * 0.6 + Noise(drift * 2.3 + 7.0) * 0.4;
                float3 under = c * float3(0.305, 0.127, 0.429);
                under = lerp(under, float3(0.205, 0.028, 0.352), 0.2);
                under = lerp(under, float3(0.515, 0.19, 0.79), 0.16 * smoothstep(0.45, 0.9, veil));
                c = lerp(c, under, smoothstep(0.6, -0.6, px));
                float flicker = 0.85 + 0.15 * sin(time * 1.7 + vMap.x * 1.1 + vMap.y * 0.7);
                c += float3(0.79, 0.155, 1.0) * (exp(-abs(px) / 7.0) * 0.36 + exp(-abs(px) / 22.0) * 0.12) * flicker;
                c = lerp(c, float3(0.97, 0.72, 1.0), 1.0 - smoothstep(0.7, 1.7, abs(px)));
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
