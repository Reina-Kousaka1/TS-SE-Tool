namespace TSSE.Analytics.Models;

public sealed record AnalyticsWindow(
    DateTimeOffset Start,
    DateTimeOffset End,
    int RecordedTransactionCount,
    decimal RecordedIncome,
    decimal RecordedExpenses,
    decimal RecordedNet,
    decimal? BalanceDelta,
    decimal? NetWorthDelta);
