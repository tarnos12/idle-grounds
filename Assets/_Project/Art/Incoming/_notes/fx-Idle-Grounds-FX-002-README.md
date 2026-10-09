# Idle Grounds FX 002 — Harvest fragments

Four distinct 8x8 fragments for each of fourteen materials. Each PNG is a 32x8 horizontal strip. Pick a random frame and emit 5–8 gravity particles per hit; these are random variants, not an animation loop. Centre pivot, PPU 32, Point filter, no compression or mipmaps, sRGB, Multiple grid 8x8.

Validation: all fourteen files are RGBA 8-bit, exact dimensions, four nonempty distinct frames, material-specific approved palette only and binary alpha. See VALIDATION.json.

Generation: separate built-in image-generation prompt for each material. Generated frame silhouettes cropped independently and normalized to a native 8x8 grid using nearest-neighbour sampling, palette quantization and binary alpha. Source paths and prompt subject/palette in GENERATION-NOTES.json.

Limitations: physical particle motion is supplied by Unity. Gold leaf dot retained on only the first fragment. The game integration was not tested.
