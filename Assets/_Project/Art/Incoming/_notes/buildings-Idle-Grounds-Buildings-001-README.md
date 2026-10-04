# Idle Grounds — P1 logistics buildings batch 001

Contains `bld_gathering_stone.png`, `bld_wisp_lantern.png`, and `bld_warding_seal.png`.

Unity import: Sprite Single, PPU 32, bottom-centre pivot, Filter Point, Compression None, mipmaps off, Wrap Clamp, sRGB enabled. Every sprite is a 32×48 RGBA PNG with binary alpha; all visible RGB values use the current 32-colour ART-SPEC palette. Each object touches the canvas bottom.

Generation: built-in image generation, one call per asset, then nearest-neighbour normalization to 32×48, palette remapping without dithering, binary-alpha thresholding, and bottom alignment. Full prompts are in `prompts.json`.

Validation: exact filename and dimensions; RGBA; alpha values 0/255 only; approved palette only; non-empty transparent background; base contacts bottom edge; ZIP CRC test. Visual review performed at native size and enlarged with nearest-neighbour.

Limitations: generated sources were normalized from larger images rather than hand-pixelled. Gathering Stone has small orbiting fragments and hard-banded green qi; Wisp Lantern is visually wide because the post shares its 1×1 canvas; Warding Seal uses a simple symbolic red seal rather than readable script. Unity runtime/in-game scale remains to be reviewed after intake.

Source: ART-SPEC.md ID `1LUe_-yU0a8D9Upt9bHqPxyMUsavXqByz`, modified `2026-10-04T09:16:55.289Z`. This is a partial P1 delivery; all other requested keys remain outstanding.
