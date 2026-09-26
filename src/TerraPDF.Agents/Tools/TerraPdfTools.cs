using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using TerraPDF.Agents.Rendering;
using TerraPDF.Agents.Spec;

namespace TerraPDF.Agents.Tools;

/// <summary>Result of the <c>create_pdf</c> tool, serialised back to the model.</summary>
public sealed class CreatePdfResult
{
    public bool Success { get; init; }
    public string? Location { get; init; }
    public string? FileName { get; init; }
    public int PageCount { get; init; }
    public long SizeBytes { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>What the model should do next; present only on failure.</summary>
    public string? Hint { get; init; }
}

/// <summary>
/// The TerraPDF agent tools. Framework-neutral: call the methods directly, turn
/// them into <c>AIFunction</c>s with <see cref="TerraPdfAIFunctions.AsAIFunctions"/>
/// (Microsoft Agent Framework, Semantic Kernel, any <c>IChatClient</c>), or host
/// them in the TerraPDF MCP server.
/// </summary>
public sealed class TerraPdfTools
{
    internal const string CreatePdfName = "create_pdf";
    internal const string GetFormatName = "get_pdf_document_format";

    internal const string CreatePdfDescription =
        "Creates a PDF file from a JSON document description and returns where it was saved, its page count, " +
        "and any warnings. On failure nothing is written and the result lists each error with a JSON path; fix " +
        "those and call again. Call get_pdf_document_format first if unsure of the format.\n\n" +
        "Document shape: {\"title\",\"author\",\"page\":{\"size\":\"A4|Letter|...\",\"orientation\":\"portrait|landscape\"," +
        "\"margin\":50},\"theme\":{\"accentColor\":\"#1A4A8A\",\"fontSize\":10},\"header\":[blocks],\"footer\":[blocks]," +
        "\"pageNumbers\":true,\"tableOfContents\":false,\"content\":[blocks]}.\n" +
        "Block types (each an object with \"type\"): heading{text,level 1-6}; paragraph{text (supports **bold** and " +
        "*italic*) or spans:[{text,bold,italic,color}],align}; list{items:[..],ordered}; table{columns:[{header,width,align}]," +
        "rows:[[..]],footerRows:[[..]]}; keyValue{entries:[{label,value}]}; callout{title,text,color}; " +
        "columns{columns:[{width,content:[blocks]}]}; image{source (data: URI or path),width,align}; " +
        "chart{chartType:bar|line|pie,title,labels:[..],values:[..] or series:[{name,values}],height}; " +
        "barcode{data}; qrCode{data,size}; divider; spacer{height}; pageBreak. Colours are hex strings. " +
        "Built-in fonts cover Western European characters only.";

    internal const string GetFormatDescription =
        "Returns the complete reference for the JSON document format accepted by create_pdf: every block type " +
        "and property, defaults, limits, and full examples (report, invoice). Call this before the first create_pdf.";

    private readonly TerraPdfToolOptions _options;
    private readonly PdfSpecRenderer _renderer;

    public TerraPdfTools(TerraPdfToolOptions? options = null)
    {
        _options = options ?? new TerraPdfToolOptions();
        _renderer = new PdfSpecRenderer(_options.AssetDirectory, _options.MaxAssetBytes, _options.FontFamilies);
    }

    /// <summary>The renderer these tools use, for direct C# access to spec rendering.</summary>
    public PdfSpecRenderer Renderer => _renderer;

    /// <summary>Validates and renders <paramref name="document"/>, then saves it as <paramref name="fileName"/>.</summary>
    [Description(CreatePdfDescription)]
    public async Task<CreatePdfResult> CreatePdfAsync(
        [Description("The document as a JSON object in the format described above.")] JsonElement document,
        [Description("File name for the PDF, e.g. \"q3-report.pdf\". A name only, not a path.")] string fileName,
        CancellationToken cancellationToken = default)
    {
        PdfDocumentSpec? spec = PdfSpecReader.TryRead(document, out SpecIssue? parseError);
        if (spec is null) return Failure([parseError!], []);

        PdfRenderResult result = _renderer.Render(spec);
        if (!result.Success) return Failure(result.Errors, result.Warnings);

        string safeName = SanitizeFileName(fileName, spec.Title);
        string location;
        try
        {
            location = _options.SaveAsync is { } save
                ? await save(safeName, result.Pdf!, cancellationToken).ConfigureAwait(false)
                : await SaveToDirectoryAsync(safeName, result.Pdf!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new CreatePdfResult
            {
                Errors = [$"The PDF rendered but could not be saved: {ex.Message}"],
                Hint = "This is a storage problem, not a document problem. Report it to the user rather than retrying.",
            };
        }

        return new CreatePdfResult
        {
            Success = true,
            Location = location,
            FileName = _options.SaveAsync is null ? Path.GetFileName(location) : safeName,
            PageCount = result.PageCount,
            SizeBytes = result.Pdf!.Length,
            Warnings = result.Warnings.Select(w => w.ToString()).ToList(),
        };
    }

    /// <summary>Returns the document-format reference.</summary>
    [Description(GetFormatDescription)]
    public static string GetPdfDocumentFormat() => s_format.Value;

    private static readonly Lazy<string> s_format = new(() =>
    {
        using Stream stream = typeof(TerraPdfTools).Assembly.GetManifestResourceStream("TerraPDF.Agents.document-format.md")
            ?? throw new InvalidOperationException("The document-format resource is missing from TerraPDF.Agents.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    private async Task<string> SaveToDirectoryAsync(string fileName, byte[] pdf, CancellationToken ct)
    {
        string directory = Path.GetFullPath(_options.OutputDirectory);
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, fileName);
        if (!_options.AllowOverwrite)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(directory, $"{stem}-{i}.pdf");
        }

        await File.WriteAllBytesAsync(path, pdf, ct).ConfigureAwait(false);
        return path;
    }

    /// <summary>Reduces a model-supplied name to a safe single file name ending in .pdf.</summary>
    internal static string SanitizeFileName(string? fileName, string? title)
    {
        // Neutralise drive letters and both separator styles, whatever the host OS.
        string cleaned = Clean(Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/').Replace(':', '-')));
        if (cleaned.Length == 0) cleaned = Clean(title);
        return (cleaned.Length == 0 ? "document" : cleaned) + ".pdf";

        static string Clean(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            char[] invalid = Path.GetInvalidFileNameChars();
            string cleaned = new string(name.Select(c => invalid.Contains(c) || c is '/' or '\\' or ':' ? '-' : c).ToArray())
                .Trim().Trim('.').Trim();
            if (cleaned.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[..^4].TrimEnd();
            return cleaned.Length > 100 ? cleaned[..100] : cleaned;
        }
    }

    private static CreatePdfResult Failure(IReadOnlyList<SpecIssue> errors, IReadOnlyList<SpecIssue> warnings) => new()
    {
        Errors = errors.Select(e => e.ToString()).ToList(),
        Warnings = warnings.Select(w => w.ToString()).ToList(),
        Hint = "No file was written. Fix every error listed (paths are JSON paths into your document) and call create_pdf again.",
    };

    internal static MethodInfo CreatePdfMethod { get; } =
        typeof(TerraPdfTools).GetMethod(nameof(CreatePdfAsync))!;

    internal static MethodInfo GetFormatMethod { get; } =
        typeof(TerraPdfTools).GetMethod(nameof(GetPdfDocumentFormat))!;
}
