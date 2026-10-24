// The buildings' cutaway in the shaders (0.10.1's buildings.ts CUT_GLSL, wallCutPatch, roofFadePatch), read from
// Cutaway's _CutMap: a texel a building (r roof gone, g walls facing the camera cut, b every wall cut, a the storey in
// view, 64 a storey). Heights in metres (0.10.1's tiles times 1.5). Nothing is cut while there is no cutaway
// (_CutCount 0).
#ifndef BASTION_BUILDING_CUT
#define BASTION_BUILDING_CUT

TEXTURE2D(_CutMap);
float _CutCount;
// The camera's view on the ground, in 0.10.1's frame (x east, z south): what the walls' outward faces are judged by.
float4 _CutView;

// A storey and a little (2.3 + 0.05 tiles), and the height the cut walls drop to (CUT_LOW, 0.95 tiles: about the low cover's).
#define CUT_STOREY 3.525
#define CUT_LOW 1.425
// aCut's way out for what fades with the roof (a façade's upper storeys, a chimney).
#define ON_ROOF 9.0

float4 CutState(float building)
{
    return LOAD_TEXTURE2D_LOD(_CutMap, uint2(max(0.0, building - 0.5), 0), 0);
}

// The storey in view in a building that is cut: what stands higher fades.
float CutFloor(float4 state)
{
    return floor(state.a * (255.0 / 64.0) + 0.5);
}

// How far what stands on storey `level` of `building` (+1; 0 none) has faded.
float CutAbove(float building, float level)
{
    if (building < 0.5 || _CutCount < 0.5) return 0;
    float4 state = CutState(building);
    return level > CutFloor(state) + 0.5 ? state.r : 0;
}

// A kit piece's cut (wallCutPatch): `cut` its building (+1), the way its outer face looks (x, z; none between rooms,
// ON_ROOF on a façade's upper storeys) and its floor (m); `level` its storey; a cap (`cap` 1) comes down with its wall.
// How high the piece is drawn up to (`top`), and how far it has faded (`fade`, dithered).
void PieceCut(float4 cut, float level, float cap, inout float3 positionWS, out float top, out float fade)
{
    top = 1e4;
    fade = 0;
    if (cut.x < 0.5 || _CutCount < 0.5) return;
    float4 state = CutState(cut.x);
    float shown = CutFloor(state);
    if (level > shown + 0.5)
    {
        // Above the storey in view (the roof, a façade's upper storeys, a building's real storeys): it fades.
        fade = state.r;
    }
    else if (level > shown - 0.5 && cut.y < ON_ROOF - 1.0)
    {
        // On the storey in view: between rooms, or facing the camera, the walls drop.
        float facing = dot(cut.yz, cut.yz) < 0.01 ? 1.0 : smoothstep(-0.15, 0.25, -dot(cut.yz, _CutView.xy));
        float k = max(state.g * facing, state.b);
        if (k > 0.0)
        {
            top = cut.w + lerp(CUT_STOREY, CUT_LOW, k);
            // A cap stands at the cut itself: it goes down with its wall, and is never cut away by it.
            if (cap > 0.5) positionWS.y = min(positionWS.y, top);
        }
    }
    if (cap > 0.5) top = 1e4;
}

// A pixel's threshold in a 4×4 ordered dither: fades with no sorting, steady on screen.
float CutDither(float2 pixel)
{
    uint2 p = uint2(pixel) % 4;
    const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

#endif
