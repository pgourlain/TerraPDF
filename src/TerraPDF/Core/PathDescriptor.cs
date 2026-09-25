using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Fluent builder for a vector path used inside a <see cref="VectorCanvas"/>.
/// All coordinates are in PDF points relative to the top-left corner of the canvas element.
/// </summary>
public sealed class PathDescriptor
{
    // Commands accumulated by the fluent calls
    internal readonly List<VectorPathCommand> Commands = [];

    // Paint state
    internal PdfColor? FillColor { get; private set; }
    internal PdfColor? StrokeColor { get; private set; }
    internal double LineWidth { get; private set; } = 1;
    internal bool EvenOddFill { get; private set; }
    internal double PaintOpacity { get; private set; } = 1;
    internal double[]? DashPattern { get; private set; }
    internal double DashPhase { get; private set; }
    internal GradientFill? Gradient { get; private set; }

    /// <summary>A two-stop gradient fill; the angle only applies to linear gradients.</summary>
    internal sealed record GradientFill(bool Radial, string FromHex, string ToHex, double Angle);

    // ── Move / Line ─────────────────────────────────────────────────────────

    /// <summary>Moves the current point to (<paramref name="x"/>, <paramref name="y"/>) without drawing.</summary>
    public PathDescriptor MoveTo(double x, double y)
    {
        Commands.Add(new MoveToCmd(x, y));
        return this;
    }

    /// <summary>Draws a straight line from the current point to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public PathDescriptor LineTo(double x, double y)
    {
        Commands.Add(new LineToCmd(x, y));
        return this;
    }

    // ── Cubic Bézier ────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a cubic Bézier curve to (<paramref name="x"/>, <paramref name="y"/>)
    /// using two control points.
    /// </summary>
    public PathDescriptor CurveTo(
        double cx1, double cy1,
        double cx2, double cy2,
        double x, double y)
    {
        Commands.Add(new CurveToCmd(cx1, cy1, cx2, cy2, x, y));
        return this;
    }

    // ── Close ───────────────────────────────────────────────────────────────

    /// <summary>Closes the current subpath with a straight line back to its start point.</summary>
    public PathDescriptor Close()
    {
        Commands.Add(new ClosePathCmd());
        return this;
    }

    // ── Convenience shapes ──────────────────────────────────────────────────

    /// <summary>Appends a rectangle subpath (counter-clockwise, top-left origin).</summary>
    public PathDescriptor Rect(double x, double y, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return MoveTo(x, y)
              .LineTo(x + width, y)
              .LineTo(x + width, y + height)
              .LineTo(x, y + height)
              .Close();
    }

    /// <summary>
    /// Appends a rounded-rectangle subpath. <paramref name="radius"/> is clamped to half the
    /// shorter side, so the corners never overlap.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A dimension or <paramref name="radius"/> is zero or negative.</exception>
    public PathDescriptor RoundedRect(double x, double y, double width, double height, double radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        double r = Math.Min(radius, Math.Min(width, height) / 2);
        double k = r * 0.5523;
        return MoveTo(x + r, y)
              .LineTo(x + width - r, y)
              .CurveTo(x + width - r + k, y, x + width, y + r - k, x + width, y + r)
              .LineTo(x + width, y + height - r)
              .CurveTo(x + width, y + height - r + k, x + width - r + k, y + height, x + width - r, y + height)
              .LineTo(x + r, y + height)
              .CurveTo(x + r - k, y + height, x, y + height - r + k, x, y + height - r)
              .LineTo(x, y + r)
              .CurveTo(x, y + r - k, x + r - k, y, x + r, y)
              .Close();
    }

    /// <summary>
    /// Appends an ellipse subpath centred at (<paramref name="cx"/>, <paramref name="cy"/>)
    /// with the given horizontal and vertical radii.
    /// </summary>
    public PathDescriptor Ellipse(double cx, double cy, double rx, double ry)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);

        // Cubic Bézier approximation constant for a quarter-circle arc
        const double k = 0.5523;
        double kx = rx * k;
        double ky = ry * k;

        return MoveTo(cx + rx, cy)
              .CurveTo(cx + rx, cy - ky, cx + kx, cy - ry, cx, cy - ry)
              .CurveTo(cx - kx, cy - ry, cx - rx, cy - ky, cx - rx, cy)
              .CurveTo(cx - rx, cy + ky, cx - kx, cy + ry, cx, cy + ry)
              .CurveTo(cx + kx, cy + ry, cx + rx, cy + ky, cx + rx, cy)
              .Close();
    }

    /// <summary>
    /// Appends a circle subpath centred at (<paramref name="cx"/>, <paramref name="cy"/>)
    /// with the given <paramref name="radius"/>.
    /// </summary>
    public PathDescriptor Circle(double cx, double cy, double radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        return Ellipse(cx, cy, radius, radius);
    }

    /// <summary>
    /// Appends an elliptical arc. Angles are degrees clockwise from the right-hand point,
    /// matching the canvas top-left coordinate system. Negative sweeps run counter-clockwise,
    /// a zero sweep adds no commands, and sweeps beyond one revolution are preserved.
    /// </summary>
    /// <param name="cx">Horizontal coordinate of the ellipse centre in points.</param>
    /// <param name="cy">Vertical coordinate of the ellipse centre in points.</param>
    /// <param name="rx">Horizontal radius in points.</param>
    /// <param name="ry">Vertical radius in points.</param>
    /// <param name="startAngle">Start angle in degrees clockwise from the right-hand point.</param>
    /// <param name="sweepAngle">Signed sweep angle in degrees.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rx"/> or <paramref name="ry"/> is zero or negative, or any argument is not finite.</exception>
    public PathDescriptor Arc(double cx, double cy, double rx, double ry,
        double startAngle, double sweepAngle)
    {
        // Non-finite values must be rejected before the subdivision loop below:
        // an infinite sweep can never be reduced by `remaining -= step`, so the loop
        // would append control points until memory ran out, and a NaN sweep would
        // silently degenerate the arc into a bare MoveTo. A caller reaching either
        // is usually dividing by a zero total, e.g. `360 * value / total`.
        ThrowIfNotFinite(cx, nameof(cx));
        ThrowIfNotFinite(cy, nameof(cy));
        ThrowIfNotFinite(rx, nameof(rx));
        ThrowIfNotFinite(ry, nameof(ry));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ThrowIfNotFinite(startAngle, nameof(startAngle));
        ThrowIfNotFinite(sweepAngle, nameof(sweepAngle));
        if (sweepAngle == 0) return this;

        double start = startAngle * Math.PI / 180.0;
        double remaining = sweepAngle * Math.PI / 180.0;
        double stepLimit = Math.PI / 2;

        double x = cx + rx * Math.Cos(start);
        double y = cy + ry * Math.Sin(start);
        MoveTo(x, y);

        while (Math.Abs(remaining) > 1e-12)
        {
            double step = Math.CopySign(Math.Min(Math.Abs(remaining), stepLimit), remaining);
            double end = start + step;
            double tangent = 4.0 / 3.0 * Math.Tan(step / 4.0);
            double endX = cx + rx * Math.Cos(end);
            double endY = cy + ry * Math.Sin(end);
            double control1X = x - tangent * rx * Math.Sin(start);
            double control1Y = y + tangent * ry * Math.Cos(start);
            double control2X = endX + tangent * rx * Math.Sin(end);
            double control2Y = endY - tangent * ry * Math.Cos(end);

            CurveTo(control1X, control1Y, control2X, control2Y, endX, endY);
            x = endX;
            y = endY;
            start = end;
            remaining -= step;
        }

        return this;
    }

    /// <summary>Rejects NaN and ±Infinity before they can reach the emitted content stream.</summary>
    private static void ThrowIfNotFinite(double value, string paramName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be a finite number.");
    }

    /// <summary>
    /// Appends a closed elliptical sector by drawing an arc, connecting its end to the
    /// centre, and closing the subpath. A zero sweep adds no commands.
    /// </summary>
    /// <param name="cx">Horizontal coordinate of the ellipse centre in points.</param>
    /// <param name="cy">Vertical coordinate of the ellipse centre in points.</param>
    /// <param name="rx">Horizontal radius in points.</param>
    /// <param name="ry">Vertical radius in points.</param>
    /// <param name="startAngle">Start angle in degrees clockwise from the right-hand point.</param>
    /// <param name="sweepAngle">Signed sweep angle in degrees; values beyond one revolution are preserved.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rx"/> or <paramref name="ry"/> is zero or negative, or any argument is not finite.</exception>
    public PathDescriptor Sector(double cx, double cy, double rx, double ry,
        double startAngle, double sweepAngle)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        if (sweepAngle == 0) return this;
        Arc(cx, cy, rx, ry, startAngle, sweepAngle);
        return LineTo(cx, cy).Close();
    }

    /// <summary>
    /// Appends a polyline (open path) through all <paramref name="points"/> in order.
    /// Points are (x, y) pairs.
    /// </summary>
    /// <exception cref="ArgumentException">Fewer than two points supplied.</exception>
    public PathDescriptor Polyline(params (double X, double Y)[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException("Polyline requires at least two points.", nameof(points));

        MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
            LineTo(points[i].X, points[i].Y);
        return this;
    }

    /// <summary>
    /// Appends a closed polygon through all <paramref name="points"/> in order.
    /// The path is automatically closed after the last point.
    /// </summary>
    /// <exception cref="ArgumentException">Fewer than three points supplied.</exception>
    public PathDescriptor Polygon(params (double X, double Y)[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 3)
            throw new ArgumentException("Polygon requires at least three points.", nameof(points));

        MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
            LineTo(points[i].X, points[i].Y);
        return Close();
    }

    // ── Paint ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the fill colour for this path.
    /// When combined with <see cref="Stroke"/> the path is both filled and stroked.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public PathDescriptor Fill(string hexColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        FillColor = PdfColor.FromHex(hexColor);
        Gradient = null;
        return this;
    }

    /// <summary>
    /// Sets the stroke colour and optional line width for this path.
    /// When combined with <see cref="Fill"/> the path is both filled and stroked.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    public PathDescriptor Stroke(string hexColor, double lineWidth = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        StrokeColor = PdfColor.FromHex(hexColor);
        LineWidth = lineWidth;
        return this;
    }

    /// <summary>
    /// Fills the path with a linear two-color gradient instead of a flat color.
    /// The gradient spans the path's bounding box; <paramref name="angle"/> is in degrees
    /// clockwise from the left-to-right direction (0 = left to right, 90 = top to bottom).
    /// Replaces any color set with <see cref="Fill"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A color is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="angle"/> is not finite.</exception>
    public PathDescriptor FillLinearGradient(string fromHex, string toHex, double angle = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(toHex);
        ThrowIfNotFinite(angle, nameof(angle));
        _ = PdfColor.FromHex(fromHex);
        _ = PdfColor.FromHex(toHex);
        Gradient = new GradientFill(false, fromHex, toHex, angle);
        FillColor = null;
        return this;
    }

    /// <summary>
    /// Fills the path with a radial two-color gradient centred on the path's bounding box:
    /// <paramref name="centerHex"/> in the middle, <paramref name="edgeHex"/> at half the
    /// larger side. Replaces any color set with <see cref="Fill"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A color is null or whitespace.</exception>
    public PathDescriptor FillRadialGradient(string centerHex, string edgeHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(centerHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeHex);
        _ = PdfColor.FromHex(centerHex);
        _ = PdfColor.FromHex(edgeHex);
        Gradient = new GradientFill(true, centerHex, edgeHex, 0);
        FillColor = null;
        return this;
    }

    /// <summary>
    /// Strokes the path with a dash pattern (alternating dash and gap lengths in points,
    /// copied). Only affects the stroke; needs <see cref="Stroke"/> to be visible.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is empty, has a negative or non-finite value, or only zeros.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="phase"/> is negative or not finite.</exception>
    public PathDescriptor Dash(double[] pattern, double phase = 0)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0 || pattern.Any(v => v < 0 || !double.IsFinite(v)) || pattern.All(v => v == 0))
            throw new ArgumentException("Dash patterns must contain at least one positive, finite value.", nameof(pattern));
        if (!double.IsFinite(phase) || phase < 0)
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Dash phase must be a nonnegative, finite number.");
        DashPattern = pattern.ToArray();
        DashPhase = phase;
        return this;
    }

    /// <summary>Bounding box of every anchor and control point, or <see langword="false"/> for an empty path.</summary>
    internal bool TryGetBounds(out double minX, out double minY, out double maxX, out double maxY)
    {
        double loX = double.MaxValue, loY = double.MaxValue, hiX = double.MinValue, hiY = double.MinValue;
        bool any = false;
        foreach (var cmd in Commands)
        {
            foreach (var (x, y) in Points(cmd))
            {
                any = true;
                loX = Math.Min(loX, x); hiX = Math.Max(hiX, x);
                loY = Math.Min(loY, y); hiY = Math.Max(hiY, y);
            }
        }
        (minX, minY, maxX, maxY) = (loX, loY, hiX, hiY);
        return any;
    }

    private static IEnumerable<(double X, double Y)> Points(VectorPathCommand cmd)
    {
        switch (cmd)
        {
            case MoveToCmd m: yield return (m.X, m.Y); break;
            case LineToCmd l: yield return (l.X, l.Y); break;
            case CurveToCmd c:
                yield return (c.Cx1, c.Cy1);
                yield return (c.Cx2, c.Cy2);
                yield return (c.X, c.Y);
                break;
        }
    }

    /// <summary>
    /// Uses the even-odd rule instead of the non-zero winding rule when filling.
    /// Useful for shapes with holes (e.g. a donut).
    /// </summary>
    public PathDescriptor UseEvenOddFill()
    {
        EvenOddFill = true;
        return this;
    }

    /// <summary>
    /// Sets constant alpha for both fill and stroke on this path (1 = fully
    /// opaque, the default; e.g. 0.4 for a translucent highlight or tint).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is outside [0, 1].</exception>
    public PathDescriptor Opacity(double opacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(opacity, 0.0, nameof(opacity));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opacity, 1.0, nameof(opacity));
        PaintOpacity = opacity;
        return this;
    }
}
