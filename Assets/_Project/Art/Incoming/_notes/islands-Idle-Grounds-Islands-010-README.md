# Idle Grounds Islands 010 — Farm biome

## Contents
- `island_farm_ground_blob_32x32_47f.png` — accepted 47-frame blob silhouette/mask order, repainted for Farm.
- `island_farm_ground_fill_32x32_3f.png` — three seamless flooded-paddy fill variants.
- `island_farm_cliff_32x32_8f.png` — terraced warm-earth cliff strip in the canonical 8-frame order.
- `blob_mask_reference.json` — canonical bit/order reference.

## Usage
Use the same blob-mask lookup and cliff frame semantics as Center. Frame 46 (mask 255) exactly equals fill variant 0. Fill variants tile in both axes. Nearest-neighbour sampling only.

## Validation
Exact dimensions, approved palette, binary alpha, accepted blob silhouette, canonical frame order, frame-46 equality, fill seams, and ZIP integrity were checked.

## Limitations
The paddy water is deliberately restrained at 32×32 so repeated terrain stays readable. Larger animated water remains a separate requested asset.

## Generation prompt notes
Image generation supplied top-down flooded rice-paddy and side-view terrace-earth source studies. Production processing remapped them to the exact project palette, created three seamless fill variants, preserved the accepted Center blob geometry/mask order, and retained the canonical cliff silhouettes.
