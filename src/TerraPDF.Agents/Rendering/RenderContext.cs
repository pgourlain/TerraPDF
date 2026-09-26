using TerraPDF.Agents.Spec;

namespace TerraPDF.Agents.Rendering;

/// <summary>Resolved page geometry, theme, and assets for one render. Built after validation succeeds.</summary>
internal sealed class RenderContext
{
    private readonly Dictionary<string, string> _fontMap;

    internal RenderContext(PdfDocumentSpec spec, AssetResolver assets, Dictionary<string, string> fontMap)
    {
        Assets = assets;
        _fontMap = fontMap;

        PageSpec page = spec.Page ?? new PageSpec();
        (double w, double h) = page.Width is { } pw && page.Height is { } ph ? (pw, ph) : NamedSize(page.Size);
        if (string.Equals(page.Orientation, "landscape", StringComparison.OrdinalIgnoreCase) && h > w) (w, h) = (h, w);
        if (string.Equals(page.Orientation, "portrait", StringComparison.OrdinalIgnoreCase) && w > h) (w, h) = (h, w);
        PageSize = (w, h);
        Margin = page.Margin ?? 50;
        PageColor = SpecText.NormalizeColor(page.BackgroundColor);
        ContentWidth = w - (2 * Margin);

        ThemeSpec theme = spec.Theme ?? new ThemeSpec();
        Accent = SpecText.NormalizeColor(theme.AccentColor) ?? "#1A4A8A";
        TextColor = SpecText.NormalizeColor(theme.TextColor) ?? "#212121";
        Muted = SpecText.NormalizeColor(theme.MutedColor) ?? "#6C757D";
        AccentTint = SpecText.Tint(Accent, 0.88);
        FontSize = theme.FontSize ?? 10;
        FontFamily = ResolveFamily(theme.FontFamily);
    }

    internal AssetResolver Assets { get; }
    internal (double Width, double Height) PageSize { get; }
    internal double Margin { get; }
    internal double ContentWidth { get; }
    internal string? PageColor { get; }
    internal string Accent { get; }
    internal string AccentTint { get; }
    internal string TextColor { get; }
    internal string Muted { get; }
    internal double FontSize { get; }
    internal string FontFamily { get; }

    private string ResolveFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Helvetica";
        if (_fontMap.TryGetValue(name, out string? internalName)) return internalName;
        return SpecText.BuiltInFamily(name) ?? name.Trim();
    }

    private static (double, double) NamedSize(string? size) => size?.ToUpperInvariant() switch
    {
        "A0" => Helpers.PageSize.A0,
        "A1" => Helpers.PageSize.A1,
        "A2" => Helpers.PageSize.A2,
        "A3" => Helpers.PageSize.A3,
        "A5" => Helpers.PageSize.A5,
        "A6" => Helpers.PageSize.A6,
        "LETTER" => Helpers.PageSize.Letter,
        "LEGAL" => Helpers.PageSize.Legal,
        "TABLOID" => Helpers.PageSize.Tabloid,
        "EXECUTIVE" => Helpers.PageSize.Executive,
        _ => Helpers.PageSize.A4,
    };
}
