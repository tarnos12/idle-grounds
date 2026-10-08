Idle Grounds — Buildings batch 008

Files
- bld_brewery.png / bld_brewery_working_96x128_4f.png
- bld_cauldron.png / bld_cauldron_working_96x128_4f.png
- bld_jade_carver.png / bld_jade_carver_working_96x128_4f.png

Usage
- Static files: Sprite Mode Single, bottom-centre pivot.
- Working files: Sprite Mode Multiple, horizontal grid 96x128, four frames, 8-12 fps looping.
- PPU 32, Point filter, compression None, no mipmaps.

Validation
- Exact dimensions and frame layout.
- Exact ART-SPEC 32-colour palette.
- RGBA binary alpha only, transparent side margins, bottom ground contact.

Limitations
- Working loops preserve the static silhouette and use deliberately restrained pixel effects:
  brewery bubbles/steam, cauldron vapour curl, jade wheel/chips.
- Frame 1 is the static state with the first subtle motion cue.
