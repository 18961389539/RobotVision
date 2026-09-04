$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$deleteFiles = @(
    'JlClassGmm.cs', 'JlClassKnn.cs', 'JlClassLUT.cs',
    'JlClassMlp.cs', 'JlClassSvm.cs', 'JlClassTrainData.cs'
)
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) { Remove-Item $path -Force; Write-Host "Deleted $name" }
}
function Test-RemoveMlClassMethod([string]$signature, [string]$fileName) {
    if ($signature -match 'OcrClass') { return $false }

    if ($signature -match 'JlClass(Gmm|Knn|LUT|Mlp|Svm|TrainData)\b') { return $true }
    if ($signature -match '(ClassifyImageClass|AddSamplesImageClass)') { return $true }

    if ($fileName -eq 'JlOperatorSet.cs') {
        if ($signature -match 'ClassTrainData|ClassGmm|ClassLut|ClassMlp|ClassSvm|ClassKnn') { return $true }
        if ($signature -match 'SelectFeatureSet(Gmm|Mlp|Svm|Knn)\(') { return $true }
    }
    return $false
}

function Remove-MlClassMethods([string]$path) {
    $fileName = Split-Path $path -Leaf
    $lines = [IO.File]::ReadAllLines($path)
    $n = $lines.Length
    $skip = New-Object bool[] $n
    $removed = 0
    $i = 0
    while ($i -lt $n) {
        if ($lines[$i] -match '^\t(public|internal|private|protected) ') {
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
            if (Test-RemoveMlClassMethod $sig $fileName) {
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

$stripTargets = @('JlOperatorSet.cs', 'JlImage.cs') | ForEach-Object { Join-Path $runtime $_ }
foreach ($path in $stripTargets) { Remove-MlClassMethods $path }

Write-Host 'Done.'
