using POS.Domain.ValueObjects;

namespace POS.Application.Views;

/// <summary>
/// Summary of a closed shift with variance information (Req 12.14).
/// </summary>
public sealed record ShiftSummary(
    Guid ShiftId,
    string UserName,
    Money OpeningCash,
    Money TotalCashSales,
    Money TotalCashRefunds,
    Money TotalVoidedCashSales,
    Money TotalWithdrawals,
    Money TotalDeposits,
    Money ExpectedCash,
    Money ClosingCash,
    Money Variance,
    string VarianceStatus,
    int TransactionCount,
    int CashReturnCount,
    int VoidedTransactionCount,
    DateTimeOffset OpenedAt,
    DateTimeOffset ClosedAt);
