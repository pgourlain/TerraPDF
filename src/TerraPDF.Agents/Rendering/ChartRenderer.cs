using System.Globalization;
using TerraPDF.Agents.Spec;
using TerraPDF.Core;

namespace TerraPDF.Agents.Rendering;

/// <summary>
/// Draws bar, line, and pie charts onto a <see cref="VectorCanvas"/>. The canvas
/// callback runs before layout, so the drawable width is passed in rather than
/// read from the canvas.
/// </summary>
internal static class ChartRenderer
{
    // Accent first, then a colour-blind-considerate categorical palette.
    private static readonly string[] s_palette =
        ["#4E79A7", "#F28E2B", "#59A14F", "#E15759", "#76B7B2", "#EDC948", "#B07AA1", "#FF9DA7", "#9C755F", "#BAB0AC"];

    private const double TitleHeight = 20;
    private const double LegendHeight = 16;
    private const double AxisFontSize = 7.5;

    internal static void Draw(VectorCanvas canvas, BlockSpec b, RenderContext ctx, double width)
    {
        double height = b.Height ?? 220;
        double top = 0;

        if (!string.IsNullOrWhiteSpace(b.Title))
        {
            canvas.Text(b.Title, 0, 12, ctx.TextColor, 11, ctx.FontFamily, bold: true);
            top = TitleHeight;
        }

        List<(string Name, List<double> Values, string Color)> series = Series(b, ctx);

        if (string.Equals(b.ChartType, "pie", StringComparison.OrdinalIgnoreCase))
        {
            DrawPie(canvas, b, ctx, width, top, height);
            return;
        }

        if (series.Count > 1)
        {
            DrawLegend(canvas, series.Select(s => (s.Name, s.Color)).ToList(), ctx, 0, top + 8, width);
            top += LegendHeight;
        }

        DrawAxesChart(canvas, b, series, ctx, width, top, height);
    }

    private static List<(string Name, List<double> Values, string Color)> Series(BlockSpec b, RenderContext ctx)
    {
        if (b.Values is { Count: > 0 })
            return [(b.Title ?? string.Empty, b.Values, SpecText.NormalizeColor(b.Color) ?? ctx.Accent)];

        return b.Series!
            .Select((s, i) => (s.Name ?? $"Series {i + 1}", s.Values!, SpecText.NormalizeColor(s.Color) ?? PaletteColor(i, ctx)))
            .ToList();
    }

    private static string PaletteColor(int i, RenderContext ctx) =>
        i == 0 ? ctx.Accent : s_palette[i % s_palette.Length];

    // ── Bar and line ────────────────────────────────────────────────────

    private static void DrawAxesChart(VectorCanvas canvas, BlockSpec b,
        List<(string Name, List<double> Values, string Color)> series, RenderContext ctx,
        double width, double top, double height)
    {
        bool line = string.Equals(b.ChartType, "line", StringComparison.OrdinalIgnoreCase);
        List<string> labels = b.Labels!;

        double dataMax = series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max();
        double dataMin = series.SelectMany(s => s.Values).DefaultIfEmpty(0).Min();
        (double axisMin, double axisMax, double step) = NiceScale(Math.Min(0, dataMin), Math.Max(0, dataMax));

        double axisLabelWidth = Enumerable.Range(0, (int)Math.Round((axisMax - axisMin) / step) + 1)
            .Select(i => VectorCanvas.MeasureTextWidth(Compact(axisMin + (i * step)), AxisFontSize, ctx.FontFamily))
            .Max();

        double left = axisLabelWidth + 6;
        double bottom = height - 16;
        double plotTop = top + 6;
        double plotWidth = Math.Max(width - left - 4, 10);
        double plotHeight = Math.Max(bottom - plotTop, 10);
        double Y(double v) => bottom - ((v - axisMin) / (axisMax - axisMin) * plotHeight);

        // Grid lines and value axis labels.
        for (double v = axisMin; v <= axisMax + (step / 2); v += step)
        {
            double y = Y(v);
            canvas.Line(left, y, left + plotWidth, y, Math.Abs(v) < step / 1e6 ? "#9AA0A6" : "#E3E6EA", 0.5);
            string label = Compact(v);
            double lw = VectorCanvas.MeasureTextWidth(label, AxisFontSize, ctx.FontFamily);
            canvas.Text(label, left - 4 - lw, y + 2.5, ctx.Muted, AxisFontSize, ctx.FontFamily);
        }

        double slot = plotWidth / labels.Count;
        DrawCategoryLabels(canvas, labels, ctx, left, slot, bottom + 11);

        if (line)
        {
            foreach (var s in series)
            {
                var points = s.Values.Select((v, i) => (X: left + (slot * (i + 0.5)), Y: Y(v))).ToArray();
                if (points.Length > 1) canvas.Path(p => p.Polyline(points).Stroke(s.Color, 1.75));
                if (points.Length <= 60)
                    foreach (var (x, y) in points) canvas.DrawCircle(x, y, 2.25, "#FFFFFF", s.Color, 1.25);
            }
            return;
        }

        double groupWidth = slot * 0.7;
        double barWidth = groupWidth / series.Count;
        for (int i = 0; i < labels.Count; i++)
        {
            double groupLeft = left + (slot * i) + ((slot - groupWidth) / 2);
            for (int s = 0; s < series.Count; s++)
            {
                double v = series[s].Values[i];
                double y0 = Y(0), y1 = Y(v);
                canvas.FillRect(groupLeft + (barWidth * s), Math.Min(y0, y1), Math.Max(barWidth - 1, 0.5),
                    Math.Max(Math.Abs(y1 - y0), 0.01), series[s].Color);
            }
        }

        // Value labels only where they fit: a single series with room above each bar.
        if (series.Count == 1 && barWidth >= 18)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                double v = series[0].Values[i];
                string text = Compact(v);
                double tw = VectorCanvas.MeasureTextWidth(text, AxisFontSize, ctx.FontFamily);
                double x = left + (slot * (i + 0.5)) - (tw / 2);
                double y = v >= 0 ? Y(v) - 3 : Y(v) + 9;
                canvas.Text(text, x, y, ctx.TextColor, AxisFontSize, ctx.FontFamily);
            }
        }
    }

    private static void DrawCategoryLabels(VectorCanvas canvas, List<string> labels, RenderContext ctx,
        double left, double slot, double baseline)
    {
        // Thin the labels out when they would collide, always keeping the first.
        double widest = labels.Max(l => VectorCanvas.MeasureTextWidth(l, AxisFontSize, ctx.FontFamily));
        int every = Math.Max(1, (int)Math.Ceiling((widest + 4) / slot));
        double maxWidth = slot * every - 4;

        for (int i = 0; i < labels.Count; i += every)
        {
            string text = Fit(labels[i], maxWidth, AxisFontSize, ctx.FontFamily);
            double tw = VectorCanvas.MeasureTextWidth(text, AxisFontSize, ctx.FontFamily);
            canvas.Text(text, left + (slot * (i + 0.5)) - (tw / 2), baseline, ctx.Muted, AxisFontSize, ctx.FontFamily);
        }
    }

    // ── Pie ─────────────────────────────────────────────────────────────

    private static void DrawPie(VectorCanvas canvas, BlockSpec b, RenderContext ctx, double width, double top, double height)
    {
        List<double> values = b.Values!;
        double total = values.Sum();
        double diameter = Math.Min(height - top - 8, width * 0.45);
        double x = 4, y = top + 4;

        double angle = -90; // twelve o'clock; the canvas measures clockwise from three o'clock
        for (int i = 0; i < values.Count; i++)
        {
            double sweep = values[i] / total * 360;
            if (sweep <= 0) continue;
            string color = PaletteColor(i, ctx);
            if (sweep >= 359.999) canvas.FillCircle(x + (diameter / 2), y + (diameter / 2), diameter / 2, color);
            else canvas.DrawPie(x, y, diameter, diameter, angle, sweep, color, "#FFFFFF", 1);
            angle += sweep;
        }

        // Legend to the right, one row per slice with its share.
        double legendX = x + diameter + 20;
        double rowHeight = Math.Min(16, (height - top - 4) / values.Count);
        double fontSize = Math.Min(9, Math.Max(rowHeight - 5, 5));
        for (int i = 0; i < values.Count; i++)
        {
            double rowY = top + 6 + (i * rowHeight);
            canvas.FillRect(legendX, rowY, fontSize, fontSize, PaletteColor(i, ctx));
            string share = (values[i] / total * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            string label = Fit($"{b.Labels![i]}  {share}", width - legendX - fontSize - 8, fontSize, ctx.FontFamily);
            canvas.Text(label, legendX + fontSize + 5, rowY + fontSize - 1, ctx.TextColor, fontSize, ctx.FontFamily);
        }
    }

    private static void DrawLegend(VectorCanvas canvas, List<(string Name, string Color)> items, RenderContext ctx,
        double x, double baseline, double width)
    {
        const double size = 7.5;
        foreach (var (name, color) in items)
        {
            double w = VectorCanvas.MeasureTextWidth(name, size, ctx.FontFamily);
            if (x + w + 14 > width) break;
            canvas.FillRect(x, baseline - size + 1, size, size, color);
            canvas.Text(name, x + size + 4, baseline, ctx.TextColor, size, ctx.FontFamily);
            x += size + w + 16;
        }
    }

    // ── Numbers and text ────────────────────────────────────────────────

    /// <summary>Rounds the axis range out to 1/2/5 × 10ⁿ steps, about five gridlines.</summary>
    internal static (double Min, double Max, double Step) NiceScale(double min, double max)
    {
        if (max - min < 1e-12) max = min + 1;
        double raw = (max - min) / 5;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double step = new[] { 1, 2, 2.5, 5, 10 }.Select(m => m * magnitude).First(s => s >= raw);
        return (Math.Floor(min / step) * step, Math.Ceiling(max / step) * step, step);
    }

    internal static string Compact(double v)
    {
        double a = Math.Abs(v);
        return a switch
        {
            >= 1e9 => (v / 1e9).ToString("0.#", CultureInfo.InvariantCulture) + "B",
            >= 1e6 => (v / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M",
            >= 1e4 => (v / 1e3).ToString("0.#", CultureInfo.InvariantCulture) + "K",
            _ => v.ToString(a < 10 && a % 1 != 0 ? "0.##" : "#,0.#", CultureInfo.InvariantCulture),
        };
    }

    private static string Fit(string text, double maxWidth, double fontSize, string family)
    {
        if (VectorCanvas.MeasureTextWidth(text, fontSize, family) <= maxWidth) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string candidate = text[..n].TrimEnd() + "...";
            if (VectorCanvas.MeasureTextWidth(candidate, fontSize, family) <= maxWidth) return candidate;
        }
        return string.Empty;
    }
}
