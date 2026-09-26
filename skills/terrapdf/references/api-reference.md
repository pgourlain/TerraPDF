# TerraPDF API reference

Every public member of TerraPDF 2.2.0 that application code uses. If a member
is not listed here, it does not exist. Do not guess.

## Package and namespaces

```bash
dotnet add package TerraPDF --version 2.2.0
```

Targets `net8.0`, `net9.0`, and `net10.0`. Zero dependencies, no native
binaries, MIT licensed.

```csharp
using TerraPDF.Core;      // Document, descriptors, VectorCanvas, PathDescriptor,
                          // EncryptionOptions, PdfPermissions, ImageFit
using TerraPDF.Helpers;   // Color, FontFamily, PageSize, TextStyle, Unit
using TerraPDF.Infra;     // IContainer, IComponent, IDocument, IDocumentContainer
using TerraPDF.Barcodes;  // QrErrorCorrectionLevel (only if you draw QR codes)
```

---

## Members

Optional parameters show their defaults.

### Entry points

| Member | Signature |
|--------|-----------|
| `Document.Create` | `static DocumentComposer Create(Action<IDocumentContainer> compose)` |
| `Document.Create` | `static DocumentComposer Create(IDocument document)` |
| Output | `void PublishPdf(string path)` · `byte[] PublishPdf()` · `void PublishPdf(Stream stream)` |

### Document level (`IDocumentContainer` / `DocumentComposer`)

| Member | Signature |
|--------|-----------|
| Page | `void Page(Action<PageDescriptor> configure)` |
| TOC | `void TableOfContents(Action<PageDescriptor>? configure = null)` |
| Bookmark | `void Bookmark(string title, int pageNumber)` |
| Bookmark | `void Bookmark(string title, int pageNumber, double top)` |
| Bookmark | `void Bookmark(string title, int pageNumber, string parentTitle)` |
| Bookmark | `void Bookmark(string title, int pageNumber, string parentTitle, double top)` |
| Metadata | `MetadataTitle` · `MetadataAuthor` · `MetadataSubject` · `MetadataKeywords` · `MetadataCreator`, each `(string?)` |
| Encryption | `void Encrypt(EncryptionOptions options)` |

Bookmark page numbers are **1-based**.

### `PageDescriptor`

| Member | Signature |
|--------|-----------|
| Size | `Size((double Width, double Height) size)` · `Size(double widthPt, double heightPt)` · `Size(double width, double height, Unit unit)` |
| Margin | `Margin(double value)` · `Margin(double value, Unit unit)` · `Margin(double top, double right, double bottom, double left)` |
| Margin | `MarginVertical(double)` · `MarginHorizontal(double)` |
| Background | `PageColor(string hexColor)` |
| Text default | `DefaultTextStyle(Func<TextStyle, TextStyle> configure)` |
| Slots | `IContainer Header()` · `IContainer Content()` · `IContainer Footer()` |
| Header scope | `HeaderOnFirstPageOnly()` |

### Container decorators (`IContainer` extensions)

| Group | Members |
|-------|---------|
| Padding | `Padding(v)` · `Padding(v, Unit)` · `PaddingVertical` · `PaddingHorizontal` · `PaddingTop` · `PaddingBottom` · `PaddingLeft` · `PaddingRight` |
| Margin | `Margin(v)` · `Margin(v, Unit)` · `MarginVertical` · `MarginHorizontal` · `MarginTop` · `MarginBottom` · `MarginLeft` · `MarginRight` |
| Background | `Background(string hexColor)` |
| Border | `Border(double lineWidth, string hexColor)` · `Border(double lineWidth = 1)` · `BorderTop/Bottom/Left/Right(double lineWidth, string hexColor = "#000000")` |
| Rounded | `RoundedBorder(double radius = 8, double lineWidth = 1, string hexColor = "#000000")` · `RoundedBox(double radius = 8, string fillHexColor = "#FFFFFF", string borderHexColor = "#000000", double lineWidth = 1)` |
| Align | `AlignLeft()` · `AlignCenter()` · `AlignRight()` · `AlignMiddle()` · `AlignBottom()` |
| Rules | `LineHorizontal(double lineWidth = 1, string hexColor = "#000000")` · `LineVertical(...)` |
| Flow | `ShowIf(bool condition)` · `PageBreak()` |
| Links | `Hyperlink(string url)` · `InternalLink(int pageNumber, double? top = null)` · `Bookmark(string title, string? parentTitle = null)` |
| Compose | `Component(IComponent component)` |

> `ShowIf(false)` discards everything chained after it. **In 2.2.0 it does not**: the chained element still renders. With 2.2.0, use a C# `if` around the item.

### Container elements (chain terminators)

| Element | Signature |
|---------|-----------|
| Text | `TextDescriptor Text(string text)` · `TextDescriptor Text(Action<TextDescriptor> compose)` |
| Headings | `H1(string)` … `H6(string)`, each returning `TextDescriptor` |
| Layout | `Column(Action<ColumnDescriptor>)` · `Row(Action<RowDescriptor>)` · `Table(Action<TableDescriptor>)` |
| Image | `Image(string filePath)` · `Image(string filePath, double width)` · `Image(byte[])` · `Image(byte[], double width)` · `Image(Stream)` · `Image(Stream, double width)` |
| Canvas | `Canvas(double height, Action<VectorCanvas> draw)` |
| Barcode | `Barcode(string data, double? width = null, double height = 40, string hexColor = "#000000", string backgroundHexColor = "#FFFFFF", bool showCaption = false, double quietZoneModules = 10)` |
| QR | `QrCode(string data, double? size = null, QrErrorCorrectionLevel level = QrErrorCorrectionLevel.M, string hexColor = "#000000", string backgroundHexColor = "#FFFFFF", int quietZoneModules = 4)` |

### `ColumnDescriptor` / `RowDescriptor` / `TableDescriptor`

| Type | Members |
|------|---------|
| `ColumnDescriptor` | `Spacing(double)` · `Item()` · `PageBreak()` · `AlignItemsLeft/Center/Right()` |
| `RowDescriptor` | `Spacing(double)` · `AutoItem()` · `RelativeItem(double weight = 1)` · `ConstantItem(double widthPt)` |
| `TableDescriptor` | `ColumnsDefinition(Action<ColumnsDefinitionDescriptor>)` · `HeaderRow(Action<TableRowDescriptor>)` · `Row(Action<TableRowDescriptor>)` |
| `ColumnsDefinitionDescriptor` | `RelativeColumn(double weight = 1)` · `ConstantColumn(double widthPt)` |
| `TableRowDescriptor` | `IContainer Cell(int columnSpan = 1, int rowSpan = 1)` |

### `TextDescriptor` and `SpanDescriptor`

| Type | Members |
|------|---------|
| `TextDescriptor` | `FontSize(double)` · `FontColor(string)` · `FontFamily(string)` · `Bold()` · `SemiBold()` · `Italic()` · `Underline()` · `Strikethrough()` · `LineHeight(double)` · `AlignLeft/Center/Right()` · `Justify()` |
| `TextDescriptor` (composite) | `Span(string text, Func<TextStyle, TextStyle>? styleAction = null)` · `CurrentPageNumber()` · `TotalPages()` |
| `SpanDescriptor` | `Bold()` · `SemiBold()` · `Italic()` · `Underline()` · `Strikethrough()` · `FontSize(double)` · `FontColor(string)` · `FontFamily(string)` |
| `TextStyle` (immutable) | same setters, each returning a new instance; plus `NormalWeight()` · `NormalStyle()` · `NoUnderline()` · `NoStrikethrough()` |

### `VectorCanvas`

Every fill and stroke primitive takes a trailing `opacity` (default `1`).

| Method | Signature |
|--------|-----------|
| Line | `Line(double x1, double y1, double x2, double y2, string hexColor = "#000000", double lineWidth = 1, double opacity = 1, double[]? dashPattern = null, double dashPhase = 0)` |
| Rect | `FillRect(x, y, width, height, hexColor = "#000000", opacity = 1)` |
| Rect | `StrokeRect(x, y, width, height, hexColor = "#000000", lineWidth = 1, opacity = 1, double[]? dashPattern = null, dashPhase = 0)` |
| Rect | `DrawRect(x, y, width, height, fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1, opacity = 1, double[]? dashPattern = null, dashPhase = 0)` |
| Rounded | `FillRoundedRect(x, y, width, height, radius, hexColor = "#000000", opacity = 1)` · `StrokeRoundedRect(..., lineWidth = 1, opacity = 1)` · `DrawRoundedRect(..., fillHex, strokeHex, lineWidth = 1, opacity = 1)` |
| Circle | `FillCircle(cx, cy, radius, hexColor = "#000000", opacity = 1)` · `StrokeCircle(..., lineWidth = 1, opacity = 1)` · `DrawCircle(..., fillHex, strokeHex, lineWidth = 1, opacity = 1)` |
| Ellipse | `FillEllipse(cx, cy, rx, ry, hexColor = "#000000", opacity = 1)` · `StrokeEllipse(...)` · `DrawEllipse(...)` |
| Pie | `FillPie(x, y, width, height, startAngle, sweepAngle, fillHex = "#000000", opacity = 1)` |
| Pie | `StrokePie(x, y, width, height, startAngle, sweepAngle, strokeHex = "#000000", lineWidth = 1, opacity = 1)` |
| Pie | `DrawPie(x, y, width, height, startAngle, sweepAngle, fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1, opacity = 1)` |
| Text | `Text(string text, double x, double y, string hexColor = "#000000", double fontSize = 12, string? fontFamily = null, bool bold = false, bool italic = false, double opacity = 1, double angle = 0)` |
| Measure | `static double MeasureTextWidth(string text, double fontSize, string? fontFamily = null, bool bold = false, bool italic = false)` |
| Image | `Image(byte[] imageData, double x, double y, double width, double height, ImageFit fit = ImageFit.Stretch)` — also `string filePath` and `Stream` overloads |
| Image size | `static (double Width, double Height) GetImageSizeInPoints(byte[] imageData)` |
| Path | `Path(Action<PathDescriptor> configure)` |
| Grid | `Grid(double cellWidth, double? cellHeight = null, string hexColor = "#CCCCCC", double lineWidth = 0.5)` |

> `Grid` is sized at draw time to the canvas's laid-out width. **In 2.2.0 it draws nothing.** With 2.2.0, draw grid lines with `Line`.

### `PathDescriptor`

`MoveTo(x, y)` · `LineTo(x, y)` · `CurveTo(cx1, cy1, cx2, cy2, x, y)` ·
`Close()` · `Rect(x, y, width, height)` · `Ellipse(cx, cy, rx, ry)` ·
`Circle(cx, cy, radius)` · `Arc(cx, cy, rx, ry, startAngle, sweepAngle)` ·
`Sector(cx, cy, rx, ry, startAngle, sweepAngle)` ·
`Polyline(params (double X, double Y)[])` · `Polygon(params (double X, double Y)[])` ·
`Fill(string hexColor)` · `Stroke(string hexColor, double lineWidth = 1)` ·
`UseEvenOddFill()` · `Opacity(double opacity)`

### Enums and helpers

| Type | Values |
|------|--------|
| `Unit` | `Point` `Millimetre` `Centimetre` `Inch` |
| `ImageFit` | `Stretch` `Contain` `Cover` `CoverTopLeft` `CropTopLeft` |
| `EncryptionAlgorithm` | `Aes256` (default) `Aes128` |
| `PdfPermissions` | `None` `Print` `PrintLowResolution` `ModifyContents` `CopyText` `ModifyAnnotations` `FillForms` `ExtractForAccessibility` `AssembleDocument` `All` — combine with `\|` |
| `QrErrorCorrectionLevel` | `L` `M` (default) `Q` `H` |
| `PageSize` | `A0`–`A6` `Letter` `Legal` `Tabloid` `Executive` · `Landscape(size)` |
| `FontFamily` | `static void Register(string familyName, string fontFilePath, bool bold = false, bool italic = false)` — also `byte[]` and `Stream` overloads |

### Reusable pieces

```csharp
class InvoiceHeader : IComponent
{
    public void Compose(IContainer container) =>
        container.Background(Color.Blue.Darken2).Padding(10).Text("INVOICE");
}

class MyReport : IDocument
{
    public void Compose(IDocumentContainer container) =>
        container.Page(page => page.Content().Text("Hello"));
}

Document.Create(new MyReport()).PublishPdf("report.pdf");
```
