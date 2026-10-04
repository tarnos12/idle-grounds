# Idle Grounds — P1 standard buildings batch 002

Contains `bld_workbench.png` and `bld_storehouse.png`.

Unity import: Sprite Single, PPU 32, bottom-centre pivot, Filter Point, Compression None, mipmaps off, Wrap Clamp, sRGB enabled. Both sprites are 96×128 RGBA PNGs for 3×3 footprints, with binary alpha and visible RGB restricted to the current 32-colour ART-SPEC palette. Each base touches the canvas bottom.

Generation: built-in image generation, one call per building, followed by nearest-neighbour normalization to 96×128, palette remapping without dithering, binary-alpha thresholding, and bottom alignment. Full prompts are in `prompts.json`.

Validation: exact dimensions and filenames; RGBA; alpha values 0/255 only; approved palette only; transparent background; base contacts bottom edge; no canvas clipping; ZIP CRC test. Visual review performed at native size and enlarged with nearest-neighbour.

Limitations: generated sources were normalized from larger images rather than hand-pixelled. The Workbench has dense tool detail at native size, while its saw, plane, planks and red awning remain identifiable. The Storehouse includes small grain bundles alongside the requested crates. Unity runtime and collision/footprint alignment remain to be reviewed after intake.

Source: ART-SPEC.md ID `1LUe_-yU0a8D9Upt9bHqPxyMUsavXqByz`, modified `2026-10-04T10:22:39.940Z`. This remains a partial P1 delivery.
