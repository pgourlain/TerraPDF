using System.Globalization;
using System.Text.RegularExpressions;
using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Geometry tests for <c>Cell(columnSpan:, rowSpan:)</c>.
///
/// These assert on the rectangles actually emitted into the content stream rather than
/// on "a PDF came out", because the failure mode being guarded against is silent: a
/// mis-placed span produces a perfectly valid PDF that simply draws cells on top of
/// each other.
/// </summary>
public class TableSpanTests
{
    /// <summary>
    /// Coordinates are written to the content stream rounded to two decimals, so a whole
    /// and the sum of its parts can legitimately differ in the last printed digit.
    /// </summary>
    private const double Tolerance = 0.05;

    /// <summary>A filled rectangle from the content stream, in PDF (bottom-left origin) points.</summary>
    private readonly record struct Rect(double X, double Y, double W, double H);

    private static void AssertClose(double expected, double actual, string because) =>
        Assert.True(Math.Abs(expected - actual) <= Tolerance,
            $"{because}: expected {expected:F2}, got {actual:F2}");

    private static double Num(Group g) => double.Parse(g.Value, CultureInfo.InvariantCulture);

    /// <summary>
    /// Extracts every <c>x y w h re</c> rectangle from the (inflated) content stream,
    /// in emission order.
    /// </summary>
    private static List<Rect> Rects(byte[] pdf)
    {
        string text = PdfTestUtils.InflatedText(pdf);
        return Regex.Matches(text, @"^(-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) re$", RegexOptions.Multiline)
            .Select(m => new Rect(Num(m.Groups[1]), Num(m.Groups[2]), Num(m.Groups[3]), Num(m.Groups[4])))
            .ToList();
    }

    /// <summary>Builds a page whose sole content is the described table.</summary>
    private static byte[] Build(Action<TableDescriptor> table) =>
        Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Table(table);
        })).PublishPdf();

    /// <summary>Three equal relative columns.</summary>
    private static void ThreeColumns(TableDescriptor t) =>
        t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn(); });

    // -- Column spans ----------------------------------------------

    [Fact]
    public void ColumnSpanPlacesTheFollowingCellAfterTheSpannedColumns()
    {
        var pdf = Build(t =>
        {
            ThreeColumns(t);
            t.Row(r =>
            {
                r.Cell(columnSpan: 2).Background("#FF0000").Padding(6).Text("span");
                r.Cell().Background("#00FF00").Padding(6).Text("after");
            });
            t.Row(r =>
            {
                r.Cell().Background("#EEEEEE").Padding(6).Text("a");
                r.Cell().Background("#EEEEEE").Padding(6).Text("b");
                r.Cell().Background("#EEEEEE").Padding(6).Text("c");
            });
        });

        var rects = Rects(pdf);
        Assert.Equal(5, rects.Count);

        // Row 2 establishes where the three column origins actually are.
        double col1 = rects[2].X, col2 = rects[3].X, col3 = rects[4].X;
        Assert.True(col1 < col2 && col2 < col3);

        // The span starts at column 1 and is two columns wide.
        AssertClose(col1, rects[0].X, "span should start at column 1");
        AssertClose(rects[2].W + rects[3].W, rects[0].W, "span should be two columns wide");

        // The cell after the span belongs in column 3, not column 2.
        AssertClose(col3, rects[1].X, "cell after a 2-column span should sit in column 3");
    }

    [Fact]
    public void ColumnSpanDoesNotOverlapTheFollowingCell()
    {
        var pdf = Build(t =>
        {
            ThreeColumns(t);
            t.Row(r =>
            {
                r.Cell(columnSpan: 2).Background("#FF0000").Padding(6).Text("span");
                r.Cell().Background("#00FF00").Padding(6).Text("after");
            });
        });

        var rects = Rects(pdf);
        Assert.Equal(2, rects.Count);

        Assert.True(rects[1].X >= rects[0].X + rects[0].W - Tolerance,
            $"cell at x={rects[1].X:F2} overlaps span ending at x={rects[0].X + rects[0].W:F2}");
    }

    [Fact]
    public void ColumnSpanCoveringEveryColumnLeavesTheRowFull()
    {
        var pdf = Build(t =>
        {
            ThreeColumns(t);
            t.Row(r => r.Cell(columnSpan: 3).Background("#FF0000").Padding(6).Text("full width"));
            t.Row(r =>
            {
                r.Cell().Background("#EEEEEE").Padding(6).Text("a");
                r.Cell().Background("#EEEEEE").Padding(6).Text("b");
                r.Cell().Background("#EEEEEE").Padding(6).Text("c");
            });
        });

        var rects = Rects(pdf);
        Assert.Equal(4, rects.Count);
        AssertClose(rects[1].W + rects[2].W + rects[3].W, rects[0].W,
            "a 3-column span should be as wide as the three columns together");
    }

    // -- Row spans -------------------------------------------------

    [Fact]
    public void RowSpanPushesTheNextRowsCellIntoTheFollowingColumn()
    {
        var pdf = Build(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); });
            t.Row(r =>
            {
                r.Cell(rowSpan: 2).Background("#0000FF").Padding(6).Text("tall");
                r.Cell().Background("#FFFF00").Padding(6).Text("top right");
            });
            t.Row(r => r.Cell().Background("#FFFF00").Padding(6).Text("bottom right"));
        });

        var rects = Rects(pdf);
        Assert.Equal(3, rects.Count);

        var span     = rects[0];
        var topRight = rects[1];
        var nextRow  = rects[2];

        // The second row's only cell belongs in column 2 — the span still owns column 1.
        AssertClose(topRight.X, nextRow.X, "row 2's cell should sit in column 2");
        Assert.True(nextRow.X >= span.X + span.W - Tolerance,
            $"row 2 cell at x={nextRow.X:F2} overlaps the span ending at x={span.X + span.W:F2}");
    }

    [Fact]
    public void RowSpanCellIsAsTallAsTheRowsItCovers()
    {
        var pdf = Build(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); });
            t.Row(r =>
            {
                r.Cell(rowSpan: 2).Background("#0000FF").Padding(6).Text("tall");
                r.Cell().Background("#FFFF00").Padding(6).Text("top");
            });
            t.Row(r => r.Cell().Background("#FFFF00").Padding(6).Text("bottom"));
        });

        var rects = Rects(pdf);
        AssertClose(rects[1].H + rects[2].H, rects[0].H, "a rowSpan:2 cell should cover both rows");
    }

    [Fact]
    public void RowSpanTallContentGrowsTheRowsItCoversInsteadOfOverflowing()
    {
        const string longText =
            "This spanned cell carries considerably more text than the short cells beside " +
            "it, so it needs several wrapped lines and the rows it covers have to grow to " +
            "hold it rather than letting it spill past the bottom of the table.";

        var pdf = Build(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); });
            t.Row(r =>
            {
                r.Cell(rowSpan: 2).Background("#0000FF").Padding(6).Text(longText);
                r.Cell().Background("#FFFF00").Padding(6).Text("short");
            });
            t.Row(r => r.Cell().Background("#FFFF00").Padding(6).Text("short"));
        });

        var rects = Rects(pdf);
        var span  = rects[0];

        AssertClose(rects[1].H + rects[2].H, span.H, "the covered rows should together match the span");

        // Every text baseline drawn for the spanned cell sits inside its box.
        // (PDF Y grows upward, so "inside" means at or above the box's bottom edge.)
        string text = PdfTestUtils.InflatedText(pdf);
        var baselines = Regex.Matches(text, @"^(-?[\d.]+) (-?[\d.]+) Td$", RegexOptions.Multiline)
            .Select(m => (X: Num(m.Groups[1]), Y: Num(m.Groups[2])))
            .Where(p => p.X > 0 && p.Y > 0)            // absolute Td moves, not intra-line advances
            .Where(p => p.X < span.X + span.W)         // baselines belonging to the spanned cell
            .ToList();

        Assert.NotEmpty(baselines);
        foreach (var b in baselines)
            Assert.True(b.Y >= span.Y - Tolerance,
                $"baseline y={b.Y:F2} falls below the spanned cell's bottom edge y={span.Y:F2}");
    }

    [Fact]
    public void RowOfOnlySpannedCellsStillHasHeight()
    {
        var pdf = Build(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); });
            t.Row(r =>
            {
                r.Cell(rowSpan: 2).Background("#0000FF").Padding(6).Text("left tall");
                r.Cell(rowSpan: 2).Background("#00FFFF").Padding(6).Text("right tall");
            });
        });

        var rects = Rects(pdf);
        Assert.Equal(2, rects.Count);
        Assert.All(rects, r => Assert.True(r.H > 0, "a row of only spanned cells collapsed to zero height"));
    }

    // -- Combined and defensive ------------------------------------

    [Fact]
    public void ColumnAndRowSpanTogetherTileWithoutOverlapping()
    {
        var pdf = Build(t =>
        {
            ThreeColumns(t);
            t.Row(r =>
            {
                r.Cell(columnSpan: 2, rowSpan: 2).Background("#FF0000").Padding(6).Text("big");
                r.Cell().Background("#00FF00").Padding(6).Text("r1c3");
            });
            t.Row(r => r.Cell().Background("#00FF00").Padding(6).Text("r2c3"));
        });

        var rects = Rects(pdf);
        Assert.Equal(3, rects.Count);

        var big = rects[0];

        // Both single cells sit in column 3, to the right of the 2x2 block.
        AssertClose(rects[1].X, rects[2].X, "both single cells belong in the same column");
        Assert.True(rects[1].X >= big.X + big.W - Tolerance);

        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
                Assert.False(Overlaps(rects[i], rects[j]), $"rect {i} overlaps rect {j}");
    }

    [Fact]
    public void SpansBelowOneAreTreatedAsOne()
    {
        var pdf = Build(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); });
            t.Row(r =>
            {
                r.Cell(columnSpan: 0, rowSpan: -1).Background("#FF0000").Padding(6).Text("a");
                r.Cell().Background("#00FF00").Padding(6).Text("b");
            });
        });

        var rects = Rects(pdf);
        Assert.Equal(2, rects.Count);
        AssertClose(rects[0].W, rects[1].W, "both cells should be one column wide");
        Assert.True(rects[1].X >= rects[0].X + rects[0].W - Tolerance, "cells should sit side by side");
    }

    [Fact]
    public void GroupedHeaderKeepsItsSpanOnEveryContinuationPage()
    {
        // The table is wrapped in a Column because that is what the pagination pass
        // splits on; a Table placed directly in the content slot is laid out as a
        // single page.
        var pdf = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col => col.Item().Table(t =>
            {
                ThreeColumns(t);
                t.HeaderRow(r =>
                {
                    r.Cell(columnSpan: 2).Background("#333333").Padding(6).Text("grouped header");
                    r.Cell().Background("#333333").Padding(6).Text("third");
                });
                for (int i = 0; i < 80; i++)
                    t.Row(r =>
                    {
                        r.Cell().Background("#EEEEEE").Padding(6).Text("a");
                        r.Cell().Background("#EEEEEE").Padding(6).Text("b");
                        r.Cell().Background("#EEEEEE").Padding(6).Text("c");
                    });
            }));
        })).PublishPdf();

        int pageCount = Regex.Count(PdfTestUtils.InflatedText(pdf), @"/Type /Page[ /]");
        Assert.True(pageCount > 1, $"expected a multi-page table, got {pageCount} page(s)");

        var rects = Rects(pdf);

        // A single data cell establishes one column's width; the grouped header is two of them.
        double oneColumn = rects.Min(r => r.W);
        var grouped = rects.Where(r => Math.Abs(r.W - oneColumn * 2) <= Tolerance).ToList();

        Assert.Equal(pageCount, grouped.Count);

        // On every page the third-column header still clears the grouped cell.
        foreach (var g in grouped)
        {
            var thirdCell = rects.FirstOrDefault(r =>
                Math.Abs(r.Y - g.Y) <= Tolerance && r.X > g.X);
            Assert.True(thirdCell.W > 0, "the third header cell is missing on a page");
            Assert.True(thirdCell.X >= g.X + g.W - Tolerance,
                $"third header cell at x={thirdCell.X:F2} overlaps the grouped header ending at {g.X + g.W:F2}");
        }
    }

    private static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.W - Tolerance && b.X < a.X + a.W - Tolerance &&
        a.Y < b.Y + b.H - Tolerance && b.Y < a.Y + a.H - Tolerance;
}
