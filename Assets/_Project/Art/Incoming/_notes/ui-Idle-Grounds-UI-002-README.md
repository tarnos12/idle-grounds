# Idle Grounds UI 002

## Contents

- `ui_panel_scroll_64x64.png` — parchment story/help panel, 9-slice insets 20,20,18,18.
- `ui_panel_jade_64x64.png` — dark lacquer modal panel with jade frame, 9-slice insets 16,16,16,16.
- `ui_panel_dark_32x32.png` — plain minor-popup panel, 9-slice insets 8,8,8,8.

## Unity usage

Import at PPU 32 with Point filtering, Compression None, no mipmaps, sRGB and Sprite Mode Single. Set Sprite Border to the inset values above, then use Image Type Sliced. Keep the scroll and jade corners unscaled; only their centre and straight rails should stretch.

## Validation

All files are RGBA PNGs at exact requested dimensions. RGB values are restricted to the approved 32-colour palette and alpha is binary (0/255). Canvas edges contain the intended frame; no external margin or drop shadow is baked in.

## Limitations

At very small rendered sizes the parchment distress and jade corner knots simplify substantially. Avoid displaying these panels below their source dimensions.

## Generation prompt notes

Each panel was generated separately as front-facing hard-edged pixel art with an empty stretchable centre and ornament confined to the border. The generated art was resized using nearest-neighbour sampling, remapped to the approved palette, and alpha-thresholded.
