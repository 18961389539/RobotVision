# 运维辅助：解析 PE 文件的导入表（验证依赖 DLL/检查二进制完整性用）。
# 用法: .\pe-imports.ps1 -Path <exe或dll路径>

param([string]$Path)
$bytes = [IO.File]::ReadAllBytes($Path)
$br = New-Object IO.BinaryReader([IO.MemoryStream]::new($bytes))
function U16($off) { [BitConverter]::ToUInt16($bytes, $off) }
function U32($off) { [BitConverter]::ToUInt32($bytes, $off) }
$peOff = U32 0x3C
$machine = U16 ($peOff + 4)
$optOff = $peOff + 24
$magic = U16 $optOff
$pe32plus = ($magic -eq 0x20B)
$dataDirOff = $optOff + $(if ($pe32plus) { 112 } else { 96 })
$numSections = U16 ($peOff + 6)
$sizeOpt = U16 ($peOff + 20)
$secOff = $optOff + $sizeOpt
function RvaToOff($rva) {
    for ($i = 0; $i -lt $numSections; $i++) {
        $s = $secOff + $i * 40
        $va = U32 ($s + 12); $vs = U32 ($s + 8); $raw = U32 ($s + 20)
        if ($rva -ge $va -and $rva -lt ($va + $vs)) { return $raw + ($rva - $va) }
    }
    return -1
}
$importRva = U32 ($dataDirOff + 8)
Write-Host "Machine: $(switch($machine){0x8664{'x64'}0x14c{'x86'}0xAA64{'arm64'}default{'?'}})  Imports:"
if ($importRva -eq 0) { Write-Host "  (无导入表)"; return }
$desc = RvaToOff $importRva
while ($true) {
    $nameRva = U32 ($desc + 12)
    if ($nameRva -eq 0) { break }
    $no = RvaToOff $nameRva
    $end = $no; while ($bytes[$end] -ne 0) { $end++ }
    Write-Host ("  " + [Text.Encoding]::ASCII.GetString($bytes, $no, $end - $no))
    $desc += 20
}
