using BenchmarkDotNet.Running;
using RobotVision.Benchmarks;

// 基准测试入口：dotnet run -c Release --project benchmarks/RobotVision.Benchmarks
// 或指定单个基准：dotnet run -c Release --project benchmarks/RobotVision.Benchmarks -- --filter *AngleGeometry*
// 注意：BenchmarkDotNet 需要 Release 构建与完整运行（每基准数次预热+测量），耗时较长，不纳入 CI 常规测试。

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
