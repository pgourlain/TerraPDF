# TerraPDF recipes

Complete, compiling patterns for common documents. Each recipe is a
self-contained method. Adapt the data and styling, and keep the structure.
All recipes assume these directives:

```csharp
using System.Globalization;
using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
```

## Invoice

A header with the seller and invoice details, a bill-to box, a line-item table
whose header row repeats on every page, and a right-aligned totals block.

```csharp
public sealed record InvoiceLine(string Description, int Quantity, decimal UnitPrice);

public static byte[] Invoice(string number, DateOnly date, string customer, IReadOnlyList<InvoiceLine> lines)
{
    const string brand = "#1A4A8A";
    const string muted = "#6C757D";
    CultureInfo money = CultureInfo.GetCultureInfo("en-US");
    decimal subtotal = lines.Sum(l => l.Quantity * l.UnitPrice);
    decimal tax = Math.Round(subtotal * 0.10m, 2);

    return Document.Create(doc =>
    {
        doc.MetadataTitle($"Invoice {number}");

        doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(s => s.FontSize(10));

            page.Header().Column(col =>
            {
                col.Spacing(4);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Northwind Traders").Bold().FontSize(16).FontColor(brand);
                        c.Item().Text("42 Harbour St, Seattle, WA 98101").FontColor(muted);
                    });
                    row.AutoItem().Column(c =>
                    {
                        c.Item().AlignRight().Text("INVOICE").Bold().FontSize(22).FontColor(brand);
                        c.Item().AlignRight().Text(t =>
                        {
                            t.Span("No. ").FontColor(muted);
                            t.Span(number).Bold();
                        });
                        c.Item().AlignRight().Text(date.ToString("d MMM yyyy", money));
                    });
                });
                col.Item().PaddingTop(4).LineHorizontal(1.5, brand);
            });

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(10);

                col.Item().Background("#EEF3FA").Padding(8).Column(c =>
                {
                    c.Item().Text("BILL TO").Bold().FontSize(8).FontColor(muted);
                    c.Item().Text(customer).Bold();
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    // HeaderRow repeats automatically when the table spans pages.
                    table.HeaderRow(row =>
                    {
                        row.Cell().Background(brand).Padding(5).Text("Description").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(5).AlignRight().Text("Qty").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(5).AlignRight().Text("Unit price").Bold().FontColor(Color.White);
                        row.Cell().Background(brand).Padding(5).AlignRight().Text("Amount").Bold().FontColor(Color.White);
                    });

                    foreach (InvoiceLine line in lines)
                    {
                        table.Row(row =>
                        {
                            row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(5).Text(line.Description);
                            row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(5).AlignRight()
                               .Text(line.Quantity.ToString(money));
                            row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(5).AlignRight()
                               .Text(line.UnitPrice.ToString("C", money));
                            row.Cell().BorderBottom(0.5, Color.Grey.Lighten2).Padding(5).AlignRight()
                               .Text((line.Quantity * line.UnitPrice).ToString("C", money));
                        });
                    }
                });

                col.Item().AlignRight().Column(totals =>
                {
                    totals.Spacing(2);
                    void Line(string label, decimal amount, bool bold = false) =>
                        totals.Item().Row(row =>
                        {
                            row.ConstantItem(90).Text(label).FontColor(muted);
                            TextDescriptor value = row.ConstantItem(90).AlignRight().Text(amount.ToString("C", money));
                            if (bold) value.Bold().FontSize(12);
                        });

                    Line("Subtotal", subtotal);
                    Line("Tax (10%)", tax);
                    Line("Total", subtotal + tax, bold: true);
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ").FontSize(8);
                t.CurrentPageNumber().FontSize(8);
                t.Span(" of ").FontSize(8);
                t.TotalPages().FontSize(8);
            });
        });
    }).PublishPdf();
}
```

## Report with table of contents and bookmarks

`TableOfContents()` builds a contents page from `H1()`-`H6()`. `Bookmark()`
adds an outline entry whose destination is resolved automatically.

```csharp
public static void ReportWithToc(string path, IReadOnlyList<(string Title, string[] Paragraphs)> chapters)
{
    Document.Create(doc =>
    {
        doc.MetadataTitle("Annual Report");

        void Configure(PageDescriptor page)
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.3));
        }

        doc.TableOfContents(Configure);   // inserted here, before the content

        doc.Page(page =>
        {
            Configure(page);
            page.Content().Column(col =>
            {
                col.Spacing(10);
                for (int i = 0; i < chapters.Count; i++)
                {
                    var (title, paragraphs) = chapters[i];
                    if (i > 0) col.PageBreak();
                    col.Item().Bookmark(title).H1(title).FontColor(Color.Blue.Darken2);
                    foreach (string paragraph in paragraphs)
                        col.Item().Text(paragraph).Justify();
                }
            });
            page.Footer().AlignRight().Text(t => t.CurrentPageNumber());
        });
    }).PublishPdf(path);
}
```

## Reusable components and document classes

Implement `IComponent` for repeated fragments and `IDocument` for whole
documents that are built from data.

```csharp
public sealed class SectionTitle(string text) : IComponent
{
    public void Compose(IContainer container) =>
        container.PaddingTop(8).BorderBottom(1, "#1A4A8A").PaddingBottom(2)
                 .Text(text).Bold().FontSize(13).FontColor("#1A4A8A");
}

public sealed class StatementDocument(string customer, IReadOnlyList<(DateOnly Date, string Memo, decimal Amount)> entries)
    : IDocument
{
    public void Compose(IDocumentContainer container) =>
        container.Page(page =>
        {
            page.Size(PageSize.Letter);
            page.Margin(0.75, Unit.Inch);
            page.Content().Column(col =>
            {
                col.Spacing(6);
                col.Item().Component(new SectionTitle($"Statement for {customer}"));
                foreach (var (date, memo, amount) in entries)
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(80).Text(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                        row.RelativeItem().Text(memo);
                        row.ConstantItem(80).AlignRight().Text(amount.ToString("N2", CultureInfo.InvariantCulture))
                           .FontColor(amount < 0 ? Color.Red.Darken2 : Color.Black);
                    });
                }
            });
        });
}

public static byte[] Statement() =>
    Document.Create(new StatementDocument("Contoso", [(new DateOnly(2026, 9, 1), "Opening balance", 120.50m)])).PublishPdf();
```

## Bar chart on the vector canvas

The canvas does not paginate and does not report its width inside the
callback. Compute the drawable width from the page size and margins. `Text` is
positioned at its baseline.

```csharp
public static void BarChart(string path, IReadOnlyList<(string Label, double Value)> data)
{
    const double margin = 50;
    const double height = 220;
    double width = PageSize.A4.Width - (2 * margin);   // the canvas spans the content width

    Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSize.A4);
        page.Margin(margin);
        page.Content().Column(col =>
        {
            col.Item().Text("Revenue by region").Bold().FontSize(14);
            col.Item().Canvas(height, canvas =>
            {
                double max = data.Max(d => d.Value);
                double plotBottom = height - 20, plotHeight = plotBottom - 10;
                double slot = width / data.Count;

                canvas.Line(0, plotBottom, width, plotBottom, Color.Grey.Medium, 0.75);
                for (int i = 0; i < data.Count; i++)
                {
                    double barHeight = data[i].Value / max * plotHeight;
                    double x = (i * slot) + (slot * 0.15);
                    canvas.FillRect(x, plotBottom - barHeight, slot * 0.7, barHeight, Color.Blue.Medium);

                    string label = data[i].Label;
                    double labelWidth = VectorCanvas.MeasureTextWidth(label, 8);
                    canvas.Text(label, (i * slot) + ((slot - labelWidth) / 2), plotBottom + 12, Color.Grey.Darken2, 8);
                }
            });
        });
    })).PublishPdf(path);
}
```

## Pie chart

```csharp
public static void PieChart(IContainer container, IReadOnlyList<(string Label, double Value, string Color)> slices)
{
    double total = slices.Sum(s => s.Value);
    container.Canvas(160, canvas =>
    {
        double angle = -90;                        // start at twelve o'clock
        foreach (var (_, value, color) in slices)
        {
            double sweep = value / total * 360;    // clockwise
            canvas.DrawPie(0, 0, 150, 150, angle, sweep, color, Color.White, 1);   // bounding box, not centre
            angle += sweep;
        }

        double y = 12;
        foreach (var (label, value, color) in slices)
        {
            canvas.FillRect(170, y - 8, 9, 9, color);
            canvas.Text($"{label} ({value / total:P0})", 184, y, Color.Black, 9);
            y += 16;
        }
    });
}
```

## Event ticket: gradients, dashed outlines, canvas links, and a QR code (2.3+)

Uses canvas members added in 2.3.0. A gradient
banner, a dashed tear-off outline, a painted button with a link laid over it,
a canvas QR code, and a bookmark for the ticket.

```csharp
public static byte[] EventTicket(string attendee, string ticketId, string eventUrl)
{
    const double width = 420, height = 170;

    return Document.Create(doc => doc.Page(page =>
    {
        page.Size(width + 40, height + 40);
        page.Margin(20);
        page.Content().Canvas(height, c =>
        {
            c.Bookmark($"Ticket {ticketId}");

            // Card with a gradient banner; the gradient spans the path's bounding box.
            c.Path(p => p.RoundedRect(0, 0, width, height, 12).Fill("#FFFFFF").Stroke("#1A3C5E", 1));
            // Banner: rounded top corners; the extra Rect squares off the bottom ones.
            c.Path(p => p.RoundedRect(0, 0, width, 46, 12).Rect(0, 23, width, 23)
                         .FillLinearGradient("#1A3C5E", "#2E6DA4"));
            c.Text("TerraConf 2026", 16, 30, "#FFFFFF", 16, bold: true);

            c.Text(attendee, 16, 76, "#212121", 13, bold: true);
            c.Text($"Ticket {ticketId}", 16, 94, "#6C757D", 9);

            // A painted button, then a link over the same rectangle (links draw nothing).
            c.FillRoundedRect(16, 112, 130, 26, 6, "#E87722");
            c.Text("Event details", 40, 129, "#FFFFFF", 10, bold: true);
            c.Link(16, 112, 130, 26, eventUrl);

            // Dashed tear-off line and stub outline.
            c.Line(300, 8, 300, height - 8, "#9AA0A6", 1, dashPattern: [4, 3]);
            c.StrokeRoundedRect(312, 56, 96, 104, 8, "#9AA0A6", 1, dashPattern: [2, 2]);

            // Size includes the quiet zone; null background leaves it transparent.
            c.QrCode($"{eventUrl}?ticket={ticketId}", 316, 60, 88, QrErrorCorrectionLevel.Q, backgroundHex: "#FFFFFF");
        });
    })).PublishPdf();
}
```

## Unicode text with a custom font

The standard fonts only cover WinAnsi (Western European). Register a
TrueType font once at startup; it is subset automatically.

```csharp
public static void UnicodeDocument(string path, string regularTtf, string boldTtf)
{
    FontFamily.Register("Noto", regularTtf);
    FontFamily.Register("Noto", boldTtf, bold: true);

    Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSize.A4);
        page.Margin(40);
        page.DefaultTextStyle(s => s.FontFamily("Noto").FontSize(11));
        page.Content().Column(col =>
        {
            col.Spacing(6);
            col.Item().Text("Здравствуйте • Καλημέρα • नमस्ते").Bold();
            col.Item().Text(t =>
            {
                t.Span("Mixed: ");
                t.Span("Latin standard font").FontFamily("Helvetica");
                t.Span(" → registered font");
            });
        });
    })).PublishPdf(path);
}
```

## Password protection

```csharp
public static void Protected(string path)
{
    Document.Create(doc =>
    {
        doc.Encrypt(new EncryptionOptions               // before any Page(...)
        {
            UserPassword = "open-me",
            OwnerPassword = "full-access",
            Permissions = PdfPermissions.Print | PdfPermissions.CopyText,
        });
        doc.Page(page => page.Content().Text("Confidential"));
    }).PublishPdf(path);
}
```

## Shipping label with barcode and QR code

```csharp
public static byte[] ShippingLabel(string trackingNumber, string address)
{
    return Document.Create(doc => doc.Page(page =>
    {
        page.Size(4, 6, Unit.Inch);
        page.Margin(0.25, Unit.Inch);
        page.Content().Border(1.5, Color.Black).Padding(10).Column(col =>
        {
            col.Spacing(10);
            col.Item().Text("SHIP TO").Bold().FontSize(8);
            col.Item().Text(address).FontSize(13).LineHeight(1.2);
            col.Item().LineHorizontal(1);
            col.Item().Barcode(trackingNumber, height: 60, showCaption: true);   // printable ASCII only
            col.Item().AlignCenter().QrCode($"https://track.example.com/{trackingNumber}", size: 90,
                level: QrErrorCorrectionLevel.Q);
        });
    })).PublishPdf();
}
```

## Tables with spanning cells, images, links, and conditional content

Call `HeaderRow` more than once for multi-level headers; all of them repeat on continuation pages.

```csharp
public static void Catalogue(string path, byte[] logoPng, bool showDiscount)
{
    Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSize.Landscape(PageSize.A4));
        page.Margin(1.5, Unit.Centimetre);
        page.Header().Row(row =>
        {
            row.ConstantItem(60).Image(logoPng, 60);
            row.RelativeItem().AlignMiddle().PaddingLeft(10).Text("Product catalogue").Bold().FontSize(18);
            row.AutoItem().AlignMiddle().Hyperlink("https://terrapdf.com").Text("terrapdf.com").Underline()
               .FontColor(Color.Blue.Medium);
        });

        page.Content().PaddingTop(10).Column(col =>
        {
            // A C# `if` works on every version; ShowIf(false) hides chained content from 2.3.0.
            if (showDiscount)
            {
                col.Item().Background(Color.Green.Lighten5).Padding(6)
                   .Text("10% off all orders this month").Bold().FontColor(Color.Green.Darken2);
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
                table.HeaderRow(row =>
                {
                    row.Cell(rowSpan: 2).Border(0.5).Padding(4).AlignMiddle().Text("Product").Bold();
                    row.Cell(columnSpan: 2).Border(0.5).Padding(4).AlignCenter().Text("Price").Bold();
                });
                table.HeaderRow(row =>
                {
                    row.Cell().Border(0.5).Padding(4).Text("Retail").Bold();
                    row.Cell().Border(0.5).Padding(4).Text("Wholesale").Bold();
                });
                table.Row(row =>
                {
                    row.Cell().Border(0.5).Padding(4).Text("Widget");
                    row.Cell().Border(0.5).Padding(4).Text("$12.00");
                    row.Cell().Border(0.5).Padding(4).Text("$9.50");
                });
            });
        });
    })).PublishPdf(path);
}
```

## ASP.NET Core endpoint

`PublishPdf()` returns the bytes; TerraPDF has no native dependencies, so this
works unchanged in containers and serverless hosts.

```csharp
public static void MapInvoiceEndpoint(Microsoft.AspNetCore.Builder.WebApplication app)
{
    app.MapGet("/invoices/{number}.pdf", (string number) =>
    {
        byte[] pdf = Invoice(number, DateOnly.FromDateTime(DateTime.Today), "Contoso Ltd.",
            [new InvoiceLine("Consulting (hours)", 12, 150m)]);
        return Microsoft.AspNetCore.Http.Results.File(pdf, "application/pdf", $"{number}.pdf");
    });
}
```
