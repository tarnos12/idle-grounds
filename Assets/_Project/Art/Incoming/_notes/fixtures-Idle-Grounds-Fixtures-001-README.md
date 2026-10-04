# Idle Grounds Fixtures 001 — Spirit Tree and Quarry

## Contents

- `fix_spirittree.png` — sacred Spirit Tree fixture, 160×192.
- `fix_quarry.png` — quarry-rock fixture, 64×80.
- `fixture_preview.png` — enlarged side-by-side preview for inspection only.
- `prompt.txt` — generation and normalization notes.
- `validation.json` — machine-readable checks.

## Unity usage

Import each PNG as Sprite (2D and UI), Sprite Mode Single, PPU 32, Point filtering, Compression None, no mipmaps, Clamp, sRGB, with a bottom-center pivot. The tree represents a 4×4 footprint with its allowed canopy overhang; the quarry represents a 2×2 footprint.

## Validation

Both generated sources were resized with nearest-neighbor sampling, remapped without dithering to the exact 32-color ART-SPEC palette, converted to binary alpha, and bottom aligned. Automated checks confirm exact dimensions, alpha values `{0,255}`, palette compliance, and no edge clipping.

## Limitations

These are normalized image-generation sources rather than hand-placed pixel art. Fine prayer-ribbon, charm, chisel, and stone-chip details may simplify at distant gameplay zoom. The Spirit Tree's trunk base and roots should be checked against the final 4×4 collision footprint in Unity; the canopy intentionally uses the specification's wider overhang. Final sorting and gameplay-scale readability remain an in-engine review step.

## Source

- ART-SPEC.md modified: `2026-10-04T13:44:00.702Z`
- Priority/status: P1 partial fixture delivery; the overall specification remains incomplete.
