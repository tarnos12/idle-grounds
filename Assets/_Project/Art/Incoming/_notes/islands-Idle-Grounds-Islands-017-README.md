# Idle Grounds Islands 017 — Spirit Grove ground fix

Replaces only the rejected Spirit Grove ground assets from Islands-014:
- `island_grove_ground_blob_32x32_47f.png`
- `island_grove_ground_fill_32x32_3f.png`

The accepted `island_grove_cliff_32x32_8f.png` is intentionally not repeated.

## Usage
Use the canonical Center blob lookup. Frame 46 equals fill variant 0. Fill frames tile in both axes. Use nearest-neighbour sampling.

## Validation
Exact dimensions, approved palette, binary alpha, accepted silhouette and mask order, frame-46 equality, fill seams, dominant-base ratio (>=92%), detail density (<=8%), and ZIP integrity were checked.

## Limitations
Large moss patches, bamboo and foliage features are excluded from the base fill and belong in zone/decor overlays.

## Generation prompt notes
Image generation supplied a deliberately calm muted-jade ground study. Production processing quantized it to P09/P08, capped irregular detail, enforced seamless borders, and applied accepted Center geometry.
