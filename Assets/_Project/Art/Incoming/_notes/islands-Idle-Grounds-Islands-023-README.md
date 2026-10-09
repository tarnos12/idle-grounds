# Idle Grounds biome scatter overlays

Four different transparent scatter decorations per biome, supplied alongside the previously delivered 47-blob ground and centre fills, as requested in §3.0–3.1. Sheets 128x32, slice into four 32x32 frames. Frames are random static variants; do not animate. Place sparsely over the biome ground tilemap.

PPU 32, Point, no compression/mipmaps, sRGB, Multiple 32x32, centre pivot, Clamp. Binary alpha and exact approved palette only. Seven strips/four distinct nonempty frames each validated.

Each biome used a separate built-in image-generation call. Source silhouettes were cropped per frame and normalized by nearest-neighbour sampling, exact palette quantization and binary alpha cleanup. Source prompt notes retained.

Limitations: spawn density and distribution belong to scene authoring. No in-game placement test.
