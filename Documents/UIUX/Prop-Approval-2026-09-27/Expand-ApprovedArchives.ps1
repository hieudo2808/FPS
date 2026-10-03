param([string]$Stage = 'E:/Unity/Project/FPS/Temp/ApprovedPropImport-20260930', [string[]]$Codes = @())
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stageRoot = [IO.Path]::GetFullPath($Stage).TrimEnd('\') + '\'
$rows = Import-Csv (Join-Path $PSScriptRoot 'Provenance-Manifest.csv') | Where-Object actualArchivePath
if ($Codes.Count) { $rows = $rows | Where-Object { $_.assetCode -in $Codes } }

function Expand-CheckedZip([string]$Archive, [string]$Destination, [int]$Depth) {
    if ($Depth -gt 4) { throw 'Nested archive depth exceeded' }
    $root = [IO.Path]::GetFullPath($Destination).TrimEnd('\') + '\'
    if (-not $root.StartsWith($stageRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Outside staging' }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    $nested = [Collections.Generic.List[string]]::new()
    try {
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $entry.FullName))
            if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path' }
            if ($entry.Length -gt 1GB) { throw 'Oversized archive entry' }
            if (-not $entry.Name) { continue }
            $ext = [IO.Path]::GetExtension($entry.Name).ToLowerInvariant()
            if ($ext -notin @('.fbx','.glb','.blend','.dae','.png','.jpg','.jpeg','.tga','.tif','.tiff','.exr','.bmp','.zip','.txt','.md','.mtl','.obj','.json')) { throw "Unexpected type $ext" }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
            finally { $stream.Dispose(); $sha.Dispose() }
            # AM5 has four different textures with the same ZIP entry name. Preserve each.
            if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -ne $hash) {
                $target = Join-Path ([IO.Path]::GetDirectoryName($target)) ([IO.Path]::GetFileNameWithoutExtension($target) + '_' + $hash.Substring(0,12) + $ext)
            }
            if (Test-Path -LiteralPath $target) {
                if ((Get-FileHash -LiteralPath $target).Hash -ne $hash) { throw "Existing file differs: $target" }
            } else {
                New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
                if ((Get-FileHash -LiteralPath $target).Hash -ne $hash) { throw 'Extraction hash mismatch' }
            }
            if ($ext -eq '.zip') { $nested.Add($target) }
        }
    } finally { $zip.Dispose() }
    foreach ($inner in $nested) { Expand-CheckedZip $inner ($inner + '.unpacked') ($Depth + 1) }
}

foreach ($row in $rows) {
    if ((Get-FileHash -LiteralPath $row.actualArchivePath).Hash -ne $row.archiveSha256) { throw "Archive hash mismatch: $($row.assetCode)" }
    Expand-CheckedZip $row.actualArchivePath (Join-Path $Stage $row.assetCode.Replace('-','_')) 0
    Write-Output "$($row.assetCode): extraction verified"
}
