#ifndef STICKER_SURFACE_INCLUDED
#define STICKER_SURFACE_INCLUDED

// The same finish calculation serves static artwork, the 3D viewer and AR.
// UVs stay anchored to the sticker; only the reflected light moves with its angle.
float3 StickerSpectrum(float phase)
{
    return .5 + .5 * cos(6.2831853 * (phase + float3(0, .3333, .6667)));
}
float StickerPattern(float2 uv, float pattern)
{
    if (pattern < .5) return .5 + .5 * sin((uv.x + uv.y) * 100.0);
    float2 cell = frac(uv * (pattern < 1.5 ? 18.0 : 10.0)) - .5;
    if (pattern < 1.5) return 1.0 - smoothstep(.12, .24, length(cell));
    float star = abs(cell.x * cell.y) + .08 * max(abs(cell.x), abs(cell.y));
    return 1.0 - smoothstep(.024, .043, star);
}
float4 StickerSurface(float4 baseColor, float2 uv, float finish, float pattern, float2 tilt, float back)
{
    float3 color = back > .5 ? float3(.93, .925, .91) : baseColor.rgb;
    if (finish < .5) return float4(color, baseColor.a);
    float sweep = uv.x * .7 + uv.y * .45 + tilt.x * .38 - tilt.y * .3;
    float band = pow(saturate(1.0 - abs(frac(sweep) - .5) * 3.8), 5.0);
    float3 spectrum = StickerSpectrum(sweep * 1.6 + tilt.x * .18);
    float motif = finish > 1.5 ? StickerPattern(uv, pattern) : 1.0;
    // Preserve ink and illustration: the reflective layer never replaces the art.
    float strength = finish > 1.5 ? .10 + motif * .28 : .22;
    color = lerp(color, color * .76 + spectrum * .48, strength);
    color += band * (.16 + motif * .17);
    return float4(saturate(color), baseColor.a);
}
#endif
