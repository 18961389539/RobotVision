using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Teach;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方编辑器派生提示文案（纯函数，无 UI 状态）。</summary>
internal static class RecipeEditorHints
{
    internal static string RotationCenter(RecipeConfig editor, ICalibrationRuntime calibration)
    {
        if (editor.RotationCompensation != RotationCompensationMode.EccentricTool)
            return "";
        if (string.IsNullOrWhiteSpace(editor.StationId) ||
            !calibration.RotationCenterProfiles.Any(p =>
                string.Equals(p.StationId, editor.StationId, StringComparison.OrdinalIgnoreCase)))
            return $"工位 {editor.StationId ?? "（空）"} 未做旋转轴心标定：偏心补偿保存/触发将被拒绝，请先在标定向导完成轴心标定";
        return "";
    }

    internal static string UndirectedEccentric(RecipeConfig editor) =>
        editor.RotationCompensation == RotationCompensationMode.EccentricTool &&
        RecipeLoader.HasUndirectedAngle(editor)
            ? "无向角（最小外接矩形或直线拟合）不能与偏心工具同时使用，保存将被拒绝。请改用分割+精修有向方法或关闭偏心补偿。"
            : "";

    internal static string Mapping(RecipeConfig editor, ICalibrationRuntime calibration)
    {
        if (string.IsNullOrWhiteSpace(editor.StationId))
            return "未选工位：检出目标后将返回 1004，请选外参/多项式/比例标定档案";
        var station = editor.StationId!;
        var poly = calibration.PolynomialProfiles.FirstOrDefault(p =>
            string.Equals(p.StationId, station, StringComparison.OrdinalIgnoreCase));
        var hasExt = calibration.HasExtrinsic(station);
        if (poly is not null && hasExt)
            return $"工位 {station} 同时有多项式与外参：生产只用多项式（原图），外参被忽略";
        if (poly is not null &&
            string.Equals(poly.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
            return $"工位 {station} 为棋盘毫米系（非机器人基座标），PLC 不能直接当 TCP 坐标使用";
        if (poly is not null &&
            string.Equals(poly.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
            poly.HasTeachPose)
            return $"工位 {station} 为末端相机：触发行必须带 X,Y,RZ，否则 1014";
        var ext = calibration.ExtrinsicProfiles.FirstOrDefault(p =>
            string.Equals(p.StationId, station, StringComparison.OrdinalIgnoreCase));
        if (ext is not null &&
            string.Equals(ext.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
            ext.HasTeachPose)
            return $"工位 {station} 为末端相机：触发行必须带 X,Y,RZ，否则 1014";
        if (calibration.GetScale(station) is not null && poly is null && !hasExt)
            return $"工位 {station} 为比例标定（图像平面 mm，非机器人基座标），PLC 不能直接当 TCP 坐标使用";
        return "";
    }

    internal static string AngleModeHint(AngleMode mode) => mode switch
    {
        AngleMode.MaskMinAreaRect => "最小外接矩形角度为 [0,180)，无头尾。与偏心工具同时保存会被拒绝。",
        AngleMode.DualCenterLine => "默认全局就近配对，多目标间距接近时可能配错；开「窗口配对」后 B 只在 A 外扩窗口内检测，多目标不配错",
        AngleMode.MaskTemplate => "分割给粗框，精修过门才输出有向角。失败默认 1019。方法推荐与赛马见配方向导；示教仅写极性/阈值。保存后才上产线。",
        AngleMode.DualBlobCenterLine => "BLOB1 只在 ROI1 内定位，BLOB2 只在 ROI2 内定向（有方向）。不设 ROI2 则用主包围盒外扩窗口。次BLOB缺失该目标不输出；无需模型",
        _ => "",
    };

    internal static string RefineMethod(RecipeConfig editor) =>
        editor.AngleMode != AngleMode.MaskTemplate
            ? ""
            : editor.Template.RefineMethod switch
            {
                SegmentRefineMethod.LineFit =>
                    "直线拟合吃掩码长边（会先剔凸起），角度无方向 [0,180)。拟合失败默认 1019。与偏心工具同时保存会被拒绝。",
                SegmentRefineMethod.CentroidHoleLine =>
                    "质心连到掩码内最大孔/槽，有头尾。分割须能画出孔或槽。失败默认 1019。",
                SegmentRefineMethod.CaliperTab =>
                    "卡尺放在壳体长边上（短轴中心取两线中线）；黄线指向暗凸起一侧。配方测试会叠加探针。失败默认 1019。切到此方法后抓取原点与模板中心不同，需重新对示教。",
                SegmentRefineMethod.Sift =>
                    "SIFT 把示教模板配到当前分割框内的原图，相似变换给出 XY 和有向角。需先示教整颗目标（不要只裁局部特征框）。试触发/监控会叠青色内点、红色外点。弱纹理或外观变化大会配不上，失败默认 1019。切到此方法后抓取原点与卡尺中心不同，需重新对示教。",
                SegmentRefineMethod.ShapeMatch =>
                    "形状匹配把示教图的 Canny 轮廓配到当前分割目标的转正窗。可示教整颗，或与模板一样框选局部轮廓（齿/缺口）。试触发/监控会叠青色命中点、红色未命中点。切到此方法后抓取原点与卡尺中心不同，需重新对示教。",
                _ => "模板匹配：十字是 NCC 匹配峰（特征中心），不是壳体中心。结果图金框「匹配」随峰；橙框「特征」是示教裁剪窗。转正裁剪窗默认开启。匹配失败默认 1019。",
            };
}
