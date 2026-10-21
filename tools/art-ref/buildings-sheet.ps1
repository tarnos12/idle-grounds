# Building reference sheet + pixel metrics for all 26 buildings (+ Spirit Bridge).
#   powershell -ExecutionPolicy Bypass -File tools/art-ref/buildings-sheet.ps1 [-Out docs/art/reference/buildings-sheet.png] [-Scale 2]
# Reads the static sprites in Assets/_Project/Art/Incoming/buildings, draws each at an integer scale on the calm
# ground colour with its footprint outlined, a verdict tag (REF / CLEAN / REDRAW) and its metrics, and prints the
# metrics as a table. Verdicts are the art director's audit (ART-SPEC section 3.9); edit $verdict below when they change.
param(
    [string]$Out = "",
    [int]$Scale = 2,
    [switch]$MetricsOnly
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Out) { $Out = Join-Path $root "docs\art\reference\buildings-sheet.png" }
Add-Type -Path (Join-Path $PSScriptRoot "PixelAudit.cs") -ReferencedAssemblies System.Drawing
$dir = Join-Path $root "Assets\_Project\Art\Incoming\buildings"

# key, footprint label, row group
$rows = @(
    @("center|5x5", "dragon|5x5", "ascension_gate|5x5", "kiln|3x5", "forge|3x5", "pill_furnace|3x5", "star_anvil|3x5"),
    @("paper_mill|3x3", "storehouse|3x3", "loom|3x3", "mill|3x3", "infusion_array|3x3", "workbench|3x3", "talisman_atelier|3x3", "meditation_pavilion|3x3"),
    @("brewery|3x3", "cauldron|3x3", "jade_carver|3x3", "dragon_shrine|3x3", "charcoal_pit|3x3", "algae_farm|3x3", "herb_garden|3x3"),
    @("gathering_stone|1x1", "wisp_lantern|1x1", "warding_seal|1x1", "furnace_spirit|1x1", "spirit_bridge_unpaired|2x1")
)
# Audit verdicts (see ART-SPEC 3.9): REF = target look, CLEAN = keep design + clean-up pass, REDRAW = new drawing
$verdict = @{
    center = "REF"; ascension_gate = "REF"; forge = "REF"; paper_mill = "REF"; storehouse = "REF"; loom = "REF"; mill = "REF"; meditation_pavilion = "REF"
    dragon = "CLEAN"; pill_furnace = "CLEAN"; infusion_array = "CLEAN"; talisman_atelier = "CLEAN"; dragon_shrine = "CLEAN"; charcoal_pit = "CLEAN"
    gathering_stone = "CLEAN"; wisp_lantern = "CLEAN"; warding_seal = "CLEAN"; furnace_spirit = "CLEAN"; spirit_bridge_unpaired = "CLEAN"
    kiln = "REDRAW"; workbench = "REDRAW"; brewery = "REDRAW"; cauldron = "REDRAW"; jade_carver = "REDRAW"; star_anvil = "REDRAW"
    algae_farm = "REDRAW"; herb_garden = "REDRAW"
}
$tagColour = @{ REF = 0xFF5FCF9C; CLEAN = 0xFFF2C94C; REDRAW = 0xFFEE6A4A }

$metrics = @{}
foreach ($r in $rows) { foreach ($e in $r) {
    $k = $e.Split("|")[0]; $m = @{}
    foreach ($kv in ([PixelAudit]::Analyze((Join-Path $dir "bld_$k.png"), 0)).Split(";")) { $p = $kv.Split("="); $m[$p[0]] = $p[1] }
    $metrics[$k] = $m
} }
"{0,-24} {1,-8} {2,7} {3,7} {4,7} {5,8} {6,8} {7}" -f "key", "canvas", "colours", "noise%", "runLen", "outline%", "fill%", "verdict"
foreach ($r in $rows) { foreach ($e in $r) { $k = $e.Split("|")[0]; $m = $metrics[$k]
    "{0,-24} {1,-8} {2,7} {3,7} {4,7} {5,8} {6,8} {7}" -f $k, "$($m.w)x$($m.h)", $m.colours, $m.noisePct, $m.runLen, $m.darkOutlinePct, $m.fillPct, $verdict[$k]
} }
if ($MetricsOnly) { return }

$S = $Scale; $gap = 12 * $S; $label = 34; $margin = 16; $title = 54
# layout pass
$rowH = @(); $sheetW = 0
foreach ($r in $rows) {
    $w = $margin; $h = 0
    foreach ($e in $r) { $m = $metrics[$e.Split("|")[0]]; $w += [Math]::Max([int]$m.w * $S, 150) + $gap; $h = [Math]::Max($h, [int]$m.h * $S) }
    $rowH += ($h + $label + $gap); $sheetW = [Math]::Max($sheetW, $w)
}
$sheetH = $title + ($rowH | Measure-Object -Sum).Sum + 40
$ground = 0xFF3E5F2C   # calm ground (P13 one step darker, see owner pick vp_mood) for the background
$sheet = [PixelAudit]::NewSheet($sheetW, $sheetH, $ground)
[PixelAudit]::Text($sheet, "Idle Grounds - building reference sheet (static sprites, ${S}x, PPU 32, 1 cell = $(32*$S) px here)", $margin, 10, 18, 0xFFE8ECF3, $true)
[PixelAudit]::Text($sheet, "Tag: REF = target look   CLEAN = keep design, clean-up pass   REDRAW = new drawing.   Thin box = footprint.   n = 1-px noise %, c = colours", $margin, 32, 13, 0xFFB7BCCD, $false)
$y = $title
for ($ri = 0; $ri -lt $rows.Count; $ri++) {
    $r = $rows[$ri]; $x = $margin; $base = $y + $rowH[$ri] - $label - $gap
    foreach ($e in $r) {
        $k, $fp = $e.Split("|"); $m = $metrics[$k]; $w = [int]$m.w * $S; $h = [int]$m.h * $S
        $fw = [int]$fp.Split("x")[0] * 32 * $S; $fh = [int]$fp.Split("x")[1] * 32 * $S
        if ($k -eq "spirit_bridge_unpaired") { $fh = 32 * $S }
        [PixelAudit]::Rect($sheet, $x + [int](($w - $fw) / 2), $base - $fh, $fw, $fh, 0x60E8ECF3, $false)
        [PixelAudit]::DrawSprite($sheet, (Join-Path $dir "bld_$k.png"), 0, $x, $base, $S)
        $v = $verdict[$k]; [PixelAudit]::Rect($sheet, $x, $base + 3, 8, 8, $tagColour[$v], $true)
        $name = $k -replace "_unpaired", ""
        [PixelAudit]::Text($sheet, "$name", $x + 11, $base + 1, 12, 0xFFE8ECF3, $true)
        [PixelAudit]::Text($sheet, "$v n$($m.noisePct) c$($m.colours)", $x + 11, $base + 15, 11, $tagColour[$v], $false)
        $x += [Math]::Max($w, 150) + $gap
    }
    $y += $rowH[$ri]
}
New-Item -ItemType Directory -Force (Split-Path -Parent $Out) | Out-Null
[PixelAudit]::Save($sheet, $Out); $sheet.Dispose()
"sheet -> $Out ($sheetW x $sheetH)"
