# Vector Graphics

TerraPDF provides a fluent **Canvas API** for drawing vector graphics directly inside
any layout container. You can render lines, rectangles, circles, ellipses, rounded
rectangles, arbitrary Bézier paths, polygons, arcs, pie sectors, positioned images,
rotated text labels, and grids — all without any external dependencies.

---

## Adding a canvas

Call `container.Canvas(height, draw)` anywhere a container slot is available. The canvas
occupies the full available width and the exact height you specify.

```csharp
container.Canvas(120, c =>
{
    c.FillRect(0, 0, 200, 80, Color.Blue.Lighten4);
    c.StrokeRect(0, 0, 200, 80, Color.Blue.Darken2, 1.5);
    c.Line(0, 40, 200, 40, Color.Blue.Medium, 0.5);
});
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `height` | `double` | Canvas height in PDF points (must be > 0) |
| `draw` | `Action<VectorCanvas>` | Callback that issues drawing commands |

### Coordinate system

All coordinates are in **PDF points** with a **top-left origin** (0, 0) at the
upper-left corner of the canvas element — consistent with TerraPDF's layout
coordinate system.

```
(0,0) ──────────────────────► x
  │
  │      canvas area
  │
  ▼
  y
```

---

## VectorCanvas primitives

Every method returns `this` so calls can be chained.

---

### Lines

```csharp
canvas.Line(x1, y1, x2, y2, hexColor = "#000000", lineWidth = 1, opacity = 1,
    dashPattern = null, dashPhase = 0);
```

Draws a straight line from `(x1, y1)` to `(x2, y2)`.

```csharp
c.Line(0, 20, 300, 20, "#CCCCCC", 0.5);   // thin grey rule
c.Line(0,  0, 150, 80, Color.Red.Medium, 2);
c.Line(0, 60, 300, 60, dashPattern: [8, 4], dashPhase: 2);
```

Dash arrays alternate painted and skipped lengths in points. They must contain
at least one positive finite value and cannot contain negative values. The
pattern is copied when the command is added. Each dashed command restores the
PDF graphics state, so a following line remains solid.

---

### Rectangles

Three variants give you fill-only, stroke-only, or both:

```csharp
// Filled rectangle
canvas.FillRect(x, y, width, height, hexColor = "#000000", opacity = 1);

// Stroked (outline) rectangle
canvas.StrokeRect(x, y, width, height, hexColor = "#000000", lineWidth = 1, opacity = 1);

// Filled + stroked rectangle
canvas.DrawRect(x, y, width, height,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1, opacity = 1);
```

`StrokeRect` and `DrawRect` also accept trailing `dashPattern` and `dashPhase`
arguments with the same semantics as `Line`.

```csharp
c.FillRect  (  0, 0, 80, 50, Color.Blue.Lighten3);
c.StrokeRect(100, 0, 80, 50, Color.Blue.Darken2, 1.5);
c.DrawRect  (200, 0, 80, 50, Color.Blue.Lighten5, Color.Blue.Darken2, 1);
```

---

### Rounded rectangles

Identical variants to the rectangle API, but with a `radius` parameter for
the corner curve:

```csharp
canvas.FillRoundedRect  (x, y, w, h, radius, hexColor = "#000000", opacity = 1);
canvas.StrokeRoundedRect(x, y, w, h, radius, hexColor = "#000000", lineWidth = 1, opacity = 1);
canvas.DrawRoundedRect  (x, y, w, h, radius,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1, opacity = 1);
```

```csharp
c.FillRoundedRect  (  0, 0, 90, 50,  6, "#2E6DA4");      // r=6 badge
c.StrokeRoundedRect(110, 0, 90, 50, 12, "#E87722", 2);   // r=12 outline
c.DrawRoundedRect  (220, 10, 90, 30, 15, "#FFF", "#1A3C5E", 1); // pill
```

`StrokeRoundedRect` and `DrawRoundedRect` also accept trailing `dashPattern`
and `dashPhase` arguments with the same semantics as `Line`.

---

### Circles

```csharp
canvas.FillCircle  (cx, cy, radius, hexColor = "#000000", opacity = 1);
canvas.StrokeCircle(cx, cy, radius, hexColor = "#000000", lineWidth = 1, opacity = 1);
canvas.DrawCircle  (cx, cy, radius,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1, opacity = 1);
```

`(cx, cy)` is the centre of the circle.

```csharp
c.FillCircle  ( 40, 40, 30, Color.Blue.Medium);
c.StrokeCircle(120, 40, 30, Color.Orange.Medium, 2);
c.DrawCircle  (200, 40, 30, Color.Green.Lighten4, Color.Green.Darken2, 1.5);
```

---

### Ellipses

Same variants as circles, but with independent horizontal (`rx`) and vertical
(`ry`) radii:

```csharp
canvas.FillEllipse  (cx, cy, rx, ry, hexColor = "#000000", opacity = 1);
canvas.StrokeEllipse(cx, cy, rx, ry, hexColor = "#000000", lineWidth = 1, opacity = 1);
canvas.DrawEllipse  (cx, cy, rx, ry,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1, opacity = 1);
```

```csharp
c.FillEllipse(100, 40, 80, 30, Color.Purple.Lighten3);   // wide, flat ellipse
c.StrokeEllipse(100, 40, 80, 30, "#1A3C5E", 1.5, dashPattern: [6, 3]);
```

`StrokeEllipse` and `DrawEllipse` (and therefore dashed circles drawn as
ellipses) also accept trailing `dashPattern` and `dashPhase` arguments with the
same semantics as `Line`.

---

### Text

```csharp
canvas.Text(text, x, y,
    hexColor = "#000000", fontSize = 12,
    fontFamily = null, bold = false, italic = false, opacity = 1, angle = 0);
```

Places one line of text with its **baseline** at `(x, y)` — not the top-left
corner every other primitive uses, since a baseline is what lets a label sit
flush against an axis line or the shape it annotates. `fontFamily` renders
through a font registered with `FontFamily.Register(...)` when the name
matches one, otherwise through the standard-14 family it resolves to
(Helvetica/Times/Courier; `null` or unrecognised defaults to Helvetica) —
exactly the same resolution every other TerraPDF text API uses. `Text` draws
one line only: no wrapping, no automatic fitting.

`angle` rotates clockwise in degrees around the baseline point `(x, y)` in the
top-left canvas coordinate system. Negative angles rotate counter-clockwise;
values outside one revolution are accepted unchanged.

```csharp
c.Text("Q1", 10, 100, Color.Grey.Darken2, 9);                       // axis label
c.Text("Revenue", 10, 20, Color.Blue.Darken2, 16, bold: true);      // title
c.Text("Vertical", 220, 80, angle: 90);                            // rotated around its baseline
c.Text("वित्तीय रिपोर्ट", 10, 140, fontFamily: "NotoSansDevanagari"); // via a registered custom font
```

Use `VectorCanvas.MeasureTextWidth(text, fontSize, fontFamily, bold, italic)`
(a `static` method — no canvas instance needed) to measure a label before
placing it, since `Text` doesn't align or fit text itself:

```csharp
string label = "Total: $4,820";
double w = VectorCanvas.MeasureTextWidth(label, 12, bold: true);
c.Text(label, (canvasWidth - w) / 2, 20, bold: true);   // centred
```

---

### Positioned images

```csharp
canvas.Image(path, x, y, width, height, ImageFit.Contain);
canvas.Image(bytes, x, y, width, height, ImageFit.Cover);
canvas.Image(stream, x, y, width, height, ImageFit.Stretch);
```

Coordinates and dimensions are points. `Contain` centres the complete image;
`Cover` centres and clips it; `CoverTopLeft` clips from the top-left;
`CropTopLeft` keeps the natural 96-DPI size and clips right/bottom overflow.
Internally, TerraPDF computes the image transformation matrix and scopes any
clip with `q`/`Q`, so later canvas commands are unaffected. See
[Images](images.md#positioned-images-on-a-vector-canvas) for source ownership
and the complete fit-mode table.

---

### Arcs and pie sectors

`PathDescriptor.Arc` appends an elliptical arc, while `Sector` connects the arc
to its centre and closes the path. Convenience methods render sectors directly:

```csharp
canvas.FillPie(x, y, width, height, startAngle, sweepAngle, fillHex);
canvas.StrokePie(x, y, width, height, startAngle, sweepAngle, strokeHex, lineWidth);
canvas.DrawPie(x, y, width, height, startAngle, sweepAngle, fillHex, strokeHex, lineWidth);

canvas.Path(path => path
    .Arc(100, 60, 80, 40, startAngle: 15, sweepAngle: 220)
    .Stroke(Color.Blue.Darken2, 2));
```

Angles start at the ellipse's right-hand point and increase clockwise. Negative
sweeps run counter-clockwise. A zero sweep adds no path; sweeps whose absolute
value exceeds 360 degrees retain every revolution. Arcs are split into cubic
Bézier segments of at most 90 degrees.

`StrokePie` and `DrawPie` also accept trailing `dashPattern` and `dashPhase`
arguments:

```csharp
canvas.StrokePie(0, 0, 64, 64, -90, 270, "#27AE60", 1.5, dashPattern: [8, 3, 2, 3]);
```

---

### Links and bookmarks

A canvas can place interactive regions at absolute positions. They draw nothing
themselves: paint a button or label first, then lay the link over the same area.

```csharp
canvas.Link(x, y, width, height, url);                        // URI link annotation
canvas.InternalLink(x, y, width, height, pageNumber, top?);   // GoTo link to a page of this document
canvas.Bookmark(title, y = 0, parentTitle = null);            // outline entry for the current page
```

```csharp
c.FillRoundedRect(0, 10, 180, 30, 6, "#2E6DA4");
c.Text("Open the repository", 12, 29, "#FFFFFF", 9, bold: true);
c.Link(0, 10, 180, 30, "https://github.com/sahebansari/TerraPDF");

c.InternalLink(200, 10, 180, 30, pageNumber: 1);            // fits page 1 in the window
c.InternalLink(200, 50, 180, 30, pageNumber: 3, top: 120);  // scrolls to 120 pt from the top

c.Bookmark("Chapter 1");
c.Bookmark("Section 1.1", y: 40, parentTitle: "Chapter 1");
```

- `pageNumber` is the 1-based physical page number. Rendering throws
  `InvalidOperationException` when the document has fewer pages.
- `top` is the distance from the top of the target page; `null` fits the page.
- `Bookmark` points at the page the canvas is drawn on; `y` is relative to the
  canvas and a negative value is clamped to 0. `parentTitle` nests the entry
  under an earlier bookmark with that title. A repeated (title, parent) pair is
  recorded once — useful for canvases repeated on every page. See also
  [Bookmarks](bookmarks.md).

---

### QR codes

`QrCode` draws a QR code (ISO/IEC 18004, byte mode, smallest version that fits)
inside a square, as a single vector path — horizontal runs of dark modules are
merged into rectangles and filled in one operation:

```csharp
canvas.QrCode(data, x, y, size,
    level = QrErrorCorrectionLevel.M, hexColor = "#000000",
    backgroundHex = null, quietZoneModules = 4);
```

```csharp
c.QrCode("https://github.com/sahebansari/TerraPDF", 0, 0, 110, backgroundHex: "#FFFFFF");
c.QrCode("TerraPDF", 130, 0, 110, QrErrorCorrectionLevel.H, hexColor: "#1A3C5E");
c.Link(0, 0, 110, 110, "https://github.com/sahebansari/TerraPDF");   // make it clickable too
```

- `size` includes the quiet zone; the spec asks for 4 modules, lower it only
  when the surrounding area is already light.
- `backgroundHex = null` leaves the square transparent.
- Data too large for a version-40 symbol at the chosen level throws
  `NotSupportedException` when `QrCode` is called, not at render time.

For a QR code placed in the layout flow instead of at a canvas position, use
`container.QrCode(...)`.

---

### Grid helper

Draws a full-canvas grid of evenly spaced vertical and horizontal lines:

```csharp
canvas.Grid(cellWidth, cellHeight = null, hexColor = "#CCCCCC", lineWidth = 0.5);
```

When `cellHeight` is `null`, square cells are used (`cellHeight = cellWidth`).

```csharp
c.Grid(20);                          // 20 × 20 pt square grid, light grey
c.Grid(30, 20, "#E0E0E0", 0.3);     // 30 × 20 pt rectangular grid
```

The grid is sized when the canvas is drawn, so it fills whatever width the
layout gives the canvas, and it draws in call order: shapes added before `Grid`
sit underneath it, and shapes added after sit on top. Only interior lines are
drawn; add a `StrokeRect` for a border.

> **Before 2.3.0:** `Grid` drew nothing, because the canvas size was
> not yet known when the callback ran. On those versions, draw the lines with `Line`.

---

## Arbitrary paths with `PathDescriptor`

`canvas.Path(p => ...)` gives you full control via a fluent `PathDescriptor`. Use it
for triangles, custom polygons, Bézier curves, compound shapes, and shapes with holes.

### Move and line commands

```csharp
canvas.Path(p => p
    .MoveTo(50, 10)     // lift pen, move to (50, 10)
    .LineTo(90, 80)     // line to (90, 80)
    .LineTo(10, 80)     // line to (10, 80)
    .Close()            // close subpath back to (50, 10)
    .Fill(Color.Blue.Lighten3)
    .Stroke(Color.Blue.Darken2, 1.5));
```

### Cubic Bézier curves

```csharp
p.CurveTo(cx1, cy1, cx2, cy2, x, y)
```

Draws a cubic Bézier curve from the current point to `(x, y)`, using `(cx1, cy1)`
and `(cx2, cy2)` as control points.

```csharp
canvas.Path(p => p
    .MoveTo(10, 60)
    .CurveTo(30, 10, 70, 10, 90, 60)   // smooth arch
    .Stroke("#1A3C5E", 2));
```

### Convenience shapes on PathDescriptor

These helpers append subpaths to the current descriptor:

| Method | Description |
|--------|-------------|
| `Rect(x, y, width, height)` | Rectangular subpath |
| `Ellipse(cx, cy, rx, ry)` | Ellipse subpath (cubic Bézier approximation) |
| `Circle(cx, cy, radius)` | Circle subpath |
| `RoundedRect(x, y, width, height, radius)` | Rounded-rectangle subpath; radius clamped to half the shorter side |
| `Arc(cx,cy,rx,ry,start,sweep)` | Append an elliptical arc |
| `Sector(cx,cy,rx,ry,start,sweep)` | Append a closed elliptical sector |
| `Polyline((x,y)[] points)` | Open polyline through 2+ points |
| `Polygon((x,y)[] points)` | Closed polygon through 3+ points |

```csharp
// Star-of-David using two overlapping triangles
canvas.Path(p => p
    .Polygon((50,10), (90,80), (10,80))
    .Fill(Color.Blue.Lighten4)
    .Stroke(Color.Blue.Darken2, 1));

canvas.Path(p => p
    .Polygon((50,80), (10,10), (90,10))
    .Fill(Color.Blue.Lighten4)
    .Stroke(Color.Blue.Darken2, 1));
```

### Paint methods

| Method | Description |
|--------|-------------|
| `.Fill(hexColor)` | Fill the path with the given colour |
| `.Stroke(hexColor, lineWidth = 1)` | Stroke the path outline |
| `.UseEvenOddFill()` | Use even-odd rule (for shapes with holes, e.g. donuts) |
| `.Opacity(opacity)` | Constant alpha for both fill and stroke (1 = opaque, default) |
| `.Dash(pattern, phase = 0)` | Dashed stroke (same semantics as `Line`); needs `.Stroke()` |
| `.FillLinearGradient(fromHex, toHex, angle = 0)` | Linear two-color gradient fill |
| `.FillRadialGradient(centerHex, edgeHex)` | Radial two-color gradient fill |

You can call both `.Fill()` and `.Stroke()` on the same path to fill and stroke it.

```csharp
canvas.Path(p => p
    .RoundedRect(0, 0, 80, 56, 16)
    .Fill("#FFFFFF")
    .Stroke("#1A3C5E", 1.5)
    .Dash([4, 4], phase: 2));
```

### Gradient fills

`FillLinearGradient` and `FillRadialGradient` replace the flat fill with a
two-stop PDF shading (axial type 2 or radial type 3), clipped to the path:

```csharp
// Left to right, blue to white
canvas.Path(p => p.Rect(0, 0, 110, 70).FillLinearGradient("#2E6DA4", "#FFFFFF"));

// Top to bottom, with an outline
canvas.Path(p => p.RoundedRect(125, 0, 110, 70, 14)
    .FillLinearGradient("#E87722", "#1A3C5E", angle: 90)
    .Stroke("#1A3C5E", 1));

// Radial: centre color in the middle, edge color at half the larger side
canvas.Path(p => p.Circle(290, 35, 35).FillRadialGradient("#FFFFFF", "#E87722"));
```

- The gradient spans the path's bounding box. For linear gradients, `angle` is
  in degrees clockwise from left-to-right (0 = left to right, 90 = top to
  bottom); the two end colors land exactly on the box corners.
- Beyond the gradient axis the end colors are extended, so the whole shape is
  painted.
- `.Fill(...)` and the gradient methods replace each other: the last call wins.
- `.Stroke(...)`, `.UseEvenOddFill()`, `.Opacity(...)` and `.Dash(...)` still
  apply. Each gradient becomes one page-level `/Shading` resource.

### Shapes with holes (even-odd fill)

```csharp
// Donut: outer circle + inner circle, even-odd fill creates the hole
canvas.Path(p => p
    .Circle(100, 60, 50)   // outer
    .Circle(100, 60, 25)   // inner (becomes a hole)
    .Fill(Color.Orange.Medium)
    .UseEvenOddFill());
```

---

## Opacity

Every fill/stroke primitive (and `PathDescriptor.Opacity(...)` for arbitrary
paths) takes a trailing `opacity` parameter — 1 (fully opaque) by default, down
to 0 (fully transparent). It sets a real PDF `/ExtGState` constant alpha
(`/ca`/`/CA`), so shapes actually blend with whatever is underneath — not a
lighter version of the same colour:

```csharp
container.Canvas(120, c =>
{
    c.FillRect(0,  0, 100, 100, "#FF0000");              // opaque red
    c.FillRect(50, 50, 100, 100, "#0000FF", opacity: 0.5); // translucent blue overlay
    // the overlap renders as a genuine alpha blend, not a third flat colour
});
```

Omitting `opacity` (or passing `1`) costs nothing — no `/ExtGState` resource
or `gs` operator is emitted at all, so existing calls are unaffected. Distinct
opacity values used anywhere in a document are deduplicated into shared
`/ExtGState` resources, the same way repeated images and fonts are.

This is the building block "highlight fills, tinted overlays, and ghosted
backgrounds" are made of — draw a shape at reduced opacity over existing
content.

---

## All `VectorCanvas` methods at a glance

| Method | Description |
|--------|-------------|
| `Line(x1,y1, x2,y2, color, lw, opacity, dash?, phase)` | Solid or dashed straight line |
| `FillRect(x,y,w,h, color, opacity)` | Filled rectangle |
| `StrokeRect(x,y,w,h, color, lw, opacity, dash?, phase)` | Solid or dashed stroked rectangle |
| `DrawRect(x,y,w,h, fill, stroke, lw, opacity, dash?, phase)` | Filled + solid or dashed rectangle |
| `FillRoundedRect(x,y,w,h, r, color, opacity)` | Filled rounded rectangle |
| `StrokeRoundedRect(x,y,w,h, r, color, lw, opacity, dash?, phase)` | Solid or dashed stroked rounded rectangle |
| `DrawRoundedRect(x,y,w,h, r, fill, stroke, lw, opacity, dash?, phase)` | Filled + solid or dashed rounded rect |
| `FillCircle(cx,cy, r, color, opacity)` | Filled circle |
| `StrokeCircle(cx,cy, r, color, lw, opacity)` | Stroked circle |
| `DrawCircle(cx,cy, r, fill, stroke, lw, opacity)` | Filled + stroked circle |
| `FillEllipse(cx,cy, rx,ry, color, opacity)` | Filled ellipse |
| `StrokeEllipse(cx,cy, rx,ry, color, lw, opacity, dash?, phase)` | Solid or dashed stroked ellipse |
| `DrawEllipse(cx,cy, rx,ry, fill, stroke, lw, opacity, dash?, phase)` | Filled + solid or dashed ellipse |
| `Path(Action<PathDescriptor>)` | Arbitrary path with full Bézier support |
| `Image(source, x,y,w,h, fit)` | Positioned PNG/JPEG from a file, bytes, or stream |
| `FillPie(x,y,w,h,start,sweep,fill,opacity)` | Filled elliptical sector |
| `StrokePie(x,y,w,h,start,sweep,stroke,lw,opacity, dash?, phase)` | Solid or dashed stroked elliptical sector |
| `DrawPie(x,y,w,h,start,sweep,fill,stroke,lw,opacity, dash?, phase)` | Filled and solid or dashed elliptical sector |
| `Text(text, x,y, color, size, family, bold, italic, opacity, angle)` | Rotatable text label, baseline at (x, y) |
| `MeasureTextWidth(text, size, family, bold, italic)` (static) | Advance width for aligning/centring a label |
| `Link(x,y,w,h, url)` | Clickable URI link area |
| `InternalLink(x,y,w,h, page, top?)` | Clickable link to a page of the document |
| `Bookmark(title, y, parentTitle?)` | Outline entry for the current page |
| `QrCode(data, x,y, size, level, color, background?, quietZone)` | Vector QR code |
| `Grid(cw, ch?, color, lw)` | Full-canvas rectangular grid |

---

## All `PathDescriptor` methods at a glance

| Method | Description |
|--------|-------------|
| `MoveTo(x, y)` | Move current point without drawing |
| `LineTo(x, y)` | Straight line to point |
| `CurveTo(cx1,cy1, cx2,cy2, x,y)` | Cubic Bézier curve |
| `Close()` | Close subpath to its start |
| `Rect(x,y,w,h)` | Append rectangular subpath |
| `Ellipse(cx,cy,rx,ry)` | Append ellipse subpath |
| `Circle(cx,cy,r)` | Append circle subpath |
| `RoundedRect(x,y,w,h,r)` | Append rounded-rectangle subpath |
| `Arc(cx,cy,rx,ry,start,sweep)` | Append elliptical arc |
| `Sector(cx,cy,rx,ry,start,sweep)` | Append closed elliptical sector |
| `Polyline(points[])` | Append open polyline (≥ 2 points) |
| `Polygon(points[])` | Append closed polygon (≥ 3 points) |
| `Fill(hexColor)` | Set fill paint |
| `Stroke(hexColor, lw)` | Set stroke paint and width |
| `UseEvenOddFill()` | Switch to even-odd fill rule |
| `Opacity(opacity)` | Constant alpha for fill and stroke (1 = opaque, default) |
| `Dash(pattern, phase)` | Dashed stroke |
| `FillLinearGradient(from, to, angle)` | Linear two-color gradient fill |
| `FillRadialGradient(center, edge)` | Radial two-color gradient fill |

---

## Practical examples

### Simple bar chart

```csharp
(string Label, double Value)[] data = [("Q1", 38), ("Q2", 52), ("Q3", 71), ("Q4", 64)];
double maxValue = 80;
double canvasHeight = 120;
double barWidth = 40;
double gap = 20;

container.Canvas(canvasHeight, c =>
{
    for (int i = 0; i < data.Length; i++)
    {
        double barH = data[i].Value / maxValue * (canvasHeight - 20);
        double x = i * (barWidth + gap);
        double y = canvasHeight - 20 - barH;
        c.FillRoundedRect(x, y, barWidth, barH, 3, Color.Blue.Medium);
    }
    // baseline
    c.Line(0, canvasHeight - 20, data.Length * (barWidth + gap), canvasHeight - 20,
           Color.Grey.Lighten2, 0.5);
});
```

### Donut chart segment

```csharp
// Draw a filled donut wedge using Path + UseEvenOddFill
container.Canvas(160, c =>
{
    // Full outer circle minus inner circle
    c.Path(p => p
        .Circle(80, 80, 60)   // outer radius
        .Circle(80, 80, 35)   // inner radius (hole)
        .Fill(Color.Blue.Lighten3)
        .UseEvenOddFill());

    c.Path(p => p
        .Circle(80, 80, 60)
        .Stroke(Color.White, 2));
});
```

### Sparkline

```csharp
double[] values = [22, 35, 29, 48, 41, 60, 55, 73];
double canvasH = 60;
double stepX = 40;

container.Canvas(canvasH, c =>
{
    // Shaded area under the line
    c.Path(p =>
    {
        p.MoveTo(0, canvasH);
        for (int i = 0; i < values.Length; i++)
            p.LineTo(i * stepX, canvasH - (values[i] / 80.0 * canvasH));
        p.LineTo((values.Length - 1) * stepX, canvasH);
        p.Close();
        p.Fill(Color.Blue.Lighten5);
    });

    // Line on top
    c.Path(p =>
    {
        p.MoveTo(0, canvasH - (values[0] / 80.0 * canvasH));
        for (int i = 1; i < values.Length; i++)
            p.LineTo(i * stepX, canvasH - (values[i] / 80.0 * canvasH));
        p.Stroke(Color.Blue.Darken2, 1.5);
    });
});
```

---

## Samples

Three samples in `samples/TerraPDF.Sample/Samples/` cover the canvas API.

`10_VectorGraphicsShowcase.cs` demonstrates every primitive and three chart
types across three declared pages:

```
Primitives reference sheet (lines, rects, rounded rects, circles, ellipses)
Arbitrary paths — polygons, Bézier curves, even-odd fills with holes
Data visualisation — bar chart, line chart, donut chart
```

`18_CanvasMediaShowcase.cs` covers the canvas *media* APIs across four pages,
two sections each:

```
Page 1  Image fit modes · PNG soft-mask transparency
Page 2  Constant-alpha layering · image sources and natural sizing
Page 3  Dash patterns and phase · elliptical arcs
Page 4  Pie sectors · rotated text labels
```

`19_CanvasExtrasShowcase.cs` covers the canvas *extras* across two pages:

```
Page 1  Dashed ellipses, rounded rects, pies and paths · linear and radial gradients
Page 2  Hyperlinks, in-document links and bookmarks · QR codes
```

All three are generated by the sample runner, which writes **every** sample:

```sh
cd samples/TerraPDF.Sample
dotnet run                  # writes all samples to Desktop\SamplePDF
dotnet run -- ./out         # or to a directory of your choosing
```
