using System.Globalization;
using System.Text.RegularExpressions;
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

        Assert.Contains("0.000000 -1.000000 1.000000 0.000000 20.00", content);
        Assert.Contains(" Tm\n", content);
        Assert.Contains("(Turn) Tj\n", content);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(5)]
    [InlineData(37)]
    [InlineData(-25)]
    public void RotationAngleSurvivesContentStreamRounding(double angle)
    {
        string content = PdfTestUtils.InflatedText(
            Render(canvas => canvas.Text("Tilt", 20, 30, angle: angle)));

        var m = Regex.Match(content,
            @"(?m)^(-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) [\d.]+ [\d.]+ Tm$");
        Assert.True(m.Success, "Text matrix not found in content stream.");

        double Parse(int group) =>
            double.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);
        double cos = Parse(1), sin = Parse(3);

        // The emitted pair must still describe the angle that was asked for — at two
        // decimals a 0.3° rotation came out as 0.57° — and must stay a unit vector,
        // since a rounded (cos, sin) also rescales every glyph it sets.
        Assert.Equal(angle, Math.Atan2(sin, cos) * 180.0 / Math.PI, 3);
        Assert.Equal(1.0, Math.Sqrt(cos * cos + sin * sin), 5);
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
