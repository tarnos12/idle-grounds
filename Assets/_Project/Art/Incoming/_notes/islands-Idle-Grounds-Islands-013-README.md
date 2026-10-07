# Idle Grounds Islands 013 — Volcano biome

## Contents
- `island_volcano_ground_blob_32x32_47f.png` — canonical 47-frame blob geometry, repainted as fractured obsidian.
- `island_volcano_ground_fill_32x32_3f.png` — three seamless obsidian/lava-crack variants.
- `island_volcano_cliff_32x32_8f.png` — dark volcanic cliff strip in canonical order.
- `blob_mask_reference.json` — canonical bit/order reference.

## Usage
Use the Center blob lookup and cliff semantics. Frame 46 equals fill variant 0. Fill frames tile in both axes. Nearest-neighbour sampling only.

## Validation
Exact dimensions, palette, binary alpha, accepted blob and cliff silhouettes, frame order, frame-46 equality, fill seams and ZIP integrity were checked.

## Limitations
This set provides static volcanic ground and cliffs. Animated lava hazards or ambient effects are separate assets.

## Generation prompt notes
Image generation supplied top-down obsidian and side-view volcanic cliff studies. Production processing remapped them to the exact palette and applied accepted Center geometry and mask order.
