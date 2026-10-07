Idle Grounds — Fishing resource nodes (P2)

Files
- node_fish_32x48_4f.png — surfacing koi loop: surface, rise, peak, fall.
- node_algae_32x48_4f.png — floating algae/lotus mat, subtle four-frame bob.

Usage
- Slice each 128x48 strip horizontally into four 32x48 frames.
- Pivot bottom-centre; play at 6-8 fps with loop enabled.
- Both sprites are transparent overlays intended for the Fishing water tile.
- Import with nearest-neighbour filtering and no compression.

Validation
- Exact dimensions and horizontal frame order.
- RGBA with binary alpha only.
- Every opaque pixel uses the ART-SPEC P01-P32 palette.
- Transparent side margins and bottom-centred anchoring.

Limitations
- The koi splash is deliberately compact at 32px width; fine droplets simplify at 1x.
- The algae loop uses restrained motion to avoid visible positional jitter.
