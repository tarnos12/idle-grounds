# Idle Grounds Islands 009

## Contents
- `island_underside_fill_32x32_3f.png` — three seamless dark earth/rock fill variants in a horizontal strip.

## Usage
Each 32×32 frame tiles independently in both axes. Randomize the three frames across the inverted-mountain body behind the accepted hanging rock and root decorations. Use nearest-neighbour sampling.

## Validation
- Exact sheet size: 96×32; 3 horizontal 32×32 frames.
- Fully opaque RGBA.
- Palette restricted to P02–P04 and P20–P21.
- Opposite edges match pixel-for-pixel on both axes for every frame.
- All three frames are distinct.

## Limitations
This is a deliberately low-contrast body fill. Large focal rocks, roots, veins and silhouettes remain the job of the accepted overlay decorations.

## Generation prompt notes
Image generation supplied dark compact-earth and layered-rock source material. Production normalization selected three distinct regions, reduced them to 32×32, remapped every pixel to the required five colours, and reconciled opposite-edge bands for two-axis tiling.
