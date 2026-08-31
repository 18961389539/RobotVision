using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;

namespace RobotVision.Hosting;

/// <summary>结果库闭环提示（只读，不改配方）。分析页 / 配方页 / 对话 dashboard 共用。</summary>
public sealed record RecipeHealthHint(string Id, string Severity, string Message);

public static class RecipeHealthAdvisor
{
    public const double RefineFailRate = 0.12;
    public const int MinTotalForRate = 20;
    public const int MinAnglesForBimodal = 16;
    public const double BimodalShare = 0.22;
    public const double ScoreFloor = 0.55;
    public const double ScoreVsPeak = 0.70;
    public const int MinTotalForScore = 12;

    public static IReadOnlyList<RecipeHealthHint> Analyze(
        long total,
        IReadOnlyList<ResultCodeCount> codes,
        IReadOnlyList<double> angles,
        ResultPoseSpread? spread,
        double teachPeakScore = 0)
    {
        var hints = new List<RecipeHealthHint>(3);
        if (total <= 0)
            return hints;

        var n1019 = CountCode(codes, (int)VisionErrorCode.RefineFailed);
        if (total >= MinTotalForRate && n1019 / (double)total >= RefineFailRate)
        {
            hints.Add(new("refine_fail_rate", "warn",
                $"1019 精修失败约占 {100.0 * n1019 / total:0}%（{n1019}/{total}）。查照明与匹配阈值，或在模板匹配与卡尺+凸起之间改方法；不要把 1019 当 1007 报废。"));
        }

        if (angles.Count >= MinAnglesForBimodal)
        {
            var near = 0;
            var far = 0;
            for (var i = 0; i < angles.Count; i++)
            {
                var a = Math.Abs(AngleGeometry.NormalizeSignedDeg(angles[i]));
                if (a <= 45)
                    near++;
                else if (a >= 135)
                    far++;
            }

            var n = angles.Count;
            if (near / (double)n >= BimodalShare && far / (double)n >= BimodalShare)
            {
                hints.Add(new("angle_bimodal", "warn",
                    $"合格角呈 ±180° 双峰（近 0° {near}/{n}，近 180° {far}/{n}）。锁定壳体/凸起极性，或框选头尾特征；无向角不要声称有方向。"));
            }
        }

        var avg = spread?.AvgConfidence;
        if (avg is { } score)
        {
            if (teachPeakScore >= 0.5 && score < teachPeakScore * ScoreVsPeak)
            {
                hints.Add(new("score_drift", "warn",
                    $"合格精修分均值 {score:0.00}，低于示教峰 {teachPeakScore:0.00} 的 70%。请重新示教模板，不要自动改模板图。"));
            }
            else if (teachPeakScore < 0.5 && total >= MinTotalForScore && score < ScoreFloor)
            {
                hints.Add(new("score_low", "info",
                    $"合格精修分均值 {score:0.00} 偏低。检查照明、匹配阈值或改卡尺/孔槽精修。"));
            }
        }

        return hints;
    }

    public static RecipePrior? ToPlaybookPrior(
        IReadOnlyList<RecipeHealthHint> hints,
        SegmentRefineMethod? current,
        IReadOnlyList<SegmentRefineMethod>? policyOrder = null) =>
        ScenePlaybook.FromHealth(
            hints.Any(h => h.Id == "refine_fail_rate"),
            hints.Any(h => h.Id == "angle_bimodal"),
            hints.Any(h => h.Id is "score_drift" or "score_low"),
            current,
            policyOrder);

    private static long CountCode(IReadOnlyList<ResultCodeCount> codes, int code)
    {
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Code == code)
                return codes[i].Count;
        }

        return 0;
    }
}
