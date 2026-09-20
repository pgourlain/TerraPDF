using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

public sealed class CanvasTextRotationTests
{
    private static readonly string LatoRegularPath =
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "Lato-Regular.ttf");

    private static byte[] Render(Action<VectorCanvas> draw) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(0);
            page.Content().Canvas(100, draw);
        })).PublishPdf();

    [Fact]
    public void NinetyDegreeTextEmitsClockwiseTextMatrix()
    {
        string content = PdfTestUtils.InflatedText(
            Render(canvas => canvas.Text("Turn", 20, 30, angle: 90)));

        Assert.Contains("0.00 -1.00 1.00 0.00 20.00", content);
        Assert.Contains(" Tm\n", content);
        Assert.Contains("(Turn) Tj\n", content);
    }

    [Fact]
    public void RotatedCustomFontUsesIdentityHEncoding()
    {
        string family = $"Rotated-{Guid.NewGuid():N}";
        FontFamily.Register(family, LatoRegularPath);

        string content = PdfTestUtils.InflatedText(
            Render(canvas => canvas.Text("Café", 10, 20, fontFamily: family, angle: 37)));

        Assert.Contains("/Encoding /Identity-H", content);
        Assert.Contains(" Tm\n", content);
        Assert.Matches(@"<[0-9A-F]+> Tj", content);
    }

    [Fact]
    public void RotatedTextCanShareOpacityGraphicsState()
    {
        string content = PdfTestUtils.InflatedText(
            Render(canvas => canvas.Text("Ghost", 10, 20, angle: -25, opacity: 0.4)));

        Assert.Contains("/Type /ExtGState /ca 0.4000 /CA 0.4000", content);
        Assert.Contains("Tm\n", content);
        Assert.Contains("Q\n", content);
    }
}
