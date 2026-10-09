# Small icon button

Quest-minimise icon-button background requested in the §5 narrative feedback. No size was specified there: 20x20 is the explicit delivery assumption, matching the close-button footprint. Four horizontal states normal/hover/pressed/disabled. Add the requested glyph separately in Unity. Fixed size, no 9-slice. PPU32, Point, no compression/mipmaps, sRGB, Multiple 20x20, centre pivot.

Built-in image generation supplied the four-state design; native pixel reconstruction removed gradients and imposed exact stepped corners, flat colours and binary alpha. Prompt retained in GENERATION-NOTES.json. Validated RGBA8, exact dimensions, project palette, binary alpha, four distinct states. No in-game integration test.
