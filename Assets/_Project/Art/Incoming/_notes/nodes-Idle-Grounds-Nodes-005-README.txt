Idle Grounds — Volcano resource nodes (P2)

Files
- node_obsidian_1x1_32x48_2f.png — small obsidian spire, intact/cracked.
- node_obsidian_2x2_64x80_2f.png — large obsidian cluster, intact/cracked.
- node_firevein_64x80_2f.png — lava-veined rock, intact/cracked-brighter.

Usage
- Slice each horizontal strip into two equal frames.
- Frame 1 intact; frame 2 damaged/cracked. Pivot bottom-centre.
- Nearest-neighbour filtering, no compression, pixels per unit 32.

Validation
- Exact sheet/frame dimensions, ART-SPEC palette, binary alpha.
- Transparent margins and bottom ground contact in each frame.

Limitations
- The fire vein is deliberately bright for gameplay readability on the dark Volcano biome.
- Small loose chips are state-specific while the main silhouette remains consistent.
