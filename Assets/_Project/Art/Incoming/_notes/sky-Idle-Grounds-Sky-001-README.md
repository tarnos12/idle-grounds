# Idle Grounds Sky 001 — Vertical gradient

Contains `sky_gradient_512x1024.png`, the opaque stretchable background layer. It transitions from ink/night at the top through deep-sea and water blue to qi cyan and mist white at the cloud-sea horizon. Seven faint stars and a pale upper-left sun haze provide restrained landmarks.

The transition uses broad exact-palette bands with 4×4 ordered dithering; no smooth RGB gradient or off-palette color is present. Import with Point filtering, no compression and no mipmaps. Stretch to cover the camera background behind all parallax layers.

Validation confirms exact 512×1024 dimensions, full opacity and project-palette compliance. See `validation.json`.

Built-in image generation supplied the visual direction. The final delivery was deterministically rebuilt with exact palette bands and hard dithering.

Limitations: cloud banks are intentionally absent because they are separate `sky_clouds_*` parallax assets. Overall ART-SPEC remains incomplete.

Source ART-SPEC modified: `2026-10-05T01:46:54.136Z`.
