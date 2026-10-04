# Idle Grounds Islands 004 — Center fill texture v2

Targeted replacement for `island_center_ground_fill_32x32_3f.png` only. Slice horizontally into three 32×32 centre-fill variants. Import at PPU 32 with Point filtering, no compression and no mipmaps.

The accepted P13/P14/P15 jade-grass palette is preserved. Paired light dots and their visible grid were removed. Each frame now has only seven isolated highlights with deliberately varied spacing, unique rows and columns, plus sparse irregular shadow blades. A uniform two-pixel grass border keeps every variant seamless on all four sides.

Validation confirms exact dimensions, binary alpha, palette compliance, seam-safe borders, no paired-highlight motifs and no regular row/column repetition. See `validation.json`.

Built-in image generation was used to explore a more irregular highlight distribution; the delivered sheet was then deterministically normalized to the exact palette, frame layout and seamless-edge constraints.

Limitations: this ZIP intentionally replaces only the fill strip; the accepted v3 blob strip and cliff are not duplicated. Overall ART-SPEC remains incomplete.

Source ART-SPEC modified: `2026-10-04T21:45:36.527Z`.
