using System.Text;
using System.Text.RegularExpressions;
using TerraPDF.Agents.Rendering;
using TerraPDF.Agents.Spec;
using TerraPDF.Agents.Tools;

namespace TerraPDF.Agents.Tests;

public class SpecRenderingTests
{
    private static readonly PdfSpecRenderer s_renderer = new();

    private static PdfRenderResult Render(string json) => s_renderer.Render(json);

    private static PdfRenderResult RenderOk(string json)
    {
        PdfRenderResult result = Render(json);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(result.Pdf!, 0, 5));
        return result;
    }

    public static TheoryData<string> FormatGuideExamples()
    {
        // The guide the model reads must only show documents that render.
        var data = new TheoryData<string>();
        foreach (Match m in Regex.Matches(TerraPdfTools.GetPdfDocumentFormat(), "```json\\s*(.*?)```", RegexOptions.Singleline))
            data.Add(m.Groups[1].Value);
        return data;
    }

    [Theory]
    [MemberData(nameof(FormatGuideExamples))]
    public void FormatGuideExamplesRenderWithoutWarnings(string json)
    {
        PdfRenderResult result = RenderOk(json);
        Assert.Empty(result.Warnings);
        Assert.True(result.PageCount >= 1);
    }

    [Fact]
    public void FormatGuideHasExamples() => Assert.True(FormatGuideExamples().Count >= 2);

    [Fact]
    public void EveryBlockTypeRenders()
    {
        const string png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        string json = $$"""
        {
          "title": "All blocks",
          "header": [ { "type": "paragraph", "text": "Header" } ],
          "footer": [ { "type": "divider" } ],
          "tableOfContents": true,
          "content": [
            { "type": "heading", "level": 1, "text": "Heading" },
            { "type": "paragraph", "spans": [ { "text": "a" }, { "text": "b", "bold": true, "color": "#FF0000" } ] },
            { "type": "paragraph", "text": "line one\nline **two**", "link": "https://terrapdf.com" },
            { "type": "list", "items": ["x", "y"], "ordered": true },
            { "type": "table", "columns": [ { "header": "A" }, { "header": "B", "align": "right" } ],
              "rows": [ ["1", 2], ["3"] ], "footerRows": [ ["Total", "5"] ] },
            { "type": "keyValue", "entries": [ { "label": "Key", "value": "Value" } ] },
            { "type": "callout", "title": "Note", "text": "Body", "color": "teal" },
            { "type": "columns", "columns": [ { "content": [ { "type": "paragraph", "text": "L" } ] },
                                              { "width": 2, "content": [ { "type": "chart", "chartType": "pie", "labels": ["a","b"], "values": [1, 3] } ] } ] },
            { "type": "image", "source": "data:image/png;base64,{{png1x1}}", "width": 20, "align": "center" },
            { "type": "chart", "chartType": "line", "labels": ["a","b","c"], "series": [ { "name": "s1", "values": [1,-2,3] }, { "name": "s2", "values": [2,2,2] } ] },
            { "type": "chart", "chartType": "bar", "labels": ["a","b"], "values": [0, 0] },
            { "type": "barcode", "data": "ABC-123" },
            { "type": "qrCode", "data": "https://terrapdf.com", "errorCorrection": "H" },
            { "type": "spacer", "height": 20 },
            { "type": "pageBreak" },
            { "type": "heading", "level": 2, "text": "Second page" }
          ]
        }
        """;

        PdfRenderResult result = RenderOk(json);
        SpecIssue warning = Assert.Single(result.Warnings);
        Assert.Equal("$.content[4].rows[1]", warning.Path); // the deliberately short row
        Assert.True(result.PageCount >= 3, $"TOC + two content pages expected, got {result.PageCount}");
    }

    [Fact]
    public void LongTablePaginates()
    {
        string rows = string.Join(",", Enumerable.Range(1, 200).Select(i => $"[\"Item {i}\", {i}]"));
        PdfRenderResult result = RenderOk($$"""
            { "content": [ { "type": "table", "columns": [ { "header": "Name" }, { "header": "N" } ], "rows": [{{rows}}] } ] }
            """);
        Assert.True(result.PageCount > 3);
    }

    [Fact]
    public void EncryptedDocumentRendersAndCountsPages()
    {
        PdfRenderResult result = RenderOk("""
            { "encryption": { "userPassword": "open", "ownerPassword": "owner" },
              "content": [ { "type": "paragraph", "text": "secret" } ] }
            """);
        Assert.Contains("/Encrypt", Encoding.Latin1.GetString(result.Pdf!), StringComparison.Ordinal);
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public void LandscapeSwapsDimensions()
    {
        PdfRenderResult result = RenderOk("""{ "page": { "size": "Letter", "orientation": "landscape" }, "content": [ { "type": "paragraph", "text": "x" } ] }""");
        Assert.Contains("/MediaBox [0 0 792", Encoding.Latin1.GetString(result.Pdf!), StringComparison.Ordinal);
    }

    [Fact]
    public void LenientInputIsAccepted()
    {
        // Comments, trailing commas, camel/Pascal casing, aliases, and numeric strings.
        RenderOk("""
            {
              // a comment
              "Content": [
                { "type": "h2", "text": "Alias heading" },
                { "Type": "text", "Text": "Alias paragraph", "fontSize": "12" },
                { "type": "bullets", "items": ["one", "two",], },
              ],
            }
            """);
    }
}
