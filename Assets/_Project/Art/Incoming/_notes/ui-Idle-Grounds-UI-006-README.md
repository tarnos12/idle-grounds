# Idle Grounds UI Batch 006

+Assets: four primary button states, four-frame close button strip, jade toggle-on and cinnabar danger toggle.
+
+## Unity usage
+- Primary/toggle sprites: Sprite Single, PPU 32, Point, no compression, no mipmaps; 9-slice borders L/R/T/B = 8 px.
+- Close strip: Sprite Multiple, grid 20x20, four frames ordered normal, hover, pressed, disabled.
+- All pivots centered; sRGB; Wrap Clamp.
+
+## Validation
+Exact dimensions; RGBA 8-bit; approved 32-color art-bible palette only; binary alpha; stable 8 px corner regions and calm stretchable centers.
+
+## Limitations
+Button faces intentionally contain no baked text. Labels and state tinting remain Unity UI elements.
+
+## Generation notes
+An OpenAI image-generation source sheet established the xianxia lacquer/jade/gold direction. Final sprites were reconstructed at native resolution, palette-quantized, and state-aligned to satisfy production constraints.
+