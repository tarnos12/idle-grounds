# Idle Grounds Islands 015 — Farm ground fix

Replaces only the rejected Farm ground assets from Islands-010:
- `island_farm_ground_blob_32x32_47f.png`
- `island_farm_ground_fill_32x32_3f.png`

The accepted `island_farm_cliff_32x32_8f.png` is intentionally not repeated.

## Usage
Use the canonical Center blob lookup. Frame 46 equals fill variant 0. Fill frames tile in both axes. Use nearest-neighbour sampling.

## Validation
Exact dimensions, approved palette, binary alpha, accepted silhouette and mask order, frame-46 equality, fill seams, dominant-base ratio (>=92%), detail density (<=8%), and ZIP integrity were checked.

## Limitations
Large paddy-water and field features are excluded from the base fill and belong in zone overlays.

## Generation prompt notes
Image generation supplied a deliberately calm tan-soil study. Production processing quantized it to P23/P22/P15, capped irregular detail, enforced seamless borders, and applied accepted Center geometry.
