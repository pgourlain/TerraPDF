using TerraPDF.Core;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

public sealed class CanvasImageTests
{
    private static byte[] Render(Action<VectorCanvas> draw) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(0);
            page.Content().Canvas(100, draw);
        })).PublishPdf();

    [Fact]
    public void ImageFromBytesEmitsImageObjectAndRequestedMatrix()
    {
        byte[] pdf = Render(canvas => canvas.Image(TestImageData.MakePng(4, 5, false, 255), 12, 18, 40, 30));
        string content = PdfTestUtils.InflatedText(pdf);

        Assert.Contains("/Subtype /Image", content);
        Assert.Contains("40.00 0 0 30.00 12.00", content);
        Assert.Contains("/Im", content);
        Assert.Contains(" Do", content);
    }

    [Fact]
    public void JpegImageEmitsDctDecodeResource()
    {
        byte[] pdf = Render(canvas =>
            canvas.Image(TestImageData.MakeJpegHeaderOnly(7, 9), 5, 6, 30, 20));
        string raw = System.Text.Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Subtype /Image", raw);
        Assert.Contains("/Filter /DCTDecode", raw);
    }

    [Fact]
    public void SameCanvasImageIsReusedAcrossPages()
    {
        byte[] image = TestImageData.MakePng(2, 2, false, 255);
        byte[] pdf = Document.Create(document =>
        {
            document.Page(page => page.Content().Canvas(100, canvas =>
                canvas.Image(image, 0, 0, 20, 20)));
            document.Page(page => page.Content().Canvas(100, canvas =>
                canvas.Image(image, 10, 10, 30, 30)));
        }).PublishPdf();
        string raw = System.Text.Encoding.Latin1.GetString(pdf);
        string content = PdfTestUtils.InflatedText(pdf);

        Assert.Equal(1, raw.Split("/Subtype /Image").Length - 1);
        Assert.Equal(2, content.Split(" Do").Length - 1);
    }

    [Fact]
    public void TransparentImageFromStreamEmitsSoftMaskAndKeepsStreamOpen()
    {
        using var stream = new MemoryStream(TestImageData.MakePng(3, 4, true, 128));
        byte[] pdf = Render(canvas => canvas.Image(stream, 1, 2, 20, 25));

        Assert.True(stream.CanRead);
        Assert.Contains("/SMask", System.Text.Encoding.Latin1.GetString(pdf));
    }

    [Fact]
    public void ContainPreservesAspectRatioAndCentersImage()
    {
        byte[] pdf = Render(canvas =>
            canvas.Image(TestImageData.MakePng(4, 2, false, 255), 0, 0, 40, 30, ImageFit.Contain));
        string content = PdfTestUtils.InflatedText(pdf);

        Assert.Matches(@"40\.00 0 0 20\.00 0\.00 [0-9.]+ cm", content);
    }

    [Fact]
    public void CoverClipsToTargetRectangleAndRestoresGraphicsState()
    {
        byte[] pdf = Render(canvas =>
        {
            canvas.Image(TestImageData.MakePng(4, 2, false, 255), 10, 15, 40, 30, ImageFit.Cover);
            canvas.FillRect(0, 0, 5, 5, "#FF0000");
        });
        string content = PdfTestUtils.InflatedText(pdf);

        Assert.Matches(@"10\.00 [0-9.]+ 40\.00 30\.00 re W n", content);
        Assert.Matches(@"60\.00 0 0 30\.00 0\.00 [0-9.]+ cm", content);
        Assert.Contains("Q\n1.0000 0.0000 0.0000 rg", content);
    }

    [Fact]
    public void CoverTopLeftClipsWithoutCenteringImage()
    {
        byte[] pdf = Render(canvas =>
        {
            canvas.Image(TestImageData.MakePng(4, 2, false, 255), 10, 15, 40, 30, ImageFit.CoverTopLeft);
            canvas.FillRect(0, 0, 5, 5, "#FF0000");
        });
        string content = PdfTestUtils.InflatedText(pdf);

        Assert.Matches(@"60\.00 0 0 30\.00 10\.00 [0-9.]+ cm", content);
        Assert.Contains("Q\n1.0000 0.0000 0.0000 rg", content);
    }

    [Fact]
    public void CropTopLeftKeepsNaturalSizeAndClipsRightAndBottom()
    {
        byte[] pdf = Render(canvas =>
            canvas.Image(TestImageData.MakePng(200, 200, false, 255), 10, 15, 135, 90, ImageFit.CropTopLeft));
        string content = PdfTestUtils.InflatedText(pdf);

        Assert.Matches(@"10\.00 [0-9.]+ 135\.00 90\.00 re W n", content);
        Assert.Matches(@"150\.00 0 0 150\.00 10\.00 [0-9.]+ cm", content);
    }

    [Fact]
    public void PortraitImageCanUseCoverFit()
    {
        byte[] pdf = Render(canvas =>
            canvas.Image(TestImageData.MakePng(2, 4, false, 255), 5, 6, 30, 20, ImageFit.Cover));
        string content = PdfTestUtils.InflatedText(pdf);

        Assert.Matches(@"5\.00 [0-9.]+ 30\.00 20\.00 re W n", content);
        Assert.Matches(@"30\.00 0 0 60\.00 [0-9.-]+ [0-9.]+ cm", content);
    }

    [Fact]
    public void ImageCanBeLoadedFromFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"terrapdf-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, TestImageData.MakePng(2, 3, false, 255));
        try
        {
            byte[] pdf = Render(canvas => canvas.Image(path, 0, 0, 12, 18));
            Assert.Contains("/Subtype /Image", System.Text.Encoding.Latin1.GetString(pdf));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmptyOrUnsupportedCanvasImageThrows()
    {
        var canvas = new VectorCanvas();

        Assert.Throws<ArgumentException>(() => canvas.Image([], 0, 0, 10, 10));
        Assert.Throws<ArgumentException>(() => canvas.Image([1, 2, 3], 0, 0, 10, 10));
    }

    [Fact]
    public void NonPositiveCanvasImageDimensionsThrow()
    {
        var canvas = new VectorCanvas();
        byte[] png = TestImageData.MakePng(1, 1, false, 255);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.Image(png, 0, 0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.Image(png, 0, 0, 10, -1));
    }
}
