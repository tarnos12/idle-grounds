# Idle Grounds Islands 001 — Center Ground and Cliff

Contains the preferred 47-frame Center grass blob strip and the eight-frame Center cliff rim. Import at PPU 32, Point, None, no mipmaps. Slice both horizontally on a 32×32 grid. Use `blob_mask_reference.json` to map frame indices to neighbour masks.

The blob order is the ascending set of all 47 valid 8-neighbour masks; diagonal bits are present only when both adjacent cardinal neighbours exist. The fully surrounded tile is frame 46 and is verified seamless on both axes.

The cliff strip follows the requested order: three middles, left end, right end, inner left, inner right, waterfall source. Built-in image generation supplied the source art; output was nearest-neighbour normalized, remapped without dithering to the exact palette, and converted to binary alpha.

Limitations: Unity Rule Tile mapping must follow the included reference rather than assuming another engine's frame order. In-engine coastline and cliff-turn review remains required.

Source ART-SPEC modified: `2026-10-04T15:43:43.860Z`. Partial P1 delivery; overall spec incomplete.
