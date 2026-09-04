using RobotVision.Core.Recipe;

namespace RobotVision.Teach;

/// <summary>
/// 示教/赛马建议的<b>人话渲染出口</b>：把「文案」从「算法计算」里剥离出来集中在此。
/// 计算侧（<see cref="SegmentRefineBakeOff"/> / <see cref="RefineParamTuner"/> / <see cref="SegmentRefineAdvisor"/>）
/// 只负责判定与数值，字符串一律走本类，改措辞不再动算法。文案与历史逐字节一致。
/// </summary>
public static class TeachNarrator
{
    // —— 多帧赛马聚合一行（Aggregate 与 ScoreThreshold 共用，消除重复）——
    public static string RaceSummary(RaceScore.Aggregate a)
    {
        if (a.OkCount == 0)
            return $"{a.TotalFrames} 帧均未过门";
        if (a.AngleSampleCount < 2)
            return FormattableString.Invariant($"{a.OkCount}/{a.TotalFrames} 过门，均分 {a.MeanScore:0.00}");
        return FormattableString.Invariant($"{a.OkCount}/{a.TotalFrames} 过门，均分 {a.MeanScore:0.00}，角σ {a.AngleStdDeg:0.00}°");
    }

    // —— 单帧四路赛马：每方法候选的过门/未过门/未跑说明 ——
    public static string LineFitOk(double residualDeg) => FormattableString.Invariant($"直线拟合过门（残差 {residualDeg:0.00}°）");
    public const string LineFitMiss = "直线拟合未过门";

    public static string CentroidHoleOk(double quality) => FormattableString.Invariant($"质心-内标连线过门（质量 {quality:0.00}）");
    public const string CentroidHoleMiss = "掩码内无稳定孔/槽";
    public const string CentroidHoleSkip = "无分割掩码，未跑孔槽";

    public static string CaliperOk(double parallelDeg) => FormattableString.Invariant($"卡尺过门（平行差 {parallelDeg:0.00}°）");
    public const string CaliperMiss = "卡尺未过门（无边或凸起不可判）";

    public const string TemplateSkip = "未示教模板，未跑匹配";
    public const string ShapeSkip = "未示教模板，未跑形状匹配";
    public const string SiftSkip = "未示教模板，未跑 SIFT";

    public const string TemplateNoPeak = "模板匹配无峰";
    public const string TemplateCropFail = "转正裁剪失败";

    public static string TemplatePeak(double score, double threshold, bool passed) =>
        passed ? FormattableString.Invariant($"模板过门（NCC {score:0.00}）") : FormattableString.Invariant($"模板分 {score:0.00} 低于阈值 {threshold:0.00}");

    public const string ShapeNoEdge = "示教图边缘太少，未跑形状匹配";
    public static string ShapeResult(bool passed, double hitRate, double meanDistPx) =>
        passed
            ? FormattableString.Invariant($"形状匹配过门（命中 {hitRate:0.00} 均距 {meanDistPx:0.00}px）")
            : FormattableString.Invariant($"形状匹配未过门（命中 {hitRate:0.00} 均距 {meanDistPx:0.00}px）");

    public const string SiftNoFeature = "示教图 SIFT 特征太少";
    public const string SiftMiss = "SIFT 未过门（匹配点不够）";
    public static string SiftOk(int inliers, int matches) => $"SIFT 过门（内点 {inliers}/{matches}）";

    // —— 参数寻优（RefineParamTuner.Tune）——
    public static string TunerThreshold(double from, double to) => FormattableString.Invariant($"匹配门 {from:0.00}→{to:0.00}");
    public static string TunerRange(double from, double to) => FormattableString.Invariant($"搜索角 ±{from:0}°→±{to:0}°");
    public static string TunerEdgeMatch(bool on) => on ? "开边缘定角" : "关边缘定角";
    public static string TunerEdgePolarity(HousingEdgePolarity p) =>
        p == HousingEdgePolarity.BrightToDark ? "锁定亮→暗" : "锁定暗→亮";
    public static string TunerTabPolarity(TabPolarityLock p) =>
        p == TabPolarityLock.PlusShortAxis ? "锁定凸起 +短轴" : "锁定凸起 −短轴";
    public static string TunerExpectedCount(int mode, int taskExpected) =>
        taskExpected == 0
            ? $"期望件数 {mode}（回放假数众数）"
            : $"回放假数众数 {mode}，与任务期望 {taskExpected} 不符";

    public static string TunerSummary(IReadOnlyList<string> parts) =>
        parts.Count == 0
            ? "参数与当前配方接近，无需改门限。"
            : "方法已定，参数寻优（未写入产线）：" + string.Join("；", parts) + "。采用后请再试触发。";

    // —— 示教建议（SegmentRefineAdvisor）——
    /// <summary>精修方法中文短名的唯一来源（ScenePlaybook.RefineLabel / SegmentRefineAdvisor 均引用此）。</summary>
    public static string MethodLabel(SegmentRefineMethod method) => method switch
    {
        SegmentRefineMethod.LineFit => "直线拟合",
        SegmentRefineMethod.CentroidHoleLine => "质心-内标连线",
        SegmentRefineMethod.CaliperTab => "卡尺长边+凸起极性",
        SegmentRefineMethod.Sift => "SIFT特征匹配",
        SegmentRefineMethod.ShapeMatch => "形状匹配",
        _ => "模板匹配",
    };

    public static string RaceTable(IReadOnlyList<string> rows) =>
        rows.Count == 0 ? "" : " 赛马：" + string.Join(" · ", rows);
    public static string RaceRow(string label, bool ok, double score) =>
        $"{label} {(ok ? FormattableString.Invariant($"✓{score:0.00}") : "—")}";

    public static string MeasuredWin(string methodLabel, string note) =>
        $" 采用实测胜出：{methodLabel}。{note}";
    public static string ReplayWin(string methodLabel, string note) =>
        $" 采用回放胜出：{methodLabel}。{note}";

    public static string TemplateNoOrientation(bool hasFeatureRoi) =>
        hasFeatureRoi
            ? " 已建议特征框（对 180° 最不对称的块）。"
            : " 当前 0°/180° 分差偏小，示教时请框选凸起或齿脚。";

    public static string TeachPeak(double teachPeak, double suggestedTh, double peakSharp)
    {
        var s = FormattableString.Invariant($" 示教峰 NCC {teachPeak:0.00}，建议匹配阈值 {suggestedTh:0.00}");
        if (peakSharp < 0.08)
            s += FormattableString.Invariant($"（主峰较钝 锐度 {peakSharp:0.00}，已抬高匹配门）");
        return s + "。";
    }

    public static string BoxConfidence(double suggested, double instanceConfidence) =>
        FormattableString.Invariant($" 检测置信度建议 {suggested:0.00}（实例 {instanceConfidence:0.00}，当前门过紧）。");
    public static string PixelConfidence(double suggested) =>
        FormattableString.Invariant($" 掩码像素置信度建议 {suggested:0.00}。");
    public static string EdgeLocked(HousingEdgePolarity p) =>
        p == HousingEdgePolarity.DarkToBright ? " 已锁定暗场边缘。" : " 已锁定亮场边缘。";
    public static string TabLocked(TabPolarityLock p) =>
        p == TabPolarityLock.PlusShortAxis
            ? " 示教凸起在 +短轴（运行每帧实测）。"
            : " 示教凸起在 −短轴（运行每帧实测）。";
}
