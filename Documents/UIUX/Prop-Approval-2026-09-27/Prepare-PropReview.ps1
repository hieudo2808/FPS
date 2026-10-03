$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$project = 'E:/Unity/Project/FPS'
$props = Join-Path $project 'Assets/ThirdParty/ApprovedProps'
$review = Join-Path $PSScriptRoot 'Validation-2026-09-30'

# Adapt only the protected/real-world labels; preserve the downloaded textures.
$bag = [Drawing.Bitmap]::new((Join-Path $props 'M1_S1/Textures/Image_0.png'))
try {
    $changed = 0
    for ($y = 0; $y -lt $bag.Height; $y++) {
        for ($x = 0; $x -lt $bag.Width; $x++) {
            $c = $bag.GetPixel($x, $y)
            if ($c.R -gt 55 -and $c.R -gt $c.G * 1.8 -and $c.R -gt $c.B * 1.4) {
                $bag.SetPixel($x, $y, [Drawing.Color]::FromArgb($c.A, $c.G, $c.R, $c.B))
                $changed++
            }
        }
    }
    if ($changed -lt 1000) { throw 'Medical patch recolor found too few pixels' }
    $bag.Save((Join-Path $props 'M1_S1/Textures/MedicalBag_GreenMark.png'), [Drawing.Imaging.ImageFormat]::Png)
} finally { $bag.Dispose() }

$label = [Drawing.Bitmap]::new((Join-Path $props 'M2/Textures/az.png'))
$g = [Drawing.Graphics]::FromImage($label)
$title = [Drawing.Font]::new('Arial', 66, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
$body = [Drawing.Font]::new('Arial', 49, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
try {
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    # Original label UV island, measured on the 2048px source atlas.
    $g.FillRectangle([Drawing.Brushes]::White, 31, 1150, 1989, 727)
    $g.DrawString('BRT  /  T-9 SUPPRESSANT', $title, [Drawing.Brushes]::Black, 130, 1220)
    $g.DrawString('FIELD MEDICAL SUPPLY', $body, [Drawing.Brushes]::Black, 130, 1345)
    $g.DrawString('TEMPORARY INFECTION CONTROL', $body, [Drawing.Brushes]::Black, 130, 1440)
    $g.DrawString('NOT A CURE  /  AUTHORIZED PERSONNEL', $body, [Drawing.Brushes]::Black, 130, 1535)
    $g.DrawString('LOT T9-17  /  VANGUARD', $body, [Drawing.Brushes]::Black, 130, 1680)
    $label.Save((Join-Path $props 'M2/Textures/T9_Label.png'), [Drawing.Imaging.ImageFormat]::Png)
} finally { $title.Dispose(); $body.Dispose(); $g.Dispose(); $label.Dispose() }

$groups = @{
    Combat = @('G1','G2','M1_S1','M2','AM1','AM2')
    Equipment = @('AM3','AM4','AM5','L2_R','K2','R1')
    LabCandidates = @('LAB_C1','LAB_C2','LAB_C3','LAB_C4','K1')
}
foreach ($group in $groups.GetEnumerator()) {
    $sheet = [Drawing.Bitmap]::new(1920, 1080)
    $canvas = [Drawing.Graphics]::FromImage($sheet)
    $font = [Drawing.Font]::new('Arial', 26, [Drawing.FontStyle]::Bold)
    try {
        $canvas.Clear([Drawing.Color]::FromArgb(25, 29, 35))
        $canvas.DrawString('UNITY IMPORT REVIEW - ' + $group.Key + ' - NOT SCENE ACCEPTANCE', $font, [Drawing.Brushes]::White, 24, 16)
        for ($i = 0; $i -lt $group.Value.Count; $i++) {
            $code = $group.Value[$i]
            $source = [Drawing.Image]::FromFile((Join-Path $review ($code + '-isolated.png')))
            try {
                $x = ($i % 3) * 640; $y = 70 + [math]::Floor($i / 3) * 500
                $canvas.DrawImage($source, [Drawing.Rectangle]::new($x + 10, $y, 620, 440))
                $canvas.DrawString($code, $font, [Drawing.Brushes]::White, $x + 24, $y + 445)
            } finally { $source.Dispose() }
        }
        $sheet.Save((Join-Path $review ($group.Key + '-1920x1080.png')), [Drawing.Imaging.ImageFormat]::Png)
        $small = [Drawing.Bitmap]::new($sheet, 1280, 720)
        try { $small.Save((Join-Path $review ($group.Key + '-1280x720.png')), [Drawing.Imaging.ImageFormat]::Png) }
        finally { $small.Dispose() }
    } finally { $font.Dispose(); $canvas.Dispose(); $sheet.Dispose() }
}
Write-Output 'Generated two label derivatives and three review sheets at both resolutions.'
