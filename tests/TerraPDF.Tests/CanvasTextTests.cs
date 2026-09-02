using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for <see cref="VectorCanvas.Text"/> and <see cref="VectorCanvas.MeasureTextWidth"/>:
/// placing a text label on the canvas through both the standard-14 and custom
/// (embedded TrueType) font paths, and the opacity/graphics-state wiring shared
/// with the other canvas primitives. Per this project's established bar, these
/// assert on the actual emitted content stream (decompressed via
/// <see cref="PdfTestUtils.InflatedText"/>), not just "a PDF came out".
/// </summary>
public sealed class CanvasTextTests
{
    private static readonly string LatoRegularPath =
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "Lato-Regular.ttf");

    private static byte[] CanvasPage(double height, Action<VectorCanvas> draw) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(1, Unit.Centimetre);
            page.Content().Canvas(height, draw);
        })).PublishPdf();

    private static string UniqueFamily([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"{caller}-{Guid.NewGuid():N}";

    // ---------------------------------------------------------------
    //  Standard-font path
    // ---------------------------------------------------------------

    [Fact]
    public void TextEmitsBtTfTjEt()
    {
        byte[] pdf = CanvasPage(50, c => c.Text("Hello", 10, 30));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Matches(@"BT\n[\s\S]*?\(Hello\) Tj\n[\s\S]*?ET\n", text);
    }

    [Fact]
    public void DifferentYPlacesTextAtADifferentBaseline()
    {
        // y is the baseline directly (matching PdfPage.ShowTextAt's own contract) —
        // moving y must move the emitted Td, proving no hidden font-size offset
        // is silently applied on top of what the caller asked for.
        static string TdLine(byte[] pdf) =>
            System.Text.RegularExpressions.Regex.Match(
                PdfTestUtils.InflatedText(pdf), @"[\d.]+ [\d.]+ Td").Value;

        string tdAt20 = TdLine(CanvasPage(60, c => c.Text("Hi", 10, 20)));
        string tdAt40 = TdLine(CanvasPage(60, c => c.Text("Hi", 10, 40)));

        Assert.NotEqual(tdAt20, tdAt40);
    }

    [Fact]
    public void TextUsesRequestedFontSizeAndColor()
    {
        byte[] pdf = CanvasPage(50, c => c.Text("Sized", 5, 20, "#FF0000", fontSize: 24));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Contains("1.0000 0.0000 0.0000 rg\n", text); // red
        Assert.Contains("/F1 24.00 Tf\n", text); // Helvetica alias, size 24
    }

    [Fact]
    public void BoldItalicSelectsTheCorrectStandardFontAlias()
    {
        byte[] pdf = CanvasPage(50, c => c.Text("BI", 5, 20, bold: true, italic: true));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Contains("/F4 ", text); // Helvetica-BoldOblique alias per PdfFonts.All
    }

    // ---------------------------------------------------------------
    //  Custom (embedded) font path
    // ---------------------------------------------------------------

    [Fact]
    public void TextThroughRegisteredCustomFontUsesIdentityHEncoding()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularPath);

        byte[] pdf = CanvasPage(50, c => c.Text("Café", 5, 30, fontFamily: family));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Contains("/Subtype /Type0", text);
        Assert.Contains("/Encoding /Identity-H", text);
        Assert.Matches(@"<[0-9A-F]+> Tj", text); // hex glyph-ID string, not a literal ( ) string
    }

    // ---------------------------------------------------------------
    //  Opacity / graphics-state wiring (shared with other primitives)
    // ---------------------------------------------------------------

    [Fact]
    public void DefaultOpacityEmitsNoExtGState()
    {
        byte[] pdf = CanvasPage(50, c => c.Text("Plain", 5, 20));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.DoesNotContain("/ExtGState", text);
        Assert.DoesNotContain(" gs\n", text);
    }

    [Fact]
    public void TranslucentTextIsWrappedInQGsQAroundBtEt()
    {
        byte[] pdf = CanvasPage(50, c => c.Text("Ghost", 5, 20, opacity: 0.3));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Matches(@"<< /Type /ExtGState /ca 0\.3000 /CA 0\.3000 >>", text);
        Assert.Matches(@"q\n/GS1 gs\nBT\n[\s\S]*?ET\nQ\n", text);
    }

    [Fact]
    public void TextSharesExtGStateWithOtherPrimitivesAtTheSameOpacity()
    {
        byte[] pdf = CanvasPage(80, c =>
        {
            c.FillRect(0, 0, 20, 20, Color.Blue.Medium, opacity: 0.5);
            c.Text("Label", 0, 40, opacity: 0.5);
        });

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(text, @"/Type /ExtGState"));
    }

    // ---------------------------------------------------------------
    //  Validation
    // ---------------------------------------------------------------

    [Fact]
    public void EmptyTextThrows()
    {
        var canvas = new VectorCanvas();
        Assert.Throws<ArgumentException>(() => canvas.Text("", 0, 0));
    }

    [Fact]
    public void NonPositiveFontSizeThrows()
    {
        var canvas = new VectorCanvas();
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.Text("x", 0, 0, fontSize: 0));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void OutOfRangeOpacityThrows(double badOpacity)
    {
        var canvas = new VectorCanvas();
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.Text("x", 0, 0, opacity: badOpacity));
    }

    // ---------------------------------------------------------------
    //  MeasureTextWidth
    // ---------------------------------------------------------------

    [Fact]
    public void MeasureTextWidthScalesLinearlyWithFontSize()
    {
        double at10 = VectorCanvas.MeasureTextWidth("Hello", 10);
        double at20 = VectorCanvas.MeasureTextWidth("Hello", 20);

        Assert.True(at10 > 0);
        Assert.Equal(at20, at10 * 2, precision: 6);
    }

    [Fact]
    public void MeasureTextWidthMatchesCustomFontVariantMeasureWidth()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularPath);

        double canvasMeasured = VectorCanvas.MeasureTextWidth("Measure", 14, family);

        // Cross-check against the exact same underlying measurement TextBlock uses.
        var resolved = TerraPDF.Drawing.PdfFonts.ResolveFont(family, false, false);
        double directMeasured = resolved.Custom!.MeasureWidth("Measure", 14);

        Assert.Equal(directMeasured, canvasMeasured, precision: 6);
    }

    [Fact]
    public void MeasureTextWidthCanCentreALabel()
    {
        // Practical use case this exists for: right-align/centre text before placing it.
        double canvasWidth = 200;
        double textWidth = VectorCanvas.MeasureTextWidth("Centered", 16);
        double centeredX = (canvasWidth - textWidth) / 2;

        Assert.InRange(centeredX, 0, canvasWidth);

        byte[] pdf = CanvasPage(50, c => c.Text("Centered", centeredX, 30, fontSize: 16));
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }
}
