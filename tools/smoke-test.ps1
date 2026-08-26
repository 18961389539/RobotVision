# RobotVision TCP 冒烟测试：逐条发送命令并断言应答，失败退出码 1（CI/脚本可判定）。
# 用法:
#   .\tools\smoke-test.ps1                              # 默认 PING + A03(文件回放相机)
#   .\tools\smoke-test.ps1 -Commands @("PING","STATUS")
#   .\tools\smoke-test.ps1 -Commands @("PING","A03,100,100,0")   # 位姿四段触发
#   .\tools\smoke-test.ps1 -Commands @("PING","#3")              # 序列号触发
#   .\tools\smoke-test.ps1 -Commands @("PING","A03","NO_SUCH") -Expect @("PONG","^OK,","ERR,1001")
# 断言规则:
#   - 未提供 -Expect 时按命令自动推导：
#       PING  → 精确应答 PONG
#       STATUS→ 正则 ^OK,(ready|busy),\d+,\d+,\d+  （完整字段：状态,队列深度,队列上限,最近耗时）
#       其他  → 正则 ^(OK|ERR),   （触发行必须应答 OK 或 ERR）
#   - 提供 -Expect 时逐条覆盖自动规则；支持：
#       精确字符串（如 "PONG"）、正则（以 ^ 开头，如 "^OK,15.000"）、
#       前缀（其余情况，如 "ERR,1001"）
param(
    [string]$Server = "127.0.0.1",
    [int]$Port = 9999,
    # A03 = 文件回放相机（开发机可跑通全链路）；A01 = Basler 实拍（需 pylon + 相机），
    # 在装好 pylon 的工控机上把 A03 换成 A01 验证真实采集。亦可用序列号如 "3" 或 "#3"，
    # 或位姿四段 "A03,100,100,0"（OnArm 工位会做 1012 位姿一致性校验）。
    [string[]]$Commands = @("PING", "A03"),
    # 可选：期望断言，与 Commands 一一对应；留空按自动规则。
    [string[]]$Expect = @(),
    [int]$TimeoutMs = 8000
)
$failed = 0

function Send-TcpLine {
    param([string]$Line)
    $client = $null
    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $client.Connect($Server, $Port)
        $stream = $client.GetStream()
        $stream.WriteTimeout = 5000
        $stream.ReadTimeout = $TimeoutMs
        $bytes = [System.Text.Encoding]::ASCII.GetBytes("$Line`n")
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
        $buffer = New-Object byte[] 8192
        $sb = [System.Text.StringBuilder]::new()
        do {
            $n = $stream.Read($buffer, 0, $buffer.Length)
            if ($n -le 0) { break }
            [void]$sb.Append([System.Text.Encoding]::ASCII.GetString($buffer, 0, $n))
        } while ($sb.ToString() -notmatch "`n$")
        return $sb.ToString().Trim()
    }
    finally {
        if ($client) { $client.Close() }
    }
}

function Get-AutoExpect {
    param([string]$Cmd)
    if ($Cmd -eq "PING") { return "PONG" }
    if ($Cmd -eq "STATUS") { return "^OK,(ready|busy),\d+,\d+,\d+$" }
    # 其余命令按触发处理：应答必须以 OK, 或 ERR, 开头
    return "^(OK|ERR),"
}

function Test-Reply {
    param([string]$Reply, [string]$Expect)
    # 正则：以 ^ 开头
    if ($Expect.StartsWith("^")) {
        return $Reply -match $Expect
    }
    # 精确：完全一致（PING→PONG）
    if ($Reply -eq $Expect) { return $true }
    # 前缀：如 "ERR,1001" / "OK,15.000"
    return $Reply.StartsWith($Expect)
}

for ($i = 0; $i -lt $Commands.Count; $i++) {
    $cmd = $Commands[$i]
    $reply = $null
    try {
        $reply = Send-TcpLine $cmd
        Write-Host ">>> $cmd"
        Write-Host "<<< $reply"
    }
    catch {
        Write-Host "[失败] 命令 $cmd 异常: $($_.Exception.Message)"
        $failed++
        continue
    }

    $expect = if ($i -lt $Expect.Count -and !$Expect[$i].StartsWith("~")) {
        # 显式期望（不以 ~ 开头）
        $Expect[$i]
    }
    elseif ($i -lt $Expect.Count) {
        # ~ 前缀 = 强制使用自动规则（便于占位）
        Get-AutoExpect $cmd
    }
    else {
        Get-AutoExpect $cmd
    }

    if (Test-Reply -Reply $reply -Expect $expect) {
        Write-Host "[通过] 满足期望: $expect"
    }
    else {
        Write-Host "[失败] 应答 '$reply' 不满足期望 '$expect'"
        $failed++
    }
}

if ($failed -gt 0) {
    Write-Host "[冒烟测试] $failed 项失败"
    exit 1
}
Write-Host "[冒烟测试] 全部通过"
exit 0
