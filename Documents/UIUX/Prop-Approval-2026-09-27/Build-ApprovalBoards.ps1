param([switch]$RefreshPreviews)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$previewDir = Join-Path $root 'Previews'
[void][IO.Directory]::CreateDirectory($previewDir)
$candidates = @(
    @{code='hard-cases'; id='ce859ba7507148588262207255974aa4'},
    @{code='medical-bag'; id='abc2f95bce4c4e69bfc0d8742d37e78b'},
    @{code='medical-kit'; id='7cbe75f34c194e1a9d0c6cccabb2e563'},
    @{code='keycard-scanner'; id='65f41d0b8a9c4c88915690b76307b326'},
    @{code='keycard'; id='a0033bf027364417a7777d98d03980e1'},
    @{code='radio'; id='f5c8f0420f054b32a2e977fbcd248d84'},
    @{code='fuse'; id='d2ed4a3206c1497ba9634adff36d24a0'},
    @{code='vintage-fuse-box'; id='4109ae3e180d4ed8b3427fe6a97d58b6'},
    @{code='computer-mark'; id='e6332b415ccd4a61acdff22b4636ce16'},
    @{code='computer-tobalation'; id='ab309ab0cf094372b6f1b26b91c35eca'},
    @{code='frag-m67'; id='d202644dfaf441a0a145befbd7add45a'},
    @{code='incendiary'; id='dec7bc413f12429c99f81cf4e6890ac7'},
    @{code='ammo-classic'; id='023400b57dbd48f5b6e257695f96489c'},
    @{code='ammo-vandal'; id='0e880a3c2cfd4ea9a4891a81f621733b'},
    @{code='ammo-bucky'; id='964f780f87d34d54890f9de94792fd01'},
    @{code='ammo-operator'; id='5338de8c47ea4f68aa40dd51602a2b53'},
    @{code='ammo-odin'; id='17898b0b68114516b704d0aabc9d2142'},
    @{code='injector'; id='e1e3ff6091c04cf2a183ac8a3775f50f'},
    @{code='incendiary-real'; id='74a5e9a18c1947118d1895dca900afb5'},
    @{code='computer-desk'; id='ff0674f66e11462e8b3417dee2b58ac9'},
    @{code='syringe'; id='6d4847ceddf145d19e7de3ae560db8d9'},
    @{code='ammo-classic-remake'; id='74871b58382a418ab310e6d22b1e263f'},
    @{code='medical-vial'; id='a1bb302ae09645e78cbda7bd53fd9dff'}
)
foreach ($candidate in $candidates) {
    $jsonPath = Join-Path $previewDir ($candidate.code + '.json')
    $imagePath = Join-Path $previewDir ($candidate.code + '.jpg')
    if ($RefreshPreviews -or !(Test-Path -LiteralPath $jsonPath)) {
        $model = Invoke-RestMethod ('https://api.sketchfab.com/v3/models/' + $candidate.id) -TimeoutSec 30
        $thumb = $model.thumbnails.images | Sort-Object width -Descending | Select-Object -First 1
        $record = [ordered]@{
            checkedAtUtc=[DateTime]::UtcNow.ToString('o'); id=$candidate.id;
            title=$model.name; author=$model.user.displayName; source=$model.viewerUrl;
            license=$model.license; downloadable=$model.isDownloadable;
            triangles=$model.faceCount; description=$model.description; previewUrl=$thumb.url;
            previewKind='Author thumbnail; not imported or tested in Unity'
        }
        [IO.File]::WriteAllText($jsonPath, ($record | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    }
    $record = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
    if (!$record.downloadable -or $record.license.slug -ne 'by' -or !$record.previewUrl) {
        throw "Recheck the license/download/preview captions for $($candidate.code) before publishing the board."
    }
    if ($RefreshPreviews -or !(Test-Path -LiteralPath $imagePath)) {
        Invoke-WebRequest -Uri $record.previewUrl -OutFile $imagePath -TimeoutSec 30 -UseBasicParsing
    }
    Write-Output "$($candidate.code): $($record.title) | $($record.author) | $($record.license.label) | $($record.triangles) tris"
}

function New-Board([string]$name, [string]$title, [string]$subtitle, [array]$cards) {
    $width=1920; $cellWidth=924; $cellHeight=492; $gap=24
    $height=130 + [int][Math]::Ceiling($cards.Count / 2.0) * ($cellHeight+$gap) + 16
    $bitmap=[Drawing.Bitmap]::new($width,$height)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.Clear([Drawing.Color]::FromArgb(15,20,26))
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $fontTitle=[Drawing.Font]::new('Segoe UI',30,[Drawing.FontStyle]::Bold)
    $fontCard=[Drawing.Font]::new('Segoe UI',22,[Drawing.FontStyle]::Bold)
    $fontText=[Drawing.Font]::new('Segoe UI',16)
    $white=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(236,240,244))
    $muted=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(174,188,201))
    $panel=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(29,37,47))
    try {
        $g.DrawString($title,$fontTitle,$white,24,12)
        $g.DrawString($subtitle,$fontText,$muted,26,69)
        for ($i=0; $i -lt $cards.Count; $i++) {
            $c=$cards[$i]; $x=24+($i%2)*($cellWidth+$gap); $y=120+[Math]::Floor($i/2)*($cellHeight+$gap)
            $g.FillRectangle($panel,[int]$x,[int]$y,$cellWidth,$cellHeight)
            $img=[Drawing.Image]::FromFile($c.image)
            try {
                $imageHeight=352
                $scale=[Math]::Min($cellWidth/[double]$img.Width,$imageHeight/[double]$img.Height)
                $iw=[int]($img.Width*$scale); $ih=[int]($img.Height*$scale)
                $g.DrawImage($img,[int]($x+($cellWidth-$iw)/2),[int]($y+($imageHeight-$ih)/2),$iw,$ih)
            } finally { $img.Dispose() }
            $g.DrawString($c.title,$fontCard,$white,[Drawing.RectangleF]::new($x+18,$y+361,$cellWidth-36,40))
            $g.DrawString($c.note,$fontText,$muted,[Drawing.RectangleF]::new($x+18,$y+406,$cellWidth-36,77))
        }
        $path=Join-Path $root $name
        $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
        Write-Output $path
    } finally {
        $g.Dispose(); $bitmap.Dispose(); $fontTitle.Dispose(); $fontCard.Dispose(); $fontText.Dispose()
        $white.Dispose(); $muted.Dispose(); $panel.Dispose()
    }
}

$webCards = foreach($c in ($candidates | Select-Object -First 8)) {
    $record = Get-Content -LiteralPath (Join-Path $previewDir ($c.code+'.json')) -Raw | ConvertFrom-Json
    @{image=(Join-Path $previewDir ($c.code+'.jpg'));title=$c.code;note=($record.title+' | '+$record.author+' | '+$record.license.label+' | '+$record.triangles+' tris')}
}
New-Board 'Web-Candidates-Review.png' 'WEB CANDIDATES - VISUAL REVIEW' 'Author previews only. No models downloaded, imported or approved.' $webCards
$newCards = foreach($c in ($candidates | Select-Object -Skip 8)) {
    $record = Get-Content -LiteralPath (Join-Path $previewDir ($c.code+'.json')) -Raw | ConvertFrom-Json
    @{image=(Join-Path $previewDir ($c.code+'.jpg'));title=$c.code;note=($record.title+' | '+$record.author+' | '+$record.license.label+' | '+$record.triangles+' tris')}
}
New-Board 'Additional-Candidates-Review.png' 'ADDITIONAL CANDIDATES - VISUAL REVIEW' 'Author previews only. Not imported or tested. See captions and source metadata.' $newCards

$shots = [IO.Path]::GetFullPath((Join-Path $root '../../../Assets/Screenshots'))
New-Board '01-Existing-Scene-Assets.png' '01 / USE THE MACHINES AND FURNITURE ALREADY HERE' 'Actual Unity screenshots. Current state, not an edited preview of a finished replacement.' @(
    @{image="$shots/Approval_PC_Final.png";title='A1 - GUARD-ROOM COMPUTER';note='AsylumAccess: interact with the existing screen. No extra pedestal.'},
    @{image="$shots/Approval_ElectricalBox_Final.png";title='A2-A - EXISTING ELECTRICAL-BOX ASSET';note='AsylumInstall + AsylumPower: reuse this prefab on the basement wall. Location not changed yet.'},
    @{image="$shots/Approval_PatientDesk_Final.png";title='A3 - EXAMINATION-ROOM DESK';note='AsylumPatient: patient record on this existing desk. No new console.'},
    @{image="$shots/Approval_MorgueTable_Final.png";title='A4 - MORGUE WRITING DESK';note='AsylumTransfer: transfer record + B2 keycard. Proposed placement, not installed yet.'},
    @{image="$shots/Approval_L1_PowerMachines.png";title='L1 - LAB POWER ROOM';note='LabPower: the existing blue generator / electrical cabinet. Remove the redundant cube device after approval.'},
    @{image="$shots/Approval_LabDeskFront_Final.png";title='L2 - EXISTING LAB WORKSTATION';note='LabTrace: existing B-room desk. LabTransmit: existing F-room desk. Photo shows this workstation family, not relocated objectives.'}
)
New-Board '02-External-Asset-Proposals.png' '02 / SOURCED MODELS FOR THE MISSING PROPS' 'Unedited author previews. CC BY 4.0, credit required. Models not imported or tested in Unity.' @(
    @{image="$previewDir/hard-cases.jpg";title='K3 + S2 - HARD CASES';note='RPaciorek | Black: evidence cases. Silver: equipment supplies. 8,210 triangles for the listed set.'},
    @{image="$previewDir/medical-bag.jpg";title='S1 - MEDICAL KIT BAG';note='yronthal | Replace MedicalCase visuals. 9,983 tris. Proposed label change to neutral MEDICAL marking.'},
    @{image="$previewDir/keycard.jpg";title='K2 - B2 ACCESS CARD';note='nuFF3 | Real keycard silhouette, 38 tris. B2 label can be changed after approval.'},
    @{image="$previewDir/radio.jpg";title='R1 - EXTRACTION RADIO';note='curichenkow | Replace the electrical box used as a radio. 28,807 tris; one set-piece prop.'},
    @{image="$previewDir/vintage-fuse-box.jpg";title='A2-B - VISIBLE FUSE-BOX ALTERNATIVE';note='Alex Filip | 3,950 tris. Alternative to A2-A; not an additional cabinet. Separate fuse mesh unverified.'},
    @{image="$previewDir/fuse.jpg";title='K1 - SERVICE FUSE / TECHNICAL HOLD';note='AliA Animations | Shape reference only. 126,296 tris: do not approve this mesh as-is.'}
)
$iconDir='E:/ProjectSettings/Assets/Resources/inventory/icons'
New-Board '03-Supplied-Icons-Review.png' '03 / THE SUPPLIED FOLDER: ICONS ARE NOT WORLD MODELS' 'Original PNG artwork from the user-supplied folder. These are 2D inventory icons only.' @(
    @{image="$iconDir/BlueCard.png";title='UI1 - BLUECARD';note='Suitable existing 2D card icon; preferably align with the approved 3D card later.'},
    @{image="$iconDir/BatteryPart1.png";title='BATTERYPART1 - FUSE MAPPING NEEDS REVIEW';note='Named as a battery part; visually different from the ceramic fuse candidate. Prefer a thumbnail of the approved model.'},
    @{image="$iconDir/VirusSample.png";title='NOT A CASE - VIRUSSAMPLE';note='Sample-vial artwork. Do not use it as a literal evidence-case thumbnail.'},
    @{image="$iconDir/Notebook.png";title='NOT A PAPER NOTE - NOTEBOOK';note='Laptop artwork. Suitable only for a digital terminal/file category, not physical paper.'}
)

New-Board '04-Computer-Grenades-Medical.png' '04 / COMPUTER, GRENADES AND MEDICAL ITEMS' 'Author previews unless marked LOCAL. Proposed replacements, not imported or approved yet.' @(
    @{image="$previewDir/computer-desk.jpg";title='L2-R - SOURCED COMPUTER PERIPHERALS';note='CR!STALLL | Use monitor, keyboard and mouse on existing lab desks. Do not add this entire desk/chair set.'},
    @{image="$previewDir/frag-m67.jpg";title='G1 - FRAGMENTATION GRENADE';note='Tiago Lopes | M67, 4,747 tris. Proposed shared model for pickup, held item, projectile and HUD thumbnail.'},
    @{image="$previewDir/incendiary-real.jpg";title='G2 - INCENDIARY GRENADE';note='R.Linden | 2,796 tris. Distinct canister silhouette and flame label; not just a recolored frag.'},
    @{image="$previewDir/medical-bag.jpg";title='M1 - USABLE MEDKIT';note='yronthal | Extend S1 visual family to MedkitPickup / held medkit. Neutral MEDICAL label proposed.'},
    @{image="$previewDir/medical-vial.jpg";title='M2 - ANTIDOTE / RELABEL REQUIRED';note='VRC-IW | 1,330 tris. Use vial shape only; replace all real vaccine/brand labels with fictional T-9 treatment.'},
    @{image=([IO.Path]::GetFullPath((Join-Path $root '../../../Assets/FPS/Features/Survival/Content/Textures/ItemIcon0.png')));title='LOCAL REFERENCE - CURRENT FRAG';note='Existing thumbnail from supplied HE_Grenade_3PV mesh. G1 is a proposed style replacement, not proof this was handmade.'}
)
New-Board '05-Ammo-Per-Weapon.png' '05 / ONE AMMO PICKUP LOOK FOR EACH WEAPON' 'Unedited author previews. CC BY 4.0. Caliber text on packaging is NOT a confirmed weapon specification.' @(
    @{image="$previewDir/ammo-classic-remake.jpg";title='AM1 - CLASSIC / PISTOL';note='jsandwich96 | Handgun Ammo Box REMAKE, 1,330 tris. Compact cartridge tray.'},
    @{image="$previewDir/ammo-vandal.jpg";title='AM2 - VANDAL / RIFLE';note='jsandwich96 | Assault Rifle Ammo Box REMAKE, 6,724 tris. Rifle carton with long rounds.'},
    @{image="$previewDir/ammo-bucky.jpg";title='AM3 - BUCKY / SHOTGUN';note='jsandwich96 | Shotgun Ammo Box, 2,732 tris. Clearly visible shotgun shells.'},
    @{image="$previewDir/ammo-operator.jpg";title='AM4 - OPERATOR / SNIPER';note='jsandwich96 | Sniper Ammo Box, 6,000 tris. Precision-ammunition tray.'},
    @{image="$previewDir/ammo-odin.jpg";title='AM5 - ODIN / MACHINE GUN';note='Darren McNerney 3D | Ammunition Box, 7,492 tris for listed set. Select one metal box, not the entire display.'}
)

# Runnable output check: a broken or incomplete image must not pass as a delivered board.
foreach ($name in @('01-Existing-Scene-Assets.png','02-External-Asset-Proposals.png','03-Supplied-Icons-Review.png','04-Computer-Grenades-Medical.png','05-Ammo-Per-Weapon.png')) {
    $check=[Drawing.Image]::FromFile((Join-Path $root $name))
    try {
        if ($check.Width -ne 1920 -or $check.Height -lt 1100) { throw "Unexpected board dimensions: $name" }
    } finally { $check.Dispose() }
}
Write-Output 'Verified: all five approval boards decode correctly at the intended size.'
