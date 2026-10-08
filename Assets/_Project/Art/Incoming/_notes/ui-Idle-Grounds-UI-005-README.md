# Idle Grounds UI 005 — Standard Buttons

## Contents

- `ui_btn_normal_48x24.png`
- `ui_btn_hover_48x24.png`
- `ui_btn_pressed_48x24.png`
- `ui_btn_disabled_48x24.png`

## Unity usage

Import at PPU 32, Point filtering, Compression None, no mipmaps, sRGB and Sprite Mode Single. Set Sprite Border to 8,8,8,8 and use Image Type Sliced. Runtime text is drawn over the calm centre.

## State intent

Normal has a restrained jade rim; hover is brighter; pressed uses a darker inset treatment with the visual face lowered; disabled is fully slate/grey-blue without jade saturation.

## Validation

Every sprite is exactly 48×24 RGBA, uses only approved palette colours and binary alpha. Transparent padding was trimmed before nearest-neighbour reduction. Corners and side caps remain inside the 8-pixel 9-slice borders.

## Generation prompt notes

All four states were generated separately as front-facing hard-edged pixel-art buttons with shared xianxia cloud-scroll geometry. Outputs were trimmed, nearest-neighbour reduced, palette-remapped, and alpha-thresholded.
