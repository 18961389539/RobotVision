# 运维辅助：从 vc_redist.x64.exe 安装包中提取内嵌 CAB 负载（离线部署 VC++ 运行库时用）。
# 用法: 先下载 vc_redist.x64.exe 到 $env:TEMPc_redist\，再运行本脚本，输出 payload.cab。

$exe = [IO.File]::ReadAllBytes("$env:TEMP\vc_redist\vc_redist.x64.exe")
$marker = [Text.Encoding]::ASCII.GetBytes("AttachedContainer")
$pos = -1
for ($i = 0; $i -lt $exe.Length - $marker.Length; $i++) {
    $ok = $true
    for ($j = 0; $j -lt $marker.Length; $j++) {
        if ($exe[$i + $j] -ne $marker[$j]) { $ok = $false; break }
    }
    if ($ok) { $pos = $i; break }
}
Write-Host "AttachedContainer marker at: $pos"
if ($pos -lt 0) { return }
# 在 marker 之后寻找 CAB 头 MSCF（0x4D534346）
$cabOff = -1
for ($i = $pos; $i -lt $exe.Length - 4; $i++) {
    if ($exe[$i] -eq 0x4D -and $exe[$i+1] -eq 0x53 -and $exe[$i+2] -eq 0x43 -and $exe[$i+3] -eq 0x46) { $cabOff = $i; break }
}
Write-Host "MSCF header at: $cabOff"
if ($cabOff -lt 0) { return }
# CAB 头：+4 csum, +8 cbCabinet(DWORD 总长)
$len = [BitConverter]::ToUInt32($exe, $cabOff + 8)
Write-Host "CAB size: $len (file tail: $($exe.Length - $cabOff))"
if ($len -gt ($exe.Length - $cabOff)) { $len = $exe.Length - $cabOff }
$out = "$env:TEMP\vc_redist\payload.cab"
[IO.File]::WriteAllBytes($out, $exe[$cabOff..($cabOff + $len - 1)])
Write-Host "Saved: $out ($len bytes)"
