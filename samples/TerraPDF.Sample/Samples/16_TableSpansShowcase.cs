using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  16. TABLE SPANS & PAGINATION SHOWCASE
//
//  Shows: Cell(columnSpan:, rowSpan:) placement, spanned cells growing the rows
//         they cover, and the three table pagination rules — header-less tables
//         split, a table placed straight into the content slot splits, and a
//         row span is never cut in half by a page break.
//
//         Every table on page 3 crosses a page boundary on purpose: the
//         "Quarterly Detail" table has no header row, and the grouped ledger
//         carries rowSpan cells across the break.
// =============================================================================
internal static class TableSpansShowcase
{
    private const string Brand    = "#243B53";
    private const string Accent   = "#C9531F";
    private const string Muted    = "#6B7A8D";
    private const string Light    = "#F5F7FA";
    private const string GridLine = "#D3DAE3";
    private const string White    = "#FFFFFF";
    private const string Success  = "#1B7A4C";
    private const string SpanFill = "#DCE7F5";
    private const string ZebraBg  = "#EEF2F7";

    internal static void Generate(string path)
    {
        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF — Table Spans & Pagination Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Demonstrates columnSpan / rowSpan and table pagination");
            doc.MetadataKeywords("pdf; table; colspan; rowspan; pagination; TerraPDF");
            doc.MetadataCreator("TerraPDF Sample Generator");

            // ── Page 1-2: the span API ────────────────────────────────────────
            doc.Page(page =>
            {
                Chrome(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(15);

                    col.Item().Text("Cells span columns and rows, and the cursor moves past them")
                       .Bold().FontSize(13).FontColor(Brand);
                    col.Item().Text(
                        "A cell is placed in the first column no earlier cell already covers. A span "
                      + "therefore moves the cursor past everything it covers, so you write one Cell "
                      + "call per cell the row actually contains — never a placeholder for a column a "
                      + "span already fills.").FontColor(Muted);

                    // 1. Column span
                    SectionHeader(col.Item(), "1  Column span");
                    col.Item().Table(t =>
                    {
                        ThreeColumns(t);
                        t.Row(r =>
                        {
                            Cell(r.Cell(columnSpan: 2), "columnSpan: 2 — covers columns 1 and 2", SpanFill);
                            Cell(r.Cell(), "column 3", White);
                        });
                        t.Row(r =>
                        {
                            Cell(r.Cell(), "column 1", ZebraBg);
                            Cell(r.Cell(), "column 2", ZebraBg);
                            Cell(r.Cell(), "column 3", ZebraBg);
                        });
                    });
                    CodeBlock(col.Item(),
                        "row.Cell(columnSpan: 2).Text(\"covers columns 1 and 2\");\n" +
                        "row.Cell().Text(\"column 3\");   // lands in column 3, not column 2");

                    // 2. Row span
                    SectionHeader(col.Item(), "2  Row span");
                    col.Item().Table(t =>
                    {
                        ThreeColumns(t);
                        t.Row(r =>
                        {
                            Cell(r.Cell(rowSpan: 3), "rowSpan: 3\n\ncovers all three rows below", SpanFill);
                            Cell(r.Cell(), "row 1, column 2", White);
                            Cell(r.Cell(), "row 1, column 3", White);
                        });
                        t.Row(r =>
                        {
                            Cell(r.Cell(), "row 2, column 2", ZebraBg);
                            Cell(r.Cell(), "row 2, column 3", ZebraBg);
                        });
                        t.Row(r =>
                        {
                            Cell(r.Cell(), "row 3, column 2", White);
                            Cell(r.Cell(), "row 3, column 3", White);
                        });
                    });
                    CodeBlock(col.Item(),
                        "row.Cell(rowSpan: 3).Text(\"covers all three rows\");\n" +
                        "// the next two rows start at column 2 — column 1 is still held");

                    // 3. Both at once
                    SectionHeader(col.Item(), "3  Both together");
                    col.Item().Table(t =>
                    {
                        ThreeColumns(t);
                        t.Row(r =>
                        {
                            Cell(r.Cell(columnSpan: 2, rowSpan: 2), "columnSpan: 2\nrowSpan: 2", SpanFill);
                            Cell(r.Cell(), "row 1, column 3", White);
                        });
                        t.Row(r => Cell(r.Cell(), "row 2, column 3", ZebraBg));
                        t.Row(r =>
                        {
                            Cell(r.Cell(), "column 1", White);
                            Cell(r.Cell(), "column 2", White);
                            Cell(r.Cell(), "column 3", White);
                        });
                    });

                    // 4. Spanned cell grows its rows
                    SectionHeader(col.Item(), "4  A spanned cell grows the rows it covers");
                    col.Item().Text(
                        "Row heights are measured in two passes. When a spanned cell needs more room "
                      + "than the rows it covers currently give it, the shortfall is shared across "
                      + "them rather than letting the content overflow the table.").FontColor(Muted);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(d => { d.RelativeColumn(2); d.RelativeColumn(1); });
                        t.Row(r =>
                        {
                            Cell(r.Cell(rowSpan: 2),
                                "This spanned cell carries far more text than the two short cells "
                              + "beside it. Both covered rows grow so the whole paragraph stays "
                              + "inside the cell, and nothing spills past the bottom border.", SpanFill);
                            Cell(r.Cell(), "short", White);
                        });
                        t.Row(r => Cell(r.Cell(), "short", ZebraBg));
                    });
                });
            });

            // ── Page 3+: pagination ───────────────────────────────────────────
            doc.Page(page =>
            {
                Chrome(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(15);

                    col.Item().Text("Tables split across pages — and spans travel intact")
                       .Bold().FontSize(13).FontColor(Brand);
                    col.Item().Text(
                        "The ledger below has no header row and is far taller than one page, so it "
                      + "splits between rows. Each account occupies two rows joined by a rowSpan, and "
                      + "those pairs are never divided by a page break: the split falls above or "
                      + "below a pair, never through it.").FontColor(Muted);

                    SectionHeader(col.Item(), "5  Header-less table with row spans across pages");
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(d =>
                        {
                            d.RelativeColumn(2);   // account (spans two rows)
                            d.RelativeColumn(3);   // line
                            d.ConstantColumn(80);  // amount
                        });

                        for (int i = 1; i <= 26; i++)
                        {
                            int n = i;
                            string bg = n % 2 == 0 ? ZebraBg : White;

                            t.Row(r =>
                            {
                                r.Cell(rowSpan: 2).Background(SpanFill).Border(0.5, GridLine).Padding(6)
                                 .Text($"AC-{n:0000}\nGranted {2024 + (n % 3)}")
                                 .FontSize(8).Bold().FontColor(Brand);
                                Cell(r.Cell(), $"Opening balance, account {n}", bg, 8);
                                Amount(r.Cell(), n * 1250.00m, bg);
                            });
                            t.Row(r =>
                            {
                                Cell(r.Cell(), $"Adjustment posted for account {n}", bg, 8);
                                Amount(r.Cell(), n * -184.25m, bg);
                            });
                        }
                    });
                });
            });

            // ── Final page: a table placed straight in the content slot ───────
            doc.Page(page =>
            {
                Chrome(page);

                // No Column wrapper: the table is the content slot's only child.
                // It is taller than the page and still paginates.
                page.Content().PaddingTop(16).Table(t =>
                {
                    t.ColumnsDefinition(d =>
                    {
                        d.RelativeColumn(3);
                        d.RelativeColumn(2);
                        d.ConstantColumn(90);
                    });

                    t.HeaderRow(r =>
                    {
                        r.Cell(columnSpan: 2).Background(Brand).Padding(6)
                         .Text("Quarterly detail — grouped header spans two columns")
                         .Bold().FontSize(9).FontColor(White);
                        r.Cell().Background(Brand).Padding(6).AlignRight()
                         .Text("Total").Bold().FontSize(9).FontColor(White);
                    });

                    for (int i = 1; i <= 34; i++)
                    {
                        int n = i;
                        string bg = n % 2 == 0 ? ZebraBg : White;
                        t.Row(r =>
                        {
                            Cell(r.Cell(), $"Cost centre {n:00} — operations", bg, 8);
                            Cell(r.Cell(), n % 4 == 0 ? "Under review" : "Approved", bg, 8);
                            Amount(r.Cell(), n * 4820.75m, bg);
                        });
                    }
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [16] Table spans & pagination showcase -> {path}");
    }

    // -- Shared helpers -----------------------------------------------------

    private static void Chrome(PageDescriptor page)
    {
        page.Size(PageSize.A4);
        page.Margin(2, Unit.Centimetre);
        page.PageColor(Color.White);
        page.DefaultTextStyle(s => s.FontSize(10));

        page.Header().Column(col =>
        {
            col.Item().Background(Brand).PaddingVertical(10).PaddingHorizontal(14)
               .Text("TerraPDF — Table Spans & Pagination").Bold().FontSize(15).FontColor(White);
            col.Item().Canvas(3, _ => { });
        });

        page.Footer().BorderTop(0.5, GridLine).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("TerraPDF — Table Spans & Pagination Showcase")
               .FontSize(8).FontColor(Muted);
            row.AutoItem().Text(t =>
            {
                t.Span("Page ").FontSize(8).FontColor(Muted);
                t.CurrentPageNumber().FontSize(8).FontColor(Brand).Bold();
                t.Span(" / ").FontSize(8).FontColor(Muted);
                t.TotalPages().FontSize(8).FontColor(Brand).Bold();
            });
        });
    }

    private static void ThreeColumns(TableDescriptor t) =>
        t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn(); });

    private static void Cell(IContainer cell, string text, string background, double fontSize = 9) =>
        cell.Background(background).Border(0.5, GridLine).Padding(6)
            .Text(text).FontSize(fontSize).FontColor(Brand);

    private static void Amount(IContainer cell, decimal value, string background) =>
        cell.Background(background).Border(0.5, GridLine).Padding(6).AlignRight()
            .Text($"{value:N2}").FontSize(8)
            .FontColor(value < 0 ? Accent : Success);

    private static void SectionHeader(IContainer container, string title) =>
        container.PaddingTop(4).BorderBottom(1.5, Brand).PaddingBottom(4)
                 .Text(title).Bold().FontSize(11).FontColor(Brand);

    private static void CodeBlock(IContainer container, string code) =>
        container.RoundedBox(4, "#1E2D3D", "#1E2D3D").Padding(10)
                 .Text(code).FontSize(8).FontColor("#A8D8EA");
}
