using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>MaskMinAreaRect 策略工厂：单分割模型，最小外接矩形长边方向。</summary>
public sealed class MaskMinAreaRectStrategyFactory : IAngleStrategyFactory
{
    public AngleMode Mode => AngleMode.MaskMinAreaRect;

    public string Label => "单分割模型（最小外接矩形角度）";

    public IAngleStrategy Create(ModelManager models, ILogger? logger = null) =>
        new MaskMinAreaRectStrategy(models);
}

/// <summary>DualCenterLine 策略工厂：双检测模型，两目标中心连线方向。</summary>
public sealed class DualCenterLineStrategyFactory : IAngleStrategyFactory
{
    public AngleMode Mode => AngleMode.DualCenterLine;

    public string Label => "双检测模型（中心连线角度）";

    public IAngleStrategy Create(ModelManager models, ILogger? logger = null) =>
        new DualCenterLineStrategy(models);
}

/// <summary>KeyPointLine 策略工厂：关键点模型，两关键点连线方向。</summary>
public sealed class KeyPointLineStrategyFactory : IAngleStrategyFactory
{
    public AngleMode Mode => AngleMode.KeyPointLine;

    public string Label => "关键点模型（两点连线角度）";

    public IAngleStrategy Create(ModelManager models, ILogger? logger = null) =>
        new KeyPointLineStrategy(models);
}

/// <summary>MaskTemplate 策略工厂：分割粗定位 + 精修（模板匹配/直线拟合）。</summary>
public sealed class MaskTemplateStrategyFactory : IAngleStrategyFactory
{
    public AngleMode Mode => AngleMode.MaskTemplate;

    public string Label => "分割+精修（模板匹配或直线拟合）";

    public IAngleStrategy Create(ModelManager models, ILogger? logger = null) =>
        new MaskTemplateStrategy(models);
}
