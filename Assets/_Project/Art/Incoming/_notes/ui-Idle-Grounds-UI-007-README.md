# Idle Grounds UI Batch 007

+Four build-menu cards, three item icon frames, and two recipe-picker cells.
+
+## Unity usage
+- Cards: Sprite Single, centre pivot, PPU 32, Point, no compression/mipmaps, Wrap Clamp; 9-slice borders 10 px.
+- Icon frames and recipe cells are fixed-size Sprite Single assets. Place item icons on a separate child Image.
+
+## Validation
+Exact dimensions, RGBA 8-bit, approved palette, binary alpha, stable card corners, calm centres.
+
+## Limitations
+No item imagery or text is baked into the frames. Dim-card translucency should be applied by Unity tint/CanvasGroup; the source remains binary alpha.
+
+## Generation prompt notes
+An OpenAI image-generation source sheet established the xianxia ink-lacquer, jade and restrained-gold direction. Final assets were reconstructed at native pixel resolution and palette-quantized for exact production constraints.
+