# Idle Grounds — Sky Cloud Parallax Batch 002

Three horizontally tileable 1024×256 RGBA pixel-art cloud strips.

## Usage

Layer in this order above the sky gradient: `far`, `mid`, `near`. Scroll the far layer slowest and near layer fastest. Use nearest-neighbor sampling and repeat on X only.

## Validation

- Exact 1024×256 dimensions
- Binary alpha only (0/255)
- Approved ART-SPEC palette only
- First and last columns match exactly and contain opaque pixels

See `validation.json` for per-file machine-readable results.

## Limitations

Procedurally normalized from separate image-generation studies to guarantee exact palette, hard alpha, pixel alignment, and mathematically matching horizontal seams. No soft antialiasing is retained.
