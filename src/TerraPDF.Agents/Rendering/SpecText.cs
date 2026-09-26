using System.Globalization;
using System.Text;

namespace TerraPDF.Agents.Rendering;

/// <summary>Colour, font-name, and inline-markup helpers shared by the validator and renderer.</summary>
internal static class SpecText
{
    // A few CSS names models reach for when they forget to write hex.
    private static readonly Dictionary<string, string> s_namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["white"] = "#FFFFFF", ["gray"] = "#9E9E9E", ["grey"] = "#9E9E9E",
        ["red"] = "#E53935", ["green"] = "#43A047", ["blue"] = "#1E88E5", ["navy"] = "#1A237E",
        ["orange"] = "#FB8C00", ["yellow"] = "#FDD835", ["purple"] = "#8E24AA", ["teal"] = "#00897B",
    };

    private static readonly Dictionary<string, string> s_fontAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Helvetica"] = "Helvetica", ["Arial"] = "Helvetica", ["Sans"] = "Helvetica", ["sans-serif"] = "Helvetica",
        ["Times"] = "Times", ["Times New Roman"] = "Times", ["Serif"] = "Times",
        ["Courier"] = "Courier", ["Courier New"] = "Courier", ["Monospace"] = "Courier",
    };

    /// <summary>Normalises #RGB, RRGGBB, #RRGGBBAA, and a few CSS names to #RRGGBB. Returns null when unrecognised.</summary>
    internal static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string v = value.Trim();
        if (s_namedColors.TryGetValue(v, out string? named)) return named;

        string hex = v.TrimStart('#');
        if (!hex.All(Uri.IsHexDigit)) return null;
        return hex.Length switch
        {
            3 => "#" + string.Concat(hex.Select(c => new string(c, 2))).ToUpperInvariant(),
            6 => "#" + hex.ToUpperInvariant(),
            8 => "#" + hex[..6].ToUpperInvariant(),
            _ => null,
        };
    }

    /// <summary>Mixes a colour with white; <paramref name="amount"/> 0 = unchanged, 1 = white.</summary>
    internal static string Tint(string hex, double amount)
    {
        int Channel(int offset)
        {
            int c = int.Parse(hex.AsSpan(1 + offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (int)Math.Round(c + ((255 - c) * amount));
        }
        return string.Create(CultureInfo.InvariantCulture, $"#{Channel(0):X2}{Channel(2):X2}{Channel(4):X2}");
    }

    /// <summary>Maps a built-in family name or alias to Helvetica, Times, or Courier; null when it is not built in.</summary>
    internal static string? BuiltInFamily(string name) =>
        s_fontAliases.TryGetValue(name.Trim(), out string? family) ? family : null;

    /// <summary>True when every character is representable in WinAnsiEncoding (the standard-14 fonts).</summary>
    internal static bool IsWinAnsi(string text, out char offending)
    {
        foreach (char c in text)
        {
            if (c is '\n' or '\r' or '\t') continue;
            if (c is >= ' ' and <= '~') continue;
            if (c is >= ' ' and <= 'ÿ') continue;
            if ("€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ".Contains(c)) continue;
            offending = c;
            return false;
        }
        offending = '\0';
        return true;
    }

    /// <summary>
    /// Splits <c>**bold**</c> and <c>*italic*</c> / <c>_italic_</c> markup into styled
    /// runs. A marker without a closing partner is kept literally, and <c>\*</c> escapes
    /// an asterisk, so prices and maths ("5 * 3") survive untouched.
    /// </summary>
    internal static List<InlineRun> ParseInline(string text)
    {
        var runs = new List<InlineRun>();
        var sb = new StringBuilder();
        bool bold = false, italic = false;

        void Flush()
        {
            if (sb.Length > 0) runs.Add(new InlineRun(sb.ToString(), bold, italic));
            sb.Clear();
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length && text[i + 1] is '*' or '_')
            {
                sb.Append(text[++i]);
                continue;
            }

            if (c == '*' && i + 1 < text.Length && text[i + 1] == '*'
                && (bold || text.IndexOf("**", i + 2, StringComparison.Ordinal) > i + 2))
            {
                Flush();
                bold = !bold;
                i++;
                continue;
            }

            if ((c == '*' || c == '_') && IsItalicMarker(text, i, italic))
            {
                Flush();
                italic = !italic;
                continue;
            }

            sb.Append(c);
        }

        Flush();
        return runs;
    }

    private static bool IsItalicMarker(string text, int i, bool closing)
    {
        char marker = text[i];
        if (closing) return i > 0 && !char.IsWhiteSpace(text[i - 1]);

        // Opening: must be followed by a non-space, preceded by a non-word character,
        // and closed later on the same line — so "a * b" and snake_case stay literal.
        if (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])) return false;
        if (i > 0 && char.IsLetterOrDigit(text[i - 1])) return false;
        int close = text.IndexOf(marker, i + 1);
        int newline = text.IndexOf('\n', i + 1);
        return close > i + 1 && (newline < 0 || close < newline) && !char.IsWhiteSpace(text[close - 1]);
    }
}

internal readonly record struct InlineRun(string Text, bool Bold, bool Italic);
