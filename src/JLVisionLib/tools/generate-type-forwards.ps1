$root = Split-Path $PSScriptRoot -Parent
$runtimeDir = Join-Path $root 'JLVisionLib.Runtime'
$outFile = Join-Path $root 'TypeForwards.cs'

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('using System.Runtime.CompilerServices;')
$lines.Add('')

$matches = Select-String -Path (Join-Path $runtimeDir '*.cs') -Pattern '^public (class|enum|struct) (\w+)|^public delegate .+ (\w+)\('
$publicTypes = $matches | ForEach-Object {
    if ($_.Matches[0].Groups[2].Success -and $_.Matches[0].Groups[2].Value) { $_.Matches[0].Groups[2].Value }
    elseif ($_.Matches[0].Groups[3].Success -and $_.Matches[0].Groups[3].Value) { $_.Matches[0].Groups[3].Value }
} | Sort-Object -Unique

foreach ($type in $publicTypes) {
    if ($type -eq 'JlNativeApi') { continue }
    $lines.Add("[assembly: TypeForwardedTo(typeof(global::JLVisionLib.$type))]")
}

[IO.File]::WriteAllLines($outFile, $lines)
Write-Host "Generated $($lines.Count - 2) type forwards -> $outFile"
