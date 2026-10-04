# Idle Grounds — P1 kiln building batch 003

Contains `bld_kiln.png`.

Unity import: Sprite Single, PPU 32, bottom-centre pivot, Filter Point, Compression None, mipmaps off, Wrap Clamp, sRGB enabled. The sprite is a 96×192 RGBA PNG for the 3×5 burner footprint, with binary alpha and visible RGB restricted to the current 32-colour ART-SPEC palette. Its base touches the canvas bottom.

Generation: built-in image generation followed by nearest-neighbour normalization to 96×192, palette remapping without dithering, binary-alpha thresholding, and bottom alignment. The complete prompt is in `prompt.txt`.

Validation: exact filename and dimensions; RGBA; alpha values 0/255 only; approved palette only; transparent background; base contacts bottom edge; no canvas clipping; ZIP CRC test. Visual review performed at native size and 3× nearest-neighbour.

Limitations: generated source was normalized from a larger image rather than hand-pixelled. The silhouette is tall and narrow inside the 3×5 footprint, emphasizing the stepped firing chambers and chimney. Small side supports, brick stacks, and jars may be visually dense at the farthest camera zoom. The static idle sprite includes the requested glowing furnace mouth. Unity runtime footprint and sorting alignment remain to be reviewed after intake.

Source: ART-SPEC.md ID `1LUe_-yU0a8D9Upt9bHqPxyMUsavXqByz`, modified `2026-10-04T11:43:35.342Z`. This remains a partial P1 delivery.
