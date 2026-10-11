# Idle Grounds 3/4 world correction — ART-SPEC §3.6

Source ART-SPEC.md ID 1LUe_-yU0a8D9Upt9bHqPxyMUsavXqByz, modified 2026-10-09T17:38:49.243Z.

Unity: PPU32, Point filtering, no compression or mipmaps, sRGB. Strip assets: Multiple, grid slice to filename frame size. Static decals use Centre pivot. Cliff walls attach at top-centre under SOUTH coast only; frames 0–2 mid variants, 3 left end, 4 right end, 5 inner-left, 6 inner-right, 7 waterfall spout. Cloud/depth layers Repeat both axes. Macro frames are four static variants, not animation.

Validation: RGBA 8bit PNG; exact dimensions, frame populations, fixed 32-colour palette, binary or four-step alpha; cliff mid seam columns or repeat borders checked where applicable.

Built-in image generation created each asset independently. Native-size normalization uses nearest-neighbour sampling, palette quantization and prescribed alpha steps. Shared seam columns/borders normalized for repeatability. Prompts recorded in GENERATION-NOTES.json.

Limitations: rendered Unity scene not available; scale, attachment positions, blended cloud parallax and variation distribution require in-game review. Opposing border equality is checked; visual repetition should also be reviewed across several repeats. These files complete only the included keys, not the entire spec.
