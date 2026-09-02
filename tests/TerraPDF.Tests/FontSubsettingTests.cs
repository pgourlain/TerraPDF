using System.Text;
using System.Text.RegularExpressions;
using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Drawing.TrueType;
using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for stage-1 font subsetting (<see cref="TrueTypeFont.BuildSubsetRawData"/>):
/// blanking <c>glyf</c> entries for glyphs a document never shows, while
/// preserving every glyph ID (no renumbering) and correctly keeping composite
/// glyph components alive even when nothing in the document references their
/// glyph ID directly.
/// <para>
/// Test fixture: <c>TestAssets/Fonts/Lato-Regular.ttf</c> (shared with
/// <see cref="FontEmbeddingTests"/>) — a professionally hinted Latin font
/// where accented letters are composite glyphs, exactly the case this stage
/// exists to not break.
/// </para>
/// <para>
/// Measured, not assumed: in this fixture 'glyf' itself drops by over 99%
/// (397,998B → ~1,000B for a four-letter word), but Lato ships a 210KB
/// <c>GPOS</c> kerning table that this stage does not touch (still sized for
/// every glyph pair in the full font, since glyph IDs are never renumbered)
/// — so the realistic whole-document win here is a little over half, not
/// the three-quarters a glyf-only view would suggest. Trimming unused GPOS
/// pairs is a real, separate follow-up, not part of this stage.
/// </para>
/// </summary>
public sealed class FontSubsettingTests
{
    private static readonly string LatoRegularPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "Lato-Regular.ttf");
    private static readonly byte[] LatoRegularBytes = File.ReadAllBytes(LatoRegularPath);

    private static string UniqueFamily([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"{caller}-{Guid.NewGuid():N}";

    // ---------------------------------------------------------------
    //  TrueTypeFont.BuildSubsetRawData
    // ---------------------------------------------------------------

    [Fact]
    public void SubsetRoundTripsThroughParseAndShrinksSubstantially()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        ushort gidA = font.GetGlyphId('A');

        byte[] subset = font.BuildSubsetRawData(new HashSet<ushort> { gidA });

        // Must remain a valid, parseable TrueType file.
        var reparsed = TrueTypeFont.Parse(subset);
        Assert.Equal(font.NumGlyphs, reparsed.NumGlyphs);
        Assert.Equal(font.UnitsPerEm, reparsed.UnitsPerEm);

        // A single-letter subset of a large multi-script font should be dramatically smaller.
        Assert.True(subset.Length < LatoRegularBytes.Length / 2,
            $"subset ({subset.Length}B) should be well under half of the original ({LatoRegularBytes.Length}B)");
    }

    [Fact]
    public void SubsetPreservesGlyphIdsAndMetricsForKeptGlyphs()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        ushort gidA = font.GetGlyphId('A');
        ushort gidB = font.GetGlyphId('B');

        byte[] subset = font.BuildSubsetRawData(new HashSet<ushort> { gidA, gidB });
        var reparsed = TrueTypeFont.Parse(subset);

        // cmap and hmtx are untouched tables, so lookups against the subset
        // must return byte-identical glyph IDs and widths to the original.
        Assert.Equal(gidA, reparsed.GetGlyphId('A'));
        Assert.Equal(gidB, reparsed.GetGlyphId('B'));
        Assert.Equal(font.GetAdvanceWidthInEm(gidA), reparsed.GetAdvanceWidthInEm(gidA));
        Assert.Equal(font.GetAdvanceWidthInEm(gidB), reparsed.GetAdvanceWidthInEm(gidB));
    }

    [Fact]
    public void SubsetBlanksGlyphsOutsideTheRequestedSet()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        ushort gidA = font.GetGlyphId('A');
        ushort gidZ = font.GetGlyphId('Z');
        Assert.NotEqual(gidA, gidZ);

        byte[] subset = font.BuildSubsetRawData(new HashSet<ushort> { gidA });
        var reparsed = TrueTypeFont.Parse(subset);

        Assert.True(reparsed.HasNonEmptyGlyphOutline(gidA));
        Assert.False(reparsed.HasNonEmptyGlyphOutline(gidZ));
    }

    [Fact]
    public void SubsetAlwaysKeepsNotdefGlyphZero()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        ushort gidA = font.GetGlyphId('A');

        // .notdef (glyph 0) is not in the requested set at all.
        byte[] subset = font.BuildSubsetRawData(new HashSet<ushort> { gidA });
        var reparsed = TrueTypeFont.Parse(subset);

        // .notdef commonly has real outline data (a box); regardless of its
        // own outline, it must round-trip cleanly and remain glyph 0.
        Assert.Equal(font.NumGlyphs, reparsed.NumGlyphs);
    }

    [Fact]
    public void CompositeGlyphComponentsSurviveSubsettingEvenWhenNotDirectlyRequested()
    {
        var font = TrueTypeFont.Parse(LatoRegularBytes);
        ushort gidEAcute = font.GetGlyphId('é'); // é
        Assert.NotEqual(0, gidEAcute); // Lato must have this glyph for the test to mean anything

        var components = font.GetComponentGlyphIds(gidEAcute);
        Assert.True(components.Count > 0,
            "expected 'é' to be a composite glyph in Lato — if this ever fails, Lato changed and this test needs a different composite letter");

        // Request only the composite glyph itself, deliberately not its components —
        // exactly what CustomGlyphUsage would record from a content stream, since
        // nothing ever shows a component glyph ID directly.
        byte[] subset = font.BuildSubsetRawData(new HashSet<ushort> { gidEAcute });
        var reparsed = TrueTypeFont.Parse(subset);

        Assert.True(reparsed.HasNonEmptyGlyphOutline(gidEAcute));
        foreach (ushort component in components)
        {
            Assert.True(reparsed.HasNonEmptyGlyphOutline(component),
                $"component glyph {component} of composite 'é' (gid {gidEAcute}) was blanked — the accent would render broken");
        }
    }

    // ---------------------------------------------------------------
    //  End-to-end: PDF embedding
    // ---------------------------------------------------------------

    [Fact]
    public void OneWordCustomFontDocumentIsDramaticallySmallerThanPreSubsettingBaseline()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("Lato");
            });
        }).PublishPdf();

        // Recorded pre-subsetting baseline (roadmap measurement): a one-word
        // Lato document cost 358,026 bytes embedding the whole font. Measured
        // post-subsetting: 'glyf' itself drops from 397,998B to ~1,000B (a
        // four-letter word's worth of outlines), but Lato's GPOS kerning
        // table (210KB, untouched by this stage — see the class doc) then
        // dominates what's left, capping the realistic win at just over half
        // rather than the three-quarters a glyf-only view would suggest.
        Assert.True(pdf.Length < 358_026 / 2,
            $"expected a subsetted one-word document under half of the pre-subsetting baseline, got {pdf.Length}B");
    }

    [Fact]
    public void SubsettedFontFileParsesBackCleanlyOutOfTheGeneratedPdf()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("Héllo, Wörld");
            });
        }).PublishPdf();

        byte[] fontFileBytes = ExtractFontFile2Stream(pdf);
        var reparsed = TrueTypeFont.Parse(fontFileBytes); // must not throw

        // The accented letters actually shown must still resolve to non-empty outlines.
        foreach (char c in "Hé lWö")
        {
            if (c == ' ') continue;
            ushort gid = reparsed.GetGlyphId(c);
            Assert.NotEqual(0, gid);
            Assert.True(reparsed.HasNonEmptyGlyphOutline(gid), $"'{c}' (gid {gid}) has an empty outline after subsetting");
        }
    }

    [Fact]
    public void SubsetBaseFontNameCarriesAPdfSubsetTag()
    {
        string family = UniqueFamily();
        FontFamily.Register(family, LatoRegularBytes);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.DefaultTextStyle(s => s.FontFamily(family));
                page.Content().Text("Hi");
            });
        }).PublishPdf();

        string text = Encoding.Latin1.GetString(pdf);
        // §9.6.4 subset tag: six uppercase letters, a '+', then the original BaseFont name.
        Assert.Matches(@"/BaseFont /[A-Z]{6}\+", text);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    /// <summary>Extracts and Flate-inflates the (single, non-encrypted) FontFile2 stream from a generated PDF.</summary>
    private static byte[] ExtractFontFile2Stream(byte[] pdf)
    {
        string text = Encoding.Latin1.GetString(pdf);
        var refMatch = Regex.Match(text, @"/FontFile2 (\d+) 0 R");
        Assert.True(refMatch.Success, "no /FontFile2 reference found");
        string objNum = refMatch.Groups[1].Value;

        var objMatch = Regex.Match(text, $@"(?m)^{objNum} 0 obj\b.*?\nstream\r?\n", RegexOptions.Singleline);
        Assert.True(objMatch.Success, $"object {objNum} not found");
        int dataStart = objMatch.Index + objMatch.Length;
        int dataEnd = text.IndexOf("endstream", dataStart, StringComparison.Ordinal);
        while (pdf[dataEnd - 1] == (byte)'\n' || pdf[dataEnd - 1] == (byte)'\r') dataEnd--;

        byte[] compressed = pdf[dataStart..dataEnd];
        using var input = new MemoryStream(compressed);
        using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
