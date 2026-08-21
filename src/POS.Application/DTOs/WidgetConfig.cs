namespace POS.Application.DTOs;

/// <summary>
/// Represents a single widget configuration in a user's dashboard (Req 8.2, 8.3).
/// </summary>
public sealed record WidgetConfig(
    string Type,
    int Position)
{
    /// <summary>Supported widget types.</summary>
    public static class Types
    {
        public const string DailySalesLine = "daily_sales_line";
        public const string TopProductsBar = "top_products_bar";
        public const string SalesByCategoryPie = "sales_by_category_pie";
        public const string TotalSalesNumeric = "total_sales_numeric";

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            DailySalesLine,
            TopProductsBar,
            SalesByCategoryPie,
            TotalSalesNumeric
        };
    }
}
