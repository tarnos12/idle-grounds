# Idle Grounds Nodes 001 — Center Bush

## Contents

- `node_bush_32x48_2f.png` — horizontal two-frame node strip.
- `preview.png` — enlarged nearest-neighbor preview for inspection only.
- `prompt.txt` — generation and normalization notes.
- `validation.json` — machine-readable checks.

## Frame layout

The PNG is 64×48 pixels total and contains two equal 32×48 frames with no gaps:

1. Intact flowering leafy shrub.
2. Harvested/trimmed shrub with bare twigs and a few remaining leaves.

## Unity usage

Import as Sprite (2D and UI), Sprite Mode Multiple, PPU 32, Point filtering, Compression None, no mipmaps, Clamp, sRGB. Slice as a 32×48 grid with no padding or offset. Use a bottom-center pivot for each frame.

## Validation

The generated states were separated, nearest-neighbor resized, remapped without dithering to the exact 32-color ART-SPEC palette, converted to binary alpha, bottom aligned, and reassembled horizontally. Automated checks confirm the total size, equal frame layout, alpha values `{0,255}`, palette compliance, bottom alignment, and no frame-edge clipping.

## Limitations

This is a normalized image-generation source rather than hand-placed pixel art. Small blossom and leaf clusters intentionally simplify at gameplay scale. The two states share their baseline and general root mass, but the trimmed frame has a deliberately sparser silhouette for immediate hit-state readability.

## Source

- ART-SPEC.md modified: `2026-10-04T14:15:23.625Z`
- Priority/status: P1 partial nodes delivery; the overall specification remains incomplete.
