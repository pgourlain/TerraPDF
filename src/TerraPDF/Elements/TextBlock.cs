using System.Globalization;
using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

// --- Span types ---------------------------------------------------------------

internal abstract class TextSpan
{
    internal TextStyle? Style { get; set; }
}

internal sealed class LiteralSpan : TextSpan
{
    internal required string Text { get; init; }
}

internal sealed class PageNumberSpan : TextSpan { }
internal sealed class TotalPagesSpan : TextSpan { }

// --- Text element -------------------------------------------------------------

/// <summary>Renders one or more styled text spans with automatic word-wrapping.</summary>
internal sealed class TextBlock : Element
{
    internal List<TextSpan> Spans     { get; } = [];
    internal TextStyle?     SpanStyle { get; set; }

    /// <summary>
    /// Counts layouts, on the current thread, whose result depends on the total-page
    /// hint (blocks holding page-number spans). The composer compares it before and
    /// after a layout pass: when unchanged, re-laying out with the real page count
    /// would produce the same result and is skipped.
    /// </summary>
    [ThreadStatic]
    internal static int PageCountDependentLayouts;

    internal TextBlock(string text) => Spans.Add(new LiteralSpan { Text = text });
    internal TextBlock() { }

    // -- Tokenisation ------------------------------------------------------

    /// <summary>
    /// A single word-level unit ready for line-packing.
    /// <c>Text</c> carries the page-count placeholder for page-number spans;
    /// the actual value is substituted at draw time via the flags.
    /// <c>Font</c>, <c>Color</c> and <c>Width</c> are resolved once when the token is
    /// created (see <see cref="Create"/>) and reused by line breaking, measuring and drawing.
    /// </summary>
    internal readonly record struct TextToken(
        string       Text,
        TextStyle    Style,
        bool         IsPageNumber,
        bool         IsTotalPages,
        ResolvedFont Font,
        PdfColor     Color,
        double       Width)
    {
        internal static TextToken Create(string text, TextStyle style, ResolvedFont font, PdfColor color,
            bool isPageNumber = false, bool isTotalPages = false) =>
            new(text, style, isPageNumber, isTotalPages, font, color, Measure(text, style, font));

        /// <summary>Same token showing different text (a fragment or a page number), re-measured.</summary>
        internal TextToken WithText(string text) =>
            this with { Text = text, Width = Measure(text, Style, Font) };

        private static double Measure(string text, TextStyle style, ResolvedFont font) =>
            FontMetrics.MeasureWidth(text, style.Size ?? 12, font,
                style.IsBold ?? false, style.IsItalic ?? false);
    }

    private static ResolvedFont ResolveFont(TextStyle style) =>
        PdfFonts.ResolveFont(style.Family, style.IsBold ?? false, style.IsItalic ?? false);

    /// <summary>
    /// Breaks all spans into word-level <see cref="TextToken"/>s with fully resolved styles.
    /// </summary>
    private List<TextToken> Tokenize(TextStyle baseStyle, string pageNum, string totalPages)
    {
        var tokens = new List<TextToken>();
        foreach (var span in Spans)
        {
            TextStyle s = baseStyle.MergeWith(span.Style);
            var font  = ResolveFont(s);
            var color = PdfColor.FromHex(s.Color ?? "#000000");
            switch (span)
            {
                case LiteralSpan ls:
                    foreach (string word in SplitWords(ls.Text))
                        tokens.Add(TextToken.Create(word, s, font, color));
                    break;
                case PageNumberSpan:
                    tokens.Add(TextToken.Create(pageNum,    s, font, color, isPageNumber: true));
                    break;
                case TotalPagesSpan:
                    tokens.Add(TextToken.Create(totalPages, s, font, color, isTotalPages: true));
                    break;
            }
        }
        return tokens;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into alternating non-whitespace word tokens,
    /// whitespace-run tokens, and explicit newline tokens.
    /// </summary>
    private static IEnumerable<string> SplitWords(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                yield return "\n";
                i++;
            }
            else if (char.IsWhiteSpace(text[i]))
            {
                int s = i;
                while (i < text.Length && char.IsWhiteSpace(text[i]) && text[i] != '\n') i++;
                yield return text[s..i];
            }
            else
            {
                int s = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                yield return text[s..i];
            }
        }
    }

    // -- Line building -----------------------------------------------------

    /// <summary>One wrapped line and whether it ends a paragraph (last line / hard-break line).</summary>
    internal readonly record struct WrappedLine(List<TextToken> Tokens, bool IsLastInParagraph);

    /// <summary>
    /// Greedily packs tokens into lines no wider than <paramref name="availableWidth"/>.
    /// A single word wider than the line is broken at character boundaries so no
    /// line ever exceeds the available width. Returns at least one (possibly empty) line.
    /// </summary>
    private static List<WrappedLine> BuildLines(List<TextToken> tokens, double availableWidth)
    {
        var lines   = new List<WrappedLine>();
        var current = new List<TextToken>();
        double lineW = 0;

        foreach (var token in tokens)
        {
            // Hard line-break
            if (token.Text == "\n")
            {
                lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: true));
                current = [];
                lineW   = 0;
                continue;
            }

            // Skip leading whitespace at the start of a new line
            if (current.Count == 0 && string.IsNullOrWhiteSpace(token.Text))
                continue;

            double tw = token.Width;

            // Token overflows the current line – wrap first
            if (lineW + tw > availableWidth && current.Count > 0)
            {
                lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: false));
                current = [];
                lineW   = 0;

                // Drop the whitespace token that triggered the wrap
                if (string.IsNullOrWhiteSpace(token.Text))
                    continue;
            }

            // The token now starts its line. If it alone is wider than the line,
            // break it at character boundaries; the last fragment stays in
            // `current` so following tokens can pack after it. Page-number
            // tokens are exempt: their text is a placeholder substituted at
            // draw time, so each fragment would redraw the full number.
            var placed = token;
            if (tw > availableWidth && !token.IsPageNumber && !token.IsTotalPages)
            {
                var fragments = BreakOversizedToken(token, availableWidth);
                for (int f = 0; f < fragments.Count - 1; f++)
                    lines.Add(new WrappedLine([fragments[f]], IsLastInParagraph: false));
                placed = fragments[^1];
                tw     = placed.Width;
            }

            current.Add(placed);
            lineW += tw;
        }

        if (current.Count > 0)
            lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: true));

        return lines.Count > 0 ? lines : [new WrappedLine([], true)];
    }

    /// <summary>
    /// Splits a token wider than <paramref name="availableWidth"/> into fragments
    /// that each fit, keeping surrogate pairs intact. Character advance widths are
    /// additive (<see cref="FontMetrics.MeasureWidth(string, double, PdfFontFamily, bool, bool)"/> applies no kerning), so a
    /// fragment's width equals the sum of its characters'. Every fragment carries
    /// at least one character so layout always makes progress, even when the line
    /// is narrower than a single glyph.
    /// </summary>
    private static List<TextToken> BreakOversizedToken(in TextToken token, double availableWidth)
    {
        var fragments = new List<TextToken>();
        string text = token.Text;
        int start = 0;
        while (start < text.Length)
        {
            double w   = 0;
            int    end = start;
            while (end < text.Length)
            {
                int next = end + (char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1);
                double cw = token.WithText(text[end..next]).Width;
                if (end > start && w + cw > availableWidth)
                    break;
                w   += cw;
                end  = next;
            }
            fragments.Add(token.WithText(text[start..end]));
            start = end;
        }
        return fragments;
    }

    private static List<TextToken> TrimTrailing(List<TextToken> line)
    {
        int last = line.Count - 1;
        while (last >= 0 && string.IsNullOrWhiteSpace(line[last].Text)) last--;
        return line[..(last + 1)];
    }

    // -- Measure / line layout --------------------------------------

    /// <summary>
    /// Tokenizes and wraps this block's text for the given width, using the
    /// document's page-count hint as the placeholder for page-number spans
    /// (the actual values are substituted at draw time).  Shared by
    /// <see cref="Measure"/>, <see cref="Draw"/>, and the pagination engine's
    /// line-splitting path, so all three always agree on line breaks.
    /// </summary>
    /// <remarks>
    /// Within one <see cref="LayoutPass"/> the result of the last call is memoised:
    /// the same block is measured by its parents, by the pagination engine and again
    /// when drawn, usually at the same width. The page-count hint is part of the key
    /// only for blocks that hold page-number spans. The returned lines are shared and
    /// must not be mutated.
    /// </remarks>
    internal (List<WrappedLine> Lines, TextStyle Resolved, double LineHeight) LayoutLines(
        double w, TextStyle? defaultStyle, int totalPagesHint)
    {
        bool hasPageNumbers = Spans.Exists(s => s is PageNumberSpan or TotalPagesSpan);
        if (hasPageNumbers)
            PageCountDependentLayouts++;

        int pass = LayoutPass.Current;
        var cached = _layoutCache;
        if (pass != 0 && cached is not null && cached.Pass == pass && cached.Width == w
            && ReferenceEquals(cached.DefaultStyle, defaultStyle)
            && (!hasPageNumbers || cached.TotalPagesHint == totalPagesHint))
        {
            return cached.Result;
        }

        TextStyle resolved = (defaultStyle ?? TextStyle.Default).MergeWith(SpanStyle);
        double    lineH    = (resolved.Size ?? 12) * (resolved.LineHeightMultiplier ?? 1.4);

        string placeholder = totalPagesHint.ToString(CultureInfo.InvariantCulture);
        var tokens = Tokenize(resolved, placeholder, placeholder);
        var result = (BuildLines(tokens, w), resolved, lineH);

        if (pass != 0)
            _layoutCache = new LayoutCacheEntry(pass, w, defaultStyle, totalPagesHint, result);
        return result;
    }

    /// <summary>The last <see cref="LayoutLines"/> result and the inputs it was computed for.</summary>
    private sealed record LayoutCacheEntry(
        int Pass, double Width, TextStyle? DefaultStyle, int TotalPagesHint,
        (List<WrappedLine> Lines, TextStyle Resolved, double LineHeight) Result);

    // Replaced as a whole (never mutated), so concurrent publishes of the same
    // document at worst recompute a layout.
    private LayoutCacheEntry? _layoutCache;

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        var (lines, _, lineH) = LayoutLines(w, defaultStyle, totalPagesHint);

        // Return the width of the widest line so that container-level alignment elements
        // (AlignCenter, AlignRight) can compute the correct offset.
        double contentW = lines.Count > 0
            ? lines.Max(l => l.Tokens.Sum(t => t.Width))
            : 0;

        return new ElementSize(contentW, lines.Count * lineH);
    }

    /// <summary>A pending underline/strikethrough stroke, drawn after the line's text object closes.</summary>
    private readonly record struct DecorationStroke(double X, double Y, double Width, PdfColor Color, double LineWidth);

    /// <summary>
    /// Shows one token inside the currently open text object and queues its
    /// underline/strikethrough strokes (graphics operators are illegal inside
    /// <c>BT…ET</c>, so they are flushed after the text object closes).
    /// </summary>
    private static void DrawToken(DrawingContext ctx, in TextToken token, double x, double lineY,
        List<DecorationStroke> decorations)
    {
        double sf  = token.Style.Size    ?? 12;
        bool   sb  = token.Style.IsBold  ?? false;
        bool   si  = token.Style.IsItalic ?? false;
        var    sc  = token.Color;
        var    font = token.Font;

        double bl = lineY + sf;
        double tw = token.Width;

        if (font.IsCustom)
            ctx.Page.ShowTextAtCustomFont(token.Text, x, bl, sf, sc, font.Custom!);
        else
            ctx.Page.ShowTextAt(token.Text, x, bl, sf, sc, font.StandardFamily, sb, si);

        if (token.Style.IsStrikethrough ?? false)
            decorations.Add(new DecorationStroke(x, bl - sf * 0.35, tw, sc, sf * 0.07));

        if (token.Style.IsUnderline ?? false)
            decorations.Add(new DecorationStroke(x, bl + sf * 0.12, tw, sc, sf * 0.07));
    }

    // -- Draw ------------------------------------------------------

    /// <summary>
    /// Replaces the layout-time page-number placeholder with the actual value
    /// for the page being drawn.
    /// </summary>
    private static TextToken ResolveDynamicText(in TextToken t, DrawingContext ctx)
    {
        if (t.IsPageNumber)
            return t.WithText(ctx.PageNumber.ToString(CultureInfo.InvariantCulture));
        if (t.IsTotalPages)
            return t.WithText(ctx.TotalPages.ToString(CultureInfo.InvariantCulture));
        return t;
    }

    internal override void Draw(DrawingContext ctx)
    {
        var (lines, resolved, lineH) = LayoutLines(ctx.Width, ctx.DefaultTextStyle, ctx.TotalPages);
        DrawLines(ctx, lines, resolved, lineH);
    }

    /// <summary>
    /// Draws pre-wrapped lines starting at <c>ctx.Y</c>, honouring per-line
    /// alignment/justification.  Also used by <see cref="TextBlockSlice"/> to
    /// draw a page-sized subrange of a split paragraph.
    /// </summary>
    internal static void DrawLines(DrawingContext ctx, List<WrappedLine> lines,
        TextStyle resolved, double lineH)
    {
        var alignment = resolved.Alignment ?? TextAlignment.Left;

        double curY = ctx.Y;
        var decorations = new List<DecorationStroke>();

        foreach (var (rawTokens, isLastInParagraph) in lines)
        {
            // Substitute the actual page numbers for placeholder tokens before
            // widths are computed, so alignment uses the drawn text's width.
            var lineTokens = rawTokens;
            for (int t = 0; t < rawTokens.Count; t++)
            {
                if (rawTokens[t].IsPageNumber || rawTokens[t].IsTotalPages)
                {
                    lineTokens = rawTokens.Select(tok => ResolveDynamicText(tok, ctx)).ToList();
                    break;
                }
            }

            // Determine the effective alignment for this line:
            // the last line of a justified paragraph falls back to left.
            var lineAlignment = (alignment == TextAlignment.Justify && isLastInParagraph)
                ? TextAlignment.Left
                : alignment;

            // One text object per line; opened lazily so empty lines emit nothing.
            bool textOpen = false;
            decorations.Clear();

            void Show(in TextToken token, double x)
            {
                if (!textOpen)
                {
                    ctx.Page.BeginTextObject();
                    textOpen = true;
                }
                DrawToken(ctx, token, x, curY, decorations);
            }

            if (lineAlignment == TextAlignment.Justify)
            {
                // Justify: skip whitespace tokens, distribute extra space between word gaps.
                var    words    = lineTokens.Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToList();
                double wordsW   = words.Sum(t => t.Width);
                int    gapCount = words.Count - 1;
                double extra    = gapCount > 0 ? (ctx.Width - wordsW) / gapCount : 0;

                double curX    = ctx.X;
                int    wordIdx = 0;
                foreach (var token in words)
                {
                    Show(token, curX);
                    curX += token.Width;
                    if (wordIdx < gapCount)
                        curX += extra;
                    wordIdx++;
                }
            }
            else
            {
                // Left / Center / Right: preserve natural whitespace widths.
                double totalLineW = lineTokens.Sum(t => t.Width);
                double curX = lineAlignment switch
                {
                    TextAlignment.Right  => ctx.X + ctx.Width - totalLineW,
                    TextAlignment.Center => ctx.X + (ctx.Width - totalLineW) / 2,
                    _                    => ctx.X,
                };

                foreach (var token in lineTokens)
                {
                    if (!string.IsNullOrEmpty(token.Text))
                        Show(token, curX);
                    curX += token.Width;
                }
            }

            if (textOpen)
                ctx.Page.EndTextObject();

            // Underline/strikethrough are graphics operators — draw them after
            // the text object closes, at the exact per-token coordinates.
            foreach (var s in decorations)
                ctx.Page.AddLine(s.X, s.Y, s.X + s.Width, s.Y, s.Color, s.LineWidth);

            curY += lineH;
        }
    }
}

/// <summary>
/// A page-sized subrange of a split <see cref="TextBlock"/>'s wrapped lines,
/// produced by the pagination engine when a paragraph is taller than the
/// remaining page.  Follows the same pattern as <see cref="TableSlice"/>:
/// the lines were wrapped once at layout time, so every slice agrees on
/// line breaks with the measured whole.
/// </summary>
internal sealed class TextBlockSlice : Element
{
    private readonly List<TextBlock.WrappedLine> _lines;
    private readonly TextStyle _resolved;
    private readonly double    _lineHeight;

    internal TextBlockSlice(List<TextBlock.WrappedLine> lines, TextStyle resolved, double lineHeight)
    {
        _lines      = lines;
        _resolved   = resolved;
        _lineHeight = lineHeight;
    }

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint) =>
        new(w, _lines.Count * _lineHeight);

    internal override void Draw(DrawingContext ctx) =>
        TextBlock.DrawLines(ctx, _lines, _resolved, _lineHeight);
}
