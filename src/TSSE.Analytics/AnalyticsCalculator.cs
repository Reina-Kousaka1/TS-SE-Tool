using TSSE.Analytics.Models;

namespace TSSE.Analytics;

public static class AnalyticsCalculator
{
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

        if (windowSnapshots.Count >= 2)
        {
            LocalBalanceSnapshot first = windowSnapshots[0];
            LocalBalanceSnapshot last = windowSnapshots[^1];
            if (first.BankBalance.HasValue && last.BankBalance.HasValue)
                balanceDelta = last.BankBalance.Value - first.BankBalance.Value;
            if (first.NetWorth.HasValue && last.NetWorth.HasValue)
                netWorthDelta = last.NetWorth.Value - first.NetWorth.Value;
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
