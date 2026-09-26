namespace TerraPDF.Sample.Samples;

using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;

/// <summary>
/// Demonstrates the canvas "extras": dashed ellipses, rounded rectangles, pies
/// and paths, the rounded-rectangle path, linear and radial gradient fills,
/// canvas hyperlinks, in-document links, canvas bookmarks, and canvas QR codes.
/// </summary>
/// <remarks>
/// Labels stay inside WinAnsiEncoding, the encoding the standard-14 fonts use.
/// </remarks>
internal static class CanvasExtrasShowcase
{
    internal static void Generate(string path)
    {
        // ── Palette ───────────────────────────────────────────────────────────────
        const string brand      = "#1A3C5E";   // deep navy
        const string brandLight = "#2E6DA4";   // medium blue
        const string accent     = "#E87722";   // vivid orange
        const string green      = "#27AE60";
        const string grid       = "#E0E6ED";   // light rules
        const string panelBg    = "#F4F7FA";   // panel background
        const string muted      = "#7A8A99";   // subdued text
        const string white      = "#FFFFFF";

        // Helper: caption under a canvas panel
        void Caption(TerraPDF.Infra.IContainer container, string text) =>
            container.PaddingTop(4).AlignCenter()
                     .Text(text).FontSize(9).FontColor(muted).Italic();

        // Helper: the header strip every page repeats
        void Header(PageDescriptor page, string subtitle) =>
            page.Header().Column(col =>
            {
                col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14)
                   .Row(hdr =>
                   {
                       hdr.RelativeItem().AlignMiddle()
                          .Text("TerraPDF — Canvas Extras Showcase")
                          .Bold().FontSize(17).FontColor(white);
                       hdr.AutoItem().AlignRight().AlignMiddle()
                          .Text(subtitle).FontSize(10).FontColor(grid);
                   });
                col.Item().Background(accent).Canvas(3, _ => { });   // thin accent stripe
            });

        // Helper: the footer every page repeats
        void Footer(PageDescriptor page) =>
            page.Footer().Column(f =>
            {
                f.Item().LineHorizontal(0.5, grid);
                f.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text("TerraPDF — Canvas Extras Showcase")
                       .FontSize(8).FontColor(muted);
                    row.AutoItem().AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(brand);
                        t.Span(" / ").FontSize(8).FontColor(muted);
                        t.TotalPages().FontSize(8).FontColor(brand);
                    });
                });
            });

        // Helper: one standard page shell
        void Shell(PageDescriptor page, string subtitle, Action<ColumnDescriptor> body)
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Color.White);
            page.DefaultTextStyle(s => s.FontSize(11));

            Header(page, subtitle);
            page.Content().PaddingTop(14).Column(col =>
            {
                col.Spacing(16);
                body(col);
            });
            Footer(page);
        }

        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF – Canvas Extras Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Dashed shapes, gradients, canvas links, bookmarks and QR codes");
            doc.MetadataKeywords("pdf; canvas; dash; gradient; shading; link; bookmark; qr");
            doc.MetadataCreator("TerraPDF Sample Generator v1.0");

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 1 — Dashes and gradients
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page => Shell(page, "Dashes & Gradients", col =>
            {
                // ── 1. Dashed shapes and paths ───────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "1  Dashed Shapes & Paths"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(96, c =>
                   {
                       // Canvas bookmarks point at a vertical position of the page
                       // the canvas is drawn on; the second one nests under the first.
                       c.Bookmark("Canvas extras");
                       c.Bookmark("Dashed shapes", 0, "Canvas extras");

                       c.StrokeEllipse(45, 40, 40, 28, brandLight, 1.5, dashPattern: [6, 3]);
                       c.DrawRoundedRect(100, 12, 90, 56, 10, white, accent, 1.5, dashPattern: [2, 2]);
                       c.StrokePie(205, 10, 64, 64, -90, 270, green, 1.5, dashPattern: [8, 3, 2, 3]);
                       c.Path(p => p
                           .RoundedRect(290, 12, 80, 56, 16)
                           .Fill(white)
                           .Stroke(brand, 1.5)
                           .Dash([4, 4], 2));
                       c.Path(p => p
                           .Polygon((390, 68), (430, 12), (470, 68))
                           .Stroke(accent, 1.5)
                           .Dash([1, 3]));

                       c.Text("ellipse", 22, 90, muted, 8);
                       c.Text("rounded rect", 120, 90, muted, 8);
                       c.Text("pie", 228, 90, muted, 8);
                       c.Text("RoundedRect path", 294, 90, muted, 8);
                       c.Text("polygon path", 404, 90, muted, 8);
                   });
                Caption(col.Item(),
                    "Native PDF dash patterns with phase — scoped with q/Q so solid strokes stay solid");

                // ── 2. Gradient fills ────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "2  Gradient Fills"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(110, c =>
                   {
                       c.Bookmark("Gradients", 0, "Canvas extras");

                       c.Path(p => p.Rect(0, 8, 110, 70).FillLinearGradient(brandLight, white));
                       c.Path(p => p.Rect(125, 8, 110, 70).FillLinearGradient(accent, brand, 90));
                       c.Path(p => p.RoundedRect(250, 8, 110, 70, 14)
                           .FillLinearGradient(green, brandLight, 45)
                           .Stroke(brand, 1));
                       c.Path(p => p.Circle(415, 43, 35).FillRadialGradient(white, accent));

                       c.Text("linear, 0°", 0, 96, muted, 8);
                       c.Text("linear, 90°", 125, 96, muted, 8);
                       c.Text("45° + outline", 250, 96, muted, 8);
                       c.Text("radial", 400, 96, muted, 8);
                   });
                Caption(col.Item(),
                    "Axial and radial PDF shadings clipped to the path — vector, resolution independent");
            }));

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 2 — Links and QR codes
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page => Shell(page, "Links & QR Codes", col =>
            {
                // ── 3. Links ─────────────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "3  Links & Bookmarks"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(70, c =>
                   {
                       c.Bookmark("Links and QR codes");

                       // A link is only a clickable rectangle: draw the button first,
                       // then lay the annotation over the same area.
                       c.FillRoundedRect(0, 10, 180, 30, 6, brandLight);
                       c.Text("Open the TerraPDF repository", 12, 29, white, 9, bold: true);
                       c.Link(0, 10, 180, 30, "https://github.com/sahebansari/TerraPDF");

                       c.StrokeRoundedRect(200, 10, 180, 30, 6, brand, 1);
                       c.Text("Back to page 1", 212, 29, brand, 9, bold: true);
                       c.InternalLink(200, 10, 180, 30, 1);

                       c.Text("Open the viewer's outline pane to see the canvas bookmarks.", 0, 60, muted, 8);
                   });
                Caption(col.Item(),
                    "URI and GoTo link annotations, plus outline entries, all placed from a canvas");

                // ── 4. QR codes ──────────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "4  QR Codes on the Canvas"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(130, c =>
                   {
                       c.QrCode("https://github.com/sahebansari/TerraPDF", 0, 0, 110,
                           backgroundHex: white);
                       c.QrCode("TerraPDF", 130, 0, 110, QrErrorCorrectionLevel.H,
                           hexColor: brand, backgroundHex: white);
                       c.QrCode("https://github.com/sahebansari/TerraPDF", 260, 0, 110,
                           hexColor: accent, quietZoneModules: 1);
                       c.Link(0, 0, 110, 110, "https://github.com/sahebansari/TerraPDF");

                       c.Text("level M, linked", 0, 124, muted, 8);
                       c.Text("level H, colored", 130, 124, muted, 8);
                       c.Text("no background, 1-module quiet zone", 260, 124, muted, 8);
                   });
                Caption(col.Item(),
                    "One filled vector path per symbol — module runs merged into rectangles");
            }));
        }).PublishPdf(path);

        Console.WriteLine($"  [19] Canvas extras showcase -> {path}");
    }
}
