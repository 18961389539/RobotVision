# RobotVision TCP 冒烟测试：逐条发送命令并断言应答，失败退出码 1（CI/脚本可判定）。
# 用法: .\tools\smoke-test.ps1 [-Server 127.0.0.1] [-Port 9999] [-Commands @("PING","TRIGGER,A03")]
param(
    [string]$Server = "127.0.0.1",
    [int]$Port = 9999,
    # A03 = 文件回放相机（开发机可跑通全链路）；A01 = Basler 实拍（需 pylon + 相机），
    # 在装好 pylon 的工控机上把 A03 换成 A01 验证真实采集。
    [string[]]$Commands = @("PING", "TRIGGER,A03")
)
$failed = 0
foreach ($cmd in $Commands) {
    $client = $null
    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $client.Connect($Server, $Port)
        $stream = $client.GetStream()
        $stream.WriteTimeout = 5000
        $stream.ReadTimeout = 8000
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("$cmd`n")
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
        $buffer = New-Object byte[] 8192
        $sb = [System.Text.StringBuilder]::new()
        do {
            $n = $stream.Read($buffer, 0, $buffer.Length)
            if ($n -le 0) { break }
            [void]$sb.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $n))
        } while ($sb.ToString() -notmatch "`n$")
        $reply = $sb.ToString().Trim()
        Write-Host ">>> $cmd"
        Write-Host "<<< $reply"

        if ($cmd -eq "PING") {
            if ($reply -ne "PONG") { Write-Host "[失败] PING 应返回 PONG"; $failed++ }
        }
        elseif ($cmd -like "TRIGGER*") {
            if ($reply -notmatch "^(OK|ERR),") { Write-Host "[失败] TRIGGER 应答应以 OK, 或 ERR, 开头"; $failed++ }
            elseif ($reply -match "^ERR,") { Write-Host "[警告] TRIGGER 返回错误（配方/相机/标定相关，按产线预期判断）" }
        }
        else {
            if ($reply.Length -eq 0) { Write-Host "[失败] 无应答"; $failed++ }
        }
    } catch {
        Write-Host "[失败] 命令 $cmd 异常: $($_.Exception.Message)"
        $failed++
    } finally {
        if ($client) { $client.Close() }
    }
}

if ($failed -gt 0) {
    Write-Host "[冒烟测试] $failed 项失败"
    exit 1
}
Write-Host "[冒烟测试] 全部通过"
exit 0
