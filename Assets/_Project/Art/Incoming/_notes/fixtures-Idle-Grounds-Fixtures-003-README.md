# Spirit Tree sparkle overlay

`fix_spirittree_sparkle_160x192_4f.png` is a 640x192 RGBA horizontal strip with four 160x192 cells. Overlay on the accepted `fix_spirittree.png` at the same position, scale and bottom-centre pivot. The tree itself is not included in the sprite.

Unity: Sprite Multiple; grid slice 160x192; PPU 32; Point filtering; Compression None; no mipmaps; sRGB; Clamp. Animate frames left to right at 8 fps in a loop. Use a normal alpha-blended sprite material and a sorting order immediately above the tree. Do not flip the overlay independently.

Art: drifting gold glints and a subtle canopy shimmer. Palette is P23 (#D9A45C), P24 (#F2C94C), P25 (#FFF0A0). Binary alpha is intentional and meets the stricter world-art rule. No smooth glow or gradients. Lower 40 rows are empty.

Production: generated through built-in ImageGen using the accepted tree from Drive Fixtures-001 as a positioning reference. Exported with nearest-neighbour sampling, palette remapping and a binary alpha mask. The preview composites the output over the unchanged accepted tree, for review only.

Limitations: a four-frame decorative shimmer, not a simulated particle system. Art and loop timing still need in-game review; automated tests verify format, sparsity, colours and layout, not aesthetic acceptance. This is a partial P2 delivery; the overall specification remains incomplete.
