#ifndef K4RMA_SW05_CRIMSON_PALETTE
#define K4RMA_SW05_CRIMSON_PALETTE
half3 SW05CrimsonPalette(half3 source)
{
    half chroma = max(source.r, max(source.g, source.b)) - min(source.r, min(source.g, source.b));
    half metal = smoothstep(0.05h, 0.36h, chroma);
    half3 steel = source * half3(1.0h, 0.83h, 0.8h);
    half3 crimson = half3(source.r * 0.92h, source.g * 0.2h + source.b * 0.08h,
        source.b * 0.22h + source.g * 0.055h);
    return lerp(steel, crimson, metal);
}
#endif
