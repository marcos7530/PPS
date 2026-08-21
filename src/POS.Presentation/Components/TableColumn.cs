using Microsoft.AspNetCore.Components;

namespace POS.Presentation.Components;

/// <summary>
/// Defines a column for ResponsiveTable:
/// a header string and a render function that produces a cell's content.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public sealed class TableColumn<TItem>
{
    /// <summary>
    /// Column header text displayed in the table header row and as the card field label on mobile.
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// Function that produces a <see cref="RenderFragment"/> for a given row item.
    /// </summary>
    public Func<TItem, RenderFragment> CellTemplate { get; }

    public TableColumn(string header, Func<TItem, RenderFragment> cellTemplate)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        CellTemplate = cellTemplate ?? throw new ArgumentNullException(nameof(cellTemplate));
    }
}

/// <summary>
/// Static factory methods for creating TableColumn instances.
/// Separated from the generic class to avoid CA1000 (static members in generic types).
/// </summary>
public static class TableColumnFactory
{
    /// <summary>
    /// Convenience factory for simple text-based cells.
    /// </summary>
    public static TableColumn<TItem> Text<TItem>(string header, Func<TItem, string?> selector)
    {
        return new TableColumn<TItem>(header, item => builder =>
        {
            builder.AddContent(0, selector(item));
        });
    }
}
