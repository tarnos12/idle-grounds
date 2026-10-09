# Idle Grounds remaining P3 art

PPU 32, Point, no compression or mipmaps, sRGB. Static Single; strips Multiple sliced to the dimensions in each filename. Centre pivot for FX, icons and overlays; bottom-centre for buildings and creatures; top-centre for hanging island underside pieces and waterfall mist.

Loop FX and working building strips 8 fps; creature idle 6 fps. Death and dragon stir are one-shot actions. Zone patches are random static variants. Veil edge repeats horizontally.

Exact RGBA PNG dimensions/frame layout, populated frames, visible frame differences, approved palette and binary/four-step alpha validated. See VALIDATION.json.

Built-in image generation supplied each asset separately. Original accepted sprites were provided as references for building/creature animation. Nearest-neighbour native-grid normalization, palette quantization and alpha cleanup applied. Building loops preserve accepted static base pixels, with localized native-grid motion/effects guided by the generated animation. Source prompts in GENERATION-NOTES.json.

Limitations: in-game integration and event timing were not tested. Loop motion and one-shot endings should be reviewed alongside game effects. No smooth-alpha gradients. Use alpha blending on mist/shadows; additive blending only if deliberately chosen in Unity.
