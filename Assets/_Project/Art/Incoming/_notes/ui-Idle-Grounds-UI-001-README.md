# Idle Grounds UI 001

## Contents

- `ui_fuel_rack_96x64.png` — fixed 3-column × 2-row burner fuel rack.
- `ui_fuel_slot_32x32.png` — single empty fuel slot.

## Unity usage

Import at PPU 32, Point filtering, Compression None, no mipmaps, sRGB, Sprite Mode Single, centre pivot. Place the 96×64 rack immediately left of a burner with its top edge aligned to the building top. The rack is fixed-size and contains six 32×32 visual cells.

## Validation

Both files are RGBA PNGs at their exact requested dimensions. RGB values are restricted to the project palette, alpha is binary (0/255), and there are no external margins or baked shadows.

## Limitations

These are deliberately compact UI sprites. At 1× scale the ornamental rail details simplify into chunky pixels. The rack background is opaque inside its rectangular UI footprint.

## Generation prompt notes

Generated as crisp orthographic pixel-art fuel storage UI in a handmade xianxia sect style: charred dark wood, warm orange metal rim, six empty recesses, no text or inventory items. Output was reduced with nearest-neighbour sampling, remapped to the approved 32-colour palette, and alpha-thresholded.
