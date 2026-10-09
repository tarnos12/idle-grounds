# Idle Grounds UI Batch 009

+Unlock frame, checkbox strip, scrollbar, scrim tile, and three cursor states.
+
+## Unity usage
+- Unlock frame: Sprite Single, 9-slice 12 px; states via tint.
+- Checkbox: Sprite Multiple, grid 16x16, frames off/on.
+- Scrollbar: Sprite Single, 9-slice 3 px.
+- Scrim: Repeat tile; checker coverage provides 50% P01 using binary alpha.
+- Cursor hotspots: open hand (12,4), closed hand (12,8), target (16,16).
+- PPU 32, Point, Compression None, no mipmaps.
+
+## Validation
+Exact dimensions/frame strips, approved palette, RGBA 8-bit, binary alpha, repeat-safe scrim edges.
+
+## Limitations
+Scrollbar combines track and handle as requested; Unity may slice or mask it for variable lengths.
+
+## Generation prompt notes
+An OpenAI-generated source sheet established the xianxia jade/gold/ink direction and cursor silhouettes. Final sprites were reconstructed at native resolution and palette-quantized.
+