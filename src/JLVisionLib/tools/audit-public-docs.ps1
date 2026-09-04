$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'
$files = Get-ChildItem $runtime -Filter '*.cs' -Recurse | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }

function Test-HasEditorBrowsableNever([string[]]$lines, [int]$index) {
    for ($i = $index - 1; $i -ge 0 -and $i -ge $index - 6; $i--) {
        if ($lines[$i] -match 'EditorBrowsable\(EditorBrowsableState\.Never\)') { return $true }
        if ($lines[$i] -match '^\t(public|internal|private|protected|namespace|class|struct|enum|\[assembly)') { break }
    }
    return $false
}

function Get-MemberDocState([string[]]$lines, [int]$index) {
    $start = $index
    while ($start -gt 0 -and ($lines[$start - 1] -match '^\t///' -or $lines[$start - 1] -match '^\t\[')) { $start-- }
    $doc = ($lines[$start..($index - 1)] | Where-Object { $_ -match '^\t///' }) -join "`n"
    $hasSummary = $doc -match '<summary>'
    $hasRemarks = $doc -match '<remarks>'
    $hasUsage = $doc -match '使用方法|<example>'
    return [pscustomobject]@{ HasSummary = $hasSummary; HasRemarks = $hasRemarks; HasUsage = $hasUsage }
}

$stats = [ordered]@{
    PublicMembers = 0
    MissingSummary = 0
    MissingRemarks = 0
    MissingUsage = 0
}
$issues = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $lines = [IO.File]::ReadAllLines($file.FullName)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -notmatch '^\tpublic ') { continue }
        if (Test-HasEditorBrowsableNever $lines $i) { continue }
        if ($lines[$i] -match '^\tpublic class |^\tpublic enum |^\tpublic struct |^\tpublic delegate ') { continue }
        $stats.PublicMembers++
        $doc = Get-MemberDocState $lines $i
        if (-not $doc.HasSummary) {
            $stats.MissingSummary++
            if ($issues.Count -lt 30) { $issues.Add("$($file.Name):$($i + 1) missing summary") }
        }
        if (-not $doc.HasRemarks) {
            $stats.MissingRemarks++
        }
        if (-not $doc.HasUsage) {
            $stats.MissingUsage++
        }
    }
}

$stats.GetEnumerator() | ForEach-Object { Write-Host ("{0}: {1}" -f $_.Key, $_.Value) }
if ($issues.Count -gt 0) {
    Write-Host 'Sample missing summary:'
    $issues | ForEach-Object { Write-Host "  $_" }
}
