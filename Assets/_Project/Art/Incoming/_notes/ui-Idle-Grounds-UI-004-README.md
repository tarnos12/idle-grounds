# Idle Grounds UI 004 — Scroll Panel Fix

## Contents

- `ui_panel_scroll_64x64.png` — replacement aged-rice-paper scroll panel.

This file replaces the same key delivered in UI-002.

## Unity usage

Import at PPU 32, Point filtering, Compression None, no mipmaps, sRGB and Sprite Mode Single. Set Sprite Border to L20, R20, T18, B18 and use Image Type Sliced.

## Validation

The replacement is exactly 64×64 RGBA, uses only approved palette colours and binary alpha. Its centre remains calm and low contrast for dark text, while stains and dither are concentrated near the border. The rods are dark brown.

## Limitations

The light rice-paper centre is intended for dark text. Avoid light body text on this panel.

## Generation prompt notes

Edited from the rejected UI-002 scroll layout. Bright gold/yellow parchment was replaced by P06/P07 cream-grey rice paper with sparse P20/P21 staining and darker wooden rods. The output was trimmed, nearest-neighbour reduced, palette-remapped and alpha-thresholded.
