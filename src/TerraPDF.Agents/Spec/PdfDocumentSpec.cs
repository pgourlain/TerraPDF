namespace TerraPDF.Agents.Spec;

/// <summary>
/// A declarative, JSON-serialisable description of a PDF document. This is the
/// contract an LLM writes when it calls the <c>create_pdf</c> tool; it is also
/// usable directly from C# via <see cref="Rendering.PdfSpecRenderer"/>.
/// </summary>
/// <remarks>
/// Every property is optional except <see cref="Content"/>. Blocks are a single
/// flat type discriminated by <see cref="BlockSpec.Type"/> rather than a
/// polymorphic hierarchy, so the JSON an LLM produces never depends on property
/// order and the schema stays free of <c>anyOf</c>/<c>$ref</c> constructs that
/// some model providers reject.
/// </remarks>
public sealed class PdfDocumentSpec
{
    /// <summary>Document title (PDF metadata). Also used as the default file title.</summary>
    public string? Title { get; set; }

    /// <summary>Author (PDF metadata).</summary>
    public string? Author { get; set; }

    /// <summary>Subject (PDF metadata).</summary>
    public string? Subject { get; set; }

    /// <summary>Comma-separated keywords (PDF metadata).</summary>
    public string? Keywords { get; set; }

    /// <summary>Page size, orientation, margins, and background.</summary>
    public PageSpec? Page { get; set; }

    /// <summary>Colours and default typography.</summary>
    public ThemeSpec? Theme { get; set; }

    /// <summary>Blocks repeated at the top of every page.</summary>
    public List<BlockSpec>? Header { get; set; }

    /// <summary>Blocks repeated at the bottom of every page.</summary>
    public List<BlockSpec>? Footer { get; set; }

    /// <summary>When true (the default), appends "Page X of Y" to the footer.</summary>
    public bool? PageNumbers { get; set; }

    /// <summary>When true, the header is drawn on the first page only.</summary>
    public bool? HeaderOnFirstPageOnly { get; set; }

    /// <summary>When true, inserts a table-of-contents page built from heading blocks.</summary>
    public bool? TableOfContents { get; set; }

    /// <summary>TrueType fonts to register, for text outside Western European (WinAnsi) characters.</summary>
    public List<FontSpec>? Fonts { get; set; }

    /// <summary>Password protection and permissions.</summary>
    public EncryptionSpec? Encryption { get; set; }

    /// <summary>The document body, rendered top to bottom and paginated automatically.</summary>
    public List<BlockSpec>? Content { get; set; }
}

/// <summary>Page geometry.</summary>
public sealed class PageSpec
{
    /// <summary>A0-A6, Letter, Legal, Tabloid, or Executive. Defaults to A4.</summary>
    public string? Size { get; set; }

    /// <summary>"portrait" (default) or "landscape".</summary>
    public string? Orientation { get; set; }

    /// <summary>Custom page width in points; overrides <see cref="Size"/> when set with <see cref="Height"/>.</summary>
    public double? Width { get; set; }

    /// <summary>Custom page height in points.</summary>
    public double? Height { get; set; }

    /// <summary>Margin on all four sides in points (72 pt = 1 inch, ~28.35 pt = 1 cm). Defaults to 50.</summary>
    public double? Margin { get; set; }

    /// <summary>Page background colour as hex, e.g. "#FFFFFF".</summary>
    public string? BackgroundColor { get; set; }
}

/// <summary>Document-wide colours and typography.</summary>
public sealed class ThemeSpec
{
    /// <summary>Colour for headings, table headers, dividers, and charts. Defaults to "#1A4A8A".</summary>
    public string? AccentColor { get; set; }

    /// <summary>Body text colour. Defaults to "#212121".</summary>
    public string? TextColor { get; set; }

    /// <summary>Secondary text colour (captions, labels). Defaults to "#6C757D".</summary>
    public string? MutedColor { get; set; }

    /// <summary>Helvetica (default), Times, Courier, or a family registered in <see cref="PdfDocumentSpec.Fonts"/>.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Body font size in points. Defaults to 10.</summary>
    public double? FontSize { get; set; }
}

/// <summary>A TrueType font family. Each face is a file path (under the asset directory) or base64 data.</summary>
public sealed class FontSpec
{
    public string? Family { get; set; }
    public string? Regular { get; set; }
    public string? Bold { get; set; }
    public string? Italic { get; set; }
    public string? BoldItalic { get; set; }
}

/// <summary>AES-256 password protection.</summary>
public sealed class EncryptionSpec
{
    /// <summary>Password required to open the document. Empty means it opens without a prompt.</summary>
    public string? UserPassword { get; set; }

    /// <summary>Password that grants full access.</summary>
    public string? OwnerPassword { get; set; }

    public bool? AllowPrinting { get; set; }
    public bool? AllowCopying { get; set; }
    public bool? AllowEditing { get; set; }
}

/// <summary>
/// One content block. <see cref="Type"/> selects the kind; only the properties
/// relevant to that kind are read. See <c>get_pdf_document_format</c> for the
/// per-type property list.
/// </summary>
public sealed class BlockSpec
{
    /// <summary>
    /// heading, paragraph, list, table, keyValue, callout, columns, image,
    /// chart, barcode, qrCode, divider, spacer, pageBreak.
    /// </summary>
    public string? Type { get; set; }

    // ── Text ────────────────────────────────────────────────────────────
    public string? Text { get; set; }
    public int? Level { get; set; }
    public List<SpanSpec>? Spans { get; set; }
    public string? Align { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public string? Link { get; set; }
    public string? Title { get; set; }

    // ── List ────────────────────────────────────────────────────────────
    public List<string>? Items { get; set; }
    public bool? Ordered { get; set; }

    // ── Table / columns ─────────────────────────────────────────────────
    public List<ColumnSpec>? Columns { get; set; }
    public List<List<string?>>? Rows { get; set; }
    public List<List<string?>>? FooterRows { get; set; }
    public bool? Striped { get; set; }
    public double? Spacing { get; set; }

    // ── Key/value ───────────────────────────────────────────────────────
    public List<KeyValueSpec>? Entries { get; set; }

    // ── Image ───────────────────────────────────────────────────────────
    public string? Source { get; set; }
    public double? Width { get; set; }

    // ── Chart ───────────────────────────────────────────────────────────
    public string? ChartType { get; set; }
    public List<string>? Labels { get; set; }
    public List<double>? Values { get; set; }
    public List<SeriesSpec>? Series { get; set; }

    // ── Barcode / QR / spacer / divider ─────────────────────────────────
    public string? Data { get; set; }
    public double? Height { get; set; }
    public double? Size { get; set; }
    public bool? ShowCaption { get; set; }
    public string? ErrorCorrection { get; set; }
    public double? Thickness { get; set; }
}

/// <summary>A run of text with its own style inside a paragraph.</summary>
public sealed class SpanSpec
{
    public string? Text { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public bool? Strikethrough { get; set; }
    public string? Color { get; set; }
    public double? FontSize { get; set; }
}

/// <summary>A table column, or one side of a <c>columns</c> layout block.</summary>
public sealed class ColumnSpec
{
    /// <summary>Table header text.</summary>
    public string? Header { get; set; }

    /// <summary>Relative width weight. Defaults to 1.</summary>
    public double? Width { get; set; }

    /// <summary>left, center, or right.</summary>
    public string? Align { get; set; }

    /// <summary>Nested blocks (columns layout only).</summary>
    public List<BlockSpec>? Content { get; set; }
}

/// <summary>One label/value pair in a <c>keyValue</c> block.</summary>
public sealed class KeyValueSpec
{
    public string? Label { get; set; }
    public string? Value { get; set; }
}

/// <summary>One data series in a multi-series bar or line chart.</summary>
public sealed class SeriesSpec
{
    public string? Name { get; set; }
    public List<double>? Values { get; set; }
    public string? Color { get; set; }
}
