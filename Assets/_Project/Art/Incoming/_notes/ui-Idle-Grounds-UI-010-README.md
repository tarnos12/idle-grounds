# Idle Grounds UI 010

Altar upgrade-tree base, three hollow state borders, selected corner brackets and tileable connector; corrected flat-alpha scrim; distinct scrollbar handle.

## Unity usage
PPU 32, Point filter, no compression or mipmaps, sRGB, centre pivot. All sprites fixed-size except scrollbar handle (border 3,3,3,3). Base 52px; overlay state ring 56px, brackets 72px. Keep centred at integer UI pixels. Scrim uses P01 at alpha 140/255 (54.9%), Sprite/Default with white UI tint; stretch or Repeat without checker pattern. It replaces the rejected UI-009 scrim. Connector repeats horizontally; rotate in Unity for other directions.

## Validation
Eight exact-sized RGBA PNGs, approved palette only. Binary alpha on seven assets; uniform partial alpha only on scrim. Hollow border interiors, horizontal connector continuity checked. VALIDATION.json records results.

## Generation and limitations
Each asset used a separate built-in image generation call. Sources were larger than requested and included off-palette shading. Final native grid layout and colour cleanup reproduces the generated frame direction under the supplied exact pixel rules. Prompt notes and source paths in GENERATION-NOTES.json. In-game visual integration is not tested; the fixed frames must be centred, and ring colour should use white UI tint to preserve the supplied palette.
