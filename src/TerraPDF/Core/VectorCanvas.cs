using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Fluent vector-graphics canvas.  Returned by <c>IContainer.Canvas(…)</c>.
/// Coordinates are in PDF points with a <b>top-left origin</b> relative to the
/// element's bounding box, matching every other TerraPDF API.
///
/// <para>
/// Supported primitives:
/// <list type="bullet">
///   <item>Lines, polylines</item>
///   <item>Rectangles (filled / stroked / both)</item>
///   <item>Circles and ellipses</item>
///   <item>Rounded rectangles</item>
///   <item>Arbitrary paths with <c>MoveTo / LineTo / CurveTo / Close</c></item>
///   <item>Filled polygons</item>
///   <item>Text labels, standard or via a registered custom font</item>
///   <item>Grid helpers</item>
/// </list>
/// </para>
/// </summary>
public sealed class VectorCanvas
{
    // -----------------------------------------------------------------------
    //  Internal drawing command records
    // -----------------------------------------------------------------------

    // Each Draw* method appends one of these to _commands.
    // CanvasElement.Draw() replays them all onto PdfPage in order.

    internal abstract record DrawCommand;

    internal sealed record DrawLineCmd(
        double X1, double Y1, double X2, double Y2,
        string HexColor, double LineWidth, double Opacity) : DrawCommand;

    internal sealed record DrawRectCmd(
        double X, double Y, double W, double H,
        string? FillHex, string? StrokeHex, double LineWidth, double Opacity) : DrawCommand;

    internal sealed record DrawRoundedRectCmd(
        double X, double Y, double W, double H, double Radius,
        string? FillHex, string? StrokeHex, double LineWidth, double Opacity) : DrawCommand;

    internal sealed record DrawEllipseCmd(
        double Cx, double Cy, double Rx, double Ry,
        string? FillHex, string? StrokeHex, double LineWidth, double Opacity) : DrawCommand;

    internal sealed record DrawPathCmd(PathDescriptor Path) : DrawCommand;

    internal sealed record DrawTextCmd(
        double X, double Y, string Text, string HexColor, double FontSize,
        string? FontFamily, bool Bold, bool Italic, double Opacity) : DrawCommand;

    // -----------------------------------------------------------------------
    //  State
    // -----------------------------------------------------------------------

    internal readonly List<DrawCommand> Commands = [];

    // The bounding box is injected at render time by CanvasElement.
    internal double AllocatedWidth  { get; set; }
    internal double AllocatedHeight { get; set; }

    // -----------------------------------------------------------------------
    //  Opacity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validates that an <c>opacity</c> argument is a legal constant-alpha value.
    /// PDF's <c>/ExtGState</c> <c>/ca</c>/<c>/CA</c> range is [0, 1]; 1 (fully
    /// opaque, the default on every shape method) emits no <c>/ExtGState</c> at all.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is outside [0, 1].</exception>
    private static void ValidateOpacity(double opacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(opacity, 0.0, nameof(opacity));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opacity, 1.0, nameof(opacity));
    }

    // -----------------------------------------------------------------------
    //  Line
    // -----------------------------------------------------------------------

    /// <summary>Draws a straight line from (<paramref name="x1"/>, <paramref name="y1"/>)
    /// to (<paramref name="x2"/>, <paramref name="y2"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas Line(double x1, double y1, double x2, double y2,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawLineCmd(x1, y1, x2, y2, hexColor, lineWidth, opacity));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Rectangle
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillRect(double x, double y, double width, double height,
        string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRectCmd(x, y, width, height, hexColor, null, 0, opacity));
        return this;
    }

    /// <summary>Draws a stroked (outline-only) rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas StrokeRect(double x, double y, double width, double height,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRectCmd(x, y, width, height, null, hexColor, lineWidth, opacity));
        return this;
    }

    /// <summary>Draws a filled and stroked rectangle.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas DrawRect(double x, double y, double width, double height,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRectCmd(x, y, width, height, fillHex, strokeHex, lineWidth, opacity));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Rounded rectangle
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled rounded rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillRoundedRect(double x, double y, double width, double height,
        double radius, string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, hexColor, null, 0, opacity));
        return this;
    }

    /// <summary>Draws a stroked rounded rectangle.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas StrokeRoundedRect(double x, double y, double width, double height,
        double radius, string hexColor = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, null, hexColor, lineWidth, opacity));
        return this;
    }

    /// <summary>Draws a filled and stroked rounded rectangle.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas DrawRoundedRect(double x, double y, double width, double height,
        double radius, string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawRoundedRectCmd(x, y, width, height, radius, fillHex, strokeHex, lineWidth, opacity));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Circle / Ellipse
    // -----------------------------------------------------------------------

    /// <summary>Draws a filled circle centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillCircle(double cx, double cy, double radius, string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, hexColor, null, 0, opacity));
        return this;
    }

    /// <summary>Draws a stroked circle centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas StrokeCircle(double cx, double cy, double radius,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, null, hexColor, lineWidth, opacity));
        return this;
    }

    /// <summary>Draws a filled and stroked circle.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas DrawCircle(double cx, double cy, double radius,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, radius, radius, fillHex, strokeHex, lineWidth, opacity));
        return this;
    }

    /// <summary>Draws a filled ellipse centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either radius is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas FillEllipse(double cx, double cy, double rx, double ry,
        string hexColor = "#000000", double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, hexColor, null, 0, opacity));
        return this;
    }

    /// <summary>Draws a stroked ellipse centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either radius or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas StrokeEllipse(double cx, double cy, double rx, double ry,
        string hexColor = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, null, hexColor, lineWidth, opacity));
        return this;
    }

    /// <summary>Draws a filled and stroked ellipse.</summary>
    /// <exception cref="ArgumentException">Either color argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any radius or <paramref name="lineWidth"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas DrawEllipse(double cx, double cy, double rx, double ry,
        string fillHex = "#FFFFFF", string strokeHex = "#000000", double lineWidth = 1, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ValidateOpacity(opacity);
        Commands.Add(new DrawEllipseCmd(cx, cy, rx, ry, fillHex, strokeHex, lineWidth, opacity));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Arbitrary path
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds an arbitrary vector path built via the <see cref="PathDescriptor"/> fluent API.
    /// Use <c>MoveTo</c>, <c>LineTo</c>, <c>CurveTo</c>, convenience shapes,
    /// and <c>Fill</c> / <c>Stroke</c> paint setters on the descriptor.
    /// </summary>
    /// <example>
    /// <code>
    /// canvas.Path(p => p
    ///     .MoveTo(10, 10)
    ///     .LineTo(90, 10)
    ///     .LineTo(50, 80)
    ///     .Close()
    ///     .Fill(Color.Blue.Medium)
    ///     .Stroke(Color.Blue.Darken2, 1.5));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public VectorCanvas Path(Action<PathDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var pd = new PathDescriptor();
        configure(pd);
        Commands.Add(new DrawPathCmd(pd));
        return this;
    }

    // -----------------------------------------------------------------------
    //  Text
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a single line of text with its <b>baseline</b> at (<paramref name="x"/>,
    /// <paramref name="y"/>) — not its top-left corner, since a text baseline (not a
    /// bounding-box corner) is what lets a label sit flush against a line, chart axis,
    /// or shape it annotates. Renders through a registered custom font when
    /// <paramref name="fontFamily"/> names one (see
    /// <see cref="Helpers.FontFamily.Register(string, string, bool, bool)"/>),
    /// otherwise through the standard-14 family <paramref name="fontFamily"/> resolves
    /// to (Helvetica/Times/Courier — see <see cref="PdfFonts.Resolve"/>; a
    /// <see langword="null"/>/unrecognised name defaults to Helvetica). Does not wrap
    /// or measure-and-fit — use <see cref="MeasureTextWidth"/> to size or centre it
    /// yourself first.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> or <paramref name="hexColor"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize"/> is zero or negative, or <paramref name="opacity"/> is outside [0, 1].</exception>
    public VectorCanvas Text(string text, double x, double y,
        string hexColor = "#000000", double fontSize = 12,
        string? fontFamily = null, bool bold = false, bool italic = false, double opacity = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        ValidateOpacity(opacity);
        Commands.Add(new DrawTextCmd(x, y, text, hexColor, fontSize, fontFamily, bold, italic, opacity));
        return this;
    }

    /// <summary>
    /// Total advance width <paramref name="text"/> would occupy at <paramref name="fontSize"/>
    /// in the same font <see cref="Text"/> would render it in — use this to right-align or
    /// centre a label before placing it, since <see cref="Text"/> itself does not.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize"/> is zero or negative.</exception>
    public static double MeasureTextWidth(string text, double fontSize,
        string? fontFamily = null, bool bold = false, bool italic = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        var font = PdfFonts.ResolveFont(fontFamily, bold, italic);
        return FontMetrics.MeasureWidth(text, fontSize, font, bold, italic);
    }

    // -----------------------------------------------------------------------
    //  Grid helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a rectangular grid of vertical and horizontal lines that fills the canvas area.
    /// </summary>
    /// <param name="cellWidth">Width of each cell in PDF points.</param>
    /// <param name="cellHeight">Height of each cell in PDF points. When <c>null</c> uses <paramref name="cellWidth"/> (square cells).</param>
    /// <param name="hexColor">Line colour. Defaults to light grey.</param>
    /// <param name="lineWidth">Stroke width. Defaults to 0.5 pt.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellWidth"/> is zero or negative.</exception>
    public VectorCanvas Grid(double cellWidth, double? cellHeight = null,
        string hexColor = "#CCCCCC", double lineWidth = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);

        double ch = cellHeight ?? cellWidth;

        // Vertical lines
        double x = cellWidth;
        while (x < AllocatedWidth)
        {
            Line(x, 0, x, AllocatedHeight, hexColor, lineWidth);
            x += cellWidth;
        }

        // Horizontal lines
        double y = ch;
        while (y < AllocatedHeight)
        {
            Line(0, y, AllocatedWidth, y, hexColor, lineWidth);
            y += ch;
        }

        return this;
    }
}
