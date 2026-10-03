param([switch]$DownloadCandidatePreviews)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$records = Get-Content -LiteralPath (Join-Path $root 'Asset-Gate-Sources.json') -Raw | ConvertFrom-Json
$previewDir = Join-Path $root 'Previews'
# Reuse the approved author previews. Download only the four newly researched candidates.
if ($DownloadCandidatePreviews) {
    foreach ($record in ($records | Where-Object { $_.code -like 'LAB-*' })) {
        $path = Join-Path $previewDir ($record.previewName + '.jpg')
        if (!(Test-Path -LiteralPath $path)) {
            Invoke-WebRequest -Uri $record.previewUrl -OutFile $path -TimeoutSec 30 -UseBasicParsing
        }
    }
}
$boards = @(
    @{name='Asset-Gate-Combat'; title='COMBAT PROPS / APPROVED LOOKS - ORIGINAL FILES MISSING'; ids=@('G1','G2','M1-S1','M2','AM1','AM2')},
    @{name='Asset-Gate-Equipment'; title='EQUIPMENT / APPROVED LOOKS - ORIGINAL FILES MISSING'; ids=@('AM3','AM4','AM5','L2-R','K2','R1')},
    @{name='Asset-Gate-Lab-Candidates'; title='LAB FAMILY / CANDIDATES ONLY - NOT APPROVED'; ids=@('LAB-C1','LAB-C2','LAB-C3','LAB-C4','K1')}
)
foreach ($board in $boards) {
    $bitmap = [Drawing.Bitmap]::new(1920,1080)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.Clear([Drawing.Color]::FromArgb(17,23,29))
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $title = [Drawing.Font]::new('Segoe UI',26,[Drawing.FontStyle]::Bold)
    $label = [Drawing.Font]::new('Segoe UI',19,[Drawing.FontStyle]::Bold)
    $body = [Drawing.Font]::new('Segoe UI',15)
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(238,243,247))
    $muted = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(184,198,207))
    $panel = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(31,41,49))
    try {
        $g.DrawString($board.title,$title,$white,24,14)
        $g.DrawString('Original author previews. NOT Unity imports, NOT after-replacement screenshots. Sources: Asset-Gate-Sources.json',$body,$muted,26,70)
        for ($i=0; $i -lt $board.ids.Count; $i++) {
            $record = $records | Where-Object { $_.code -eq $board.ids[$i] }
            if (@($record).Count -ne 1) { throw ('Missing/duplicate record: ' + $board.ids[$i]) }
            $x=24+($i%3)*632; $y=116+[Math]::Floor($i/3)*470
            $g.FillRectangle($panel,[int]$x,[int]$y,608,450)
            $img=[Drawing.Image]::FromFile((Join-Path $previewDir ($record.previewName + '.jpg')))
            try {
                $scale=[Math]::Min(584/[double]$img.Width,298/[double]$img.Height)
                $iw=[int]($img.Width*$scale); $ih=[int]($img.Height*$scale)
                $g.DrawImage($img,[int]($x+(608-$iw)/2),[int]($y+(306-$ih)/2),$iw,$ih)
            } finally { $img.Dispose() }
            $shortTitle = switch ($record.code) {
                'M1-S1' {'M1 / S1 - MEDKIT + MEDICAL CACHE'}
                'LAB-C1' {'LAB-C1 - SHELF + UTILITY CART'}
                'LAB-C2' {'LAB-C2 - FUME CUPBOARD'}
                'LAB-C3' {'LAB-C3 - LAB FURNITURE SET'}
                'LAB-C4' {'LAB-C4 - LAB BENCH SET'}
                'K1' {'K1 - FUSE / TECHNICAL HOLD'}
                default {$record.code + ' - ' + $record.title}
            }
            $g.DrawString($shortTitle,$label,$white,[Drawing.RectangleF]::new($x+12,$y+311,584,60))
            $g.DrawString(($record.author + ' | ' + $record.license.label),$body,$muted,[Drawing.RectangleF]::new($x+12,$y+373,584,34))
            $g.DrawString(($record.triangles.ToString('N0') + ' source tris | ' + $record.approval),$body,$muted,[Drawing.RectangleF]::new($x+12,$y+409,584,40))
        }
        $g.DrawString('Gate: geometry / materials / scale / support / collision / in-game appearance remain UNVERIFIED until original files are available.',$body,$muted,26,1050)
        foreach ($size in @(@(1920,1080),@(1280,720))) {
            $output=Join-Path $root ($board.name+'-'+$size[0]+'x'+$size[1]+'.png')
            if ($size[0] -eq 1920) { $bitmap.Save($output,[Drawing.Imaging.ImageFormat]::Png) }
            else {
                $small=[Drawing.Bitmap]::new($size[0],$size[1]); $sg=[Drawing.Graphics]::FromImage($small)
                try { $sg.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic; $sg.DrawImage($bitmap,0,0,$size[0],$size[1]); $small.Save($output,[Drawing.Imaging.ImageFormat]::Png) }
                finally { $sg.Dispose(); $small.Dispose() }
            }
            $check=[Drawing.Image]::FromFile($output)
            try { if ($check.Width -ne $size[0] -or $check.Height -ne $size[1]) { throw ('Invalid size: '+$output) } }
            finally { $check.Dispose() }
            Write-Output ('VERIFIED '+$output)
        }
    } finally {
        $g.Dispose(); $bitmap.Dispose(); $title.Dispose(); $label.Dispose(); $body.Dispose(); $white.Dispose(); $muted.Dispose(); $panel.Dispose()
    }
}
