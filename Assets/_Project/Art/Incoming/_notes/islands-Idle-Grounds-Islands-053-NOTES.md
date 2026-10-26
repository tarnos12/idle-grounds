# Idle Grounds — Islands 053

ART-SPEC v6.13 P1 coast repair. Contains only Farm, Grove and Mine `coast_edge_profile` replacements.

- 64×8 RGBA strips; two 32×8 frames: N/W/E overhang, then south rim.
- Rows 0–3 are the ragged top-down edge; rows 4–7 are opaque ground in every column.
- Farm: P21/P22/P23 only, no green. Grove: P08/P09/P10 only, no olive. Mine: P03/P04/P05 only.
- Binary alpha, P01–P32, exact 32-pixel repeat join, Point filtering, PPU 32, no compression or mipmaps.
- No side-on grass, mounds, boulders or battlement silhouettes.
- Fishing is intentionally deferred to the snowy §3.8 batch.
