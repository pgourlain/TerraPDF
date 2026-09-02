using System.Globalization;
using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  17. FONT SUBSETTING SHOWCASE
//  Shows: FontFamily.Register(...) now embeds only the glyphs a document
//  actually shows (stage 1: blanking unused 'glyf' entries, no glyph-ID
//  renumbering) instead of the whole font file. Composite Latin glyphs
//  (accented letters) and Devanagari conjuncts/reph/rakar are themselves
//  composed from component glyphs never referenced directly by any text
//  run — this document exercises that closure, not just claims it.
// =============================================================================
internal static class FontSubsettingShowcase
{
    internal static void Generate(string path, string latoRegularPath, string devanagariRegularPath)
    {
        const string brand    = "#5B2A86";
        const string light    = "#F3EEFC";
        const string muted    = "#4B4453";
        const string gridLine = "#DCD1F0";

        FontFamily.Register("Lato", latoRegularPath);
        FontFamily.Register("Devanagari", devanagariRegularPath);

        byte[] pdf = Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF — Font Subsetting Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Demonstrates automatic glyph subsetting for embedded TrueType fonts");
            doc.MetadataKeywords("pdf; fonts; subsetting; truetype; embedding");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14)
                       .Text("TerraPDF — Font Subsetting").Bold().FontSize(16).FontColor(Color.White);
                    col.Item().Canvas(3, _ => { });
                });

                page.Footer().Column(f =>
                {
                    f.Item().LineHorizontal(0.5, gridLine);
                    f.Item().PaddingTop(4).AlignCenter()
                     .Text("TerraPDF — 2.1.0").FontSize(8).FontColor(muted);
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Background(light).Border(0.5, gridLine).Padding(12).Column(box =>
                    {
                        box.Item().Text("Only the glyphs shown here are embedded")
                           .Bold().FontSize(13).FontColor(brand);
                        box.Item().PaddingTop(6).Text(
                            "FontFamily.Register(...) still embeds the whole font's cmap, hmtx, and " +
                            "shaping tables, but now blanks every glyph outline this document never " +
                            "draws — automatically, with no API change. Accented letters and Devanagari " +
                            "conjuncts are composite glyphs under the hood, built from component glyphs " +
                            "no text run ever references directly; TerraPDF keeps every component a " +
                            "shown glyph depends on alive, so nothing breaks.").FontColor(muted);
                    });

                    col.Item().Text("Composite Latin glyphs (é, ö, ï are each two glyphs under the hood)")
                       .Bold().FontSize(12).FontColor(brand);
                    col.Item().Text("café, Wörld, naïve, Zürich")
                       .FontFamily("Lato").FontSize(14).FontColor(Color.Black);

                    col.Item().Text("Devanagari conjuncts and reph, also unaffected by subsetting")
                       .Bold().FontSize(12).FontColor(brand);
                    col.Item().Text("धर्म, प्रधानमंत्री, स्वास्थ्य, राष्ट्रीय, कार्यक्रम")
                       .FontFamily("Devanagari").FontSize(16).FontColor(Color.Black);

                    col.Item().PaddingTop(4).Text(
                        "See the console output from this sample run for the actual byte counts: " +
                        "the registered font files on disk versus what this document embeds.")
                        .Italic().FontSize(9).FontColor(muted);
                });
            });
        }).PublishPdf();

        File.WriteAllBytes(path, pdf);

        long latoRawBytes = new FileInfo(latoRegularPath).Length;
        long devanagariRawBytes = new FileInfo(devanagariRegularPath).Length;

        string Fmt(long n) => n.ToString("N0", CultureInfo.InvariantCulture).PadLeft(10);
        Console.WriteLine($"  [17] Font subsetting showcase -> {path}");
        Console.WriteLine($"       Lato-Regular.ttf on disk:               {Fmt(latoRawBytes)} bytes");
        Console.WriteLine($"       NotoSansDevanagari-Regular.ttf on disk: {Fmt(devanagariRawBytes)} bytes");
        Console.WriteLine($"       Whole generated PDF (both, subsetted):  {Fmt(pdf.Length)} bytes");
    }
}
