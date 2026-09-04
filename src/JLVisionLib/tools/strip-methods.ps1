param(
    [string[]]$Files
)

$ErrorActionPreference = 'Stop'

function Test-RemoveMethod([string]$signature, [string]$fileName) {
    if ($signature -match 'JlSocket|JlSerial\b|JlFile\b|JlIODevice|JlIOChannel|JlFramegrabber') { return $true }
    if ($signature -match '\b(ReceiveImage|SendImage|ReceiveRegion|SendRegion|ReceiveXld|SendXld|GrabImage|GrabData|FreadSerializedItem|FwriteSerializedItem)\b') { return $true }

    if ($fileName -eq 'JlOperatorSet.cs') {
        if ($signature -match 'public static void (Read|Write|Open|Close|Clear|Get|Set)Serial\w*\(') { return $true }
        if ($signature -match 'public static void \w*Socket\w*\(') { return $true }
        if ($signature -match 'public static void \w*Framegrabber\w*\(') { return $true }
        if ($signature -match 'public static void \w*IoDevice\w*\(') { return $true }
        if ($signature -match 'public static void \w*IoChannel\w*\(') { return $true }
        if ($signature -match 'public static void (OpenFile|CloseFile|FreadSerializedItem|FwriteSerializedItem)\(') { return $true }
        if ($signature -match 'public static void \w*Mutex\w*\(') { return $true }
        if ($signature -match 'public static void \w*Barrier\w*\(') { return $true }
        if ($signature -match 'public static void \w*Condition\w*\(') { return $true }
        if ($signature -match 'public static void (ClearEvent|SignalEvent|TryWaitEvent|WaitEvent|CreateEvent)\(') { return $true }
        if ($signature -match 'public static void \w*MessageQueue\w*\(') { return $true }
        if ($signature -match 'public static void \w*Message\w*\(') { return $true }
        if ($signature -match 'public static void \w*ComputeDevice\w*\(') { return $true }
        if ($signature -match 'public static void \w*DeepMatching3d\w*\(') { return $true }
        if ($signature -match 'public static void \w*Dl\w*\(') { return $true }
        if ($signature -match 'public static void (QueryOperatorInfo|QueryParamInfo|GetOperatorName|GetOperatorInfo|GetParamInfo|QueryModuleInfo|GetModuleInfo|GetSystemInfo|QuerySystemInfo)\(') { return $true }
        if ($signature -match 'public static void (ReceiveImage|SendImage|ReceiveRegion|SendRegion|ReceiveXld|SendXld|ReceiveSerializedItem|SendSerializedItem|GrabImage|GrabData)\w*\(') { return $true }
    }
    return $false
}

function Remove-NonCoreMethods([string]$path) {
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
            if (Test-RemoveMethod $sig $fileName) {
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
            if (-not $skip[$idx]) {
                [void]$sb.AppendLine($lines[$idx])
            }
        }
        [IO.File]::WriteAllText($path, $sb.ToString().TrimEnd("`r", "`n") + "`r`n")
        Write-Host "Stripped $removed method(s) from $fileName"
    }
}

foreach ($path in $Files) {
    Remove-NonCoreMethods $path
}
