# Idle Grounds P2 remaining assets

PPU 32, Point filter, Compression None, no mipmaps, sRGB. Static PNGs Single; strips Multiple, grid slice by the dimensions in their filenames. World animations use 8 fps; cloudpuff frames are three random static shapes.

Centre pivot for water/zone tiles and FX; top-centre for hanging underside decorations and waterfall; tile mist uses scene overlay placement. Wrap Repeat on water (both axes), waterfall (vertical), veil mist (both axes), qi trail (horizontal); Clamp on decorations.

All PNGs validated for exact canvas/frame layout, project palette, RGBA and binary or four-step alpha as applicable. Matching edge pixels checked for declared tiling axes.

Separate built-in image-generation calls supplied each visual source. Final assets use nearest-neighbour native-grid normalization, palette cleanup, discrete alpha, repeat-edge cleanup and periodic/ping-pong motion for tiled loops. Source subjects and paths in GENERATION-NOTES.json.

Limitations: edge equality is mechanically validated; perceived repetition and tile transitions should also be reviewed in game. Sky puffs and veil use four alpha levels; use normal alpha blending, not additive. These files complete only this batch, not ART-SPEC.
