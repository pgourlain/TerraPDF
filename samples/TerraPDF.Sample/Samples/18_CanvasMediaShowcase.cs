namespace TerraPDF.Sample.Samples;

using System.Globalization;
using TerraPDF.Core;
using TerraPDF.Helpers;

/// <summary>
/// Demonstrates the canvas "media" APIs: positioned images and every fit mode,
/// PNG soft-mask transparency, constant-alpha layering, image sources and
/// natural sizing, dash patterns and phases, pie sectors, elliptical arcs,
/// and rotated text.
/// </summary>
/// <remarks>
/// Labels stay inside WinAnsiEncoding, the encoding the standard-14 fonts use.
/// Characters such as U+2192 (arrow) and U+2212 (true minus) fall outside it and
/// render as "?"; a registered custom font is required for those. See sample 14.
/// </remarks>
internal static class CanvasMediaShowcase
{
    internal static void Generate(string path, string jpegPath, string pngPath, string alphaPath)
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

        // Every fit mode, so the differences can be compared side by side. The wide
        // banner (1397x260) is used here on purpose: against a near-square target
        // its 5.4:1 aspect makes each mode's behaviour unmistakable.
        (ImageFit Fit, string Label)[] fitModes =
        [
            (ImageFit.Stretch,      "Stretch"),
            (ImageFit.Contain,      "Contain"),
            (ImageFit.Cover,        "Cover"),
            (ImageFit.CoverTopLeft, "CoverTopLeft"),
            (ImageFit.CropTopLeft,  "CropTopLeft"),
        ];

        (string Label, double Share)[] pieData =
        [
            ("Product A", 42), ("Product B", 28), ("Product C", 18), ("Product D", 12),
        ];
        string[] pieColors = [brandLight, accent, green, "#9B59B6"];

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
                          .Text("TerraPDF — Canvas Media Showcase")
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
                    row.RelativeItem().Text("TerraPDF — Canvas Media Showcase")
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

        // Helper: one standard page shell — two sections sit comfortably on each,
        // which keeps every page well clear of the content limit.
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

        byte[] jpegBytes = File.ReadAllBytes(jpegPath);
        var (naturalW, naturalH) = VectorCanvas.GetImageSizeInPoints(jpegBytes);

        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF – Canvas Media Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Positioned images, fit modes, transparency, dashes, pies, arcs and rotated text");
            doc.MetadataKeywords("pdf; canvas; images; smask; opacity; dash; arc; pie; rotation");
            doc.MetadataCreator("TerraPDF Sample Generator v1.0");

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 1 — Fit modes and transparency
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page => Shell(page, "Fit Modes & Transparency", col =>
            {
                // ── 1. Fit modes ─────────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "1  Image Fit Modes"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(96, c =>
                   {
                       const double boxW = 84, boxH = 70, gap = 10;
                       for (int i = 0; i < fitModes.Length; i++)
                       {
                           double x = i * (boxW + gap);
                           c.Text(fitModes[i].Label, x, 9, brand, 8, bold: true);
                           c.FillRect(x, 16, boxW, boxH, white);
                           c.Image(pngPath, x, 16, boxW, boxH, fitModes[i].Fit);
                           // Drawn after the image on purpose: the clip that Cover,
                           // CoverTopLeft and CropTopLeft apply is scoped with q/Q,
                           // so this border is never clipped away by it.
                           c.StrokeRect(x, 16, boxW, boxH, grid, 0.75);
                       }
                   });
                Caption(col.Item(),
                    "One 1397×260 px banner in five identical 84×70 pt targets — only Stretch distorts it");

                // ── 2. Transparency ──────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "2  PNG Transparency (/SMask)"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(108, c =>
                   {
                       // Colour bands sit behind the badge. The badge is an RGBA PNG
                       // whose ground is fully transparent and whose rim fades out
                       // gradually; TerraPDF embeds that alpha channel as a PDF soft
                       // mask, so the bands show through both.
                       string[] bands = [brandLight, accent, green, "#9B59B6"];
                       for (int i = 0; i < bands.Length; i++)
                           c.FillRect(0, 4 + i * 22, 300, 22, bands[i]);
                       c.Image(alphaPath, 16, 4, 88, 88, ImageFit.Contain);
                       c.Image(alphaPath, 120, 4, 88, 88, ImageFit.Contain);
                       c.StrokeRect(0, 4, 300, 88, grid, 0.75);

                       // The same badge over plain white, for comparison.
                       c.FillRect(320, 4, 140, 88, white);
                       c.Image(alphaPath, 320, 4, 140, 88, ImageFit.Contain);
                       c.StrokeRect(320, 4, 140, 88, grid, 0.75);

                       c.Text("transparent ground and a soft alpha rim", 0, 104, muted, 8);
                       c.Text("same badge on white", 320, 104, muted, 8);
                   });
                Caption(col.Item(),
                    "RGBA alpha is embedded as a PDF soft mask; fully opaque images skip the mask entirely");
            }));

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 2 — Opacity and image sources
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page => Shell(page, "Opacity & Image Sources", col =>
            {
                // ── 3. Opacity & layering ────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "3  Opacity & Layering"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(96, c =>
                   {
                       // A translucent caption bar over an image: opacity sets a real
                       // PDF /ExtGState constant alpha, so the bar blends with the
                       // picture underneath instead of hiding it.
                       c.Image(pngPath, 0, 4, 300, 56, ImageFit.Contain);
                       c.FillRect(0, 38, 300, 22, brand, 0.65);
                       c.Text("translucent bar over an image", 6, 53, white, 8);
                       c.StrokeRect(0, 4, 300, 56, grid, 0.75);

                       // Overlapping translucent discs: each overlap is a genuine
                       // alpha blend, not a third flat colour.
                       c.FillCircle(362, 32, 26, brandLight, 0.6);
                       c.FillCircle(400, 32, 26, accent,     0.6);
                       c.FillCircle(381, 60, 26, green,      0.6);
                       c.Text("alpha blending", 344, 92, muted, 8);
                   });
                Caption(col.Item(),
                    "Constant alpha via PDF /ExtGState — unlike /SMask it belongs to the drawing, not the image");

                // ── 4. Sources & natural size ────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "4  Sources & Natural Size"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(118, c =>
                   {
                       // The same picture from three different sources. TerraPDF
                       // hashes the pixel data, so all three share ONE embedded
                       // image XObject in the finished PDF.
                       c.Text("from file path", 0, 9, brand, 8, bold: true);
                       c.Image(jpegPath, 0, 16, naturalW, naturalH);

                       c.Text("from byte[]", 130, 9, brand, 8, bold: true);
                       c.Image(jpegBytes, 130, 16, naturalW, naturalH);

                       c.Text("from Stream", 260, 9, brand, 8, bold: true);
                       // The canvas copies the data here, so the caller stays the
                       // owner and may dispose the stream immediately afterwards.
                       using (var stream = new MemoryStream(jpegBytes))
                           c.Image(stream, 260, 16, naturalW, naturalH);

                       string size = string.Create(CultureInfo.InvariantCulture,
                           $"GetImageSizeInPoints -> {naturalW:0.#} × {naturalH:0.#} pt (pixels converted at 96 DPI)");
                       c.Text(size, 0, 113, muted, 8);
                   });
                Caption(col.Item(),
                    "File path · byte[] · Stream — identical data is embedded once and shared");
            }));

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 3 — Dash patterns and elliptical arcs
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page => Shell(page, "Dashes & Arcs", col =>
            {
                // ── 5. Dash patterns ─────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "5  Dash Patterns & Phase"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(100, c =>
                   {
                       (double[] Pattern, double Phase, string Label)[] dashes =
                       [
                           ([8, 4],        0, "[8 4]  phase 0"),
                           ([8, 4],        4, "[8 4]  phase 4  (offset start)"),
                           ([1, 3],        0, "[1 3]  dotted"),
                           ([12, 3, 3, 3], 0, "[12 3 3 3]  dash-dot"),
                       ];

                       for (int i = 0; i < dashes.Length; i++)
                       {
                           double y = 12 + i * 18;
                           c.Line(0, y, 240, y, brand, 1.5, 1, dashes[i].Pattern, dashes[i].Phase);
                           c.Text(dashes[i].Label, 248, y + 3, muted, 8);
                       }

                       // Each dashed command restores the graphics state, so a later
                       // stroke is solid without resetting anything.
                       c.Line(0, 84, 240, 84, accent, 1.5);
                       c.Text("solid — dash state never leaks", 248, 87, muted, 8);

                       c.StrokeRect(370, 8, 85, 76, Color.Green.Darken2, 1.5, 1, [6, 3], 0);
                   });
                Caption(col.Item(),
                    "Dash arrays alternate painted and skipped lengths; phase offsets the start within the pattern");

                // ── 6. Elliptical arcs ───────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "6  Elliptical Arcs — Centre + Radii"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(150, c =>
                   {
                       // PathDescriptor.Arc takes CENTRE + RADII (cx, cy, rx, ry),
                       // the opposite of the pie helpers' bounding box on page 4.
                       // The dot marks the centre and the dashed rules span rx/ry.
                       void ArcDemo(double cx, double cy, double rx, double ry,
                                    double startAngle, double sweep, string color, string label)
                       {
                           c.Line(cx - rx, cy, cx + rx, cy, grid, 0.75, 1, [3, 3], 0);
                           c.Line(cx, cy - ry, cx, cy + ry, grid, 0.75, 1, [3, 3], 0);
                           c.Path(p => p.Arc(cx, cy, rx, ry, startAngle, sweep).Stroke(color, 2.5));
                           c.FillCircle(cx, cy, 2.5, muted);
                           double w = VectorCanvas.MeasureTextWidth(label, 8);
                           c.Text(label, cx - w / 2, 132, muted, 8);   // shared baseline
                       }

                       ArcDemo(70,  62, 55, 40,   0,  270, brandLight,          "rx 55, ry 40, sweep +270");
                       ArcDemo(215, 62, 55, 40,   0, -210, accent,              "rx 55, ry 40, sweep -210");
                       ArcDemo(370, 62, 55, 55, 135,  180, Color.Green.Darken2, "rx = ry, sweep +180");
                   });
                Caption(col.Item(),
                    "Negative sweeps run counter-clockwise; arcs split into cubic Bézier segments of at most 90°");
            }));

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 4 — Pie sectors and rotated text
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page => Shell(page, "Pie Sectors & Rotated Text", col =>
            {
                // ── 7. Pie sectors ───────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "7  Pie Sectors — Revenue Mix"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(185, c =>
                   {
                       // NOTE: the pie helpers take a BOUNDING BOX (x, y, width,
                       // height) — not a centre and radii, which is what
                       // PathDescriptor.Arc on page 3 takes.
                       const double boxX = 10, boxY = 15, boxSize = 160;

                       double start = -90;   // 0 deg is 3 o'clock, so -90 starts at 12
                       for (int i = 0; i < pieData.Length; i++)
                       {
                           double sweep = pieData[i].Share / 100.0 * 360.0;
                           c.FillPie(boxX, boxY, boxSize, boxSize, start, sweep, pieColors[i]);
                           c.StrokePie(boxX, boxY, boxSize, boxSize, start, sweep, white, 1.5);
                           start += sweep;
                       }

                       for (int i = 0; i < pieData.Length; i++)
                       {
                           double ly = 24 + i * 26;
                           c.FillRoundedRect(190, ly, 14, 14, 3, pieColors[i]);
                           string label = string.Create(CultureInfo.InvariantCulture,
                               $"{pieData[i].Label}   {pieData[i].Share:0}%");
                           c.Text(label, 212, ly + 11, brand, 10);
                       }

                       // A single sector drawn filled and stroked in one call, with a
                       // negative sweep so it runs counter-clockwise.
                       c.DrawPie(350, 30, 90, 90, 0, -250, "#FFE0B2", accent, 1.5);
                       c.Text("DrawPie, sweep -250", 350, 138, muted, 8);
                   });
                Caption(col.Item(),
                    "FillPie + StrokePie build the chart; angles start at 3 o'clock and increase clockwise");

                // ── 8. Rotated text ──────────────────────────────────────────────
                col.Item().Component(new SectionHeader(brand, "8  Rotated Text Labels"));
                col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                   .Canvas(175, c =>
                   {
                       // Radial dial: each label is rotated to match its spoke.
                       const double cx = 85, cy = 78, r = 56;
                       for (int deg = 0; deg < 360; deg += 30)
                       {
                           double rad = deg * Math.PI / 180.0;
                           double ex = cx + r * Math.Cos(rad);
                           double ey = cy + r * Math.Sin(rad);
                           c.Line(cx, cy, ex, ey, grid, 0.5);
                           c.Text(deg.ToString(CultureInfo.InvariantCulture),
                                  ex + 4 * Math.Cos(rad), ey + 4 * Math.Sin(rad), brand, 8, angle: deg);
                       }
                       c.FillCircle(cx, cy, 2.5, brand);
                       c.Text("angle matches each spoke", 20, 166, muted, 8);

                       // Five angles from one origin. Each label starts 16 pt along its
                       // own spoke so the five fan out instead of piling up, and the
                       // spokes stay short enough to remain inside the panel.
                       const double ox = 250, oy = 78;
                       foreach (double a in new double[] { -45, -20, 0, 20, 45 })
                       {
                           double rad = a * Math.PI / 180.0;
                           c.Line(ox, oy, ox + 105 * Math.Cos(rad), oy + 105 * Math.Sin(rad), grid, 0.5);
                           string label = string.Create(CultureInfo.InvariantCulture, $"{a:0} degrees");
                           c.Text(label, ox + 16 * Math.Cos(rad), oy + 16 * Math.Sin(rad), brand, 9, angle: a);
                       }
                       c.FillCircle(ox, oy, 2.5, brand);
                       c.Text("text rotates around its baseline point", 250, 166, muted, 8);
                   });
                Caption(col.Item(),
                    "Text rotates clockwise around its baseline point — negative angles rotate counter-clockwise");
            }));

        }).PublishPdf(path);

        Console.WriteLine($"  [18] Canvas media showcase -> {path}");
    }
}
