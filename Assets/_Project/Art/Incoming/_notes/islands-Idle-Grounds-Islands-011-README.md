# Idle Grounds Islands 011 — Mine biome

## Contents
- `island_mine_ground_blob_32x32_47f.png` — accepted 47-frame blob silhouette/mask order, repainted as angular grey rock and gravel.
- `island_mine_ground_fill_32x32_3f.png` — three seamless rock/gravel fill variants with sparse ore flecks.
- `island_mine_cliff_32x32_8f.png` — striated mine cliff strip in the canonical 8-frame order.
- `blob_mask_reference.json` — canonical bit/order reference.

## Usage
Use the same blob lookup and cliff semantics as Center. Frame 46 equals fill variant 0. Fill frames tile in both axes. Nearest-neighbour sampling only.

## Validation
Exact dimensions, approved palette, binary alpha, accepted blob silhouette, canonical frame order, frame-46 equality, fill seams, and ZIP integrity were checked.

## Limitations
Ore glints are intentionally sparse to keep repeated terrain calm. Resource nodes should carry stronger ore colour.

## Generation prompt notes
Image generation supplied top-down fractured mine rock/gravel and side-view striated cliff studies. Production processing remapped them to the project palette and applied the accepted Center geometry and mask order.
