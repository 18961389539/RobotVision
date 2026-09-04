$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$src = Join-Path $root 'JLVisionLib'
$props = Join-Path $root 'Properties'

$files = @(
    Get-ChildItem -Path $src -Filter '*.cs' -File
    Get-ChildItem -Path $props -Filter '*.cs' -File -ErrorAction SilentlyContinue
)

function Update-Content([string]$text) {
    $replacements = [ordered]@{
        'HalconOperatorAttribute' = 'JlOperatorAttribute'
        'HalconException'         = 'JlException'
        'HalconAPI'               = 'JlNativeApi'
        'FromHalconEncoding'      = 'FromNativeEncoding'
        'ToHalconHGlobalEncoding' = 'ToNativeGlobalEncoding'
        'HalconCall'              = 'NativeCall'
        'H_ERR_'                  = 'Jl_ERR_'
        'H_MSG_'                  = 'Jl_MSG_'
    }

    foreach ($pair in $replacements.GetEnumerator()) {
        $text = $text.Replace($pair.Key, $pair.Value)
    }

    $text = [regex]::Replace($text, '\bH([A-Z][a-zA-Z0-9_]*)', 'Jl$1')

    $text = $text -replace '(?i)MVTec Software GmbH', 'JLVision'
    $text = $text -replace '(?i)MVTec', 'JLVision'
    $text = $text -replace '(?i)HALCON', 'Vision'
    $text = $text -replace '(?i)\bhalcon\b', 'native'

    return $text
}

foreach ($file in $files) {
    $original = [IO.File]::ReadAllText($file.FullName)
    $updated = Update-Content $original
    if ($updated -ne $original) {
        [IO.File]::WriteAllText($file.FullName, $updated)
        Write-Host "Updated $($file.Name)"
    }
}

$renameMap = [ordered]@{
    'HalconAPI.cs'               = 'JlNativeApi.cs'
    'HalconException.cs'         = 'JlException.cs'
    'HalconOperatorAttribute.cs' = 'JlOperatorAttribute.cs'
}

foreach ($pair in $renameMap.GetEnumerator()) {
    $oldPath = Join-Path $src $pair.Key
    $newPath = Join-Path $src $pair.Value
    if (Test-Path $oldPath) {
        Move-Item -Path $oldPath -Destination $newPath -Force
        Write-Host "Renamed $($pair.Key) -> $($pair.Value)"
    }
}

Get-ChildItem -Path $src -Filter 'H*.cs' -File | Sort-Object Name -Descending | ForEach-Object {
    $newName = 'Jl' + $_.Name.Substring(1)
    if ($_.Name -ne $newName) {
        Move-Item -Path $_.FullName -Destination (Join-Path $src $newName) -Force
        Write-Host "Renamed $($_.Name) -> $newName"
    }
}

Write-Host 'Done.'
