# Idle Grounds Buildings 004 — Altar

## Contents

- `bld_center.png` — Altar landmark sprite.
- `preview.png` — 3× nearest-neighbor preview for inspection only.
- `prompt.txt` — generation prompt notes.
- `validation.json` — machine-readable output checks.

## Unity usage

- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pixels Per Unit: 32
- Pivot: Bottom Center
- Filter Mode: Point (no filter)
- Compression: None
- Generate Mip Maps: Off
- Wrap Mode: Clamp
- sRGB: On

The sprite is 160×192 pixels: a 160×160 logical 5×5 ground footprint with up to 32 pixels of roof height. It is bottom-aligned and intended to face south.

## Validation

The generated source was resized with nearest-neighbor sampling, remapped without dithering to the exact 32-color ART-SPEC palette, converted to binary alpha, and bottom-center aligned. Automated checks confirm exact dimensions, alpha values `{0,255}`, palette compliance, and bottom alignment.

## Limitations

This is a normalized image-generation source rather than hand-placed pixel art. Dense rail, roof, and lantern details may simplify at distant gameplay zoom. The central bagua/taiji ring, shrine, stairs, and incense brazier remain prominent. Final footprint collision, draw sorting, and in-engine readability should still be reviewed in Unity.

## Source

- ART-SPEC.md modified: `2026-10-04T11:43:35.342Z`
- Priority/status: P1 partial delivery; this archive does not mark the full specification complete.
