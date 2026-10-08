Idle Grounds — Spirit Grove resource nodes (P2)

Files
- node_herbbush_32x48_2f.png — glowing spirit-herb bush, full/picked.
- node_bamboostalk_1x1_32x48_2f.png — single bamboo, standing/cut.
- node_bamboostalk_2x2_64x80_2f.png — three-stalk bamboo clump, intact/one cut.

Usage
- Slice each horizontal strip into two equal frames.
- Frame 1 intact/full; frame 2 harvested. Pivot bottom-centre.
- Nearest-neighbour filtering, no compression, pixels per unit 32.

Validation
- Exact sheet/frame dimensions, P01-P32 palette and binary alpha.
- Transparent margins and bottom contact in every frame.

Limitations
- Tall bamboo intentionally approaches the top edge as requested.
- Harvested states preserve enough silhouette for node recognition.
