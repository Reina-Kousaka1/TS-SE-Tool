namespace TSSE.Analytics.Models;

public sealed record InGameDaySummary(
    int GameDayIndex,
    TimeSpan StartGameTime,
    TimeSpan EndGameTime,
    int SnapshotCount,
    decimal? SnapshotCashChange,
    decimal? SnapshotBankBalanceChange,
    decimal? SnapshotNetWorthChange);
