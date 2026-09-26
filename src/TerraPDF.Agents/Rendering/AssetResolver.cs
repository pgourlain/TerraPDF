namespace TerraPDF.Agents.Rendering;

internal enum AssetKind { Image, Font }

/// <summary>
/// Loads image and font bytes named in a spec. A source is a <c>data:</c> URI,
/// raw base64, or a file path. File paths resolve against a single asset
/// directory and may not escape it, because the path comes from model output
/// and must not be able to read arbitrary files. URLs are never fetched.
/// </summary>
internal sealed class AssetResolver
{
    private static readonly string[] s_imageExtensions = [".png", ".jpg", ".jpeg"];
    private static readonly string[] s_fontExtensions = [".ttf", ".otf"];

    private readonly string? _root;
    private readonly long _maxBytes;
    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);

    internal AssetResolver(string? assetDirectory, long maxBytes)
    {
        _root = string.IsNullOrWhiteSpace(assetDirectory)
            ? null
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(assetDirectory));
        _maxBytes = maxBytes;
    }

    internal bool TryLoad(string source, AssetKind kind, out byte[] data, out string? error)
    {
        if (_cache.TryGetValue(source, out byte[]? cached))
        {
            data = cached;
            error = null;
            return true;
        }

        data = [];
        byte[]? bytes = Load(source.Trim(), kind, out error);
        if (bytes is null) return false;

        error = Sniff(bytes, kind);
        if (error is not null) return false;

        _cache[source] = data = bytes;
        return true;
    }

    private byte[]? Load(string source, AssetKind kind, out string? error)
    {
        error = null;

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            error = "URLs are not fetched. Pass a data: URI, base64 data, or a file path under the asset directory.";
            return null;
        }

        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            int comma = source.IndexOf(',');
            if (comma < 0 || !source[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only base64 data URIs are supported, e.g. \"data:image/png;base64,iVBORw0...\".";
                return null;
            }
            return DecodeBase64(source[(comma + 1)..], out error);
        }

        string[] extensions = kind == AssetKind.Image ? s_imageExtensions : s_fontExtensions;
        bool looksLikePath = extensions.Any(e => source.EndsWith(e, StringComparison.OrdinalIgnoreCase))
            || source.Contains('/') || source.Contains('\\');

        if (!looksLikePath) return DecodeBase64(source, out error);

        if (_root is null)
        {
            error = "File paths are disabled for this tool (no asset directory is configured). Pass the " +
                    (kind == AssetKind.Image ? "image" : "font") + " as a base64 data URI instead.";
            return null;
        }

        string full = Path.GetFullPath(Path.IsPathRooted(source) ? source : Path.Combine(_root, source));
        if (!IsUnderRoot(full))
        {
            error = $"The path is outside the asset directory. Use a path relative to the asset directory.";
            return null;
        }

        if (!extensions.Any(e => full.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            error = $"Unsupported file type. Expected {string.Join(", ", extensions)}.";
            return null;
        }

        var info = new FileInfo(full);
        if (!info.Exists)
        {
            error = $"File not found: '{source}' (relative to the asset directory).";
            return null;
        }
        if (info.Length > _maxBytes)
        {
            error = $"File is {info.Length:N0} bytes; the limit is {_maxBytes:N0}.";
            return null;
        }

        return File.ReadAllBytes(full);
    }

    private bool IsUnderRoot(string fullPath)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(_root + Path.DirectorySeparatorChar, comparison);
    }

    private byte[]? DecodeBase64(string base64, out string? error)
    {
        error = null;
        if (base64.Length / 4 * 3 > _maxBytes)
        {
            error = $"Embedded data exceeds the {_maxBytes:N0}-byte limit.";
            return null;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            if (bytes.Length == 0) error = "Embedded data is empty.";
            return bytes.Length == 0 ? null : bytes;
        }
        catch (FormatException)
        {
            error = "Not a file path with a supported extension, and not valid base64 data.";
            return null;
        }
    }

    private static string? Sniff(byte[] b, AssetKind kind)
    {
        if (kind == AssetKind.Image)
        {
            bool png = b.Length > 8 && b[0] == 0x89 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G';
            bool jpeg = b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8;
            return png || jpeg ? null : "Unsupported image format. Only PNG and JPEG are supported.";
        }

        if (b.Length < 12) return "Font data is too short to be a TrueType font.";
        uint tag = (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
        return tag switch
        {
            0x00010000u or 0x74727565u => null,
            0x4F54544Fu => "CFF-flavoured OpenType fonts (OTTO) are not supported. Use a TrueType-outline .ttf font.",
            0x74746366u => "TrueType Collections (.ttc) are not supported. Use a single .ttf font.",
            _ => "Not a TrueType font.",
        };
    }
}
