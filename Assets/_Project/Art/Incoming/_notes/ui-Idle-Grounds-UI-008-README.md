# Idle Grounds UI Batch 008

+Four status pills, progress track and three fills, two-frame HP pip, and count badge.
+
+## Unity usage
+- Pills: Sprite Single, 9-slice 8 px.
+- Progress assets: Sprite Single, 9-slice 3 px; layer fill over track.
+- HP pip: Sprite Multiple, grid 8x8, two frames (filled red, empty grey).
+- Count badge: Sprite Single, 9-slice 6 px; render count text separately.
+- PPU 32, Point, Compression None, no mipmaps, centre pivot.
+
+## Validation
+Exact dimensions/frame strips, RGBA 8-bit, approved palette, binary alpha, calm stretchable centres.
+
+## Limitations
+No labels or numbers are baked in; Unity supplies text and sizing.
+
+## Generation prompt notes
+An OpenAI-generated source sheet established the compact xianxia lacquer/jade/gold forms. Final sprites were reconstructed at native pixel resolution and palette-quantized for exact production constraints.
+