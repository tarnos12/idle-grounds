# Idle Grounds Islands 018 — Celestial Peak biome

## Contents
- `island_celestial_ground_blob_32x32_47f.png` — canonical 47-frame blob geometry, repainted as pale celestial marble.
- `island_celestial_ground_fill_32x32_3f.png` — three seamless calm marble variants with sparse inlay.
- `island_celestial_cliff_32x32_8f.png` — white marble cliff with cyan inlay and wisps in canonical order.
- `blob_mask_reference.json` — canonical bit/order reference.

## Usage
Use the Center blob lookup and cliff semantics. Frame 46 equals fill variant 0. Fill frames tile in both axes. Nearest-neighbour sampling only.

## Validation
Exact dimensions, palette, binary alpha, accepted blob and cliff silhouettes, frame order, frame-46 equality, fill seams, <=8% detail density and ZIP integrity were checked.

## Limitations
This set provides static Celestial Peak ground and cliffs. Larger animated cloud or wisp effects are separate assets.

## Generation prompt notes
Image generation supplied top-down pale-marble and side-view celestial-cliff studies. Production processing remapped them to the exact palette, capped fill detail, and applied accepted Center geometry and mask order.
