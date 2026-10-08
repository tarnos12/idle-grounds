# Idle Grounds UI 003

## Contents

- `ui_bottombar_bg_64x48.png` — horizontal bottom bar; 9-slice border 12,12,10,0.
- `ui_questpanel_frame_64x64.png` — parchment-dark quest frame with cinnabar seal; border 20,20,20,20.
- `ui_tooltip_frame_24x24.png` — compact dark tooltip; border 8,8,8,8.
- `ui_handchip_frame_24x24.png` — compact gold hand-chip frame; border 8,8,8,8.

## Unity usage

Import at PPU 32, Point filtering, Compression None, no mipmaps, sRGB, Sprite Mode Single. Set each Sprite Border to the values above and use Image Type Sliced. The bottom bar is intended to stretch horizontally; the other frames can stretch in both axes.

## Validation

All sprites use the exact requested dimensions, the approved project palette and binary alpha. Transparent exterior padding was trimmed before nearest-neighbour reduction. Centres are visually plain and corner ornaments stay within their 9-slice borders.

## Limitations

The quest seal is decorative and intentionally contains no readable glyph. Very small frames should remain at integer scale.

## Generation prompt notes

Generated individually as front-facing hard-edged pixel-art UI: lacquered wood and gold cloud-scroll bottom bar, parchment quest frame with cinnabar seal, slate tooltip frame, and gold hand-chip frame. Each output was trimmed, reduced with nearest-neighbour sampling, palette-remapped, and alpha-thresholded.
