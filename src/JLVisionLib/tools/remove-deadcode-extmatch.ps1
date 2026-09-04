$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

# --- Phase 1: dead code / legacy files ---
$legacyFiles = @(
    (Join-Path $runtime 'JlErrorDef.cs'),
    (Join-Path $root 'TypeForwards.cs'),
    (Join-Path $root 'tools\TypeForwards.cs'),
    (Join-Path $root 'tools\decompiled\JLVisionLib.JlNativeApi.decompiled.cs')
)
foreach ($path in $legacyFiles) {
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "Deleted $(Split-Path $path -Leaf)"
    }
}
$decompiledDir = Join-Path $root 'tools\decompiled'
if ((Test-Path $decompiledDir) -and -not (Get-ChildItem $decompiledDir -Force | Where-Object { $_.Name -ne '.' -and $_.Name -ne '..' })) {
    Remove-Item $decompiledDir -Force -Recurse
    Write-Host 'Deleted tools\decompiled'
}

# --- Phase 2: extended match model types ---
$deleteFiles = @(
    'JlDeformableModel.cs',
    'JlDescriptorModel.cs',
    'JlVariationModel.cs',
    'JlGenericShapeModelResult.cs'
)
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "Deleted $name"
    }
}

function Test-RemoveExtMatchMethod([string]$signature) {
    if ($signature -match 'Jl(Deformable|Descriptor|Variation|GenericShapeModelResult)\b') { return $true }
    if ($signature -match '(DeformableModel|DescriptorModel|VariationModel|GenericShapeModel)') { return $true }
    if ($signature -match 'PlanarUncalib') { return $true }
    return $false
}

function Remove-ExtMatchMethods([string]$path) {
    $fileName = Split-Path $path -Leaf
    if (-not (Test-Path $path)) { return }
    $lines = [IO.File]::ReadAllLines($path)
    $n = $lines.Length
    $skip = New-Object bool[] $n
    $removed = 0
    $i = 0
    while ($i -lt $n) {
        if ($lines[$i] -match '^\t(public|internal|private|protected|public static) ') {
            $start = $i
            while ($start -gt 0 -and ($lines[$start - 1] -match '^\t///' -or $lines[$start - 1] -match '^\t\[')) { $start-- }
            $j = $i
            $sig = $lines[$i]
            while ($j -lt $n -and $lines[$j] -notmatch '\{') {
                if ($j -ne $i) { $sig += ' ' + $lines[$j].Trim() }
                $j++
            }
            if ($j -ge $n) { $i++; continue }
            $depth = 0
            $k = $j
            do {
                $depth += ([regex]::Matches($lines[$k], '\{')).Count
                $depth -= ([regex]::Matches($lines[$k], '\}')).Count
                $k++
            } while ($k -lt $n -and $depth -gt 0)
            if (Test-RemoveExtMatchMethod $sig) {
                for ($x = $start; $x -lt $k; $x++) { $skip[$x] = $true }
                $removed++
                $i = $k
                continue
            }
        }
        $i++
    }
    if ($removed -gt 0) {
        $sb = New-Object System.Text.StringBuilder
        for ($idx = 0; $idx -lt $n; $idx++) {
            if (-not $skip[$idx]) { [void]$sb.AppendLine($lines[$idx]) }
        }
        [IO.File]::WriteAllText($path, $sb.ToString().TrimEnd("`r", "`n") + "`r`n")
        Write-Host "Stripped $removed method(s) from $fileName"
    }
}

$stripTargets = @(
    'JlOperatorSet.cs', 'JlImage.cs', 'JlXLDCont.cs', 'JlShapeModel.cs'
) | ForEach-Object { Join-Path $runtime $_ }

foreach ($path in $stripTargets) { Remove-ExtMatchMethods $path }

Write-Host 'Done.'
