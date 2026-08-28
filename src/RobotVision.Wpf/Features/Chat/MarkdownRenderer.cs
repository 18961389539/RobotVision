using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace RobotVision.WpfHost.Features.Chat;

// Markdig 与 WPF 都有 Inline，别名区分：MdInline = Markdig 行内节点
using MdInline = Markdig.Syntax.Inlines.Inline;

/// <summary>气泡文本 → FlowDocument 转换器（绑定 Text 用）。流式更新时整体重建，短文本开销可忽略。</summary>
public sealed class MarkdownToFlowDocumentConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        MarkdownRenderer.Render(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>FlowDocumentScrollViewer.Document 非依赖属性，用附加属性桥接绑定。
/// 用法: chat:DocumentBinder.Document="{Binding Text}"</summary>
public static class DocumentBinder
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.RegisterAttached(
            "Document", typeof(string), typeof(DocumentBinder),
            new PropertyMetadata(null, OnDocumentChanged));

    public static void SetDocument(DependencyObject element, string? value) =>
        element.SetValue(DocumentProperty, value);

    public static string? GetDocument(DependencyObject element) =>
        (string?)element.GetValue(DocumentProperty);

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlowDocumentScrollViewer viewer)
            viewer.Document = MarkdownRenderer.Render(e.NewValue as string);
    }
}

/// <summary>Markdig(MIT) 解析 markdown → WPF FlowDocument，暗色工业主题。
/// 支持：段落/标题/列表/代码块/引用/表格/分隔线 + 行内粗斜体/行内代码/链接/换行。</summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private static readonly Brush Text = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly Brush CodeBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly Brush CodeFg = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE));
    private static readonly Brush Link = new SolidColorBrush(Color.FromRgb(0x5B, 0x9B, 0xF5));
    private static readonly Brush Border = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

    public static FlowDocument Render(string? markdown)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 13,
            Foreground = Text,
            PagePadding = new Thickness(0),
            LineHeight = 1.15,
        };
        if (string.IsNullOrWhiteSpace(markdown))
            return doc;

        foreach (var block in Markdown.Parse(markdown, Pipeline))
        {
            if (RenderBlock(block) is { } b)
                doc.Blocks.Add(b);
        }

        return doc;
    }

    private static System.Windows.Documents.Block? RenderBlock(Markdig.Syntax.Block block) => block switch
    {
        ParagraphBlock p => Paragraph(p.Inline),
        HeadingBlock h => Heading(h),
        FencedCodeBlock code => CodeBlock(code),
        ListBlock list => ListBlock(list),
        QuoteBlock quote => Quote(quote),
        Markdig.Extensions.Tables.Table table => Table(table),
        ThematicBreakBlock => new Paragraph
        {
            BorderBrush = Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 6, 0, 6),
        },
        _ => null,
    };

    private static Paragraph Paragraph(ContainerInline? inline)
    {
        var p = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
        RenderInlineContainer(inline, p.Inlines);
        return p;
    }

    private static Paragraph Heading(HeadingBlock h)
    {
        var size = h.Level switch
        {
            1 => 17.0,
            2 => 15.0,
            3 => 14.0,
            _ => 13.0,
        };
        var p = new Paragraph { FontSize = size, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 6) };
        RenderInlineContainer(h.Inline, p.Inlines);
        return p;
    }

    private static Paragraph CodeBlock(FencedCodeBlock code)
    {
        var p = new Paragraph
        {
            Background = CodeBg,
            FontFamily = new FontFamily("Consolas"),
            Foreground = CodeFg,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 2, 0, 8),
        };
        var parts = code.Lines.ToString().Split('\n');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(parts[i].TrimEnd('\r')));
        }
        return p;
    }

    private static System.Windows.Documents.List ListBlock(ListBlock list)
    {
        var flow = new System.Windows.Documents.List
        {
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            MarkerOffset = 14,
            Margin = new Thickness(12, 0, 0, 6),
        };
        foreach (var rawItem in list)
        {
            var li = new ListItem { Margin = new Thickness(0, 0, 0, 2) };
            foreach (var child in (ContainerBlock)rawItem)
            {
                if (RenderBlock(child) is { } b)
                    li.Blocks.Add(b);
            }
            flow.ListItems.Add(li);
        }
        return flow;
    }

    private static Paragraph Quote(QuoteBlock quote)
    {
        var p = new Paragraph
        {
            Foreground = Muted,
            FontStyle = FontStyles.Italic,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(4, 2, 0, 6),
            BorderBrush = Border,
            BorderThickness = new Thickness(3, 0, 0, 0),
        };
        foreach (var child in (ContainerBlock)quote)
        {
            if (child is ParagraphBlock pb)
                RenderInlineContainer(pb.Inline, p.Inlines);
        }
        return p;
    }

    private static System.Windows.Documents.Table Table(Markdig.Extensions.Tables.Table table)
    {
        var flow = new System.Windows.Documents.Table
        {
            CellSpacing = 0,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 0, 8),
        };
        var rowGroups = new TableRowGroup();
        flow.RowGroups.Add(rowGroups);

        var maxCols = 0;
        foreach (var rawRow in (ContainerBlock)table)
        {
            var row = (Markdig.Extensions.Tables.TableRow)rawRow;
            var tr = new TableRow();
            foreach (var rawCell in (ContainerBlock)row)
            {
                var mdCell = (Markdig.Extensions.Tables.TableCell)rawCell;
                var tc = new TableCell
                {
                    BorderBrush = Border,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 3, 6, 3),
                };
                if (mdCell.Count > 0 && RenderBlock(mdCell[0]) is { } b)
                    tc.Blocks.Add(b);
                tr.Cells.Add(tc);
            }
            maxCols = Math.Max(maxCols, tr.Cells.Count);
            rowGroups.Rows.Add(tr);
        }

        for (var i = 0; i < maxCols; i++)
            flow.Columns.Add(new TableColumn());
        return flow;
    }

    private static void RenderInlineContainer(ContainerInline? container, InlineCollection target)
    {
        if (container is null)
            return;
        foreach (var inline in container)
            RenderInline(inline, target);
    }

    private static void RenderInline(MdInline inline, InlineCollection target)
    {
        switch (inline)
        {
            case LiteralInline lit:
                target.Add(new Run(lit.Content.ToString()));
                break;
            case CodeInline code:
                target.Add(new Run(code.Content)
                {
                    Background = CodeBg,
                    Foreground = CodeFg,
                    FontFamily = new FontFamily("Consolas"),
                });
                break;
            case LineBreakInline:
                target.Add(new LineBreak());
                break;
            case LinkInline link when link.Url is not null && TryUri(link.Url) is { } uri:
                var hyper = new Hyperlink { NavigateUri = uri, Foreground = Link };
                RenderInlineContainer(link, hyper.Inlines);
                target.Add(hyper);
                break;
            case EmphasisInline em:
                var span = new Span();
                // Markdig 0.38: 粗体用 DelimiterCount>=2 判定（** 或 __），单个 * / _ 为斜体
                var strong = em.DelimiterCount >= 2;
                if (strong)
                    span.FontWeight = FontWeights.Bold;
                else
                    span.FontStyle = FontStyles.Italic;
                RenderInlineContainer(em, span.Inlines);
                target.Add(span);
                break;
            case HtmlInline:
                break; // 行内 HTML 不渲染（对话内 HTML 走「预览 HTML」按钮）
            default:
                break;
        }
    }

    private static Uri? TryUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}
