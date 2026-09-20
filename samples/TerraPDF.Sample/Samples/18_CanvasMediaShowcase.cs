namespace TerraPDF.Sample.Samples;

using TerraPDF.Core;
using TerraPDF.Helpers;

internal static class CanvasMediaShowcase
{
    internal static void Generate(string path, string imagePath)
    {
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().Text("Canvas Media Showcase")
                .Bold().FontSize(20).FontColor(Color.Blue.Darken2);

            page.Content().PaddingTop(16).Canvas(520, canvas =>
            {
                canvas.Text("Contain", 12, 18, Color.Blue.Darken2, 11, bold: true);
                canvas.DrawRect(10, 28, 200, 120, Color.Grey.Lighten5, Color.Grey.Medium);
                canvas.Image(imagePath, 10, 28, 200, 120, ImageFit.Contain);

                canvas.Text("Cover", 242, 18, Color.Blue.Darken2, 11, bold: true);
                canvas.DrawRect(240, 28, 200, 120, Color.Grey.Lighten5, Color.Grey.Medium);
                canvas.Image(imagePath, 240, 28, 200, 120, ImageFit.Cover);

                canvas.Line(10, 180, 440, 180, Color.Grey.Medium, 1,
                    dashPattern: [8, 4]);
                canvas.Text("Rotated text", 80, 285, Color.Orange.Darken2, 22,
                    bold: true, angle: -35);

                canvas.FillPie(260, 215, 150, 110, 20, 230, Color.Blue.Lighten2, 0.75);
                canvas.StrokePie(260, 215, 150, 110, 20, 230, Color.Blue.Darken2, 2);
                canvas.Text("Elliptical sector", 270, 350, Color.Blue.Darken2, 11);

                canvas.Path(shape => shape
                    .Arc(125, 420, 95, 45, 195, 250)
                    .Stroke(Color.Green.Darken2, 3));
                canvas.Text("PathDescriptor.Arc", 55, 490, Color.Green.Darken2, 11);
            });
        })).PublishPdf(path);
    }
}
