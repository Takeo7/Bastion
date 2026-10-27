// The ground's contact occlusion, baked (0.10.1's groundAO.ts; UI's GroundOcclusion): pass 0 draws the height of what is
// solid seen from above (world y into r); pass 1 works out, for each texel of that height field, how much of the sky the
// things round it hide (horizon-based, as GTAO on the height field): r how much sky it sees (1 open, 0 hidden), g the
// height there (a floor reads the occlusion only where it is that surface, not a roof over it); b the same seen from the
// ground's own height (0), whatever stands over it: the ground's, so a car's or a bench's overhang darkens it too. The
// walls' caps are left out of the heights, as 0.10.1 left them.
Shader "Hidden/Bastion/GroundBake"
{
    Properties
    {
        _MainTex ("Heights", 2D) = "black" {}
    }

    SubShader
    {
        Pass
        {
            Name "Height"
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // (TEXCOORD5's y is 1 on a wall's cap, WallsView's AddCap; nothing else has it.)
            struct Attributes { float4 positionOS : POSITION; float4 more : TEXCOORD5; };
            struct Varyings { float4 positionCS : SV_POSITION; float height : TEXCOORD0; float cap : TEXCOORD1; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.height = world.y;
                output.cap = input.more.y;
                output.positionCS = TransformWorldToHClip(world);
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                // The walls' caps are no occluders (buildings.ts: the key 'cap' left out): 0.10.1's walls, upright planes,
                // left nothing seen from above, so a room's floor got the dark of its furniture and doorways alone, and
                // was light in the middle (Arte, 27/10: in Unity the rooms came out dark all over).
                clip(0.5 - input.cap);
                return float4(input.height, 0, 0, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Occlusion"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_point_clamp);
            // The map in metres (x, z); how far round a texel it looks (metres), in how many directions and steps.
            float4 _BakeSize;
            #define DIRECTIONS 12
            #define STEPS 8
            #define RADIUS (2.0 * 1.5)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float h0 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_point_clamp, input.uv, 0).r;
                float spin = Hash(input.positionCS.xy) * 6.2831853 / DIRECTIONS;
                float hidden = 0.0;
                float low = 0.0;
                for (int d = 0; d < DIRECTIONS; d++)
                {
                    float a = spin + d * 6.2831853 / DIRECTIONS;
                    float2 dir = float2(cos(a), sin(a));
                    float horizon = 0.0;
                    float floorHorizon = 0.0;
                    for (int s = 1; s <= STEPS; s++)
                    {
                        float dist = s * (RADIUS / STEPS);
                        float h = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_point_clamp, input.uv + dir * dist / _BakeSize.xy, 0).r;
                        float dh = h - h0;
                        // The sine of the angle up to it, fading with distance.
                        float fall = dist / (RADIUS * 1.05);
                        horizon = max(horizon, dh / sqrt(dh * dh + dist * dist) * (1.0 - fall * fall));
                        floorHorizon = max(floorHorizon, max(h, 0.0) / sqrt(h * h + dist * dist) * (1.0 - fall * fall));
                    }
                    hidden += horizon;
                    low += floorHorizon;
                }
                // (Its height as 0.10.1's eight-bit target kept it: a tile at most, 1.5 m. Over a wall's foot the floor blends
                // towards that, not the wall's whole height, so it stays in the occlusion as close to the wall as there.)
                return float4(pow(saturate(1.0 - hidden / DIRECTIONS), 2.4), min(h0, 1.5), pow(saturate(1.0 - low / DIRECTIONS), 2.4), 1);
            }
            ENDHLSL
        }
    }
}
