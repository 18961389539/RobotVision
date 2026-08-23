using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 按配方的 AngleMode 创建对应策略（策略无状态，实例复用）。
/// 从 <see cref="AngleStrategyTypeRegistry"/> 查询工厂创建——新增角度模式 =
/// 实现 IAngleStrategyFactory 并 Register，本类与分支代码不再需要改动。
/// </summary>
public sealed class AngleStrategyFactory(ModelManager models)
{
    private readonly AngleStrategyTypeRegistry _registry = AngleStrategyTypeRegistry.Default;

    public IAngleStrategy Create(RecipeConfig recipe) =>
        _registry.Create(recipe.AngleMode, models)
        ?? throw new ArgumentOutOfRangeException(nameof(recipe), recipe.AngleMode, "未知角度模式");
}
