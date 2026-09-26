namespace TerraPDF.Benchmarks.Scenarios;

/// <summary>Loads the fonts and images copied next to the benchmark binary.</summary>
internal static class Assets
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Assets");

    internal static string LatoRegularPath => Path.Combine(Root, "Fonts", "Lato-Regular.ttf");
    internal static string LatoBoldPath => Path.Combine(Root, "Fonts", "Lato-Bold.ttf");
    internal static string DevanagariPath => Path.Combine(Root, "Fonts", "NotoSansDevanagari-Regular.ttf");

    internal static byte[] LatoRegular() => File.ReadAllBytes(LatoRegularPath);
    internal static byte[] Devanagari() => File.ReadAllBytes(DevanagariPath);
    internal static byte[] HeaderLogoPng() => File.ReadAllBytes(Path.Combine(Root, "header_logo.png"));
    internal static byte[] SmallLogoJpg() => File.ReadAllBytes(Path.Combine(Root, "small_logo.jpg"));
    internal static byte[] AlphaBadgePng() => File.ReadAllBytes(Path.Combine(Root, "alpha_badge.png"));

    private static int _fontsRegistered;

    /// <summary>
    /// Registers the custom font families once per process. Registration is
    /// process-wide in TerraPDF, so repeated calls from several benchmark classes are skipped.
    /// </summary>
    internal static void RegisterFonts()
    {
        if (Interlocked.Exchange(ref _fontsRegistered, 1) == 1) return;
        TerraPDF.Helpers.FontFamily.Register("Lato", LatoRegularPath);
        TerraPDF.Helpers.FontFamily.Register("Lato", LatoBoldPath, bold: true);
        TerraPDF.Helpers.FontFamily.Register("Devanagari", DevanagariPath);
    }
}
