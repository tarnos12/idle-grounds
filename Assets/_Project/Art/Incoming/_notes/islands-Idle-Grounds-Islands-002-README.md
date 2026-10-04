# Idle Grounds Islands 002 — Corrected Center Grass Blob

Replacement for the rejected Center ground sheet. Contains the canonical 47-mask horizontal strip, three separate fully surrounded center-fill variants, and the exact mask reference.

Changes from 001: soft jade grass dominated by P13/P14/P15; sparse organic detail; rounded coast silhouettes; 1–2 px P20/P21 earthy rims; seamless full-grass tile; no teal mandala pattern or square 8 px cut-outs.

Unity: PPU 32, Point, None, no mipmaps. Slice at 32×32. Use `blob_mask_reference.json`; frame order is the exact canonical ascending list in the current spec. `island_center_ground_fill_32x32_3f.png` provides three random center variants.

Validation: exact dimensions and frame counts, binary alpha, exact 32-color palette, canonical masks, and two-axis seamless full tile. In-engine coastline review remains required.

Source ART-SPEC modified `2026-10-04T16:49:05.118Z`. This replaces only the rejected grass blob; the accepted cliff is not repeated.
