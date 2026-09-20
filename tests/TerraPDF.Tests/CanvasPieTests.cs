using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

public sealed class CanvasPieTests
{
    private static byte[] Render(Action<VectorCanvas> draw) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(0);
            page.Content().Canvas(120, draw);
        })).PublishPdf();

    [Fact]
    public void FilledQuarterPieEmitsBezierPathAndFillOperator()
    {
        string content = PdfTestUtils.InflatedText(
            Render(canvas => canvas.FillPie(10, 20, 40, 30, 0, 90)));

        Assert.Contains(" c\n", content);
        Assert.Contains("h\n", content);
        Assert.Contains("f\n", content);
    }

    [Fact]
    public void DrawPieEmitsStrokeAndFillForNegativeSweep()
    {
        string content = PdfTestUtils.InflatedText(
            Render(canvas => canvas.DrawPie(10, 20, 40, 30, 180, -270)));

        Assert.Contains(" c\n", content);
        Assert.Contains("B\n", content);
    }

    [Fact]
    public void PieRejectsNonPositiveDimensions()
    {
        var canvas = new VectorCanvas();

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.FillPie(0, 0, 0, 10, 0, 90));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.StrokePie(0, 0, 10, -1, 0, 90));
    }

    [Theory]
    [InlineData(15, 120)]
    [InlineData(210, 120)]
    [InlineData(180, -270)]
    public void SectorIsOneSubpathClosedThroughItsCenter(double startAngle, double sweepAngle)
    {
        string content = PdfTestUtils.InflatedText(
            Render(canvas => canvas.FillPie(10, 20, 40, 30, startAngle, sweepAngle)));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(content, @"(?m)^[-\d.]+ [-\d.]+ m$").Cast<System.Text.RegularExpressions.Match>());
        Assert.Matches(@" c\n30\.00 [\d.]+ l\nh\nf\n", content);
    }
}
