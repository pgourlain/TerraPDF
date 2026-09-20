using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Renders all drawing commands accumulated in a <see cref="VectorCanvas"/>
/// onto the PDF page.  The element occupies the full available width and uses
/// an explicit height supplied via <see cref="CanvasElement(VectorCanvas, double)"/>.
/// </summary>
internal sealed class CanvasElement : Element
{
    private readonly VectorCanvas _canvas;
    private readonly double _height;

    internal CanvasElement(VectorCanvas canvas, double height)
    {
        _canvas = canvas;
        _height = height;
    }

    // ── Measure ─────────────────────────────────────────────────────────────

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        // Inject allocated size so Grid() can use it during the configure callback
        _canvas.AllocatedWidth = w;
        _canvas.AllocatedHeight = _height;
        return new ElementSize(w, Math.Min(_height, h));
    }

    // ── Draw ────────────────────────────────────────────────────────────────

    internal override void Draw(DrawingContext ctx)
    {
        _canvas.AllocatedWidth = ctx.Width;
        _canvas.AllocatedHeight = _height;

        foreach (var cmd in _canvas.Commands)
        {
            switch (cmd)
            {
                case VectorCanvas.DrawLineCmd lc:
                    ctx.Page.AddLine(
                        ctx.X + lc.X1, ctx.Y + lc.Y1,
                        ctx.X + lc.X2, ctx.Y + lc.Y2,
                        PdfColor.FromHex(lc.HexColor), lc.LineWidth, lc.Opacity,
                        lc.DashPattern, lc.DashPhase);
                    break;

                case VectorCanvas.DrawRectCmd rc:
                    DrawRect(ctx, rc);
                    break;

                case VectorCanvas.DrawRoundedRectCmd rr:
                    DrawRoundedRect(ctx, rr);
                    break;

                case VectorCanvas.DrawEllipseCmd ec:
                    DrawEllipse(ctx, ec);
                    break;

                case VectorCanvas.DrawPathCmd pc:
                    DrawPath(ctx, pc.Path);
                    break;

                case VectorCanvas.DrawImageCmd ic:
                    DrawImage(ctx, ic);
                    break;

                case VectorCanvas.DrawTextCmd tc:
                    DrawText(ctx, tc);
                    break;
            }
        }
    }

    // ── Primitive renderers ─────────────────────────────────────────────────

    private static void DrawRect(DrawingContext ctx, VectorCanvas.DrawRectCmd rc)
    {
        double ax = ctx.X + rc.X;
        double ay = ctx.Y + rc.Y;

        if (rc.FillHex is not null && rc.StrokeHex is not null)
            ctx.Page.AddRect(ax, ay, rc.W, rc.H,
                PdfColor.FromHex(rc.FillHex), PdfColor.FromHex(rc.StrokeHex), rc.LineWidth, rc.Opacity,
                rc.DashPattern, rc.DashPhase);
        else if (rc.FillHex is not null)
            ctx.Page.AddFilledRect(ax, ay, rc.W, rc.H, PdfColor.FromHex(rc.FillHex), rc.Opacity);
        else if (rc.StrokeHex is not null)
            ctx.Page.AddStrokedRect(ax, ay, rc.W, rc.H, PdfColor.FromHex(rc.StrokeHex), rc.LineWidth, rc.Opacity,
                rc.DashPattern, rc.DashPhase);
    }

    private static void DrawRoundedRect(DrawingContext ctx, VectorCanvas.DrawRoundedRectCmd rr)
    {
        double ax = ctx.X + rr.X;
        double ay = ctx.Y + rr.Y;

        if (rr.FillHex is not null && rr.StrokeHex is not null)
            ctx.Page.AddFilledAndStrokedRoundedRect(ax, ay, rr.W, rr.H, rr.Radius,
                PdfColor.FromHex(rr.FillHex), PdfColor.FromHex(rr.StrokeHex), rr.LineWidth, rr.Opacity);
        else if (rr.FillHex is not null)
            ctx.Page.AddFilledRoundedRect(ax, ay, rr.W, rr.H, rr.Radius,
                PdfColor.FromHex(rr.FillHex), rr.Opacity);
        else if (rr.StrokeHex is not null)
            ctx.Page.AddRoundedRect(ax, ay, rr.W, rr.H, rr.Radius,
                PdfColor.FromHex(rr.StrokeHex), rr.LineWidth, rr.Opacity);
    }

    private static void DrawEllipse(DrawingContext ctx, VectorCanvas.DrawEllipseCmd ec)
    {
        // Translate canvas-relative centre to page-absolute centre
        double cx = ctx.X + ec.Cx;
        double cy = ctx.Y + ec.Cy;

        if (ec.FillHex is not null && ec.StrokeHex is not null)
            ctx.Page.AddFilledAndStrokedEllipse(cx, cy, ec.Rx, ec.Ry,
                PdfColor.FromHex(ec.FillHex), PdfColor.FromHex(ec.StrokeHex), ec.LineWidth, ec.Opacity);
        else if (ec.FillHex is not null)
            ctx.Page.AddFilledEllipse(cx, cy, ec.Rx, ec.Ry, PdfColor.FromHex(ec.FillHex), ec.Opacity);
        else if (ec.StrokeHex is not null)
            ctx.Page.AddStrokedEllipse(cx, cy, ec.Rx, ec.Ry,
                PdfColor.FromHex(ec.StrokeHex), ec.LineWidth, ec.Opacity);
    }

    private static void DrawPath(DrawingContext ctx, PathDescriptor pd)
    {
        if (pd.Commands.Count == 0) return;

        bool opacityScope = ctx.Page.BeginPath(
            pd.FillColor,
            pd.StrokeColor,
            pd.LineWidth,
            pd.EvenOddFill,
            pd.PaintOpacity);

        foreach (var cmd in pd.Commands)
        {
            switch (cmd)
            {
                case MoveToCmd m:
                    ctx.Page.PathMoveTo(ctx.X + m.X, ctx.Y + m.Y);
                    break;
                case LineToCmd l:
                    ctx.Page.PathLineTo(ctx.X + l.X, ctx.Y + l.Y);
                    break;
                case CurveToCmd c:
                    ctx.Page.PathCurveTo(
                        ctx.X + c.Cx1, ctx.Y + c.Cy1,
                        ctx.X + c.Cx2, ctx.Y + c.Cy2,
                        ctx.X + c.X, ctx.Y + c.Y);
                    break;
                case ClosePathCmd:
                    ctx.Page.PathClose();
                    break;
            }
        }

        ctx.Page.EndPath(pd.FillColor, pd.StrokeColor, pd.EvenOddFill, opacityScope);
    }

    private static void DrawText(DrawingContext ctx, VectorCanvas.DrawTextCmd tc)
    {
        var font = PdfFonts.ResolveFont(tc.FontFamily, tc.Bold, tc.Italic);
        var color = PdfColor.FromHex(tc.HexColor);

        bool opacityScope = ctx.Page.BeginOpacityScope(tc.Opacity);
        ctx.Page.BeginTextObject();
        if (font.IsCustom)
        {
            if (tc.Angle == 0)
                ctx.Page.ShowTextAtCustomFont(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, font.Custom!);
            else
                ctx.Page.ShowTextAtRotated(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, tc.Angle, font.Custom!);
        }
        else
        {
            if (tc.Angle == 0)
                ctx.Page.ShowTextAt(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, font.StandardFamily, tc.Bold, tc.Italic);
            else
                ctx.Page.ShowTextAtRotated(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, tc.Angle, font.StandardFamily, tc.Bold, tc.Italic);
        }
        ctx.Page.EndTextObject();
        ctx.Page.EndOpacityScope(opacityScope);
    }

    private static void DrawImage(DrawingContext ctx, VectorCanvas.DrawImageCmd ic)
    {
        // Building the element decodes the whole PNG, and a command is replayed once
        // per page the canvas lands on — a header canvas on a 500-page document would
        // decode 500 times. The decoded element is cached on the command instead; its
        // resource alias is registered per page by PdfPage.DrawImage, so sharing one
        // element across pages is safe.
        ic.Decoded ??= new ImageElement(ic.Data);
        ic.Decoded.DrawAt(ctx.Page, ctx.X + ic.X, ctx.Y + ic.Y, ic.W, ic.H, ic.Fit);
    }
}
