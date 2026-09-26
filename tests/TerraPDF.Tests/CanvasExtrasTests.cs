using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Canvas dash patterns on curves, gradient fills, link and bookmark commands, and QR codes.
/// </summary>
public sealed class CanvasExtrasTests
{
    private static string Render(Action<VectorCanvas> draw, int pages = 1) =>
        PdfTestUtils.InflatedText(Document.Create(document =>
        {
            for (var i = 0; i < pages; i++)
                document.Page(page =>
                {
                    page.Size(PageSize.A4);
                    page.Margin(0);
                    page.Content().Canvas(200, draw);
                });
        }).PublishPdf());

    // ── Dash ────────────────────────────────────────────────────────────────

    [Fact]
    public void DashedEllipseEmitsDashOperator()
    {
        string content = Render(c => c.StrokeEllipse(50, 50, 30, 20, dashPattern: [4, 2]));
        Assert.Contains("[4.00 2.00] 0.00 d\n", content);
    }

    [Fact]
    public void DashedFilledEllipseAndRoundedRectEmitDashOperator()
    {
        string content = Render(c =>
        {
            c.DrawEllipse(50, 50, 30, 20, dashPattern: [3, 3]);
            c.DrawRoundedRect(10, 10, 40, 30, 5, dashPattern: [1, 2]);
        });
        Assert.Contains("[3.00 3.00] 0.00 d\n", content);
        Assert.Contains("[1.00 2.00] 0.00 d\n", content);
    }

    [Fact]
    public void DashedPieAndPathEmitDashOperatorAndSolidOnesDoNot()
    {
        string dashed = Render(c =>
        {
            c.StrokePie(10, 10, 60, 60, 0, 90, dashPattern: [5, 1]);
            c.Path(p => p.Polygon((0, 0), (10, 0), (5, 9)).Stroke("#000000").Dash([2, 2], 1));
        });
        Assert.Contains("[5.00 1.00] 0.00 d\n", dashed);
        Assert.Contains("[2.00 2.00] 1.00 d\n", dashed);

        string solid = Render(c =>
        {
            c.StrokeEllipse(50, 50, 30, 20);
            c.StrokePie(10, 10, 60, 60, 0, 90);
        });
        Assert.DoesNotContain(" d\n", solid);
    }

    [Fact]
    public void InvalidDashPatternIsRejectedOnEllipse() =>
        Assert.Throws<ArgumentException>(() => new VectorCanvas().StrokeEllipse(1, 1, 1, 1, dashPattern: []));

    [Fact]
    public void PathDashRejectsInvalidPatternsAndPhases()
    {
        Assert.Throws<ArgumentException>(() => new PathDescriptor().Dash([0, 0]));
        Assert.Throws<ArgumentException>(() => new PathDescriptor().Dash([double.NaN]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PathDescriptor().Dash([2, 2], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PathDescriptor().Dash([2, 2], double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VectorCanvas().StrokePie(0, 0, 10, 10, 0, 90, dashPattern: [1], dashPhase: -1));
    }

    [Fact]
    public void RoundedRectPathHasFourCornersAndClampsTheRadius()
    {
        string content = Render(c => c.Path(p => p.RoundedRect(10, 10, 40, 20, 100).Fill("#FF0000")));
        Assert.Equal(4, content.Split(" c\n").Length - 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PathDescriptor().RoundedRect(0, 0, 10, 10, 0));
    }

    // ── Gradient ────────────────────────────────────────────────────────────

    [Fact]
    public void LinearGradientWritesAxialShadingClippedToThePath()
    {
        string content = Render(c => c.Path(p => p.Rect(10, 10, 100, 50).FillLinearGradient("#FF0000", "#0000FF", 0)));

        Assert.Contains("/Shading << /Sh1 << /ShadingType 2", content);
        Assert.Contains("/C0 [1.0000 0.0000 0.0000] /C1 [0.0000 0.0000 1.0000]", content);
        Assert.Contains("W n\n/Sh1 sh\n", content);
    }

    [Fact]
    public void RadialGradientWritesRadialShading()
    {
        string content = Render(c => c.Path(p => p.Circle(50, 50, 20).FillRadialGradient("#FFFFFF", "#000000")));
        Assert.Contains("/ShadingType 3", content);
        Assert.Contains("/Sh1 sh\n", content);
    }

    [Fact]
    public void GradientWithOutlineStrokesThePathAfterTheShading()
    {
        string content = Render(c => c.Path(p =>
            p.Rect(10, 10, 100, 50).FillLinearGradient("#FF0000", "#0000FF", 90).Stroke("#00FF00", 2)));
        int sh = content.IndexOf("/Sh1 sh", StringComparison.Ordinal);
        int stroke = content.IndexOf("0.0000 1.0000 0.0000 RG", StringComparison.Ordinal);
        Assert.True(sh >= 0 && stroke > sh);
    }

    [Fact]
    public void EachGradientGetsItsOwnShadingResource()
    {
        string content = Render(c =>
        {
            c.Path(p => p.Rect(0, 0, 10, 10).FillLinearGradient("#FF0000", "#0000FF"));
            c.Path(p => p.Rect(20, 0, 10, 10).FillLinearGradient("#00FF00", "#0000FF"));
        });
        Assert.Contains("/Sh1 sh", content);
        Assert.Contains("/Sh2 sh", content);
    }

    [Fact]
    public void GradientRejectsBadColors()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PathDescriptor().FillLinearGradient("", "#000000"));
        Assert.ThrowsAny<ArgumentException>(() => new PathDescriptor().FillRadialGradient("#FFFFFF", " "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PathDescriptor().FillLinearGradient("#FFFFFF", "#000000", double.NaN));
    }

    [Fact]
    public void FillAfterGradientReplacesTheGradient()
    {
        string content = Render(c => c.Path(p =>
            p.Rect(10, 10, 100, 50).FillLinearGradient("#FF0000", "#0000FF").Fill("#00FF00")));
        Assert.DoesNotContain("/Shading", content);
        Assert.DoesNotContain(" sh\n", content);
        Assert.Contains("0.0000 1.0000 0.0000 rg\n", content);
    }

    // ── Links and bookmarks ─────────────────────────────────────────────────

    [Fact]
    public void LinkWritesUriAnnotation()
    {
        string content = Render(c => c.Link(10, 10, 100, 20, "https://example.com"));
        Assert.Contains("/Subtype /Link", content);
        Assert.Contains("/URI (https://example.com)", content);
    }

    [Fact]
    public void InternalLinkWritesGoToDestinationAndFailsForAMissingPage()
    {
        string content = Render(c => c.InternalLink(10, 10, 100, 20, 2, top: 30), pages: 2);
        Assert.Contains("/Dest [", content);
        Assert.Contains("/XYZ 0", content);

        Assert.Throws<InvalidOperationException>(() => Render(c => c.InternalLink(10, 10, 100, 20, 5)));
    }

    [Fact]
    public void BookmarksAreNestedByParentTitleAndUsePhysicalPageNumbers()
    {
        string content = Render(c =>
        {
            c.Bookmark("Chapter", 10);
            c.Bookmark("Section", 60, "Chapter");
        }, pages: 2);

        Assert.Contains("/Type /Outlines", content);
        Assert.Contains("/Title (Chapter)", content);
        Assert.Contains("/Title (Section)", content);
        Assert.Contains("/Parent", content);
    }

    [Fact]
    public void LinkAndBookmarkValidateArguments()
    {
        var canvas = new VectorCanvas();
        Assert.Throws<ArgumentException>(() => canvas.Link(0, 0, 10, 10, " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.Link(0, 0, 0, 10, "https://a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.InternalLink(0, 0, 10, 10, 0));
        Assert.Throws<ArgumentException>(() => canvas.Bookmark(""));
        Assert.Throws<ArgumentException>(() => canvas.Bookmark("Child", 0, " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.InternalLink(0, 0, 10, 10, 1, top: -1));
    }

    // ── QR code ─────────────────────────────────────────────────────────────

    [Fact]
    public void QrCodeIsOneFilledPathOfRectangles()
    {
        string content = Render(c => c.QrCode("https://example.com", 10, 10, 100, hexColor: "#FF0000"));

        Assert.Contains("1.0000 0.0000 0.0000 rg\n", content);
        Assert.True(content.Split(" re\n").Length - 1 > 20, "modules are drawn as rectangles");
        // one fill for all modules (no background requested)
        Assert.Equal(1, content.Split("\nf\n").Length - 1);
    }

    [Fact]
    public void QrCodeBackgroundIsDrawnBehindTheModules()
    {
        string content = Render(c => c.QrCode("abc", 0, 0, 80, backgroundHex: "#00FF00"));
        int background = content.IndexOf("0.0000 1.0000 0.0000 rg", StringComparison.Ordinal);
        int modules = content.IndexOf("0.0000 0.0000 0.0000 rg", StringComparison.Ordinal);
        Assert.True(background >= 0 && modules > background);
    }

    [Fact]
    public void QrCodeValidatesArgumentsEagerly()
    {
        var canvas = new VectorCanvas();
        Assert.Throws<ArgumentException>(() => canvas.QrCode("", 0, 0, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.QrCode("a", 0, 0, 0));
        Assert.Throws<ArgumentException>(() => canvas.QrCode("a", 0, 0, 50, backgroundHex: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.QrCode("a", 0, 0, 50, quietZoneModules: -1));
        Assert.Throws<NotSupportedException>(() =>
            canvas.QrCode(new string('x', 8000), 0, 0, 50, QrErrorCorrectionLevel.H));
    }
}
