# Idle Grounds — Sky Fix 003

Replacement sky batch responding to the latest integration feedback.

## Contents and usage
- `sky_gradient_512x1024.png`: accepted gradient with only the baked moon/haze removed; stretch to screen.
- `sky_moon_96x96.png`: separate round centre-pivot moon; position independently so it does not stretch.
- `sky_clouds_far/mid/near_1024x256.png`: replacement horizontal parallax layers. Repeat on X with nearest-neighbour sampling; far scrolls slowest, near fastest.

## Corrections
Cloud forms now come from three separate image-generation studies and vary in scale, spacing, height and silhouette. They use P05/P06/P07 plus restrained P28/P29 rim light, quantized four-step bottom alpha, and hard/dithered palette transitions. There are no constructed ellipse shadows, pasted rectangles, or flat base bands.

## Validation
See `validation.json`. Exact dimensions, approved palette, allowed alpha steps, exact horizontal boundary match, and ZIP integrity are checked.
