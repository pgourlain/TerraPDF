using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Column sizing policy for <see cref="Table"/>.</summary>
internal enum TableColumnType { Relative, Constant }

internal sealed class TableColumn
{
    internal TableColumnType Type           { get; set; } = TableColumnType.Relative;
    internal double          RelativeWeight { get; set; } = 1;
    internal double          ConstantWidth  { get; set; }
}

internal sealed class TableCell
{
    internal int           Column      { get; set; }   // 1-based
    internal int           Row         { get; set; }   // 1-based
    internal int           ColumnSpan  { get; set; } = 1;
    internal int           RowSpan     { get; set; } = 1;
    internal Container Slot        { get; }       = new();
}

/// <summary>Lays out content in a grid of rows and columns.</summary>
internal sealed class Table : Element
{
    internal List<TableColumn> Columns        { get; } = [];
    internal List<TableCell>   Cells          { get; } = [];
    /// <summary>
    /// Number of leading rows treated as the table header.
    /// These rows are repeated at the top of every continuation page when the
    /// table is split by <see cref="TerraPDF.Core.DocumentComposer"/>.
    /// </summary>
    internal int HeaderRowCount { get; set; }

    /// <summary>
    /// Grid slots already covered by a previously placed cell, keyed by 1-based row.
    /// A cell with a column span occupies several slots in its own row; a cell with a
    /// row span also occupies slots in the rows below it, so the rows that follow skip
    /// over it instead of drawing through it.
    /// </summary>
    private readonly Dictionary<int, HashSet<int>> _occupied = [];

    private bool IsOccupied(int row, int column) =>
        _occupied.TryGetValue(row, out var columns) && columns.Contains(column);

    private void Occupy(int row, int column)
    {
        if (!_occupied.TryGetValue(row, out var columns))
            _occupied[row] = columns = [];
        columns.Add(column);
    }

    /// <summary>
    /// Places a cell in the first free column of <paramref name="row"/> and reserves every
    /// grid slot it covers.  Callers append cells left to right without tracking columns
    /// themselves — spans are what move the cursor, so a span can never be overlapped by
    /// the cell that follows it or by the next row.
    /// </summary>
    internal TableCell PlaceCell(int row, int columnSpan = 1, int rowSpan = 1)
    {
        columnSpan = Math.Max(1, columnSpan);
        rowSpan    = Math.Max(1, rowSpan);

        int column = 1;
        while (IsOccupied(row, column)) column++;

        var cell = new TableCell { Column = column, Row = row, ColumnSpan = columnSpan, RowSpan = rowSpan };
        Cells.Add(cell);

        for (int r = row; r < row + rowSpan; r++)
            for (int c = column; c < column + columnSpan; c++)
                Occupy(r, c);

        return cell;
    }

    // -- Column widths ---------------------------------------------

    internal double[] GetColumnWidths(double available)
    {
        if (Columns.Count == 0) return [];

        var    widths       = new double[Columns.Count];
        double constTotal   = 0;
        double relWeightSum = 0;

        for (int i = 0; i < Columns.Count; i++)
        {
            var col = Columns[i];
            if (col.Type == TableColumnType.Constant)
            {
                widths[i]  = col.ConstantWidth;
                constTotal += col.ConstantWidth;
            }
            else
            {
                relWeightSum += col.RelativeWeight;
            }
        }

        double relSpace = Math.Max(0, available - constTotal);
        for (int i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Type == TableColumnType.Relative)
                widths[i] = relWeightSum > 0
                    ? relSpace * (Columns[i].RelativeWeight / relWeightSum)
                    : 0;
        }

        return widths;
    }

    // -- Row heights -----------------------------------------------

    internal double[] GetRowHeights(double[] colWidths, TextStyle? defaultStyle = null,
        int totalPagesHint = Element.DefaultTotalPagesHint)
    {
        int rowCount = Cells.Count > 0
            ? Cells.Max(c => c.Row + c.RowSpan - 1)
            : 0;
        var heights = new double[rowCount];

        // Pass 1 — single-row cells establish each row's natural height.
        foreach (var cell in Cells)
        {
            if (cell.RowSpan > 1) continue;

            var sz = cell.Slot.Measure(CellWidth(cell, colWidths), double.MaxValue, defaultStyle, totalPagesHint);
            int r  = cell.Row - 1;
            if (r < heights.Length && sz.Height > heights[r])
                heights[r] = sz.Height;
        }

        // Pass 2 — a spanned cell taller than the rows it covers grows them, sharing the
        // shortfall evenly, so its content stays inside the span instead of overflowing
        // into whatever is drawn next.
        foreach (var cell in Cells)
        {
            if (cell.RowSpan <= 1) continue;

            int first = cell.Row - 1;
            if (first < 0 || first >= heights.Length) continue;
            int end = Math.Min(first + cell.RowSpan, heights.Length);   // exclusive

            var sz = cell.Slot.Measure(CellWidth(cell, colWidths), double.MaxValue, defaultStyle, totalPagesHint);

            double covered = 0;
            for (int r = first; r < end; r++) covered += heights[r];

            double deficit = sz.Height - covered;
            if (deficit <= 0) continue;

            double share = deficit / (end - first);
            for (int r = first; r < end; r++) heights[r] += share;
        }

        return heights;
    }

    /// <summary>Total width of the columns <paramref name="cell"/> spans.</summary>
    private static double CellWidth(TableCell cell, double[] colWidths)
    {
        double width = 0;
        for (int c = cell.Column - 1; c < cell.Column - 1 + cell.ColumnSpan && c < colWidths.Length; c++)
            if (c >= 0) width += colWidths[c];
        return width;
    }

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        var colWidths  = GetColumnWidths(w);
        var rowHeights = GetRowHeights(colWidths, defaultStyle, totalPagesHint);
        return new ElementSize(w, rowHeights.Sum());
    }

    // -- Draw (full table) -----------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        var colWidths  = GetColumnWidths(ctx.Width);
        var rowHeights = GetRowHeights(colWidths, totalPagesHint: ctx.TotalPages);
        var allRows    = Enumerable.Range(0, rowHeights.Length).ToList();
        DrawRows(ctx, colWidths, rowHeights, allRows);
    }

    // -- Draw (row subset) -----------------------------------------

    /// <summary>
    /// Draws only the rows identified by <paramref name="rowIndices"/> (0-based),
    /// stacking them from <c>ctx.Y</c> downward.  Used by the page-break splitter
    /// to render header rows + a page-sized slice of data rows per page.
    /// </summary>
    internal void DrawRows(DrawingContext ctx, double[] colWidths, double[] rowHeights,
                           List<int> rowIndices)
    {
        if (rowIndices.Count == 0) return;

        // Pre-compute column X positions
        var colX = new double[colWidths.Length];
        double cx = ctx.X;
        for (int i = 0; i < colWidths.Length; i++) { colX[i] = cx; cx += colWidths[i]; }

        // Map each drawn row index → its Y position (stacked from ctx.Y)
        var rowY = new Dictionary<int, double>(rowIndices.Count);
        double y = ctx.Y;
        foreach (int ri in rowIndices)
        {
            if (ri >= 0 && ri < rowHeights.Length)
            {
                rowY[ri] = y;
                y += rowHeights[ri];
            }
        }

        foreach (var cell in Cells)
        {
            int ri = cell.Row    - 1;  // 0-based
            int ci = cell.Column - 1;  // 0-based

            if (!rowY.TryGetValue(ri, out double cellY)) continue;
            if (ci < 0 || ci >= colWidths.Length || ri >= rowHeights.Length) continue;

            double cellX = colX[ci];
            double cellW = CellWidth(cell, colWidths);

            // Height: sum spanned rows that are present in the drawn set; truncate at page boundary
            double cellH = 0;
            for (int r = ri; r < ri + cell.RowSpan && r < rowHeights.Length; r++)
            {
                if (rowY.ContainsKey(r)) cellH += rowHeights[r];
                else break;  // row not on this page — truncate span
            }
            if (cellH <= 0) cellH = rowHeights[ri];

            cell.Slot.Draw(ctx.At(cellX, cellY, cellW, cellH));
        }
    }
}
