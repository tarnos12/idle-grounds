# Idle Grounds Islands 003 — Center Grass Straight-Edge Fix

Targeted v3 replacement for `island_center_ground_blob_32x32_47f.png`. It keeps the accepted v2 jade palette, grass texture, earthy rim, three center-fill variants, and canonical mask order.

The mask geometry now carves only genuinely exposed cardinal edges. Whenever a cardinal neighbor exists, grass and rim extend straight to the shared tile border. Corners are rounded only for true outer corners; small bites are used only for missing diagonals between two present cardinals.

Unity: PPU 32, Point, None, no mipmaps; slice 32×32 and map using `blob_mask_reference.json`. The unchanged accepted fill variants are included for completeness.

Validation: 1504×32, 47 canonical frames, binary alpha, exact palette, seamless full tile, and 20 automated straight-edge border-continuity assertions passed.

Source ART-SPEC modified `2026-10-04T17:22:11.887Z`. This replaces the v2 blob geometry only; the accepted cliff is not repeated.
