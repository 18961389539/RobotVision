$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$deleteFiles = @(
    'JlBarCode.cs', 'JlDataCode2D.cs', 'JlOCV.cs', 'JlDict.cs', 'JlSystem.cs'
)
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) { Remove-Item $path -Force; Write-Host "Deleted $name" }
}

function Test-RemoveMiscMethod([string]$signature, [string]$fileName) {
    if ($signature -match 'Jl(BarCode|DataCode2D|OCV|Dict)\b') { return $true }
    if ($signature -match '(BarCode|DataCode2d|DataCode2D)') { return $true }
    if ($signature -match '\bOcv\b|OcvProj|OcvSimple') { return $true }

    if ($signature -match 'GenCanonicalVariatesTrans') { return $true }
    if ($signature -match 'AdaptShapeModelHighNoise') { return $true }
    if ($fileName -eq 'JlObjectModel3D.cs' -and $signature -match 'FindBox3d') { return $true }

    if ($signature -match 'TupleGetDictTuple|TupleGetDictObject|TupleTestEqualDictItem') { return $true }
    if ($signature -match 'DictToJson|JsonToDict|CreateDict|CopyDict|ReadDict|WriteDict|GetDict|SetDict|RemoveDict') { return $true }

    if ($fileName -eq 'JlOperatorSet.cs') {
        if ($signature -match 'GetSystemTime|WaitSeconds|SystemCall|SetSystem|SetCheck|ResetObjDb|GetSystem|GetCheck|GetErrorText|CountSeconds|CountRelation|GetExtendedErrorInfo|GetModules|QuerySpy|SetSpy|GetSpy|SetAopInfo|GetAopInfo|QueryAopInfo|OptimizeAop|WriteAopKnowledge|ReadAopKnowledge|SetWindowType|GetWindowAttr|SetWindowAttr|GetCurrentHthreadId|GetSystemInfo|InterruptOperator') {
            return $true
        }
    }
    return $false
}

function Remove-MiscMethods([string]$path) {
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
            if (Test-RemoveMiscMethod $sig $fileName) {
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
    'JlOperatorSet.cs', 'JlImage.cs', 'JlTuple.cs',
    'JlShapeModel.cs', 'JlObjectModel3D.cs'
) | ForEach-Object { Join-Path $runtime $_ }

foreach ($path in $stripTargets) { Remove-MiscMethods $path }

Write-Host 'Done.'
