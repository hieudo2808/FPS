param([string]$ProjectRoot = 'E:/Unity/Project/FPS')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$curated = Join-Path $ProjectRoot 'Assets/FPS/Features/UI/Content/Sprites/Survival/Curated'
$samples = @('Surfaces/Panel.png','Surfaces/Distress.png','Controls/Control.png','Controls/MenuFocus.png','Bars/HealthFrame.png','Bars/HealthTrack.png','Bars/HealthFill.png','Icons/Biohazard.png','Icons/Health.png','Icons/Check.png','Icons/Mission/B2AccessCard.png','Icons/Mission/ServiceFuse.png','Icons/Mission/EvidenceCase.png','Icons/Mission/Notebook.png','Icons/Mission/DataBus.png')
foreach ($width in @(1920,1280)) {
    $height = [int]($width * 9 / 16)
    $bitmap = New-Object System.Drawing.Bitmap($width,$height)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $font = New-Object System.Drawing.Font('Segoe UI',([single]($width / 85)))
    $small = New-Object System.Drawing.Font('Segoe UI',([single]($width / 125)))
    try {
        $g.Clear([System.Drawing.Color]::FromArgb(18,20,20))
        $g.DrawString('OUTBREAK PROTOCOL / EXISTING ASSET AUDIT',$font,[System.Drawing.Brushes]::White,30,20)
        $g.DrawString('Lotus + SunGraphica panels / authorized supplied mission icons',$small,[System.Drawing.Brushes]::Silver,30,65)
        for ($i=0;$i -lt $samples.Count;$i++) {
            $cellW=$width/5; $cellH=($height-150)/3
            $x=($i%5)*$cellW+25; $y=[Math]::Floor($i/5)*$cellH+110
            $source=[System.Drawing.Image]::FromFile((Join-Path $curated $samples[$i]))
            try {
                $scale=[Math]::Min(($cellW-50)/$source.Width,($cellH-55)/$source.Height)
                $rect=New-Object System.Drawing.RectangleF([single]$x,[single]$y,[single]($source.Width*$scale),[single]($source.Height*$scale))
                $g.DrawImage($source,$rect)
                $g.DrawString([System.IO.Path]::GetFileNameWithoutExtension($samples[$i]),$small,[System.Drawing.Brushes]::White,[single]$x,[single]($y+$cellH-40))
            } finally { $source.Dispose() }
        }
        $bitmap.Save((Join-Path $ProjectRoot "Documents/UIUX/Campaign-Assets-${width}x${height}.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $font.Dispose(); $small.Dispose(); $g.Dispose(); $bitmap.Dispose() }
}
