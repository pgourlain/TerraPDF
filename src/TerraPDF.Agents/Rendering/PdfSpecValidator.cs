using TerraPDF.Agents.Spec;

namespace TerraPDF.Agents.Rendering;

/// <summary>
/// Checks a <see cref="PdfDocumentSpec"/> before rendering and reports every
/// problem at once, each with a JSON path. Errors block rendering; warnings
/// describe output that will render but probably not as the model intended.
/// </summary>
internal sealed class PdfSpecValidator
{
    internal const int MaxDepth = 6;
    internal const int MaxBlocks = 10_000;
    internal const int MaxChartPoints = 500;

    internal static readonly string[] BlockTypes =
    [
        "heading", "paragraph", "list", "table", "keyValue", "callout", "columns",
        "image", "chart", "barcode", "qrCode", "divider", "spacer", "pageBreak",
    ];

    internal static readonly string[] PageSizes =
        ["A0", "A1", "A2", "A3", "A4", "A5", "A6", "Letter", "Legal", "Tabloid", "Executive"];

    private readonly AssetResolver _assets;
    private readonly List<SpecIssue> _errors = [];
    private readonly List<SpecIssue> _warnings = [];
    private readonly HashSet<string> _customFamilies = new(StringComparer.OrdinalIgnoreCase);
    private int _blockCount;
    private bool _unicodeWarned;

    internal PdfSpecValidator(AssetResolver assets) => _assets = assets;

    /// <summary>Families the host registered itself; specs may name them without a "fonts" entry.</summary>
    internal void AddKnownFamilies(IEnumerable<string> families) => _customFamilies.UnionWith(families);

    internal IReadOnlyList<SpecIssue> Errors => _errors;
    internal IReadOnlyList<SpecIssue> Warnings => _warnings;

    internal void Validate(PdfDocumentSpec spec)
    {
        ValidateFonts(spec.Fonts);
        ValidatePage(spec.Page);
        ValidateTheme(spec.Theme);
        ValidateEncryption(spec.Encryption);

        if (spec.Content is null || spec.Content.Count == 0)
            Error("$.content", "The document has no content. Add at least one block to \"content\".");
        else
            ValidateBlocks(spec.Content, "$.content", depth: 0, inHeaderOrFooter: false);

        if (spec.Header is not null) ValidateBlocks(spec.Header, "$.header", 0, inHeaderOrFooter: true);
        if (spec.Footer is not null) ValidateBlocks(spec.Footer, "$.footer", 0, inHeaderOrFooter: true);

        if (spec.TableOfContents == true && spec.Content?.Any(b => Is(b, "heading")) != true)
            Warn("$.tableOfContents", "A table of contents is collected from heading blocks, but the content has none.");
    }

    // ── Document-level ──────────────────────────────────────────────────

    private void ValidateFonts(List<FontSpec>? fonts)
    {
        if (fonts is null) return;
        for (int i = 0; i < fonts.Count; i++)
        {
            string path = $"$.fonts[{i}]";
            FontSpec f = fonts[i];
            if (string.IsNullOrWhiteSpace(f.Family)) { Error(path + ".family", "A font needs a family name."); continue; }
            if (SpecText.BuiltInFamily(f.Family) is not null)
                Error(path + ".family", $"'{f.Family}' is a built-in family name; choose a different name for a registered font.");
            if (string.IsNullOrWhiteSpace(f.Regular))
                Error(path + ".regular", "A font needs a regular face.");

            CheckAsset(f.Regular, AssetKind.Font, path + ".regular");
            CheckAsset(f.Bold, AssetKind.Font, path + ".bold");
            CheckAsset(f.Italic, AssetKind.Font, path + ".italic");
            CheckAsset(f.BoldItalic, AssetKind.Font, path + ".boldItalic");
            _customFamilies.Add(f.Family);
        }
    }

    private void ValidatePage(PageSpec? page)
    {
        if (page is null) return;
        if (page.Size is not null && !PageSizes.Contains(page.Size, StringComparer.OrdinalIgnoreCase))
            Error("$.page.size", $"Unknown page size '{page.Size}'. Use one of: {string.Join(", ", PageSizes)}.");
        if (page.Orientation is not null && !OneOf(page.Orientation, "portrait", "landscape"))
            Error("$.page.orientation", "Orientation must be \"portrait\" or \"landscape\".");
        if ((page.Width is null) != (page.Height is null))
            Error("$.page", "Set both width and height for a custom page size, or neither.");
        if (page.Width is <= 72 or > 14400) Error("$.page.width", "Width must be between 72 and 14400 points.");
        if (page.Height is <= 72 or > 14400) Error("$.page.height", "Height must be between 72 and 14400 points.");
        if (page.Margin is < 0 or > 200) Error("$.page.margin", "Margin must be between 0 and 200 points.");
        CheckColor(page.BackgroundColor, "$.page.backgroundColor");
    }

    private void ValidateTheme(ThemeSpec? theme)
    {
        if (theme is null) return;
        CheckColor(theme.AccentColor, "$.theme.accentColor");
        CheckColor(theme.TextColor, "$.theme.textColor");
        CheckColor(theme.MutedColor, "$.theme.mutedColor");
        CheckFontSize(theme.FontSize, "$.theme.fontSize");
        CheckFamily(theme.FontFamily, "$.theme.fontFamily");
    }

    private void ValidateEncryption(EncryptionSpec? enc)
    {
        if (enc is null) return;
        if (string.IsNullOrEmpty(enc.UserPassword) && string.IsNullOrEmpty(enc.OwnerPassword))
            Error("$.encryption", "Encryption needs a userPassword, an ownerPassword, or both.");
    }

    // ── Blocks ──────────────────────────────────────────────────────────

    private void ValidateBlocks(List<BlockSpec> blocks, string path, int depth, bool inHeaderOrFooter)
    {
        if (depth > MaxDepth)
        {
            Error(path, $"Blocks are nested more than {MaxDepth} levels deep.");
            return;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            if (++_blockCount > MaxBlocks)
            {
                Error(path, $"The document has more than {MaxBlocks} blocks.");
                return;
            }
            ValidateBlock(blocks[i], $"{path}[{i}]", depth, inHeaderOrFooter);
        }
    }

    private void ValidateBlock(BlockSpec? b, string path, int depth, bool inHeaderOrFooter)
    {
        if (b is null) { Error(path, "Block is null."); return; }
        string? type = CanonicalType(b.Type);
        if (type is null)
        {
            Error(path + ".type", b.Type is null
                ? $"Every block needs a \"type\". Use one of: {string.Join(", ", BlockTypes)}."
                : $"Unknown block type '{b.Type}'. Use one of: {string.Join(", ", BlockTypes)}.");
            return;
        }
        if (type == "heading" && b.Level is null && b.Type!.Length == 2 && b.Type[0] is 'h' or 'H' && char.IsDigit(b.Type[1]))
            b.Level = b.Type[1] - '0';
        b.Type = type;

        CheckColor(b.Color, path + ".color");
        CheckFontSize(b.FontSize, path + ".fontSize");
        if (b.Align is not null && !OneOf(b.Align, "left", "center", "right", "justify"))
            Error(path + ".align", "Align must be left, center, right, or justify.");
        if (b.Link is not null && !IsSafeLink(b.Link))
            Error(path + ".link", "Links must be absolute http, https, or mailto URLs.");

        if (inHeaderOrFooter && type is "heading" or "pageBreak")
            Error(path + ".type", $"A {type} block cannot be used in the header or footer. Use a bold paragraph instead.");

        switch (type)
        {
            case "heading":
                RequireText(b.Text, path + ".text");
                if (b.Level is < 1 or > 6) Error(path + ".level", "Heading level must be 1 to 6.");
                break;

            case "paragraph":
                if (b.Spans is { Count: > 0 })
                {
                    for (int i = 0; i < b.Spans.Count; i++)
                    {
                        CheckColor(b.Spans[i].Color, $"{path}.spans[{i}].color");
                        CheckFontSize(b.Spans[i].FontSize, $"{path}.spans[{i}].fontSize");
                        CheckText(b.Spans[i].Text, $"{path}.spans[{i}].text");
                    }
                }
                else RequireText(b.Text, path + ".text");
                break;

            case "callout":
                RequireText(b.Text, path + ".text");
                CheckText(b.Title, path + ".title");
                break;

            case "list":
                if (b.Items is not { Count: > 0 }) Error(path + ".items", "A list needs at least one item.");
                else for (int i = 0; i < b.Items.Count; i++) CheckText(b.Items[i], $"{path}.items[{i}]");
                break;

            case "table": ValidateTable(b, path); break;

            case "keyValue":
                if (b.Entries is not { Count: > 0 }) Error(path + ".entries", "A keyValue block needs at least one entry.");
                else for (int i = 0; i < b.Entries.Count; i++)
                {
                    CheckText(b.Entries[i].Label, $"{path}.entries[{i}].label");
                    CheckText(b.Entries[i].Value, $"{path}.entries[{i}].value");
                }
                break;

            case "columns":
                if (b.Columns is not { Count: > 0 }) { Error(path + ".columns", "A columns block needs at least one column."); break; }
                if (b.Columns.Count > 6) Error(path + ".columns", "A columns block supports at most 6 columns.");
                for (int i = 0; i < b.Columns.Count; i++)
                {
                    ColumnSpec c = b.Columns[i];
                    if (c.Width is <= 0) Error($"{path}.columns[{i}].width", "Column width is a relative weight and must be positive.");
                    if (c.Content is not { Count: > 0 }) Error($"{path}.columns[{i}].content", "Each column needs a \"content\" array of blocks.");
                    else ValidateBlocks(c.Content, $"{path}.columns[{i}].content", depth + 1, inHeaderOrFooter);
                }
                break;

            case "image":
                if (string.IsNullOrWhiteSpace(b.Source)) Error(path + ".source", "An image needs a source (file path or base64 data URI).");
                else CheckAsset(b.Source, AssetKind.Image, path + ".source");
                if (b.Width is <= 0) Error(path + ".width", "Width must be positive (points).");
                break;

            case "chart": ValidateChart(b, path); break;

            case "barcode":
                if (string.IsNullOrWhiteSpace(b.Data)) Error(path + ".data", "A barcode needs data.");
                else if (b.Data.Any(c => c is < ' ' or > '~'))
                    Error(path + ".data", "Code128 barcodes encode printable ASCII only. Use a qrCode block for other text.");
                if (b.Height is <= 0) Error(path + ".height", "Height must be positive.");
                if (b.Width is <= 0) Error(path + ".width", "Width must be positive.");
                break;

            case "qrCode":
                if (string.IsNullOrWhiteSpace(b.Data)) Error(path + ".data", "A QR code needs data.");
                else if (b.Data.Length > 2900) Error(path + ".data", "QR code data is too long (maximum ~2900 characters).");
                if (b.ErrorCorrection is not null && !OneOf(b.ErrorCorrection, "L", "M", "Q", "H"))
                    Error(path + ".errorCorrection", "errorCorrection must be L, M, Q, or H.");
                if (b.Size is <= 0) Error(path + ".size", "Size must be positive.");
                break;

            case "divider":
                if (b.Thickness is <= 0 or > 20) Error(path + ".thickness", "Thickness must be between 0 and 20 points.");
                break;

            case "spacer":
                if (b.Height is null or <= 0 or > 1000) Error(path + ".height", "A spacer needs a height between 1 and 1000 points.");
                break;
        }
    }

    private void ValidateTable(BlockSpec b, string path)
    {
        if (b.Columns is not { Count: > 0 })
        {
            Error(path + ".columns", "A table needs a \"columns\" array, e.g. [{\"header\":\"Item\",\"width\":3},{\"header\":\"Qty\",\"align\":\"right\"}].");
            return;
        }

        for (int i = 0; i < b.Columns.Count; i++)
        {
            ColumnSpec c = b.Columns[i];
            if (c.Width is <= 0) Error($"{path}.columns[{i}].width", "Column width is a relative weight and must be positive.");
            if (c.Align is not null && !OneOf(c.Align, "left", "center", "right"))
                Error($"{path}.columns[{i}].align", "Column align must be left, center, or right.");
            if (c.Content is not null) Error($"{path}.columns[{i}].content", "Table columns do not take content; put cell values in \"rows\".");
            CheckText(c.Header, $"{path}.columns[{i}].header");
        }

        CheckRows(b.Rows, b.Columns.Count, path + ".rows");
        CheckRows(b.FooterRows, b.Columns.Count, path + ".footerRows");
        if (b.Rows is null or { Count: 0 }) Warn(path + ".rows", "The table has no rows; only the header will render.");
    }

    private void CheckRows(List<List<string?>>? rows, int columnCount, string path)
    {
        if (rows is null) return;
        for (int r = 0; r < rows.Count; r++)
        {
            if (rows[r] is null) { Error($"{path}[{r}]", "Row is null."); continue; }
            if (rows[r].Count > columnCount)
                Error($"{path}[{r}]", $"Row has {rows[r].Count} cells but the table defines {columnCount} columns.");
            else if (rows[r].Count < columnCount)
                Warn($"{path}[{r}]", $"Row has {rows[r].Count} of {columnCount} cells; the rest are left empty.");
            for (int c = 0; c < rows[r].Count; c++) CheckText(rows[r][c], $"{path}[{r}][{c}]");
        }
    }

    private void ValidateChart(BlockSpec b, string path)
    {
        string? kind = b.ChartType?.ToLowerInvariant();
        if (kind is not ("bar" or "line" or "pie"))
        {
            Error(path + ".chartType", "chartType must be bar, line, or pie.");
            return;
        }

        if (b.Labels is not { Count: > 0 }) { Error(path + ".labels", "A chart needs labels."); return; }
        if (b.Labels.Count > MaxChartPoints) Error(path + ".labels", $"A chart supports at most {MaxChartPoints} points.");
        for (int i = 0; i < b.Labels.Count; i++) CheckText(b.Labels[i], $"{path}.labels[{i}]");
        if (b.Height is < 80 or > 700) Error(path + ".height", "Chart height must be between 80 and 700 points.");

        bool hasValues = b.Values is { Count: > 0 };
        bool hasSeries = b.Series is { Count: > 0 };
        if (hasValues == hasSeries)
        {
            Error(path, "A chart needs exactly one of \"values\" (single series) or \"series\" (multi-series).");
            return;
        }

        if (kind == "pie")
        {
            if (hasSeries) { Error(path + ".series", "A pie chart takes \"values\", not \"series\"."); return; }
            if (b.Values!.Any(v => v < 0 || !double.IsFinite(v))) Error(path + ".values", "Pie chart values must be zero or positive.");
            else if (b.Values!.Sum() <= 0) Error(path + ".values", "Pie chart values must add up to more than zero.");
        }

        if (hasValues)
        {
            CheckSeriesLength(b.Values!, b.Labels.Count, path + ".values");
            return;
        }

        if (b.Series!.Count > 10) Error(path + ".series", "A chart supports at most 10 series.");
        for (int i = 0; i < b.Series!.Count; i++)
        {
            SeriesSpec s = b.Series[i];
            CheckColor(s.Color, $"{path}.series[{i}].color");
            CheckText(s.Name, $"{path}.series[{i}].name");
            if (s.Values is null) Error($"{path}.series[{i}].values", "Each series needs values.");
            else CheckSeriesLength(s.Values, b.Labels.Count, $"{path}.series[{i}].values");
        }
    }

    private void CheckSeriesLength(List<double> values, int labelCount, string path)
    {
        if (values.Count != labelCount)
            Error(path, $"Has {values.Count} values but there are {labelCount} labels; they must match.");
        if (values.Any(v => !double.IsFinite(v)))
            Error(path, "Values must be finite numbers.");
    }

    // ── Leaf checks ─────────────────────────────────────────────────────

    private void CheckAsset(string? source, AssetKind kind, string path)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        if (!_assets.TryLoad(source, kind, out _, out string? error)) Error(path, error!);
    }

    private void CheckColor(string? value, string path)
    {
        if (value is not null && SpecText.NormalizeColor(value) is null)
            Error(path, $"'{value}' is not a colour. Use hex such as \"#1A4A8A\".");
    }

    private void CheckFontSize(double? size, string path)
    {
        if (size is < 4 or > 200) Error(path, "Font size must be between 4 and 200 points.");
    }

    private void CheckFamily(string? family, string path)
    {
        if (family is null || SpecText.BuiltInFamily(family) is not null || _customFamilies.Contains(family)) return;
        Error(path, $"Font family '{family}' is neither built in (Helvetica, Times, Courier) nor registered in \"fonts\".");
    }

    private void RequireText(string? text, string path)
    {
        if (string.IsNullOrWhiteSpace(text)) Error(path, "Text is required.");
        else CheckText(text, path);
    }

    private void CheckText(string? text, string path)
    {
        if (_unicodeWarned || _customFamilies.Count > 0 || string.IsNullOrEmpty(text)) return;
        if (SpecText.IsWinAnsi(text, out char c)) return;

        _unicodeWarned = true;
        Warn(path, $"Character '{c}' (U+{(int)c:X4}) is outside the built-in fonts' Western European character set " +
                   "and will render as '?'. Register a TrueType font covering it in \"fonts\" and set theme.fontFamily, " +
                   "or replace the character (e.g. '->' for an arrow).");
    }

    private static bool IsSafeLink(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto);

    internal static string? CanonicalType(string? type)
    {
        if (type is null) return null;
        string key = type.Replace("_", string.Empty, StringComparison.Ordinal)
                         .Replace("-", string.Empty, StringComparison.Ordinal);
        string? match = BlockTypes.FirstOrDefault(t => t.Equals(key, StringComparison.OrdinalIgnoreCase));
        return match ?? key.ToLowerInvariant() switch
        {
            "text" => "paragraph",
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "title" => "heading",
            "bullets" or "bulletlist" => "list",
            "keyvalues" or "definitionlist" => "keyValue",
            "note" or "alert" => "callout",
            "qr" => "qrCode",
            "hr" or "rule" or "line" => "divider",
            "pagebreak" or "break" => "pageBreak",
            _ => null,
        };
    }

    private static bool Is(BlockSpec b, string type) => CanonicalType(b.Type) == type;

    private static bool OneOf(string value, params string[] options) =>
        options.Contains(value, StringComparer.OrdinalIgnoreCase);

    private void Error(string path, string message) => _errors.Add(new SpecIssue(path, message));
    private void Warn(string path, string message) => _warnings.Add(new SpecIssue(path, message));
}
