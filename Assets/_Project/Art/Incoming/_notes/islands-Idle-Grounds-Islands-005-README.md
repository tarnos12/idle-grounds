# Idle Grounds Islands 005 — Center blob v4 and fill v3

Targeted texture correction. `island_center_ground_blob_32x32_47f.png` keeps the accepted v3 coastline silhouettes, earthy rim, transparency and canonical 47-mask order exactly; only P13/P14/P15 grass pixels were repainted. `island_center_ground_fill_32x32_3f.png` carries the same calm light jade texture through all three fill variants.

The darker dotted coastline band and paired-dot motif are removed. Sparse irregular P13/P15 detail now reaches tile borders, with opposite borders mirrored exactly so every fill variant tiles horizontally and vertically. Blob frames reuse these same three texture fields, masked by the unchanged v3 geometry.

Slice the blob horizontally at 32×32 and follow `blob_mask_reference.json`. Slice the fill into three 32×32 variants. Import at PPU 32, Point filtering, no compression and no mipmaps.

Validation confirms exact dimensions, palette, alpha, unchanged v3 grass mask/rim/non-grass pixels, canonical order, seamless fill borders and no paired highlight motifs. Built-in image generation was used for the repaint direction; final files were deterministically normalized to the accepted geometry and exact palette.

Source ART-SPEC modified: `2026-10-04T22:51:34.178Z`. Overall specification remains incomplete.
