#ifndef K4RMA_SW05_CRIMSON_PALETTE
#define K4RMA_SW05_CRIMSON_PALETTE
half3 SW05CrimsonPalette(half3 source)
{
    // The replacement artwork is already crimson with a white cutting core.
    // Preserve those supplied colors for both flying and embedded swords.
    return source;
}
#endif
