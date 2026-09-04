$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

function Test-RemoveNicheMethod([string]$signature, [string]$fileName) {
    if ($signature -match '(?i)(Ocr|DeepOcr|SegmentCharacters|ProtectOcrTrainf|ConcatOcrTrainf|AppendOcrTrainf|CloseOcr|OcrChangeChar|OcrGetFeatures|ReadOcrTrainf)') { return $true }
    if ($signature -match '(?i)(PowerReal|MeanCurvatureFlow|CoherenceEnhancingDiff)') { return $true }
    if ($signature -match '(?i)(FitSurfaceFirstOrder|FitSurfaceSecondOrder|GetMetrologyObjectModelContour)') { return $true }
    if ($signature -match '(?i)(ExhaustiveMatch|ProjMatchPoints|Hough|PolarTrans|LearnNdimNorm|ClassNdimNorm|HistoToThresh)') { return $true }
    return $false
}

function Remove-NicheMethods([string]$path) {
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
            if (Test-RemoveNicheMethod $sig $fileName) {
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
    'JlOperatorSet.cs', 'JlImage.cs', 'JlRegion.cs', 'JlHomMat2D.cs',
    'JlXLDCont.cs', 'JlMisc.cs', 'JlMetrologyModel.cs'
) | ForEach-Object { Join-Path $runtime $_ }

foreach ($path in $stripTargets) { Remove-NicheMethods $path }

Write-Host 'Done.'
