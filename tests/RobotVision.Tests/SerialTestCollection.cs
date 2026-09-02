namespace RobotVision.Tests;

using Xunit;

/// <summary>并发/时序敏感测试集合：同集合内串行，其余测试仍可并行。</summary>
[CollectionDefinition("Serial", DisableParallelization = true)]
public sealed class SerialTestCollection;
