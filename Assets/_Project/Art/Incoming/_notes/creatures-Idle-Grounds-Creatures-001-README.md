# Idle Grounds Creatures 001 — Fox Spirit P1 animations

Contains the Fox Spirit idle, move and hit sheets. Slice every PNG horizontally on a 32×32 grid. Frames face right, use bottom-centre pivot, Point filtering, no compression and no mipmaps.

`idle`: 4-frame breathing/tail-flame pulse with an ear twitch. `move`: 4-frame alternating spirit trot. `hit`: 2-frame one-shot, white flash then backward recoil. The compact three-tail fan suggests the nine-tail silhouette without losing readability at 32 px; the cyan flame remains distinct at the tail tip.

All delivered pixels use the exact project palette and binary alpha. See `validation.json`. Built-in image generation supplied the character design reference; final sheets were normalized into deterministic 32×32 frames with exact palette, hard edges and no resampling.

Limitations: the P3 dissolve/death animation is intentionally not included in this P1 batch. Review animation speed and hit timing in Unity. Overall ART-SPEC remains incomplete.

Source ART-SPEC modified: `2026-10-04T19:50:21.710Z`.
