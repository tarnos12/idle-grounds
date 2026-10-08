Idle Grounds — Celestial Peak resource nodes (P2)

Files
- node_starrock_1x1_32x48_2f.png — small meteorite, intact/cracked.
- node_starrock_2x2_64x80_2f.png — large star rock, intact/cracked.
- node_moonshrub_32x48_2f.png — moonpetal shrub, flowering/harvested.

Usage
- Slice each horizontal strip into two equal frames.
- Frame 1 intact/full; frame 2 cracked/harvested. Pivot bottom-centre.
- Nearest-neighbour filtering, no compression, pixels per unit 32.

Validation
- Exact dimensions, approved P01-P32 palette and binary alpha.
- Transparent margins and bottom contact in every frame.

Limitations
- Floating star fragments are kept close to the primary silhouette for stable sorting.
- Moonpetal glow is simplified at 1x but retained for biome readability.
