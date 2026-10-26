# Idle Grounds — Islands 052

Native 32 px rounded-coast continuation for ART-SPEC v6.11.

- `coast_edge_profile`: frame 0 north grass overhang; frame 1 south rim.
- `sidewall_round`: W-A, E-A, W-B, E-B; each face remains readable at 11 px width and tiles vertically.
- Palette: P01–P32 only. Alpha: binary. PPU 32, Point, no compression, no mipmaps.
- Source pixels come from the accepted Islands-051 rounded cliff walls and Islands-038 sidewalls; pixels were cropped/recombined without scaling or rotation.
- The obsolete `*_diag` files are not included.

Known limitation: the October 12 build-time straight-wall end/narrow-wall compositions require an explicit new key/frame map before a safe filename-compatible delivery. They remain open and are not claimed complete by this package.
