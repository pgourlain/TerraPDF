# Troubleshooting TerraPDF code

## Mistakes that break generated code

| Wrong | Right | Why |
|-------|-------|-----|
| Two `.Text(...)` on one container | Wrap in `Column`, use `col.Item()` | A container holds one child; the second replaces the first |
| `.Save(path)` / `.Generate()` | `.PublishPdf(path)` | No other output method exists |
| `Color.Orange.Lighten3` | `Color.Orange.Medium` | Most families only have `Medium` and `Darken2` |
| `Color.FromHex("#FF0000")` | `"#FF0000"` | Colour parameters are plain strings |
| `page.Size(PageSize.A4.Landscape)` | `page.Size(PageSize.Landscape(PageSize.A4))` | `PageSize` members are tuples, not objects |
| `canvas.Text(...)` positioned by top-left | Pass the **baseline** y | `Text` is the one baseline-anchored primitive |
| `FillPie(cx, cy, rx, ry, ...)` | `FillPie(x, y, width, height, ...)` | Pies take a bounding box; only `Arc`/`Sector` take centre + radii |
| Cyrillic/Devanagari in a standard font | `FontFamily.Register(...)` then `.FontFamily("Name")` | Standard-14 fonts are WinAnsiEncoding only |
| `doc.Encrypt(...)` after `doc.Page(...)` | Call `Encrypt` first | Encryption must be configured before pages |
| `Text("Chapter 1")` expecting a TOC entry | `H1("Chapter 1")` | Only `H1`–`H6` are collected |
| `col.Item().ShowIf(cond).Text(...)` | `if (cond) col.Item().Text(...);` | In TerraPDF 2.2.0, `ShowIf(false)` is overwritten by the element chained after it, so the content still renders (fixed in later versions) |
| `canvas.Grid(20)` inside `Canvas(...)` | Draw grid lines with `Line(...)` using widths you computed | In TerraPDF 2.2.0 the draw callback runs before layout, so `Grid` sees a zero-sized canvas and draws nothing (fixed in later versions) |
| `row.Item()` | `row.RelativeItem()` / `ConstantItem()` / `AutoItem()` | `Item()` exists on `ColumnDescriptor` only |

## Compiler errors

| Error | Cause | Fix |
|---|---|---|
| `CS0117: 'Color.X' does not contain a definition for 'Lighten3'` | That shade does not exist for this colour family | Use `Medium`/`Darken2`, a full-palette family (`Red`, `Blue`, `Green`, `Grey`), or a hex string |
| `CS1061: 'IContainer' does not contain a definition for 'Text'` | Missing `using TerraPDF.Core;` (the fluent API is extension methods) | Add the using |
| `CS0246: 'PageSize' / 'Color' / 'Unit' could not be found` | Missing `using TerraPDF.Helpers;` | Add the using |
| `CS0246: 'IComponent' / 'IDocument' could not be found` | Missing `using TerraPDF.Infra;` | Add the using |
| `CS0246: 'QrErrorCorrectionLevel' could not be found` | Missing `using TerraPDF.Barcodes;` | Add the using |
| `CS1061: 'RowDescriptor' does not contain a definition for 'Item'` | `Item()` is Column-only | `RelativeItem()`, `ConstantItem(w)`, or `AutoItem()` |
| `CS1061: 'TextDescriptor' does not contain a definition for 'Padding'` | Decorators go **before** the element | `container.Padding(8).Text("x")`, not `container.Text("x").Padding(8)` |
| `CS1061: '...' does not contain a definition for 'GeneratePdf' / 'Save'` | Wrong output method | `PublishPdf(path)`, `PublishPdf()` or `PublishPdf(stream)` |
| `CS1503: cannot convert from 'string' to 'Unit'` in `Margin` | `Margin(2, "cm")` | `Margin(2, Unit.Centimetre)` |

## Runtime symptoms

| Symptom | Cause | Fix |
|---|---|---|
| Only the last of several items appears | Several elements assigned to one container | Put them in a `Column` |
| `?` instead of characters | Text outside WinAnsi in a standard font | `FontFamily.Register(...)` a `.ttf` that covers the script, then `.FontFamily(name)` |
| `NotSupportedException` on `FontFamily.Register` | CFF-flavoured `.otf` or `.ttc` collection | Use a TrueType-outline `.ttf` |
| `NotSupportedException` from `Barcode` | Code128 input outside printable ASCII | Strip or transliterate, or use `QrCode` (UTF-8) |
| `NotSupportedException` from `QrCode` | Data too long for any QR version at that error-correction level | Shorten, or lower the level (`H` → `M` → `L`) |
| Canvas drawing clipped or overlapping following content | Drawing exceeds the canvas height; canvases never paginate | Increase `Canvas(height, ...)` or scale the drawing |
| Blank area where a `PageBreak()` was expected to move content | A break at the very top of a page is skipped by design | Nothing to fix |
| Empty table-of-contents page | No `H1()`-`H6()` headings | Use heading methods for section titles |
| TOC or bookmark page numbers off by the TOC page | Manual `doc.Bookmark(title, page)` numbers are absolute, 1-based | Prefer anchored `container.Bookmark("Title")`, which resolves automatically |
| Image stretched across the page | `Image(path)` fills the available width | `Image(path, widthPt)`, wrapped in `AlignCenter()` or `AlignRight()` to position it |
