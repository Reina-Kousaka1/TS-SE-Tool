using TSSE.Analytics.Models;

namespace TSSE.Analytics;

public static class AnalyticsCalculator
{
    /// <summary>Calculates recorded transaction totals and, when unambiguous, local snapshot deltas.</summary>
    /// <remarks>
    /// The requested time window includes both its start and end. Snapshot deltas are omitted unless every
    /// in-window snapshot has a non-empty game ID, stable profile ID, and profile label, and all snapshots
    /// identify the same game/profile pair. The caller must pass transactions for the selected profile because
    /// transaction records do not currently carry profile identity.
    /// </remarks>
    public static AnalyticsWindow Calculate(
        DateTimeOffset end,
        int days,
        IEnumerable<RecordedTransaction> transactions,
        IEnumerable<LocalBalanceSnapshot> snapshots)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(days);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(snapshots);

        DateTimeOffset start = end.AddDays(-days);
        List<RecordedTransaction> windowTransactions = transactions
            .Where(item => item.OccurredAt >= start && item.OccurredAt <= end)
            .ToList();

        decimal income = windowTransactions.Where(item => item.SignedAmount > 0).Sum(item => item.SignedAmount);
        decimal expenses = windowTransactions.Where(item => item.SignedAmount < 0).Sum(item => decimal.Abs(item.SignedAmount));
        decimal? balanceDelta = null;
        decimal? netWorthDelta = null;

        List<LocalBalanceSnapshot> windowSnapshots = snapshots
            .Where(item => item.CapturedAt >= start && item.CapturedAt <= end)
            .OrderBy(item => item.CapturedAt)
            .ToList();

        bool snapshotsIdentifyOneProfile = windowSnapshots.Count > 0
            && windowSnapshots.All(item =>
                !string.IsNullOrWhiteSpace(item.GameId)
                && !string.IsNullOrWhiteSpace(item.ProfileId)
                && !string.IsNullOrWhiteSpace(item.ProfileLabel))
            && windowSnapshots
                .Select(item => (item.GameId, item.ProfileId))
                .Distinct()
                .Take(2)
                .Count() == 1;

        if (windowSnapshots.Count >= 2 && snapshotsIdentifyOneProfile)
        {
            LocalBalanceSnapshot first = windowSnapshots[0];
            LocalBalanceSnapshot last = windowSnapshots[^1];
            bool endpointsAreUnambiguous = windowSnapshots.Count(item => item.CapturedAt == first.CapturedAt) == 1
                && windowSnapshots.Count(item => item.CapturedAt == last.CapturedAt) == 1;

            if (endpointsAreUnambiguous)
            {
                if (first.BankBalance.HasValue && last.BankBalance.HasValue)
                    balanceDelta = last.BankBalance.Value - first.BankBalance.Value;
                if (first.NetWorth.HasValue && last.NetWorth.HasValue)
                    netWorthDelta = last.NetWorth.Value - first.NetWorth.Value;
            }
        }

        return new AnalyticsWindow(
            start,
            end,
            windowTransactions.Count,
            income,
            expenses,
            income - expenses,
            balanceDelta,
            netWorthDelta);
    }
}
