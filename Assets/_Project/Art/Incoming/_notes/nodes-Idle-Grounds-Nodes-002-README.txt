Idle Grounds — Farm resource nodes

Contains node_crop.png (96x112) and node_cotton.png (64x80).

Unity import: PPU 32, Point filter, Compression None, no mipmaps, Sprite Mode Single, Clamp, sRGB, bottom-centre pivot. Both are static P2 world nodes with binary transparency.

The rice node depicts a flooded ripe paddy with golden stalks and earth bunds. The cotton node depicts a dense patch of green plants with mature white bolls.

Built-in ImageGen generated one source per asset. ImageMagick performed native-size point export, exact 32-colour palette mapping and binary-alpha normalization. Full prompts are in prompts.json.

Limitations: dense foliage necessarily simplifies at gameplay zoom; verify collision footprint and visual density in Unity. No harvested-state variants were requested.
