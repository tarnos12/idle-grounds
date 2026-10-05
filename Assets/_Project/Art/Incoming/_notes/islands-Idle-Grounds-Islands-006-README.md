# Idle Grounds Islands 006 — Center blob v5 correction

This is the corrected replacement for rejected Islands-005 blob v4. The source is the accepted v3 PNG downloaded directly from Drive archive `Idle-Grounds-Islands-003.zip` (`1pgzBJ4tnUJdInK1rdsAm3YHUzaBrHDQY`), not a local lookalike.

Only P13/P14/P15 grass pixels in frames 0–45 were replaced with the accepted v2.1 fill texture. Every transparent, rim and other non-grass pixel remains identical to the Drive v3 source. Frame 46 (mask 255) is pixel-identical to accepted fill variant 0, as requested. No P10/P11/P12 pixels are present.

Slice horizontally at 32×32 and use `blob_mask_reference.json`. Import at PPU 32, Point filtering, no compression and no mipmaps. See `validation.json` for direct source-comparison assertions.

Built-in image generation was used for the targeted repaint direction; deterministic normalization enforced the exact Drive-v3 geometry and accepted fill pixels. Overall ART-SPEC remains incomplete.

Source ART-SPEC modified: `2026-10-04T23:44:29.413Z`.
