using TerraPDF.Agents.Rendering;

namespace TerraPDF.Agents.Tools;

/// <summary>Configuration for <see cref="TerraPdfTools"/>.</summary>
public sealed class TerraPdfToolOptions
{
    /// <summary>
    /// Where <c>create_pdf</c> writes files when <see cref="SaveAsync"/> is not set.
    /// Defaults to <c>%TEMP%/terrapdf</c>. The model only chooses a file name, never a directory.
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "terrapdf");

    /// <summary>
    /// Directory that image and font paths in a document resolve against. Paths
    /// may not escape it. When null (the default), the model can embed assets
    /// only as base64 data, and no local file is ever read.
    /// </summary>
    public string? AssetDirectory { get; set; }

    /// <summary>When false (the default), an existing file is never replaced; a numeric suffix is added instead.</summary>
    public bool AllowOverwrite { get; set; }

    /// <summary>Maximum size of any single image or font, in bytes.</summary>
    public long MaxAssetBytes { get; set; } = PdfSpecRenderer.DefaultMaxAssetBytes;

    /// <summary>Font families the application registered with <c>FontFamily.Register</c> that documents may use by name.</summary>
    public IList<string> FontFamilies { get; } = [];

    /// <summary>
    /// Replaces the default file-system output, e.g. to upload to blob storage or
    /// attach the PDF to a chat message. Receives the sanitised file name and the
    /// PDF bytes, and returns the location reported back to the model (a URL,
    /// blob name, or path).
    /// </summary>
    public Func<string, byte[], CancellationToken, ValueTask<string>>? SaveAsync { get; set; }
}
