# Idle Grounds — path v2 and coast lips

ART-SPEC §3.6, ID 1LUe_-yU0a8D9Upt9bHqPxyMUsavXqByz modified 2026-10-09T17:38:49.243Z.

PPU32, Point, no compression or mipmaps, sRGB; Multiple slice32x32, centre pivot. Path order: fills A/B/C, N/E/S/W edges, NW/NE/SE/SW outer corners, NW/NE/SE/SW inner corners. Accepted v1 topology and surrounding grass retained, stone faces replaced with image-generated shaded flagstone/moss texture; central texture crop keeps large stone sizes. Fill boundaries match across variants.

Lip order: west, east, north, outer NW corner. Four pixels wide, one-pixel darker rim, green only. Place at the corresponding coast; rotate/flip corner in Unity as needed. No cliff rock on these edges.

Built-in image generation supplied new flagstone material and corrected flat grass lip sheet. Nearest-neighbour normalization, palette quantization, binary alpha and exact tile-edge fitting applied. Usage/layout transformations preserve the accepted path masks. Validation includes dimensions, frame order, alpha, palette, fill seams and lip width/orientation. Prompts included in JSON notes.

Limitations: full Unity scene and diagonal path composition not rendered here; inspect moss density and connections in-game. Only these two keys are delivered in this package.
