using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Teach;

/// <summary>ScenePlaybook —— 枚举/模式到中文标签的映射（与推荐逻辑分离，单一维护点）。</summary>
public static partial class ScenePlaybook
{
    public static string SceneLabel(SceneKind kind) => kind switch
    {
        SceneKind.HousingWithHole => "带孔/槽壳体",
        SceneKind.HousingWithTab => "细长壳体+凸起",
        SceneKind.Silhouette => "剪影/弱纹理轮廓",
        SceneKind.PrintedTexture => "可分头尾的纹理件",
        SceneKind.WeakTextureBar => "弱纹理细长条",
        SceneKind.NearCircular => "近圆/近方件",
        _ => "未分类",
    };

    public static string AngleModeLabel(AngleMode mode) => mode switch
    {
        AngleMode.MaskMinAreaRect => "最小外接矩形",
        AngleMode.DualCenterLine => "双模型中心连线",
        AngleMode.KeyPointLine => "关键点连线",
        AngleMode.DualBlobCenterLine => "双 BLOB 连线",
        AngleMode.DualTemplateCenterLine => "双模板连线",
        _ => "分割+精修",
    };

    public static string RefineLabel(SegmentRefineMethod? method) =>
        method is null ? "—" : TeachNarrator.MethodLabel(method.Value);
}
