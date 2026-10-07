# Idle Grounds Islands 012 — Fishing biome

## Contents
- `island_fishing_ground_blob_32x32_47f.png` — canonical 47-frame blob geometry, repainted as wet pond bank.
- `island_fishing_ground_fill_32x32_3f.png` — three seamless moss, wet-sand and deep-water bank variants.
- `island_fishing_cliff_32x32_8f.png` — mossy blue-grey damp cliff strip in canonical order.
- `blob_mask_reference.json` — canonical bit/order reference.

## Usage
Use the Center blob lookup and cliff semantics. Frame 46 equals fill variant 0. Fill frames tile in both axes. Nearest-neighbour sampling only.

## Validation
Exact dimensions, palette, binary alpha, accepted blob silhouette, frame order, frame-46 equality, fill seams and ZIP integrity were checked.

## Limitations
This set describes the wet bank and coast. The separately requested four-frame pond-water animation remains outstanding.

## Generation prompt notes
Image generation supplied top-down pond-bank and side-view mossy cliff studies. Production processing remapped them to the exact palette and applied accepted Center geometry and mask order.
