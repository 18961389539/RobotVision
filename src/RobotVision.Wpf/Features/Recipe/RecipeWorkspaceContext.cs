using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方工作区上下文：窗口服务与向导共用的 UI 状态句柄。</summary>
public sealed record RecipeWorkspaceContext(
    IRecipeWorkspace Host,
    RecipeRoiEditor Roi,
    RecipeTestSession Test);
