namespace RobotVision.Teach;

/// <summary>
/// 跨工业场景的配方推荐：先分类场景，再套任务约束，赛马只在资格方法里比。不改 TRIGGER；由配方向导 / 示教页调用。
///
/// 原为单一大类，现按职责拆成分部类（同类 <c>ScenePlaybook</c>，公共 API 不变）：
/// <list type="bullet">
///   <item>词汇（枚举/记录）：<c>SceneModel.cs</c></item>
///   <item>标签映射：<c>ScenePlaybook.Labels.cs</c></item>
///   <item>场景分类与图像度量：<c>ScenePlaybook.Describe.cs</c></item>
///   <item>推荐决策与置信度：<c>ScenePlaybook.Recommend.cs</c></item>
///   <item>先验存取：<c>ScenePlaybook.Priors.cs</c></item>
///   <item>极性推断：<c>ScenePlaybook.Polarity.cs</c></item>
/// </list>
/// </summary>
public static partial class ScenePlaybook
{
}
