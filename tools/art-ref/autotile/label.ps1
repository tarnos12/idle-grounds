param([string]$dir)
Add-Type -AssemblyName System.Drawing
$layout = @(@(10,13,12,4),@(14,15,7,9),@(3,11,5,2),@(8,6,1,0))
function Code($i){ $nw=$i -band 1; $ne=($i -shr 1) -band 1; $sw=($i -shr 2) -band 1; $se=($i -shr 3) -band 1; return "$nw$ne`n$sw$se" }
$src = [System.Drawing.Bitmap]::FromFile("$dir\autotile_wang16_guide_128x160.png")
$S = 6; $pad = 40
$W = 128*$S + $pad*2; $H = 160*$S + $pad*2 + 130
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255,40,42,54))
# checker for transparent
$c1=[System.Drawing.Color]::FromArgb(255,90,90,100); $c2=[System.Drawing.Color]::FromArgb(255,70,70,80)
for($y=0;$y -lt 160*$S;$y+=12){ for($x=0;$x -lt 128*$S;$x+=12){ $c= if((($x+$y)/12)%2 -eq 0){$c1}else{$c2}; $b=New-Object System.Drawing.SolidBrush $c; $g.FillRectangle($b,$pad+$x,$pad+$y,12,12); $b.Dispose() } }
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($src, (New-Object System.Drawing.Rectangle $pad,$pad,(128*$S),(160*$S)), 0,0,128,160, [System.Drawing.GraphicsUnit]::Pixel)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255,200,50,43)), 2
for($i=0;$i -le 4;$i++){ $g.DrawLine($pen,$pad+$i*32*$S,$pad,$pad+$i*32*$S,$pad+160*$S); }
for($i=0;$i -le 5;$i++){ $g.DrawLine($pen,$pad,$pad+$i*32*$S,$pad+128*$S,$pad+$i*32*$S); }
$f = New-Object System.Drawing.Font "Consolas", 13, ([System.Drawing.FontStyle]::Bold)
$fs = New-Object System.Drawing.Font "Consolas", 11
$white=[System.Drawing.Brushes]::White; $gold=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,242,201,76))
$bg=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200,13,15,20))
for($r=0;$r -lt 4;$r++){ for($k=0;$k -lt 4;$k++){ $i=$layout[$r][$k]; $x=$pad+$k*32*$S+4; $y=$pad+$r*32*$S+4
  $g.FillRectangle($bg,$x,$y,58,44); $g.DrawString(("T{0:00}" -f $i),$f,$gold,$x+2,$y); $g.DrawString((Code $i),$fs,$white,$x+60,$y) } }
for($k=0;$k -lt 4;$k++){ $x=$pad+$k*32*$S+4; $y=$pad+128*$S+4; $g.FillRectangle($bg,$x,$y,58,24); $g.DrawString("F$k",$f,$gold,$x+2,$y) }
$g.DrawString("x: 0      32      64      96     128", $fs, $white, $pad, 8)
$t = "Idle Grounds AUTO-TILE TEMPLATE  (Wang 16, corner-based, 32 px cells, PPU 32)`n" +
     "Rows 0-3: one CONTINUOUS picture, 16 tiles. Row 4: F0-F3 = 4 full-terrain fill variants.`n" +
     "Light green = your terrain (opaque). Dark green = 2-px edge band (rim/lip shading).`n" +
     "Checker = transparent (the ground below shows).`n" +
     "Your alpha must equal autotile_wang16_alpha_128x160.png exactly.`n" +
     "Corner code next to each tile: NW NE / SW SE, 1 = terrain. T = NW*1+NE*2+SW*4+SE*8."
$g.DrawString($t,$fs,$white,$pad,$pad+160*$S+12)
$bmp.Save("$dir\autotile_wang16_template_LABELLED.png",[System.Drawing.Imaging.ImageFormat]::Png)
# demo scaled 2x
$d=[System.Drawing.Bitmap]::FromFile("$dir\autotile_demo_dualgrid_512x384.png")
$b2=New-Object System.Drawing.Bitmap (512*2),(384*2); $g2=[System.Drawing.Graphics]::FromImage($b2)
$g2.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $g2.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g2.DrawImage($d,(New-Object System.Drawing.Rectangle 0,0,1024,768),0,0,512,384,[System.Drawing.GraphicsUnit]::Pixel)
$pg=New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70,0,0,0)),1
for($i=0;$i -le 16;$i++){ $g2.DrawLine($pg,$i*64,0,$i*64,768) }; for($i=0;$i -le 12;$i++){ $g2.DrawLine($pg,0,$i*64,1024,$i*64) }
$b2.Save("$dir\autotile_demo_dualgrid_2x.png",[System.Drawing.Imaging.ImageFormat]::Png)
