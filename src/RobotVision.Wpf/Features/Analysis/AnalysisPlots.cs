using System.Globalization;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.Analysis;

/// <summary>分析页 OxyPlot：深色主题、中文字体；直方图带均值/σ，趋势叠加合格率，位姿等比例散点。</summary>
internal static class AnalysisPlots
{
    public static readonly OxyColor Ok = OxyColor.FromRgb(0x6B, 0xCB, 0x77);
    public static readonly OxyColor Fail = OxyColor.FromRgb(0xFF, 0x6B, 0x6B);
    public static readonly OxyColor Accent = OxyColor.FromRgb(0x5B, 0x9B, 0xF5);
    public static readonly OxyColor Yield = OxyColor.FromRgb(0xF5, 0xC2, 0x42);
    private static readonly OxyColor Panel = OxyColor.FromRgb(0x20, 0x20, 0x20);
    private static readonly OxyColor Text = OxyColor.FromRgb(0xC8, 0xC8, 0xC8);
    private static readonly OxyColor Grid = OxyColor.FromRgb(0x3A, 0x3A, 0x3A);
    private static readonly OxyColor Axis = OxyColor.FromRgb(0x66, 0x66, 0x66);

    /// <summary>仪表图：悬停读数，禁止滚轮缩放以免带动整页。</summary>
    public static IPlotController Inspect { get; } = CreateInspect();

    /// <summary>位姿散点：悬停 + 滚轮缩放 + 右键平移。</summary>
    public static IPlotController Explore { get; } = CreateExplore();

    public static PlotModel Empty(string hint) =>
        Base(withAxes: false, hint);

    public static PlotModel Histogram(
        IReadOnlyList<ResultHistogramBar> bars,
        OxyColor? color = null,
        double? mean = null,
        double? std = null,
        string unit = "°")
    {
        if (bars.Count == 0)
            return Empty("暂无样本");

        var plot = Base();
        var fill = color ?? Accent;
        var peak = Math.Max(1, bars.Max(b => b.Count));
        plot.Axes.Add(Linear(AxisTitle(unit), AxisPosition.Bottom, zoom: false));
        plot.Axes.Add(Linear("次数", minZero: true, zoom: false));

        var series = new RectangleBarSeries
        {
            Title = "次数",
            FillColor = fill,
            StrokeColor = OxyColor.FromAColor(180, fill),
            StrokeThickness = 0.6,
            TrackerFormatString = "{1}: {2:0}",
        };
        foreach (var bar in bars)
        {
            if (bar.End <= bar.Start)
                continue;
            series.Items.Add(new RectangleBarItem(bar.Start, 0, bar.End, bar.Count));
        }
        plot.Series.Add(series);

        if (mean is { } mu && std is { } sigma && sigma > 1e-9)
        {
            plot.Annotations.Add(new RectangleAnnotation
            {
                MinimumX = mu - sigma,
                MaximumX = mu + sigma,
                MinimumY = 0,
                MaximumY = peak * 1.12,
                Fill = OxyColor.FromAColor(28, Yield),
                StrokeThickness = 0,
                Layer = AnnotationLayer.BelowSeries,
            });
        }

        if (mean is { } meanValue)
            AddVerticalGuide(plot, meanValue, $"μ {Fmt(meanValue)}{unit}");

        return Done(plot);
    }

    public static PlotModel CodeShare(IReadOnlyList<ResultCodeCount> codes)
    {
        if (codes.Count == 0)
            return Empty("暂无结果码");

        var total = codes.Sum(c => c.Count);
        var labels = codes.Reverse().Select(c =>
        {
            var pct = total == 0 ? 0 : 100.0 * c.Count / total;
            return $"{ResultAnalysis.DescribeCode(c.Code)}  {pct.ToString("0.0", CultureInfo.InvariantCulture)}%";
        }).ToList();
        var plot = Base();
        var cat = CategoryLeft(labels);
        cat.Key = "cat";
        plot.Axes.Add(cat);
        var values = Linear("次数", AxisPosition.Bottom, minZero: true, zoom: false);
        values.Key = "val";
        plot.Axes.Add(values);

        var bars = new BarSeries
        {
            StrokeThickness = 0,
            XAxisKey = "val",
            YAxisKey = "cat",
            TrackerFormatString = "{1}: {2:0}",
        };
        foreach (var item in codes.Reverse())
        {
            bars.Items.Add(new BarItem(item.Count)
            {
                Color = item.Code == 0 ? Ok : Fail,
            });
        }
        plot.Series.Add(bars);
        return Done(plot);
    }

    public static PlotModel Trend(IReadOnlyList<ResultTrendBucket> buckets)
    {
        if (buckets.Count == 0)
            return Empty("暂无趋势");

        var plot = Base();
        var cats = buckets.Select(b => ShortTrendLabel(b.Label)).ToList();
        var cat = Category(cats, angle: buckets.Count > 8 ? 50 : 0);
        cat.Key = "cat";
        plot.Axes.Add(cat);

        var countAxis = Linear("次数", AxisPosition.Left, minZero: true, zoom: false);
        countAxis.Key = "count";
        plot.Axes.Add(countAxis);
        plot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Right,
            Key = "yield",
            Title = "合格率 %",
            TitleColor = Text,
            TextColor = Text,
            TicklineColor = Axis,
            Minimum = 0,
            Maximum = 105,
            FontSize = 10,
            IsPanEnabled = false,
            IsZoomEnabled = false,
        });

        var ok = new BarSeries
        {
            Title = "合格",
            FillColor = Ok,
            StrokeThickness = 0,
            IsStacked = true,
            XAxisKey = "count",
            YAxisKey = "cat",
            TrackerFormatString = "{0}\n{1}: {2:0}",
        };
        var fail = new BarSeries
        {
            Title = "失败",
            FillColor = Fail,
            StrokeThickness = 0,
            IsStacked = true,
            XAxisKey = "count",
            YAxisKey = "cat",
            TrackerFormatString = "{0}\n{1}: {2:0}",
        };
        var line = new LineSeries
        {
            Title = "合格率",
            Color = Yield,
            StrokeThickness = 1.6,
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            MarkerFill = Yield,
            XAxisKey = "cat",
            YAxisKey = "yield",
            TrackerFormatString = "{0}\n{1}: {2:0.0}%",
        };
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            ok.Items.Add(new BarItem(bucket.Ok));
            fail.Items.Add(new BarItem(bucket.Failed));
            var yld = bucket.Total == 0 ? 0 : 100.0 * bucket.Ok / bucket.Total;
            line.Points.Add(new DataPoint(i, yld));
        }
        plot.Series.Add(ok);
        plot.Series.Add(fail);
        plot.Series.Add(line);
        AddLegend(plot);
        return Done(plot);
    }

    public static PlotModel RecipeYield(IReadOnlyList<ResultRecipeStat> recipes)
    {
        if (recipes.Count == 0)
            return Empty("暂无配方统计");

        var plot = Base();
        var names = recipes.Select(r => r.Recipe).Reverse().ToList();
        var cat = CategoryLeft(names);
        cat.Key = "cat";
        plot.Axes.Add(cat);
        plot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Key = "val",
            Title = "合格率 %",
            TitleColor = Text,
            TextColor = Text,
            TicklineColor = Axis,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = Grid,
            Minimum = 0,
            Maximum = 100,
            FontSize = 10,
            IsPanEnabled = false,
            IsZoomEnabled = false,
        });
        var bars = new BarSeries
        {
            StrokeThickness = 0,
            XAxisKey = "val",
            YAxisKey = "cat",
            TrackerFormatString = "{1}: {2:0.0}%",
        };
        foreach (var item in recipes.Reverse())
        {
            var yld = item.Total == 0 ? 0 : 100.0 * item.Ok / item.Total;
            bars.Items.Add(new BarItem(yld) { Color = YieldColor(yld) });
        }
        plot.Series.Add(bars);
        return Done(plot);
    }

    public static PlotModel Scatter(
        IReadOnlyList<ResultXyPoint> points,
        double? meanX = null,
        double? meanY = null,
        double? stdX = null,
        double? stdY = null)
    {
        if (points.Count == 0)
            return Empty("暂无 XY 坐标");

        var plot = Base();
        var xs = points.Select(p => p.X).ToList();
        var ys = points.Select(p => p.Y).ToList();
        var minX = xs.Min();
        var maxX = xs.Max();
        var minY = ys.Min();
        var maxY = ys.Max();
        var span = Math.Max(maxX - minX, maxY - minY);
        if (span < 1e-9)
            span = 1;
        var pad = span * 0.15 + 1e-3;
        var cx = (minX + maxX) / 2;
        var cy = (minY + maxY) / 2;

        plot.Axes.Add(Linear("X mm", AxisPosition.Bottom, min: cx - span / 2 - pad, max: cx + span / 2 + pad));
        plot.Axes.Add(Linear("Y mm", AxisPosition.Left, min: cy - span / 2 - pad, max: cy + span / 2 + pad));

        var mx = meanX ?? xs.Average();
        var my = meanY ?? ys.Average();
        if (stdX is { } sx && stdY is { } sy && sx > 1e-9 && sy > 1e-9)
        {
            plot.Annotations.Add(new EllipseAnnotation
            {
                X = mx,
                Y = my,
                Width = 4 * sx,
                Height = 4 * sy,
                Fill = OxyColor.FromAColor(22, Accent),
                Stroke = Accent,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
            });
        }

        AddVerticalGuide(plot, mx, $"X̄ {Fmt(mx)}");
        plot.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal,
            Y = my,
            Color = Yield,
            StrokeThickness = 1.2,
            LineStyle = LineStyle.Dash,
            Text = $"Ȳ {Fmt(my)}",
            TextColor = Text,
            FontSize = 10,
        });

        var ok = new ScatterSeries
        {
            Title = "合格",
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            MarkerFill = Ok,
            MarkerStrokeThickness = 0,
            TrackerFormatString = "{0}\nX={2:0.###} Y={3:0.###}",
        };
        var fail = new ScatterSeries
        {
            Title = "失败",
            MarkerType = MarkerType.Triangle,
            MarkerSize = 3.5,
            MarkerFill = Fail,
            MarkerStrokeThickness = 0,
            TrackerFormatString = "{0}\nX={2:0.###} Y={3:0.###}",
        };
        foreach (var p in points)
        {
            var pt = new ScatterPoint(p.X, p.Y);
            if (p.Code == 0)
                ok.Points.Add(pt);
            else
                fail.Points.Add(pt);
        }
        if (ok.Points.Count > 0)
            plot.Series.Add(ok);
        if (fail.Points.Count > 0)
            plot.Series.Add(fail);
        AddLegend(plot);
        return Done(plot);
    }

    private static PlotModel Base(bool withAxes = true, string? emptyHint = null)
    {
        var plot = new PlotModel
        {
            Background = Panel,
            PlotAreaBackground = Panel,
            PlotAreaBorderColor = Axis,
            PlotAreaBorderThickness = new OxyThickness(0.6),
            TextColor = Text,
            TitleColor = Text,
            DefaultFont = "Microsoft YaHei UI",
            DefaultFontSize = 11,
            Padding = new OxyThickness(4, 4, 8, 4),
        };
        if (!withAxes && emptyHint is not null)
        {
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, IsAxisVisible = false, Minimum = 0, Maximum = 1 });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, IsAxisVisible = false, Minimum = 0, Maximum = 1 });
            plot.Annotations.Add(new TextAnnotation
            {
                Text = emptyHint,
                TextPosition = new DataPoint(0.5, 0.5),
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                StrokeThickness = 0,
                TextColor = OxyColor.FromRgb(0x88, 0x88, 0x88),
                FontSize = 12,
            });
        }
        return Done(plot);
    }

    private static PlotModel Done(PlotModel plot)
    {
        // OxyPlot 2.x: PlotModel.Update(bool) 是 internal,公开刷新入口为 InvalidatePlot(true)(内部完成 Update+重绘)
        plot.InvalidatePlot(true);
        return plot;
    }

    private static CategoryAxis Category(IList<string> labels, double angle)
    {
        var axis = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Angle = angle,
            TextColor = Text,
            TicklineColor = Axis,
            FontSize = 10,
            GapWidth = 0.28,
            IsPanEnabled = false,
            IsZoomEnabled = false,
        };
        foreach (var label in labels)
            axis.Labels.Add(label);
        return axis;
    }

    private static CategoryAxis CategoryLeft(IList<string> labels)
    {
        var axis = new CategoryAxis
        {
            Position = AxisPosition.Left,
            TextColor = Text,
            TicklineColor = Axis,
            FontSize = 10,
            GapWidth = 0.35,
            IsPanEnabled = false,
            IsZoomEnabled = false,
        };
        foreach (var label in labels)
            axis.Labels.Add(label);
        return axis;
    }

    private static LinearAxis Linear(
        string title,
        AxisPosition position = AxisPosition.Left,
        bool minZero = false,
        bool zoom = true,
        double min = double.NaN,
        double max = double.NaN) => new()
    {
        Position = position,
        Title = title,
        TitleColor = Text,
        TextColor = Text,
        TicklineColor = Axis,
        MajorGridlineStyle = LineStyle.Dot,
        MajorGridlineColor = Grid,
        MinorGridlineStyle = LineStyle.None,
        MinimumPadding = 0.05,
        MaximumPadding = 0.08,
        Minimum = minZero ? 0 : min,
        Maximum = max,
        FontSize = 10,
        IsPanEnabled = zoom,
        IsZoomEnabled = zoom,
    };

    private static void AddVerticalGuide(PlotModel plot, double x, string text) =>
        plot.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            X = x,
            Color = Yield,
            StrokeThickness = 1.2,
            LineStyle = LineStyle.Dash,
            Text = text,
            TextColor = Text,
            FontSize = 10,
        });

    private static void AddLegend(PlotModel plot) =>
        plot.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.TopRight,
            LegendPlacement = LegendPlacement.Inside,
            LegendOrientation = LegendOrientation.Horizontal,
            LegendBackground = OxyColor.FromAColor(140, OxyColor.FromRgb(0x1A, 0x1A, 0x1A)),
            LegendBorder = OxyColors.Transparent,
            LegendTextColor = Text,
            LegendFontSize = 10,
            LegendPadding = 4,
        });

    private static OxyColor YieldColor(double percent) =>
        percent >= 99 ? Ok : percent >= 95 ? Yield : Fail;

    private static string AxisTitle(string unit) => unit.Trim() switch
    {
        "°" => "角度 °",
        "ms" => "耗时 ms",
        _ => string.IsNullOrWhiteSpace(unit) ? "值" : unit.Trim(),
    };

    private static string Fmt(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string ShortTrendLabel(string label)
    {
        if (label.Length >= 16 && label[10] == ' ')
            return label[11..];
        if (label.Length >= 10)
            return label[5..];
        return label;
    }

    private static IPlotController CreateInspect()
    {
        var c = new PlotController();
        c.UnbindAll();
        c.BindMouseEnter(PlotCommands.HoverSnapTrack);
        c.BindMouseDown(OxyMouseButton.Left, PlotCommands.SnapTrack);
        c.BindMouseDown(OxyMouseButton.Right, PlotCommands.ResetAt);
        c.BindTouchDown(PlotCommands.SnapTrackTouch);
        return c;
    }

    private static IPlotController CreateExplore()
    {
        var c = new PlotController();
        c.UnbindAll();
        c.BindMouseEnter(PlotCommands.HoverSnapTrack);
        c.BindMouseDown(OxyMouseButton.Left, PlotCommands.SnapTrack);
        c.BindMouseDown(OxyMouseButton.Right, PlotCommands.PanAt);
        c.BindMouseWheel(PlotCommands.ZoomWheelFine);
        c.BindKeyDown(OxyKey.R, PlotCommands.Reset);
        c.BindTouchDown(PlotCommands.PanZoomByTouch);
        return c;
    }
}
