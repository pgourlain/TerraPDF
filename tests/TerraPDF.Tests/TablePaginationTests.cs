using System.Globalization;
using System.Text.RegularExpressions;
using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Pagination tests for tables: which tables split between rows, and where the
/// splitter is allowed to cut.
/// </summary>
public class TablePaginationTests
{
    private const double Tolerance = 0.05;

    private static string Text(byte[] pdf) => PdfTestUtils.InflatedText(pdf);

    private static int PageCount(byte[] pdf) => Regex.Count(Text(pdf), @"/Type /Page[ /]");

    /// <summary>Heights of every filled rectangle painted in <paramref name="hexColor"/>.</summary>
    private static List<double> RectHeights(byte[] pdf, string hexColor)
    {
        var rgb = PdfColorOperator(hexColor);
        var heights = new List<double>();
        string? current = null;

        foreach (Match m in Regex.Matches(
            Text(pdf),
            @"(?m)^(?:([\d.]+ [\d.]+ [\d.]+) rg|(-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) re)$"))
        {
            if (m.Groups[1].Success) { current = m.Groups[1].Value; continue; }
            if (current == rgb)
                heights.Add(double.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture));
        }
        return heights;
    }

    /// <summary>The <c>r g b rg</c> operand string the writer emits for a hex colour.</summary>
    private static string PdfColorOperator(string hexColor)
    {
        int r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
        int g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
        int b = Convert.ToInt32(hexColor.Substring(5, 2), 16);
        return string.Create(CultureInfo.InvariantCulture,
            $"{r / 255.0:F4} {g / 255.0:F4} {b / 255.0:F4}");
    }

    private static void TwoColumns(TableDescriptor t) =>
        t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); });

    private static void DataRows(TableDescriptor t, int count, string bg)
    {
        for (int i = 0; i < count; i++)
            t.Row(r =>
            {
                r.Cell().Background(bg).Padding(6).Text("a");
                r.Cell().Background(bg).Padding(6).Text("b");
            });
    }

    // -- Tables that must split ------------------------------------

    [Fact]
    public void HeaderlessTableSplitsAcrossPages()
    {
        const string bg = "#EEEEEE";
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col => col.Item().Table(t =>
            {
                TwoColumns(t);
                DataRows(t, 80, bg);
            }));
        })).PublishPdf();

        Assert.True(PageCount(pdf) > 1, "a header-less table taller than the page should split");
        Assert.Equal(160, RectHeights(pdf, bg).Count);   // every cell still drawn exactly once
    }

    [Fact]
    public void TableDirectlyInTheContentSlotSplitsAcrossPages()
    {
        const string bg = "#EEEEEE";
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Table(t =>
            {
                TwoColumns(t);
                DataRows(t, 80, bg);
            });
        })).PublishPdf();

        Assert.True(PageCount(pdf) > 1, "a table placed straight in the content slot should still paginate");
        Assert.Equal(160, RectHeights(pdf, bg).Count);
    }

    [Fact]
    public void HeaderRowsRepeatOnEveryPageOfASplitTable()
    {
        const string headerBg = "#333333";
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col => col.Item().Table(t =>
            {
                TwoColumns(t);
                t.HeaderRow(r =>
                {
                    r.Cell().Background(headerBg).Padding(6).Text("H1");
                    r.Cell().Background(headerBg).Padding(6).Text("H2");
                });
                DataRows(t, 80, "#EEEEEE");
            }));
        })).PublishPdf();

        int pages = PageCount(pdf);
        Assert.True(pages > 1);
        Assert.Equal(pages * 2, RectHeights(pdf, headerBg).Count);   // two header cells per page
    }

    // -- Tables that must NOT be split -----------------------------

    [Fact]
    public void SmallTableInTheContentSlotStaysOnOnePage()
    {
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Table(t =>
            {
                TwoColumns(t);
                DataRows(t, 3, "#EEEEEE");
            });
        })).PublishPdf();

        Assert.Equal(1, PageCount(pdf));
        Assert.Equal(6, RectHeights(pdf, "#EEEEEE").Count);
    }

    [Fact]
    public void HeaderlessTableThatFitsKeepsItsSurroundingDecorators()
    {
        // A table that fits is placed as an ordinary item, so decorators wrapped
        // around it still paint.  The splitting path deliberately drops item-level
        // chrome, which is why short tables must not take it.
        const string frame = "#FF0000";
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col => col.Item().Background(frame).Padding(4).Table(t =>
            {
                TwoColumns(t);
                DataRows(t, 3, "#EEEEEE");
            }));
        })).PublishPdf();

        Assert.Equal(1, PageCount(pdf));
        Assert.NotEmpty(RectHeights(pdf, frame));
    }

    // -- Row spans must not straddle a break -----------------------

    [Theory]
    [InlineData(57)]
    [InlineData(65)]
    [InlineData(71)]
    [InlineData(80)]
    [InlineData(86)]
    [InlineData(94)]
    [InlineData(100)]
    public void RowSpanIsNeverCutAcrossAPageBreak(double topMargin)
    {
        const string spanBg = "#BBDEFB";

        // The top margin shifts where the break falls; at some of these values it
        // would previously land inside a spanned pair, leaving a half-height cell
        // with no continuation on the next page.
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(topMargin, 56.7, 56.7, 56.7);
            p.Content().Column(col => col.Item().Table(t =>
            {
                TwoColumns(t);
                t.HeaderRow(r =>
                {
                    r.Cell().Background("#333333").Padding(6).Text("H1");
                    r.Cell().Background("#333333").Padding(6).Text("H2");
                });
                for (int i = 0; i < 40; i++)
                {
                    int idx = i;
                    t.Row(r =>
                    {
                        if (idx % 2 == 0)
                            r.Cell(rowSpan: 2).Background(spanBg).Padding(6).Text($"pair{idx}");
                        r.Cell().Background("#FFF9C4").Padding(6).Text($"r{idx}");
                    });
                }
            }));
        })).PublishPdf();

        Assert.True(PageCount(pdf) > 1, "expected the table to split");

        var spanHeights = RectHeights(pdf, spanBg);
        Assert.Equal(20, spanHeights.Count);

        // Every spanned cell covers two rows, so all of them share the tallest height.
        double full = spanHeights.Max();
        Assert.All(spanHeights, h => Assert.True(Math.Abs(h - full) <= Tolerance,
            $"a spanned cell was cut at a page break: height {h:F2} instead of {full:F2}"));
    }

    [Fact]
    public void RowSpanTallerThanAPageIsStillEmittedSoLayoutProgresses()
    {
        // A group that cannot fit any page is forced out rather than looping forever.
        const string spanBg = "#BBDEFB";
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col => col.Item().Table(t =>
            {
                TwoColumns(t);
                t.HeaderRow(r =>
                {
                    r.Cell().Background("#333333").Padding(6).Text("H1");
                    r.Cell().Background("#333333").Padding(6).Text("H2");
                });
                t.Row(r =>
                {
                    r.Cell(rowSpan: 2).Background(spanBg).Padding(6)
                     .Text(string.Join(" ", Enumerable.Repeat("overflowing", 400)));
                    r.Cell().Padding(6).Text("top");
                });
                t.Row(r => r.Cell().Padding(6).Text("bottom"));
                DataRows(t, 5, "#EEEEEE");
            }));
        })).PublishPdf();

        Assert.NotEmpty(RectHeights(pdf, spanBg));
        Assert.Equal(10, RectHeights(pdf, "#EEEEEE").Count);   // the trailing rows still land
    }
}
