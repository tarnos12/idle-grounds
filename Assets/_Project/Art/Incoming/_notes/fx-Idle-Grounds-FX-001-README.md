# Idle Grounds FX 001 — Wisp pulse strips

Contains the normal and returning wisp VFX as four-frame horizontal strips. Each frame is 12×12 px; slice on a 12×12 grid, play left-to-right, and use center pivot (6,6). Import with Point filtering, no compression, no mipmaps.

The normal wisp uses the specification cyan/white palette. The returning wisp uses the specification coral-red/white palette. Glow is built from discrete 25/50/75/100% alpha bands with no smooth gradients. Cargo is intentionally absent because the brief requires it to be drawn separately.

Validation: exact 48×12 sheet dimensions; four populated frames; colors restricted to the 32-color project palette; alpha restricted to 0/64/128/192/255. See `validation.json`.

Generation notes: built-in image generation produced the visual source concept; the delivery sprites were manually normalized into exact 12×12 frames, exact palette colors, discrete alpha bands, and a deterministic pulse sequence. No resampling or anti-aliasing is present in the delivered files.

Limitations: glow appearance depends on the engine blend mode and should be reviewed over representative backgrounds. This is a partial P1 delivery; the overall ART-SPEC remains incomplete.

Source ART-SPEC modified: `2026-10-04T19:50:21.710Z`.
