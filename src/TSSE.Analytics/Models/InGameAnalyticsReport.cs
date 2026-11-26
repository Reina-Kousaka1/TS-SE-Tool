namespace TSSE.Analytics.Models;

public sealed record InGameAnalyticsReport(
    string GameId,
    string ProfileId,
    string? SaveVersion,
    TimeSpan StartGameTime,
    TimeSpan EndGameTime,
    int RequestedDays,
    int SnapshotCount,
    decimal? SnapshotCashChange,
    decimal? SnapshotBankBalanceChange,
    decimal? SnapshotNetWorthChange,
    InGameAnalyticsStatus Status,
    IReadOnlyList<InGameDaySummary> DailySummaries);
