using System.Text.RegularExpressions;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for constant-alpha graphics state (<c>/ExtGState</c>, the <c>gs</c>
/// operator) on <see cref="VectorCanvas"/> shape primitives. Per this
/// project's established bar, these assert on the actual emitted content
/// stream and object graph (decompressed via <see cref="PdfTestUtils.InflatedText"/>),
/// not just "a PDF came out".
/// </summary>
public sealed class OpacityTests
{
    private static byte[] CanvasPage(double height, Action<VectorCanvas> draw) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(1, Unit.Centimetre);
            page.Content().Canvas(height, draw);
        })).PublishPdf();

    // ---------------------------------------------------------------
    //  Default opacity: zero footprint (regression guard)
    // ---------------------------------------------------------------

    [Fact]
    public void DefaultOpacityEmitsNoExtGStateAtAll()
    {
        byte[] pdf = CanvasPage(80, c =>
        {
            c.FillRect(0, 0, 40, 40, Color.Blue.Medium);
            c.Line(0, 0, 40, 40, Color.Black, 1);
            c.FillEllipse(20, 20, 10, 10, Color.Red.Medium);
            c.Path(p => p.Rect(0, 0, 10, 10).Fill(Color.Green.Medium));
        });

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.DoesNotContain("/ExtGState", text);
        Assert.DoesNotContain(" gs\n", text);
    }

    // ---------------------------------------------------------------
    //  opacity < 1: real q / gs / Q wrapping and a correctly-valued object
    // ---------------------------------------------------------------

    [Fact]
    public void TranslucentFillRectEmitsExtGStateAndGsOperator()
    {
        byte[] pdf = CanvasPage(80, c => c.FillRect(0, 0, 40, 40, Color.Blue.Medium, opacity: 0.5));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Matches(@"<< /Type /ExtGState /ca 0\.5000 /CA 0\.5000 >>", text);
        Assert.Matches(@"q\n/GS1 gs\n[^Q]*?re\nf\nQ\n", text);
    }

    [Fact]
    public void TranslucentLineIsWrappedInQGsQ()
    {
        byte[] pdf = CanvasPage(50, c => c.Line(0, 0, 40, 40, Color.Black, 2, opacity: 0.25));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Matches(@"<< /Type /ExtGState /ca 0\.2500 /CA 0\.2500 >>", text);
        Assert.Matches(@"q\n/GS1 gs\n[^Q]*?S\nQ\n", text);
    }

    [Fact]
    public void TranslucentPathIsWrappedInQGsQ()
    {
        byte[] pdf = CanvasPage(50, c =>
            c.Path(p => p.Rect(0, 0, 20, 20).Fill(Color.Green.Medium).Opacity(0.4)));

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Matches(@"<< /Type /ExtGState /ca 0\.4000 /CA 0\.4000 >>", text);
        Assert.Matches(@"q\n/GS1 gs\n[^Q]*?f\nQ\n", text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TranslucentRoundedRectAndEllipseAreWrapped(bool roundedRect)
    {
        byte[] pdf = CanvasPage(80, c =>
        {
            if (roundedRect) c.FillRoundedRect(0, 0, 40, 30, 6, Color.Blue.Medium, opacity: 0.6);
            else c.FillEllipse(20, 20, 15, 10, Color.Red.Medium, opacity: 0.6);
        });

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Matches(@"<< /Type /ExtGState /ca 0\.6000 /CA 0\.6000 >>", text);
        Assert.Contains("/GS1 gs\n", text);
    }

    // ---------------------------------------------------------------
    //  Deduplication: same opacity shares one object, different values don't
    // ---------------------------------------------------------------

    [Fact]
    public void SameOpacityAcrossShapesSharesOneExtGStateObject()
    {
        byte[] pdf = CanvasPage(80, c =>
        {
            c.FillRect(0, 0, 20, 20, Color.Blue.Medium, opacity: 0.5);
            c.FillEllipse(40, 40, 10, 10, Color.Red.Medium, opacity: 0.5);
        });

        string text = PdfTestUtils.InflatedText(pdf);
        int extGStateObjectCount = Regex.Count(text, @"/Type /ExtGState");
        Assert.Equal(1, extGStateObjectCount);
    }

    [Fact]
    public void DifferentOpacityValuesGetDistinctExtGStateObjects()
    {
        byte[] pdf = CanvasPage(80, c =>
        {
            c.FillRect(0, 0, 20, 20, Color.Blue.Medium, opacity: 0.5);
            c.FillEllipse(40, 40, 10, 10, Color.Red.Medium, opacity: 0.75);
        });

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Matches(@"/ca 0\.5000 /CA 0\.5000", text);
        Assert.Matches(@"/ca 0\.7500 /CA 0\.7500", text);
        Assert.Equal(2, Regex.Count(text, @"/Type /ExtGState"));
    }

    [Fact]
    public void SameOpacityAcrossDifferentPagesSharesOneExtGStateObject()
    {
        byte[] pdf = Document.Create(doc =>
        {
            for (int i = 0; i < 2; i++)
            {
                doc.Page(page =>
                {
                    page.Size(PageSize.A4);
                    page.Content().Canvas(40, c => c.FillRect(0, 0, 20, 20, Color.Blue.Medium, opacity: 0.5));
                });
            }
        }).PublishPdf();

        string text = PdfTestUtils.InflatedText(pdf);
        Assert.Equal(1, Regex.Count(text, @"/Type /ExtGState"));
    }

    // ---------------------------------------------------------------
    //  Validation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void OutOfRangeOpacityThrowsOnFillRect(double badOpacity)
    {
        var canvas = new VectorCanvas();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            canvas.FillRect(0, 0, 10, 10, Color.Black, badOpacity));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void OutOfRangeOpacityThrowsOnPathDescriptor(double badOpacity)
    {
        var path = new PathDescriptor();
        Assert.Throws<ArgumentOutOfRangeException>(() => path.Opacity(badOpacity));
    }

    [Fact]
    public void BoundaryOpacityValuesZeroAndOneAreValid()
    {
        // 0 = fully transparent, 1 = fully opaque (default) — both legal, neither should throw.
        byte[] pdf = CanvasPage(50, c =>
        {
            c.FillRect(0, 0, 10, 10, Color.Black, opacity: 0);
            c.FillRect(10, 10, 10, 10, Color.Black, opacity: 1);
        });
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }
}
