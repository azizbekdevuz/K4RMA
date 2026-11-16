#ifndef K4RMA_SW05_CRIMSON_PALETTE
#define K4RMA_SW05_CRIMSON_PALETTE
half3 SW05CrimsonPalette(half3 source)
{
    // Preserve neutral steel/white cores and already-blue artwork; grade warm
    // energy into deep azure facets, cyan edges and ice-white highlights.
    half warm = saturate((source.r - source.b) * 8);
    half brightness = max(source.r, max(source.g, source.b));
    half neutral = saturate(min(source.g, source.b) / max(0.001h, brightness));
    half3 azure = lerp(half3(0.08h, 0.48h, 1.0h), half3(0.86h, 0.97h, 1.0h), neutral) * brightness;
    return lerp(source, azure, warm);
}
// Resample only the blade; the guard, hilt, tip, length and pivot stay intact.
half SW05BladeMask(float2 uv, half presence)
{
    return presence * smoothstep(0.015,0.12,uv.y) * (1-smoothstep(0.62,0.70,uv.y));
}
float2 SW05BladeUV(float2 uv, half presence)
{
    float width = 1 + 1.20 * SW05BladeMask(uv,presence);
    return float2(0.5+(uv.x-0.5)/width,uv.y);
}
half3 SW05BladeRadiance(half3 source, float2 uv, half presence)
{
    half mask = SW05BladeMask(uv,presence);
    half core = exp(-abs(uv.x-0.5)*160) * mask;
    half scan = pow(saturate(cos(uv.y*42-_Time.y*28)),16)*mask;
    return lerp(SW05CrimsonPalette(source),half3(0.88h,1.08h,1.25h),saturate(core*0.95h+scan*0.28h));
}
#endif
