# Idle Grounds Islands 016 — Fishing ground fix

Replaces only the rejected Fishing ground assets from Islands-012:
- `island_fishing_ground_blob_32x32_47f.png`
- `island_fishing_ground_fill_32x32_3f.png`

The accepted `island_fishing_cliff_32x32_8f.png` is intentionally not repeated.

## Usage
Use the canonical Center blob lookup. Frame 46 equals fill variant 0. Fill frames tile in both axes. Use nearest-neighbour sampling.

## Validation
Exact dimensions, approved palette, binary alpha, accepted silhouette and mask order, frame-46 equality, fill seams, dominant-base ratio (>=92%), detail density (<=8%), and ZIP integrity were checked.

## Limitations
Large ponds, shore features and water animation are excluded from the base fill and belong in zone/animation overlays.

## Generation prompt notes
Image generation supplied a deliberately calm muted-blue ground study. Production processing quantized it to P27/P26/P28, capped irregular detail, enforced seamless borders, and applied accepted Center geometry.
