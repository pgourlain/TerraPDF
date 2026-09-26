---
name: terrapdf
description: Write C#/.NET code that generates PDF documents with the TerraPDF library (NuGet package TerraPDF) - invoices, reports, receipts, statements, labels, certificates, tables, charts, images, barcodes, QR codes, custom fonts, bookmarks, table of contents, and encryption. Use when creating, editing, reviewing, or debugging code that uses TerraPDF, when a .NET project needs PDF output, or when wiring TerraPDF into an AI agent as a tool.
---

# TerraPDF

TerraPDF is a zero-dependency, pure C# PDF 1.7 generator for .NET 8, 9, and 10
(MIT). Documents are composed with a fluent layout API, not HTML.

## Workflow

1. Ensure the package is referenced: `dotnet add package TerraPDF`.
2. Start from the skeleton below; look up any member you are unsure of in
   [references/api-reference.md](references/api-reference.md). **Never invent
   API members.** If something is not in the reference, it does not exist.
3. For a known document type, adapt a recipe from
   [references/recipes.md](references/recipes.md) (invoice, report with TOC,
   long tables, reusable components, charts, Unicode fonts, encryption,
   barcodes and labels, ASP.NET Core endpoint).
4. `dotnet build`, then run the code and confirm a non-empty PDF was written.
   If it does not compile, check
   [references/troubleshooting.md](references/troubleshooting.md) before
   changing anything else.

## Skeleton

```csharp
using TerraPDF.Core;      // Document, descriptors, VectorCanvas, EncryptionOptions
using TerraPDF.Helpers;   // Color, PageSize, Unit, TextStyle, FontFamily
using TerraPDF.Infra;     // IContainer, IComponent, IDocument (only when implementing them)

Document.Create(doc =>
{
    doc.MetadataTitle("Quarterly Report");

    doc.Page(page =>
    {
        page.Size(PageSize.A4);
        page.Margin(2, Unit.Centimetre);
        page.DefaultTextStyle(s => s.FontSize(10));

        page.Header().Text("Quarterly Report").Bold().FontSize(18).FontColor(Color.Blue.Darken2);

        page.Content().PaddingVertical(12).Column(col =>
        {
            col.Spacing(8);
            col.Item().Text("Revenue grew 18% quarter over quarter.");
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                table.HeaderRow(row =>
                {
                    row.Cell().Text("Product").Bold();
                    row.Cell().AlignRight().Text("Revenue").Bold();
                });
                table.Row(row =>
                {
                    row.Cell().Text("Widgets");
                    row.Cell().AlignRight().Text("$482,000");
                });
            });
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        });
    });
}).PublishPdf("report.pdf");
```

Mental model: **Document → Page → slot (`Header` / `Content` / `Footer`) →
decorators (chain) → exactly one element (ends the chain).**

## Rules that prevent almost every failure

1. **A container holds exactly one child.** A second element call on the same
   container silently replaces the first. Stack content with `Column`
   (`col.Item()`), place side by side with `Row`, and use `Table` for grids.
2. **`PublishPdf` is the only output call:** `PublishPdf(path)`,
   `byte[] PublishPdf()`, or `PublishPdf(stream)`. There is no `Save`,
   `Generate`, `GeneratePdf`, or `Render`.
3. **Decorators chain; elements terminate.** `Padding`, `Margin`,
   `Background`, `Border*`, `Align*`, `Hyperlink`, and `Bookmark`
   return `IContainer`. `Text`, `H1`-`H6`, `Column`, `Row`, `Table`, `Image`,
   `Canvas`, `Barcode`, and `QrCode` end the chain. `.Bold()` and similar calls
   after `Text(...)` style the text; they are not decorators.
4. **Colours are hex strings.** `Color.*` constants are plain strings, and
   `"#1A4A8A"` works anywhere. Only `Red`, `Blue`, `Green`, and `Grey` have
   all ten shades (`Lighten5`...`Darken4`). Most other families have only
   `Medium` and `Darken2`. When unsure, use a hex literal.
5. **Page sizes are tuples:** `page.Size(PageSize.A4)`,
   `page.Size(PageSize.Landscape(PageSize.A4))`, or
   `page.Size(210, 297, Unit.Millimetre)`.
6. **Row items:** `row.RelativeItem(weight)`, `row.ConstantItem(points)`, and
   `row.AutoItem()`. `Item()` exists only on `Column`.
7. **Standard fonts are Latin-only (WinAnsi).** Cyrillic, Greek, Devanagari,
   CJK, `→`, and `−` render as `?` unless you call
   `FontFamily.Register("Name", "file.ttf")` and use `.FontFamily("Name")`.
   Only TrueType-outline `.ttf` files work, not CFF `.otf` or `.ttc`.
8. **Call `doc.Encrypt(...)` before `doc.Page(...)`.** It uses AES-256 by default.
9. **A table of contents collects `H1()`-`H6()` only.** Plain `Text()` is
   never listed.
10. **The canvas does not paginate or know its width.** `Canvas(height, draw)`
    records commands immediately, so compute widths yourself. `Text` is
    anchored at the baseline. Pies take a bounding box; `Arc`/`Sector` take a
    centre and radii. Angles start at 3 o'clock and run clockwise.
11. **Prefer a C# `if` for conditional content.** It works on every version.
    In TerraPDF 2.2.0, `ShowIf(false)` is overridden by the element chained
    after it, and `Grid()` draws nothing. Both are fixed in later releases.
12. **Units default to points** (72 pt = 1 inch). Overloads that take a
    `Unit` accept `Point`, `Millimetre`, `Centimetre`, and `Inch`.

## Before handing back code

- It builds, with all needed `using` directives and only real API members.
- It runs and writes a non-empty PDF.
- Nothing is silently dropped: every container has one child.
- Any non-Latin text has a registered font.
- Canvas drawings fit inside the declared height.

## PDFs from an AI agent at runtime

If the task is to let an LLM or agent produce PDFs at runtime rather than
writing fixed C#, do not have the model generate C#. Use the `TerraPDF.Agents`
package, whose tools take a validated JSON document and plug into Microsoft
Agent Framework, Semantic Kernel, and `IChatClient`. MCP clients can use the
`TerraPDF.Mcp` server. See
[references/agent-tools.md](references/agent-tools.md).
