# Idle Grounds — P1 items batch 002

Contains item_leaves.png, item_stone.png, item_clay.png, item_plank.png, item_brick.png. Wood was already integrated and is not repeated.

Unity: Sprite Single, PPU 32, centre pivot, Filter Point, Compression None, mipmaps off, Wrap Clamp, sRGB enabled. All assets are 32x32 RGBA PNGs with binary alpha and visible RGB restricted to the 32 ART-SPEC colours. Do not import preview.png as a sprite; it shows the five assets at 5x nearest-neighbour zoom.

Generation: built-in image generation, one request per asset. Full prompts in prompts.json. Generated large raster sources normalized with nearest-neighbour sampling, palette remapping without dithering, and preserved thresholded alpha. Plank silhouette normalized to 26px span. No subpixel rotation or antialiased edges in delivered sprites.

Validation: exact dimensions, RGBA mode, transparent background, alpha 0/255 only, exact approved visible RGB, silhouette span 24–28px, no clipped sprite, ZIP CRC validation. Static assets require no frame layout or tiling checks. Native-size silhouettes visually reviewed.

Limitations: generated artwork normalized to pixel resolution, not hand-pixelled; Unity runtime validation remains for intake. Leaves include a small gold stem ornament in addition to the requested gold dew spot; clay has strong vermilion highlights. Visual acceptance is subject to in-game review. Technical validation does not certify artistic acceptance.

Source: ART-SPEC.md 1LUe_-yU0a8D9Upt9bHqPxyMUsavXqByz, modified 2026-10-04T08:53:27.667Z. This delivery completes only these five keys. Remaining P1 buildings, nodes, islands, sky and other requested art are outstanding.
