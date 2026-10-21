# Pictures for the owner question "building art density" (docs/Design/questions.json, id art_density).
#   powershell -ExecutionPolicy Bypass -File tools/art-ref/density-mockups.ps1 -Shots <dir with g01/g03 captures>
# Inputs: in-game captures from tools/bg/shot.sh (g01_cluster_south_zoom2.png = today's zoom 2, g03_cluster_south_int2x.png
# = the same view at exactly 2 screen px per art pixel), the artist's own high-res generation of the Spring
# (Art/Incoming/_notes/fixtures-Idle-Grounds-Fixtures-004-generation-preview.png) and the delivered sprites.
# Outputs: docs/Design/question-images/art-density-{1,2,3}.png and docs/art/reference/buildings-ingame.png
param([Parameter(Mandatory = $true)][string]$Shots)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path (Join-Path $PSScriptRoot "PixelAudit.cs") -ReferencedAssemblies System.Drawing
$inc = Join-Path $root "Assets\_Project\Art\Incoming"
$qi = Join-Path $root "docs\Design\question-images"
$white = 0xFFE8ECF3; $grey = 0xFFB7BCCD; $gold = 0xFFF2C94C; $bg = 0xFF1B1F2B

# ---- 1. today in game (zoom 2 = 1.71 screen px per art pixel) vs the same art at a whole 2 px ----------------------
$a = [PixelAudit]::NewSheet(1100, 560, $bg)
[PixelAudit]::Text($a, "Same Paper Mill, same art. Left: zoom 2 today (1.71 screen px per art pixel). Right: exactly 2 px per art pixel.", 12, 8, 15, $white, $true)
$left = New-Object System.Drawing.Bitmap 240, 200
$g01 = [System.Drawing.Bitmap]::new((Join-Path $Shots "g01_cluster_south_zoom2.png"))
$gr = [System.Drawing.Graphics]::FromImage($left); $gr.DrawImage($g01, (New-Object System.Drawing.Rectangle 0, 0, 240, 200), (New-Object System.Drawing.Rectangle 700, 290, 240, 200), [System.Drawing.GraphicsUnit]::Pixel); $gr.Dispose(); $g01.Dispose()
$right = New-Object System.Drawing.Bitmap 280, 233
$g03 = [System.Drawing.Bitmap]::new((Join-Path $Shots "g03_cluster_south_int2x.png"))
$gr = [System.Drawing.Graphics]::FromImage($right); $gr.DrawImage($g03, (New-Object System.Drawing.Rectangle 0, 0, 280, 233), (New-Object System.Drawing.Rectangle 657, 248, 280, 233), [System.Drawing.GraphicsUnit]::Pixel); $gr.Dispose(); $g03.Dispose()
[PixelAudit]::DrawBmp($a, $left, 12, 36, 2)
[PixelAudit]::DrawBmp($a, $right, 520, 36, 2)
[PixelAudit]::Text($a, "today: art pixels 1, 2 or 3 px wide (uneven)", 12, 446, 14, $grey, $false)
[PixelAudit]::Text($a, "crisp zoom step (already picked): every art pixel 2x2", 520, 510, 14, $grey, $false)
[PixelAudit]::Text($a, "(both crops enlarged 2x so the pixels can be seen)", 12, 532, 13, $grey, $false)
[PixelAudit]::Save($a, (Join-Path $qi "art-density-1.png"))
New-Item -ItemType Directory -Force (Join-Path $root "docs\art\reference") | Out-Null
[PixelAudit]::Save($a, (Join-Path $root "docs\art\reference\buildings-ingame.png")); $a.Dispose()

# ---- 2. density ladder from the artist's own high-res generation (Spring, a 2x2 object) ---------------------------
$gen = Join-Path $inc "_notes\fixtures-Idle-Grounds-Fixtures-004-generation-preview.png"
$rx = 27; $ry = 146; $rw = 376; $rh = 470          # frame 1 of the generation (~5.9 generation px per art pixel)
$half = [PixelAudit]::BoxDown($gen, $rx, $ry, $rw, $rh, 32, 40, $true)
$today = [PixelAudit]::Frame((Join-Path $inc "fixtures\fix_spring.png"), 0, 0)
$dbl = [PixelAudit]::BoxDown($gen, $rx, $ry, $rw, $rh, 128, 160, $true)
$paint = [PixelAudit]::Smooth($gen, $rx, $ry, $rw, $rh, 256, 320)
$b = [PixelAudit]::NewSheet(1112, 430, $bg)
[PixelAudit]::Text($b, "One 2x2-cell object at the same size on screen, four art densities (made from the artist's own high-res drawing)", 12, 8, 15, $white, $true)
$cap = @("16 px per cell (half)", "32 px per cell = today", "64 px per cell (double)", "hi-res painted (no pixels)")
$sub = @("for scale only", "the delivered Spring", "same drawing, 4x the pixels (auto, uncleaned)", "smooth, not pixel art")
$imgs = @($half, $today, $dbl, $paint); $sc = @(8, 4, 2, 1)
for ($i = 0; $i -lt 4; $i++) {
    $x = 16 + $i * 274
    [PixelAudit]::Rect($b, $x - 2, 38, 260, 324, 0xFF2E3345, $true)
    [PixelAudit]::DrawBmp($b, $imgs[$i], $x, 40, $sc[$i])
    [PixelAudit]::Text($b, $cap[$i], $x, 372, 15, $(if ($i -eq 2) { $gold } else { $white }), $true)
    [PixelAudit]::Text($b, $sub[$i], $x, 394, 13, $grey, $false)
}
[PixelAudit]::Save($b, (Join-Path $qi "art-density-2.png")); $b.Dispose()

# ---- 3. mixing: a double-density object among today's ground and nodes (zoom 2 crisp step) -------------------------
function Scene([System.Drawing.Bitmap]$spring, [double]$springScale) {
    # closest crisp zoom step: 4 screen px per 32-px art pixel; world 4 x 3 cells
    $s = [PixelAudit]::NewSheet(512, 384, 0xFF000000)
    $ground = [PixelAudit]::Tiles((Join-Path $inc "islands/island_center_ground_fill_32x32_3f.png"), 32, 3, 4, 3, 7)
    [PixelAudit]::DrawBmp($s, $ground, 0, 0, 4)
    $bush = [PixelAudit]::Frame((Join-Path $inc "nodes/node_bush_32x48_2f.png"), 32, 0)
    [PixelAudit]::DrawBmp($s, $spring, 24, 40, $springScale)
    [PixelAudit]::DrawBmp($s, $bush, 330, 170, 4)
    return $s
}
$c = [PixelAudit]::NewSheet(1072, 470, $bg)
[PixelAudit]::Text($c, "Mixing densities at the closest crisp zoom (4 screen px per today's art pixel): Spring next to a bush node on grass", 12, 8, 15, $white, $true)
$l = Scene $today 4; $r = Scene $dbl 2
[PixelAudit]::DrawBmp($c, $l, 16, 40, 1); [PixelAudit]::DrawBmp($c, $r, 544, 40, 1)
[PixelAudit]::Text($c, "everything 32 px per cell (today)", 16, 432, 15, $white, $true)
[PixelAudit]::Text($c, "Spring at 64 px per cell, bush and grass at 32", 544, 432, 15, $gold, $true)
[PixelAudit]::Save($c, (Join-Path $qi "art-density-3.png")); $c.Dispose()
Get-ChildItem (Join-Path $qi "art-density-*.png"), (Join-Path $root "docs\art\reference\*.png") | ForEach-Object { "{0}  {1} KB" -f $_.FullName, [int]($_.Length / 1024) }
