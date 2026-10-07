Idle Grounds — Mine resource nodes (P2)

Files
- node_ore_1x1_32x48_2f.png — 2 horizontal 32x48 frames: intact, cracked.
- node_ore_2x2_64x80_2f.png — 2 horizontal 64x80 frames: intact, cracked.
- node_ironvein_64x80_2f.png — 2 horizontal 64x80 frames: intact, cracked open.
- node_jadevein_64x80_2f.png — 2 horizontal 64x80 frames: intact, cracked.

Usage
- Slice each strip horizontally into two equal frames.
- Pivot bottom-centre. Frame 1 is the intact node; frame 2 is the damaged/cracked node.
- Nearest-neighbour filtering; no compression; pixels per unit 32.

Validation
- Exact sheet and frame dimensions.
- RGBA with binary alpha only.
- Every opaque pixel uses the ART-SPEC P01-P32 palette.
- Each frame has bottom ground contact and transparent side margins.

Limitations
- Generated source art was downsampled and palette-quantized for the production sprites.
- The two states preserve object identity, but individual loose-chip positions differ.
