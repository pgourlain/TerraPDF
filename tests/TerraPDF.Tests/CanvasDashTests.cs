using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for native dash patterns on canvas lines and stroked rectangles:
/// the emitted <c>d</c> operator, its graphics-state scoping, and argument validation.
/// </summary>
public sealed class CanvasDashTests
{
    private static string Render(Action<VectorCanvas> draw) =>
        PdfTestUtils.InflatedText(Document.Create(document => document.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(0);
            page.Content().Canvas(100, draw);
        })).PublishPdf());

    [Fact]
    public void DashedLineEmitsDashOperatorAndDoesNotLeakIntoLaterCommands()
    {
        string content = Render(canvas =>
        {
            canvas.Line(0, 10, 100, 10, dashPattern: [8, 4], dashPhase: 2);
            canvas.Line(0, 20, 100, 20);
        });

        Assert.Contains("[8.00 4.00] 2.00 d\n", content);
        // One dash scope only: the solid line that follows must not inherit it.
        Assert.Equal(1, content.Split("] 2.00 d\n").Length - 1);
        Assert.Contains("Q\n", content);
    }

    [Fact]
    public void DashedStrokeRectEmitsTheSameDashOperatorShapeAsALine()
    {
        string content = Render(canvas =>
            canvas.StrokeRect(5, 5, 50, 20, dashPattern: [3, 1.5], dashPhase: 0));

        Assert.Contains("[3.00 1.50] 0.00 d\n", content);
    }

    [Fact]
    public void SolidStrokesEmitNoDashOperator()
    {
        string content = Render(canvas =>
        {
            canvas.Line(0, 10, 100, 10);
            canvas.StrokeRect(5, 5, 50, 20);
        });

        Assert.DoesNotContain(" d\n", content);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NegativeOrNonFiniteDashPhaseIsRejected(double dashPhase)
    {
        var canvas = new VectorCanvas();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => canvas.Line(0, 0, 10, 10, dashPattern: [8, 4], dashPhase: dashPhase));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => canvas.StrokeRect(0, 0, 10, 10, dashPattern: [8, 4], dashPhase: dashPhase));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => canvas.DrawRect(0, 0, 10, 10, dashPattern: [8, 4], dashPhase: dashPhase));
    }

    [Fact]
    public void InvalidDashPatternIsRejected()
    {
        var canvas = new VectorCanvas();

        Assert.Throws<ArgumentException>(() => canvas.Line(0, 0, 10, 10, dashPattern: []));
        Assert.Throws<ArgumentException>(() => canvas.Line(0, 0, 10, 10, dashPattern: [0, 0]));
        Assert.Throws<ArgumentException>(() => canvas.Line(0, 0, 10, 10, dashPattern: [-1, 2]));
    }
}
