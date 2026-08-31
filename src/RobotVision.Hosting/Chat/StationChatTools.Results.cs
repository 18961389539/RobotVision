using System.Globalization;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting.Chat;
public sealed partial class StationChatTools
{
    private Task<ChatToolResult> QueryResults(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "" or "page" or "analysis")
            action = "dashboard";
        if (Has(doc, "from") && ParseWhen(doc, "from") is null)
            return Task.FromResult(Fail("from 无法解析，请用 ISO 时间、today 或 -7d"));
        if (Has(doc, "to") && ParseWhen(doc, "to") is null)
            return Task.FromResult(Fail("to 无法解析，请用 ISO 时间、today 或 now"));

        var query = BuildResultQuery(doc, defaultLimit: action is "dashboard" ? 12 : 20, maxLimit: 80);
        if (action is "dashboard" or "angles" or "trend" or "by_recipe")
            query = WithAnalysisDefaultRange(doc, query);
        var grain = Str(doc, "grain").ToLowerInvariant();
        if (grain is not ("hour" or "day"))
            grain = LooksLikeToday(query) ? "hour" : "day";
        var bins = Math.Clamp(Int(doc, "bins", 12), 4, 24);
        try
        {
            switch (action)
            {
                case "info":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        path = _sqlite.DatabasePath,
                        folder = _sqlite.Folder,
                        writeEnabled = _sqlite.Enabled,
                        retainedDays = _sqlite.RetainedDays,
                        total = _sqlite.Count(),
                    }));
                case "recipes":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        recipes = _sqlite.ListRecipes(),
                        stations = _sqlite.ListStations(),
                        cameras = _sqlite.ListCameras(),
                        total = _sqlite.Count(),
                    }));
                case "codes":
                {
                    var codes = _sqlite.CountByCode(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        matched = _sqlite.Count(query),
                        codes = codes.Select(c => new
                        {
                            c.Code,
                            label = ResultAnalysis.DescribeCode(c.Code),
                            c.Count,
                        }),
                    }));
                }
                case "summary":
                {
                    var summary = _sqlite.Summarize(query);
                    var spread = _sqlite.QuerySpread(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        matched = _sqlite.Count(query),
                        summary,
                        spread,
                        yield = YieldPct(summary),
                    }));
                }
                case "angles":
                {
                    var angles = _sqlite.QueryAngles(query);
                    var spread = _sqlite.QuerySpread(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        n = angles.Count,
                        spread = new { spread.MinAngle, spread.MaxAngle, spread.StdAngle, spread.AvgConfidence },
                        histogram = ResultAnalysis.BuildHistogram(angles, bins),
                    }));
                }
                case "by_recipe":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        matched = _sqlite.Count(query),
                        recipes = _sqlite.SummarizeByRecipe(query).Select(MapRecipeStat),
                    }));
                case "trend":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        grain,
                        buckets = _sqlite.QueryTrend(query, grain),
                    }));
                case "dashboard":
                {
                    var summary = _sqlite.Summarize(query);
                    var spread = _sqlite.QuerySpread(query);
                    var angles = _sqlite.QueryAngles(query);
                    var codes = _sqlite.CountByCode(query);
                    var rows = _sqlite.Query(query);
                    var okQuery = query with { OkOnly = true, Code = null };
                    var okAngles = _sqlite.QueryAngles(okQuery);
                    var okSpread = _sqlite.QuerySpread(okQuery);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        path = _sqlite.DatabasePath,
                        matched = summary.Total,
                        yield = YieldPct(summary),
                        summary,
                        spread,
                        hints = RecipeHealthAdvisor.Analyze(
                            summary.Total, codes, okAngles, okSpread, TeachPeak(query.Recipe))
                            .Select(h => new { h.Id, h.Severity, h.Message }),
                        histogram = ResultAnalysis.BuildHistogram(angles, bins),
                        codes = codes.Select(c => new
                        {
                            c.Code,
                            label = ResultAnalysis.DescribeCode(c.Code),
                            c.Count,
                        }),
                        byRecipe = _sqlite.SummarizeByRecipe(query).Select(MapRecipeStat),
                        trend = _sqlite.QueryTrend(query, grain),
                        count = rows.Count,
                        rows = rows.Select(MapResultRow),
                    }));
                }
                case "rows":
                {
                    var rows = _sqlite.Query(query);
                    var summary = _sqlite.Summarize(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        path = _sqlite.DatabasePath,
                        matched = _sqlite.Count(query),
                        summary,
                        yield = YieldPct(summary),
                        count = rows.Count,
                        offset = query.Offset,
                        rows = rows.Select(MapResultRow),
                    }));
                }
                default:
                    return Task.FromResult(Fail("action 必须是 dashboard|rows|summary|angles|codes|by_recipe|trend|recipes|info"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private static double? YieldPct(ResultDbSummary summary) =>
        summary.Total == 0 ? null : Math.Round(100.0 * summary.Ok / summary.Total, 2);

    private double TeachPeak(string? recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName) || !RecipeLoader.IsValidRecipeName(recipeName))
            return 0;
        try
        {
            return _recipes.Get(recipeName).Template.TeachPeakScore;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static object MapRecipeStat(ResultRecipeStat s) => new
    {
        s.Recipe,
        s.Total,
        s.Ok,
        s.Failed,
        yield = s.Total == 0 ? (double?)null : Math.Round(100.0 * s.Ok / s.Total, 2),
        s.AvgMs,
        s.AvgAngle,
    };

    private static object MapResultRow(ResultDbRow r) => new
    {
        r.Id,
        r.T,
        r.Recipe,
        r.Station,
        r.Camera,
        r.X,
        r.Y,
        r.Angle,
        r.Confidence,
        r.Count,
        r.ElapsedMs,
        r.Code,
        codeLabel = ResultAnalysis.DescribeCode(r.Code),
        r.Message,
    };

}
