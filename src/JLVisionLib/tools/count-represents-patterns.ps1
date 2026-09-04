$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path $PSScriptRoot -Parent) 'JLVisionLib.Runtime'
$prefixCounts = @{}
$totalRepresents = 0
$totalYuanShi = 0
Get-ChildItem $root -Filter '*.cs' | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    $totalYuanShi += ([regex]::Matches($text, '原始说明：')).Count
    foreach ($m in [regex]::Matches($text, '([A-Za-z][A-Za-z ]* represents):')) {
        $key = $m.Groups[1].Value.Trim()
        if (-not $prefixCounts.ContainsKey($key)) { $prefixCounts[$key] = 0 }
        $prefixCounts[$key]++
        $totalRepresents++
    }
}
Write-Host "Total 'X represents:' occurrences: $totalRepresents"
Write-Host "Total '原始说明：' occurrences: $totalYuanShi"
Write-Host ''
$prefixCounts.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
    Write-Host ("{0,-35} {1}" -f $_.Key, $_.Value)
}
