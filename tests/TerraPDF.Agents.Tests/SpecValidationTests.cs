using TerraPDF.Agents.Rendering;
using TerraPDF.Agents.Spec;

namespace TerraPDF.Agents.Tests;

public class SpecValidationTests
{
    private static PdfRenderResult Render(string json, string? assetDirectory = null) =>
        new PdfSpecRenderer(assetDirectory).Render(json);

    private static void AssertError(string json, string path, string fragment, string? assetDirectory = null)
    {
        PdfRenderResult result = Render(json, assetDirectory);
        Assert.False(result.Success);
        Assert.Null(result.Pdf);
        Assert.Contains(result.Errors, e => e.Path == path && e.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingContentIsAnError() =>
        AssertError("""{ "title": "x" }""", "$.content", "no content");

    [Fact]
    public void MalformedJsonReportsAPath()
    {
        PdfRenderResult result = Render("""{ "content": [ { "type": "paragraph", "text": ] }""");
        Assert.False(result.Success);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void UnknownPropertyIsAnErrorNotSilentlyIgnored()
    {
        PdfRenderResult result = Render("""{ "content": [ { "type": "paragraph", "text": "x", "colour": "#FF0000" } ] }""");
        Assert.False(result.Success);
        Assert.Contains("colour", result.Errors[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("TerraPDF.Agents.Spec", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownBlockTypeListsValidTypes() =>
        AssertError("""{ "content": [ { "type": "video" } ] }""", "$.content[0].type", "keyValue");

    [Fact]
    public void AllErrorsAreReportedAtOnce()
    {
        PdfRenderResult result = Render("""
            { "page": { "size": "B5" },
              "content": [ { "type": "heading" }, { "type": "paragraph", "text": "x", "color": "reddish" } ] }
            """);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void RowWiderThanTableIsAnError() =>
        AssertError("""{ "content": [ { "type": "table", "columns": [ { "header": "A" } ], "rows": [ ["1", "2"] ] } ] }""",
            "$.content[0].rows[0]", "2 cells");

    [Fact]
    public void ShortRowIsAWarning()
    {
        PdfRenderResult result = Render("""{ "content": [ { "type": "table", "columns": [ { "header": "A" }, { "header": "B" } ], "rows": [ ["1"] ] } ] }""");
        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Path == "$.content[0].rows[0]");
    }

    [Fact]
    public void ChartSeriesMustMatchLabels() =>
        AssertError("""{ "content": [ { "type": "chart", "chartType": "bar", "labels": ["a","b"], "series": [ { "name": "s", "values": [1] } ] } ] }""",
            "$.content[0].series[0].values", "must match");

    [Fact]
    public void PieChartRejectsNegativeValues() =>
        AssertError("""{ "content": [ { "type": "chart", "chartType": "pie", "labels": ["a","b"], "values": [1, -1] } ] }""",
            "$.content[0].values", "positive");

    [Fact]
    public void NestedErrorsCarryFullPath() =>
        AssertError("""{ "content": [ { "type": "columns", "columns": [ { "content": [ { "type": "list", "items": [] } ] } ] } ] }""",
            "$.content[0].columns[0].content[0].items", "at least one");

    [Fact]
    public void HeadingInHeaderIsAnError() =>
        AssertError("""{ "header": [ { "type": "heading", "text": "x" } ], "content": [ { "type": "paragraph", "text": "x" } ] }""",
            "$.header[0].type", "header or footer");

    [Fact]
    public void UnsafeLinkIsRejected() =>
        AssertError("""{ "content": [ { "type": "paragraph", "text": "x", "link": "javascript:alert(1)" } ] }""",
            "$.content[0].link", "http");

    [Fact]
    public void NonAsciiBarcodeIsRejected() =>
        AssertError("""{ "content": [ { "type": "barcode", "data": "café" } ] }""", "$.content[0].data", "ASCII");

    [Fact]
    public void TextOutsideWinAnsiWarnsOnce()
    {
        PdfRenderResult result = Render("""{ "content": [ { "type": "paragraph", "text": "Привет" }, { "type": "paragraph", "text": "→" } ] }""");
        Assert.True(result.Success);
        SpecIssue warning = Assert.Single(result.Warnings);
        Assert.Equal("$.content[0].text", warning.Path);
    }

    [Fact]
    public void WinAnsiTypographyDoesNotWarn()
    {
        PdfRenderResult result = Render("""{ "content": [ { "type": "paragraph", "text": "“Quotes” – dashes — bullets • €5 café" } ] }""");
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void UnregisteredFontFamilyIsAnError() =>
        AssertError("""{ "theme": { "fontFamily": "Roboto" }, "content": [ { "type": "paragraph", "text": "x" } ] }""",
            "$.theme.fontFamily", "Roboto");

    [Fact]
    public void HostRegisteredFontFamilyIsAccepted()
    {
        var renderer = new PdfSpecRenderer(hostFontFamilies: ["Roboto"]);
        PdfRenderResult result = renderer.Validate(PdfSpecReader.TryRead(
            """{ "theme": { "fontFamily": "Roboto" }, "content": [ { "type": "paragraph", "text": "x" } ] }""", out _)!);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void EncryptionNeedsAPassword() =>
        AssertError("""{ "encryption": {}, "content": [ { "type": "paragraph", "text": "x" } ] }""", "$.encryption", "password");

    [Fact]
    public void ColorsAreNormalised()
    {
        Assert.Equal("#AABBCC", SpecText.NormalizeColor("#abc"));
        Assert.Equal("#AABBCC", SpecText.NormalizeColor("aabbcc"));
        Assert.Equal("#AABBCC", SpecText.NormalizeColor("#AABBCCFF"));
        Assert.Equal("#000000", SpecText.NormalizeColor("Black"));
        Assert.Null(SpecText.NormalizeColor("#GGGGGG"));
        Assert.Null(SpecText.NormalizeColor("#ABCD"));
    }
}
