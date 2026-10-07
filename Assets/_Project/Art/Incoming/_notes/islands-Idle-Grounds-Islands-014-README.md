# Idle Grounds Islands 014 — Spirit Grove biome

## Contents
- `island_grove_ground_blob_32x32_47f.png` — canonical 47-frame blob geometry, repainted as mossy forest floor.
- `island_grove_ground_fill_32x32_3f.png` — three seamless dark-jade moss variants.
- `island_grove_cliff_32x32_8f.png` — moss-draped cliff strip with hanging roots in canonical order.
- `blob_mask_reference.json` — canonical bit/order reference.

## Usage
Use the Center blob lookup and cliff semantics. Frame 46 equals fill variant 0. Fill frames tile in both axes. Nearest-neighbour sampling only.

## Validation
Exact dimensions, palette, binary alpha, accepted blob and cliff silhouettes, frame order, frame-46 equality, fill seams and ZIP integrity were checked.

## Limitations
This set provides static Spirit Grove ground and cliffs. Animated wisps or bamboo movement are separate assets.

## Generation prompt notes
Image generation supplied top-down mossy forest-floor and side-view rooted cliff studies. Production processing remapped them to the exact palette and applied accepted Center geometry and mask order.
