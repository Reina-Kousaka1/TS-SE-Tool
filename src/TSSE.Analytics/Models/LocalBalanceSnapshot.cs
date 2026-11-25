namespace TSSE.Analytics.Models;

public sealed record LocalBalanceSnapshot(
    string SnapshotId,
    string GameId,
    string ProfileLabel,
    string SaveVersion,
    DateTimeOffset CapturedAt,
    decimal? Cash,
    decimal? BankBalance,
    decimal? NetWorth);
