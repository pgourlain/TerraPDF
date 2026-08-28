using TerraPDF.Elements;
using TerraPDF.Infra;

namespace TerraPDF.Core;

/// <summary>
/// Fluent API for adding cells within a table row.
/// Returned by <c>TableDescriptor.Row(â€¦)</c>.
/// </summary>
public sealed class TableRowDescriptor
{
    private readonly Table _element;
    private readonly int _row;

    internal TableRowDescriptor(Table element, int row)
    {
        _element = element;
        _row     = row;
    }

    /// <summary>
    /// Adds the next cell in this row and returns its container for content composition.
    /// The container supports all decoration methods: <c>Background</c>, <c>Padding</c>,
    /// <c>AlignCenter</c>, <c>AlignRight</c>, <c>Border</c>, <c>Text</c>, etc.
    /// <para>
    /// Cells are placed in the first column not already covered by an earlier cell, so a
    /// <paramref name="columnSpan"/> advances the cursor past every column it covers, and a
    /// <paramref name="rowSpan"/> keeps the rows below it from reusing those columns.
    /// A row therefore needs one <c>Cell</c> call per cell it actually contains — no
    /// placeholder calls for columns a span already fills.
    /// </para>
    /// </summary>
    /// <param name="columnSpan">Number of columns the cell covers (values below 1 are treated as 1).</param>
    /// <param name="rowSpan">Number of rows the cell covers (values below 1 are treated as 1).</param>
    public IContainer Cell(int columnSpan = 1, int rowSpan = 1) =>
        _element.PlaceCell(_row, columnSpan, rowSpan).Slot;
}
