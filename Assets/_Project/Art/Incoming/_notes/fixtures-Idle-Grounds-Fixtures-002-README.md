# Idle Grounds Fixtures 002

## Contents
- `deco_tree_32x48_3f.png` — three static decorative tree variants in a horizontal 3-frame strip; each frame is 32×48 px.

## Usage
Select frame 0, 1, or 2 to vary the decorative tree ring around islands. Pivot at bottom-centre. Nearest-neighbour sampling only.

## Validation
- Sheet: 96×48 RGBA; 3 horizontal frames of 32×48.
- Binary alpha only (0/255).
- Every opaque pixel uses the ART-SPEC 32-colour palette.
- Full trees remain inside their cells with transparent gutters.

## Limitations
The source generation was larger than final resolution. It was downsampled, hard-alpha thresholded, and remapped to the exact project palette; very fine branch detail is intentionally simplified at 32×48.

## Generation prompt notes
Generated as three distinct cultivation-fantasy decorative trees: rounded jade broadleaf, compact dark round-canopy, and elegant small pine; transparent background; crisp pixel art; no labels, dividers, ground tiles, or extra objects. Final production normalization enforces dimensions, binary alpha, and palette.
