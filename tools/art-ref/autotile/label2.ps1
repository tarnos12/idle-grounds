param([string]$dir)
Add-Type -AssemblyName System.Drawing
$src=[System.Drawing.Bitmap]::FromFile("$dir\autotile_sidewall_guide_64x128.png")
$S=6;$pad=40;$W=64*$S+$pad*2+430;$H=128*$S+$pad*2
$bmp=New-Object System.Drawing.Bitmap $W,$H;$g=[System.Drawing.Graphics]::FromImage($bmp);$g.Clear([System.Drawing.Color]::FromArgb(255,40,42,54))
$c1=[System.Drawing.Color]::FromArgb(255,90,90,100);$c2=[System.Drawing.Color]::FromArgb(255,70,70,80)
for($y=0;$y -lt 128*$S;$y+=12){for($x=0;$x -lt 64*$S;$x+=12){$c=if((($x+$y)/12)%2 -eq 0){$c1}else{$c2};$b=New-Object System.Drawing.SolidBrush $c;$g.FillRectangle($b,$pad+$x,$pad+$y,12,12);$b.Dispose()}}
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($src,(New-Object System.Drawing.Rectangle $pad,$pad,(64*$S),(128*$S)),0,0,64,128,[System.Drawing.GraphicsUnit]::Pixel)
$pen=New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255,200,50,43)),2
for($i=0;$i -le 2;$i++){$g.DrawLine($pen,$pad+$i*32*$S,$pad,$pad+$i*32*$S,$pad+128*$S)};for($i=0;$i -le 4;$i++){$g.DrawLine($pen,$pad,$pad+$i*32*$S,$pad+64*$S,$pad+$i*32*$S)}
$f=New-Object System.Drawing.Font "Consolas",11;$wb=[System.Drawing.Brushes]::White
$g.DrawString("W face   E face",$f,$wb,$pad,10)
$t="SIDE-FACE STRIP 64x128 (Idle Grounds)`n`nLeft: WEST face, x0-17 opaque.`nRight: EAST face, x46-63 opaque.`nEverything else transparent.`n`nOne CONTINUOUS rock face, 128 px tall.`nRow 127 must flow into row 0`n(vertically seamless wrap).`nThe game cuts 4 pieces of 32 px`nand stacks them 0,1,2,3,0,1,...`n`nDark slate = outer edge (sky side,`n4 px, darkest rock).`nBrown = rock face (your strata).`nLight green = 1-px ground column`n(Island side, the ground colour).`n`nNO bright cap row at the top of a`npiece, NO horizontal band that`nrepeats every 32 px."
$g.DrawString($t,$f,$wb,$pad+64*$S+30,$pad)
$bmp.Save("$dir\autotile_sidewall_template_LABELLED.png",[System.Drawing.Imaging.ImageFormat]::Png)
