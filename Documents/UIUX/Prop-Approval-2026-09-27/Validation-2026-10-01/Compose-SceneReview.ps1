$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$rows = @(Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'Furniture-After.csv'))
$finalRows = @(Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'FinalLab-After.csv'))
$pages = @{}
$cartRows = @(Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'CartsCases-After.csv'))
for ($page = 0; $page -lt [math]::Ceiling($cartRows.Count / 6); $page++) {
    $pages["Carts-Cases-$($page + 1)"] = @($cartRows | Select-Object -Skip ($page * 6) -First 6 | ForEach-Object {
        @{ Path = Join-Path $root $_.screenshot; Label = "$($_.index) / $($_.type) $($_.position)" }
    })
}
for ($page = 0; $page -lt 3; $page++) {
    $pages["Furniture-$($page + 1)"] = @($rows | Select-Object -Skip ($page * 6) -First 6 | ForEach-Object {
        @{ Path = Join-Path $root $_.screenshot; Label = "$($_.index) / $($_.type) $($_.position)" }
    })
}
for ($page = 0; $page -lt [math]::Ceiling($finalRows.Count / 6); $page++) {
    $pages["Final-Lab-$($page + 1)"] = @($finalRows | Select-Object -Skip ($page * 6) -First 6 | ForEach-Object {
        @{ Path = Join-Path $root $_.screenshot; Label = "$($_.index) / $($_.type) $($_.position) / AFTER" }
    })
}
$pages['Before-After'] = @(
    @{ Path = Join-Path $root 'Validation-2026-09-30/ControlDesk-B-Before.png'; Label = 'BEFORE / ControlDesk B' },
    @{ Path = Join-Path $PSScriptRoot 'LabBench-Legacy-Reconstructed.png'; Label = 'LEGACY RECONSTRUCTED / preserved old mesh' },
    @{ Path = Join-Path $root 'Validation-2026-09-30/FumeHood-C-Before.png'; Label = 'BEFORE / FumeHood C' },
    @{ Path = Join-Path $PSScriptRoot 'ControlDesk-03.png'; Label = 'AFTER / C3 bench + L2-R computer' },
    @{ Path = Join-Path $PSScriptRoot 'LabBench-15.png'; Label = 'AFTER / C3 bench' },
    @{ Path = Join-Path $PSScriptRoot 'FumeHood-09-Full.png'; Label = 'AFTER / C2 fume cupboard' }
)
$pages['Carts-Cases-Before-After'] = @(
    @{ Path = Join-Path $PSScriptRoot 'LabCart-Before.png'; Label = 'BEFORE / LabCart' },
    @{ Path = Join-Path $PSScriptRoot 'TransferTrolley-Before.png'; Label = 'BEFORE / TransferTrolley' },
    @{ Path = Join-Path $PSScriptRoot 'TransitStack-Before.png'; Label = 'BEFORE / TransitCase stack' },
    @{ Path = Join-Path $PSScriptRoot 'LabCart-After.png'; Label = 'AFTER / C1 / 2 carts' },
    @{ Path = Join-Path $PSScriptRoot 'TransferTrolley-After.png'; Label = 'AFTER / C1 / 3 trolleys' },
    @{ Path = Join-Path $PSScriptRoot 'TransitStack-After.png'; Label = 'AFTER / RPaciorek / 20 cases' }
)
$pages['Candidate-Approval'] = @(
    @{ Path = Join-Path $PSScriptRoot 'Cabinet-Candidate.png'; Label = 'PENDING / 9 ReagentCabinet / Naked Singularity' },
    @{ Path = Join-Path $PSScriptRoot 'Rack-Opposite.png'; Label = 'PENDING / 7 ServerRack / Spellkaze' },
    @{ Path = Join-Path $PSScriptRoot 'Stool-Candidate.png'; Label = 'PENDING / 1 LabStool / conndavis20' },
    @{ Path = Join-Path $PSScriptRoot 'Cabinet-InScene-Preview.png'; Label = 'TEMPORARY PREVIEW / not applied' },
    @{ Path = Join-Path $PSScriptRoot 'Rack-InScene-Preview.png'; Label = 'TEMPORARY PREVIEW / not applied' },
    @{ Path = Join-Path $PSScriptRoot 'Stool-InScene-Preview.png'; Label = 'TEMPORARY PREVIEW / not applied' }
)
$pages['Remaining-Candidate-Review'] = @(
    @{ Path = Join-Path $PSScriptRoot 'Fridge-Native0.png'; Label = 'HOLD / compact fridge / open door footprint' },
    @{ Path = Join-Path $PSScriptRoot 'Centrifuge0-Candidate.png'; Label = 'PENDING / ProgressTH centrifuge / open' },
    @{ Path = Join-Path $PSScriptRoot 'Existing-Sink.png'; Label = 'PENDING / existing sourced Asylum sink' },
    @{ Path = Join-Path $PSScriptRoot 'Fridge-Front.png'; Label = 'ALTERNATIVE / existing dirty tall fridge' },
    @{ Path = Join-Path $PSScriptRoot 'Centrifuge78-Candidate.png'; Label = 'ALTERNATIVE / same centrifuge / closed' },
    @{ Path = Join-Path $PSScriptRoot 'LAB_C4-Candidate.png'; Label = 'NOT RECOMMENDED / simplistic C4 device' }
)
foreach ($page in $pages.GetEnumerator()) {
    $sheet = [Drawing.Bitmap]::new(1920, 1080)
    $canvas = [Drawing.Graphics]::FromImage($sheet)
    $font = [Drawing.Font]::new('Arial', 24, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    try {
        $canvas.Clear([Drawing.Color]::FromArgb(25, 29, 35))
        $canvas.DrawString('UNITY SCENE / 2026-10-01 / LOCAL CHECKS ONLY - FINAL ACCEPTANCE PENDING', $font, [Drawing.Brushes]::White, 24, 20)
        for ($i = 0; $i -lt $page.Value.Count; $i++) {
            $entry = $page.Value[$i]
            $source = [Drawing.Image]::FromFile($entry.Path)
            try {
                $x = ($i % 3) * 640 + 10; $y = 85 + [math]::Floor($i / 3) * 480
                $canvas.DrawImage($source, [Drawing.Rectangle]::new($x, $y, 620, 349))
                $canvas.DrawString($entry.Label, $font, [Drawing.Brushes]::White, [Drawing.RectangleF]::new($x, $y + 358, 620, 95))
            } finally { $source.Dispose() }
        }
        $sheet.Save((Join-Path $PSScriptRoot "$($page.Key)-1920x1080.png"), [Drawing.Imaging.ImageFormat]::Png)
        $small = [Drawing.Bitmap]::new($sheet, 1280, 720)
        try { $small.Save((Join-Path $PSScriptRoot "$($page.Key)-1280x720.png"), [Drawing.Imaging.ImageFormat]::Png) }
        finally { $small.Dispose() }
    } finally { $font.Dispose(); $canvas.Dispose(); $sheet.Dispose() }
}
Write-Output "Composed $($pages.Count) review pages at 1920x1080 and 1280x720; these are contact sheets, not runtime resolution tests."
