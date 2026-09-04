# HALCON rectangle2 对标一键脚本（RV 导出 + 可选 HDevelop）
param(
    [string]$BenchRoot = "",
    [switch]$SkipExport,
    [switch]$RunHalcon,
    [switch]$ShapeMatch
)

$ErrorActionPreference = "Stop"
$RepoRoot = if ($BenchRoot) { Split-Path $BenchRoot -Parent } else {
    (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}
if (-not $BenchRoot) {
    $BenchRoot = Join-Path $RepoRoot "benchmarks\halcon"
}

if ($ShapeMatch) {
    Write-Host "HALCON shape_match bench root: $BenchRoot"
    Push-Location $RepoRoot
    try {
        $filter = "FullyQualifiedName~Bench_shape_match_halcon_export_fixtures|FullyQualifiedName~Bench_shape_match_halcon_robotvision_baseline"
        if (-not $SkipExport) {
            dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj -c Release --no-restore `
                --filter $filter 2>&1 | Write-Host
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
        if ($RunHalcon) {
            $hdevScript = Join-Path $BenchRoot "bench_shape_match.hdev"
            $halconOut = Join-Path $BenchRoot "results\shape_match_halcon_results.csv"
            $candidates = @(
                "${env:HALCONROOT}\bin\x64-win64\hdevelop.exe",
                "C:\Program Files\MVTec\HALCON-24.05-Steady\bin\x64-win64\hdevelop.exe",
                "C:\Program Files\MVTec\HALCON-23.11-Steady\bin\x64-win64\hdevelop.exe"
            )
            $hdev = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
            if (-not $hdev) {
                Write-Warning "HDevelop not found. Run bench_shape_match.hdev manually."
                exit 2
            }
            Write-Host "Running HDevelop: $hdev"
            & $hdev -run $hdevScript
            if (-not (Test-Path $halconOut)) {
                Write-Warning "Expected $halconOut"
                exit 3
            }
            dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj -c Release --no-restore `
                --filter "FullyQualifiedName~Bench_shape_match_halcon_side_by_side"
            exit $LASTEXITCODE
        }
        else {
            Write-Host "RV baseline: $(Join-Path $BenchRoot 'results\shape_match_robotvision_results.csv')"
            Write-Host "Side-by-side will SKIP until shape_match_halcon_results.csv exists."
            Write-Host "Run: .\run_halcon_bench.ps1 -ShapeMatch -RunHalcon"
        }
    }
    finally { Pop-Location }
    exit 0
}

# ── rectangle2 bench (default) ──

Write-Host "HALCON bench root: $BenchRoot"

if (-not $SkipExport) {
    Push-Location $RepoRoot
    try {
        dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj -c Release --no-restore `
            --filter "FullyQualifiedName~Bench_halcon_export_fixtures|FullyQualifiedName~Bench_halcon_robotvision_baseline|FullyQualifiedName~Bench_halcon_gap_report|FullyQualifiedName~Bench_halcon_contour_halcon_clip0" `
            2>&1 | Write-Host
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }
}

$hdevScript = Join-Path $BenchRoot "bench_rectangle2.hdev"
$halconOut = Join-Path $BenchRoot "results\halcon_results.csv"

if ($RunHalcon) {
    $candidates = @(
        "${env:HALCONROOT}\bin\x64-win64\hdevelop.exe",
        "C:\Program Files\MVTec\HALCON-24.05-Steady\bin\x64-win64\hdevelop.exe",
        "C:\Program Files\MVTec\HALCON-23.11-Steady\bin\x64-win64\hdevelop.exe"
    )
    $hdev = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $hdev) {
        Write-Warning "HDevelop not found. Set HALCONROOT or install HALCON, then run bench_rectangle2.hdev manually."
        exit 2
    }
    Write-Host "Running HDevelop: $hdev"
    & $hdev -run $hdevScript
    if (-not (Test-Path $halconOut)) {
        Write-Warning "Expected $halconOut — adjust paths inside bench_rectangle2.hdev"
        exit 3
    }
    Push-Location $RepoRoot
    try {
        dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj -c Release --no-restore `
            --filter "FullyQualifiedName~Bench_halcon_side_by_side_engine_parity"
        exit $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "RV baseline (daily clip=2): $(Join-Path $BenchRoot 'results\robotvision_results.csv')"
    Write-Host "RV baseline (HALCON clip=0): $(Join-Path $BenchRoot 'results\robotvision_results_halcon_clip0.csv')"
    Write-Host "Truth gaps:  $(Join-Path $BenchRoot 'results\truth_gaps.csv')"
    Write-Host ""
    Write-Host "No HALCON required for the above. Side-by-side parity test will SKIP until halcon_results.csv exists."
    Write-Host "See benchmarks\halcon\README.md (section: 没有 HALCON 环境时)."
    Write-Host ""
    Write-Host "To run HALCON (optional, on a machine with HDevelop): .\run_halcon_bench.ps1 -RunHalcon"
}
