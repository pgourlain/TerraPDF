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
