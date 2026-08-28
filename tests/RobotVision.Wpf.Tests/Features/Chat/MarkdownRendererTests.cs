using System.Windows.Documents;
using FluentAssertions;
using RobotVision.WpfHost.Features.Chat;
using Xunit;

namespace RobotVision.Wpf.Tests.Features.Chat;

public class MarkdownRendererTests
{
    [Fact]
    public void Heading_RendersBoldWithLevelSize()
    {
        var doc = MarkdownRenderer.Render("# 标题");
        var p = doc.Blocks.FirstBlock.Should().BeOfType<Paragraph>().Subject;
        p.FontSize.Should().Be(17);
        p.FontWeight.Should().Be(System.Windows.FontWeights.Bold);
    }

    [Fact]
    public void Emphasis_RendersStrongAndItalic()
    {
        var doc = MarkdownRenderer.Render("**粗** 与 *斜*");
        var p = (Paragraph)doc.Blocks.FirstBlock;
        var spans = p.Inlines.OfType<Span>().ToList();
        spans.Should().HaveCount(2);
        spans[0].FontWeight.Should().Be(System.Windows.FontWeights.Bold);
        spans[1].FontStyle.Should().Be(System.Windows.FontStyles.Italic);
    }

    [Fact]
    public void FencedCodeBlock_RendersMonoFont()
    {
        var doc = MarkdownRenderer.Render("```html\n<p>x</p>\n```");
        var p = doc.Blocks.FirstBlock.Should().BeOfType<Paragraph>().Subject;
        p.FontFamily.Should().Be(new System.Windows.Media.FontFamily("Consolas"));
        p.Inlines.OfType<Run>().Select(r => r.Text).Should().Contain("<p>x</p>");
    }

    [Fact]
    public void Table_RendersRowsAndColumns()
    {
        var doc = MarkdownRenderer.Render("|a|b|\n|-|-|\n|1|2|");
        var table = doc.Blocks.FirstBlock.Should().BeOfType<Table>().Subject;
        table.RowGroups[0].Rows.Should().HaveCount(2);
        table.RowGroups[0].Rows[0].Cells.Should().HaveCount(2);
    }

    [Fact]
    public void List_RendersListItems()
    {
        var doc = MarkdownRenderer.Render("- a\n- b");
        var list = doc.Blocks.FirstBlock.Should().BeOfType<List>().Subject;
        list.ListItems.Should().HaveCount(2);
    }

    [Fact]
    public void Link_RendersHyperlink()
    {
        var doc = MarkdownRenderer.Render("[文档](https://example.com)");
        var p = (Paragraph)doc.Blocks.FirstBlock;
        p.Inlines.OfType<Hyperlink>().Single().NavigateUri.Should().Be(new Uri("https://example.com"));
    }

    [Fact]
    public void ExtractHtml_CodeFenceWins()
    {
        ChatBubble.ExtractHtml("```html\n<p>x</p>\n``` 说明").Should().Be("<p>x</p>");
    }

    [Fact]
    public void ExtractHtml_FullDocument()
    {
        ChatBubble.ExtractHtml("<html><body>hi</body></html>").Should().Be("<html><body>hi</body></html>");
    }

    [Fact]
    public void ExtractHtml_NoneReturnsNull()
    {
        ChatBubble.ExtractHtml("普通文本").Should().BeNull();
    }
}
