using System.Text.Json;
using Microsoft.Extensions.AI;
using TerraPDF.Agents.Tools;

namespace TerraPDF.Agents.Tests;

public sealed class ToolTests : IDisposable
{
    private const string Minimal = """{ "title": "Hello", "content": [ { "type": "paragraph", "text": "Hello" } ] }""";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "terrapdf-agent-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private TerraPdfTools Tools(string? assetDirectory = null) => new(new TerraPdfToolOptions
    {
        OutputDirectory = Path.Combine(_dir, "out"),
        AssetDirectory = assetDirectory,
    });

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task CreatePdfWritesIntoOutputDirectory()
    {
        CreatePdfResult result = await Tools().CreatePdfAsync(Json(Minimal), "hello.pdf", TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Equal(Path.Combine(_dir, "out", "hello.pdf"), result.Location);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(new FileInfo(result.Location!).Length, result.SizeBytes);
    }

    [Fact]
    public async Task ExistingFileIsNotOverwritten()
    {
        TerraPdfTools tools = Tools();
        CreatePdfResult first = await tools.CreatePdfAsync(Json(Minimal), "a.pdf", TestContext.Current.CancellationToken);
        CreatePdfResult second = await tools.CreatePdfAsync(Json(Minimal), "a.pdf", TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Location, second.Location);
        Assert.Equal("a-2.pdf", second.FileName);
    }

    [Fact]
    public async Task DocumentPassedAsJsonStringIsAccepted()
    {
        JsonElement asString = JsonSerializer.SerializeToElement(Minimal);
        CreatePdfResult result = await Tools().CreatePdfAsync(asString, "s.pdf", TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join("\n", result.Errors));
    }

    [Fact]
    public async Task InvalidDocumentWritesNothingAndExplainsWhy()
    {
        CreatePdfResult result = await Tools().CreatePdfAsync(Json("""{ "content": [ { "type": "nope" } ] }"""), "x.pdf",
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(result.Hint);
        Assert.StartsWith("$.content[0].type:", result.Errors[0], StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_dir, "out")));
    }

    [Fact]
    public async Task CustomSaveReceivesBytes()
    {
        byte[]? saved = null;
        var tools = new TerraPdfTools(new TerraPdfToolOptions
        {
            SaveAsync = (name, pdf, _) => { saved = pdf; return ValueTask.FromResult("blob://pdfs/" + name); },
        });

        CreatePdfResult result = await tools.CreatePdfAsync(Json(Minimal), "r.pdf", TestContext.Current.CancellationToken);

        Assert.Equal("blob://pdfs/r.pdf", result.Location);
        Assert.Equal(result.SizeBytes, saved!.Length);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd.pdf")]
    [InlineData(@"C:\Windows\evil.pdf", "evil.pdf")]
    [InlineData("report", "report.pdf")]
    [InlineData("Q3 report.PDF", "Q3 report.pdf")]
    [InlineData("", "Title.pdf")]
    [InlineData("...", "Title.pdf")]
    [InlineData("a:b.pdf", "a-b.pdf")]
    public void FileNamesAreSanitised(string input, string expected) =>
        Assert.Equal(expected, TerraPdfTools.SanitizeFileName(input, "Title"));

    [Fact]
    public async Task ImagePathsRequireAnAssetDirectory()
    {
        CreatePdfResult result = await Tools().CreatePdfAsync(
            Json("""{ "content": [ { "type": "image", "source": "logo.png" } ] }"""), "x.pdf", TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, e => e.Contains("disabled", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../secret.png")]
    [InlineData("sub/../../secret.png")]
    public async Task ImagePathsCannotEscapeTheAssetDirectory(string source)
    {
        string assets = Path.Combine(_dir, "assets");
        Directory.CreateDirectory(Path.Combine(assets, "sub"));
        await File.WriteAllBytesAsync(Path.Combine(_dir, "secret.png"), TestPng, TestContext.Current.CancellationToken);

        CreatePdfResult result = await Tools(assets).CreatePdfAsync(
            Json($$"""{ "content": [ { "type": "image", "source": "{{source}}" } ] }"""), "x.pdf", TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, e => e.Contains("outside the asset directory", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImageInsideAssetDirectoryRenders()
    {
        string assets = Path.Combine(_dir, "assets");
        Directory.CreateDirectory(assets);
        await File.WriteAllBytesAsync(Path.Combine(assets, "logo.png"), TestPng, TestContext.Current.CancellationToken);

        CreatePdfResult result = await Tools(assets).CreatePdfAsync(
            Json("""{ "content": [ { "type": "image", "source": "logo.png" } ] }"""), "x.pdf", TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join("\n", result.Errors));
    }

    [Fact]
    public async Task UrlsAreNeverFetched()
    {
        CreatePdfResult result = await Tools().CreatePdfAsync(
            Json("""{ "content": [ { "type": "image", "source": "https://example.com/a.png" } ] }"""), "x.pdf", TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, e => e.Contains("not fetched", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonImageDataIsRejected()
    {
        string notAnImage = Convert.ToBase64String("hello world, not a png"u8.ToArray());
        CreatePdfResult result = await Tools().CreatePdfAsync(
            Json($$"""{ "content": [ { "type": "image", "source": "data:image/png;base64,{{notAnImage}}" } ] }"""), "x.pdf",
            TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, e => e.Contains("PNG and JPEG", StringComparison.Ordinal));
    }

    [Fact]
    public void AIFunctionsHaveStableNamesAndSchema()
    {
        IList<AIFunction> functions = Tools().AsAIFunctions();

        Assert.Equal(["create_pdf", "get_pdf_document_format"], functions.Select(f => f.Name));
        JsonElement properties = functions[0].JsonSchema.GetProperty("properties");
        Assert.Equal("object", properties.GetProperty("document").GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("fileName", out _));
        Assert.False(properties.TryGetProperty("cancellationToken", out _));
    }

    [Fact]
    public async Task AIFunctionInvocationRoundTrips()
    {
        IList<AIFunction> functions = Tools().AsAIFunctions();

        object? format = await functions[1].InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("create_pdf", format?.ToString(), StringComparison.Ordinal);

        object? raw = await functions[0].InvokeAsync(new AIFunctionArguments
        {
            ["document"] = Json(Minimal),
            ["fileName"] = "via-ai.pdf",
        }, TestContext.Current.CancellationToken);

        JsonElement result = Assert.IsType<JsonElement>(raw);
        Assert.True(result.GetProperty("success").GetBoolean(), result.GetRawText());
        Assert.Equal(1, result.GetProperty("pageCount").GetInt32());
    }

    // A valid 1x1 PNG.
    private static readonly byte[] TestPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
}
