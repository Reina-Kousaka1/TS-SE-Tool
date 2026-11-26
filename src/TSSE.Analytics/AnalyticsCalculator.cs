using TSSE.Analytics.Models;

namespace TSSE.Analytics;

public static class AnalyticsCalculator
{
    private const int MaximumInGameWindowDays = 365;

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

    /// <summary>
    /// Calculates snapshot-derived balance changes over a normalized in-game time window for one exact
    /// game/profile identity. This method does not infer or decode game time and does not create transactions.
    /// </summary>
    public static InGameAnalyticsReport CalculateInGameDays(
        string gameId,
        string profileId,
        TimeSpan endGameTime,
        int days,
        IEnumerable<LocalBalanceSnapshot> snapshots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(days);
        if (days > MaximumInGameWindowDays)
            throw new ArgumentOutOfRangeException(nameof(days), $"The in-game window cannot exceed {MaximumInGameWindowDays} days.");
        if (endGameTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(endGameTime), "Normalized in-game time must not be negative.");

        TimeSpan startGameTime = endGameTime - TimeSpan.FromDays(days);
        List<LocalBalanceSnapshot> suppliedSnapshots = snapshots.ToList();
        if (suppliedSnapshots.Any(snapshot => snapshot is null))
        {
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, suppliedSnapshots.Count, InGameAnalyticsStatus.InvalidSnapshotData);
        }

        var scopedSnapshots = suppliedSnapshots
            .Where(snapshot =>
                string.Equals(snapshot.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(snapshot.ProfileId, profileId, StringComparison.Ordinal))
            .ToList();

        if (scopedSnapshots.Count == 0)
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, 0, InGameAnalyticsStatus.NoSnapshots);

        if (scopedSnapshots.Any(snapshot => snapshot.InGameTime is { } time && time < TimeSpan.Zero))
            throw new ArgumentOutOfRangeException(nameof(snapshots), "Snapshot in-game times must not be negative.");

        List<LocalBalanceSnapshot> clockedSnapshots = scopedSnapshots
            .Where(snapshot => snapshot.InGameTime.HasValue)
            .ToList();
        if (clockedSnapshots.Count == 0)
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, 0, InGameAnalyticsStatus.NoInGameTime);

        List<LocalBalanceSnapshot> windowSnapshots = clockedSnapshots
            .Where(snapshot => snapshot.InGameTime!.Value >= startGameTime && snapshot.InGameTime.Value <= endGameTime)
            .OrderBy(snapshot => snapshot.InGameTime)
            .ThenBy(snapshot => snapshot.CapturedAt)
            .ToList();
        if (windowSnapshots.Count == 0)
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, 0, InGameAnalyticsStatus.NoSnapshotsInWindow);

        DateTimeOffset firstCapture = windowSnapshots.Min(snapshot => snapshot.CapturedAt);
        DateTimeOffset lastCapture = windowSnapshots.Max(snapshot => snapshot.CapturedAt);

        if (scopedSnapshots.Any(snapshot =>
                !snapshot.InGameTime.HasValue
                && snapshot.CapturedAt >= firstCapture
                && snapshot.CapturedAt <= lastCapture))
        {
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.MissingInGameTime);
        }

        if (clockedSnapshots
            .Where(snapshot => snapshot.CapturedAt >= firstCapture && snapshot.CapturedAt <= lastCapture)
            .GroupBy(snapshot => snapshot.CapturedAt)
            .Any(group => group.Count() > 1))
        {
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.AmbiguousGameTime);
        }

        if (HasClockReset(clockedSnapshots, firstCapture, lastCapture))
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.ClockResetDetected);

        if (windowSnapshots.Any(snapshot => string.IsNullOrWhiteSpace(snapshot.SnapshotId)))
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.InvalidSnapshotData);

        if (windowSnapshots
            .GroupBy(snapshot => snapshot.SnapshotId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.InvalidSnapshotData);
        }

        if (windowSnapshots.Count < 2)
            return CreateInGameReport(gameId, profileId, windowSnapshots[0].SaveVersion, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.InsufficientSnapshots);

        if (windowSnapshots.Any(snapshot => string.IsNullOrWhiteSpace(snapshot.SaveVersion)))
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.InvalidSnapshotData);

        string[] saveVersions = windowSnapshots
            .Select(snapshot => snapshot.SaveVersion)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (saveVersions.Length != 1)
            return CreateInGameReport(gameId, profileId, null, startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.SaveVersionBoundary);

        if (windowSnapshots
            .GroupBy(snapshot => snapshot.InGameTime)
            .Any(group => group.Count() > 1))
        {
            return CreateInGameReport(gameId, profileId, saveVersions[0], startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.AmbiguousGameTime);
        }

        LocalBalanceSnapshot first = windowSnapshots[0];
        LocalBalanceSnapshot last = windowSnapshots[^1];
        if (first.InGameTime!.Value > startGameTime || last.InGameTime!.Value < endGameTime)
        {
            return CreateInGameReport(gameId, profileId, saveVersions[0], startGameTime, endGameTime, days, windowSnapshots.Count, InGameAnalyticsStatus.InsufficientCoverage);
        }

        List<InGameDaySummary> dailySummaries = BuildDailySummaries(windowSnapshots, startGameTime, days);
        return new InGameAnalyticsReport(
            gameId,
            profileId,
            saveVersions[0],
            startGameTime,
            endGameTime,
            days,
            windowSnapshots.Count,
            CalculateSnapshotChange(first.Cash, last.Cash),
            CalculateSnapshotChange(first.BankBalance, last.BankBalance),
            CalculateSnapshotChange(first.NetWorth, last.NetWorth),
            InGameAnalyticsStatus.Available,
            dailySummaries.AsReadOnly());
    }

    private static List<InGameDaySummary> BuildDailySummaries(
        IReadOnlyList<LocalBalanceSnapshot> snapshots,
        TimeSpan startGameTime,
        int days)
    {
        var result = new List<InGameDaySummary>(days);
        for (int dayIndex = 0; dayIndex < days; dayIndex++)
        {
            TimeSpan dayStart = startGameTime + TimeSpan.FromDays(dayIndex);
            TimeSpan dayEnd = dayStart + TimeSpan.FromDays(1);
            List<LocalBalanceSnapshot> daySnapshots = snapshots
                .Where(snapshot => snapshot.InGameTime!.Value >= dayStart
                    && snapshot.InGameTime.Value <= dayEnd)
                .OrderBy(snapshot => snapshot.InGameTime)
                .ThenBy(snapshot => snapshot.CapturedAt)
                .ToList();

            LocalBalanceSnapshot? first = daySnapshots.Count >= 2 ? daySnapshots[0] : null;
            LocalBalanceSnapshot? last = daySnapshots.Count >= 2 ? daySnapshots[^1] : null;
            bool unambiguous = first is not null
                && last is not null
                && first.InGameTime == dayStart
                && last.InGameTime == dayEnd
                && daySnapshots.Count(snapshot => snapshot.InGameTime == first.InGameTime) == 1
                && daySnapshots.Count(snapshot => snapshot.InGameTime == last.InGameTime) == 1;

            result.Add(new InGameDaySummary(
                dayIndex + 1,
                dayStart,
                dayEnd,
                daySnapshots.Count,
                unambiguous ? CalculateSnapshotChange(first!.Cash, last!.Cash) : null,
                unambiguous ? CalculateSnapshotChange(first!.BankBalance, last!.BankBalance) : null,
                unambiguous ? CalculateSnapshotChange(first!.NetWorth, last!.NetWorth) : null));
        }

        return result;
    }

    private static bool HasClockReset(
        IReadOnlyList<LocalBalanceSnapshot> snapshots,
        DateTimeOffset firstCapture,
        DateTimeOffset lastCapture)
    {
        List<LocalBalanceSnapshot> timeline = snapshots
            .Where(snapshot => snapshot.CapturedAt <= lastCapture)
            .OrderBy(snapshot => snapshot.CapturedAt)
            .ToList();

        LocalBalanceSnapshot? previous = timeline.LastOrDefault(snapshot => snapshot.CapturedAt < firstCapture);
        foreach (LocalBalanceSnapshot current in timeline.Where(snapshot =>
                     snapshot.CapturedAt >= firstCapture && snapshot.CapturedAt <= lastCapture))
        {
            if (previous?.InGameTime is { } previousTime
                && current.InGameTime is { } currentTime
                && currentTime < previousTime)
            {
                return true;
            }

            previous = current;
        }

        return false;
    }

    private static decimal? CalculateSnapshotChange(decimal? first, decimal? last) =>
        first.HasValue && last.HasValue ? last.Value - first.Value : null;

    private static InGameAnalyticsReport CreateInGameReport(
        string gameId,
        string profileId,
        string? saveVersion,
        TimeSpan startGameTime,
        TimeSpan endGameTime,
        int days,
        int snapshotCount,
        InGameAnalyticsStatus status) =>
        new(
            gameId,
            profileId,
            saveVersion,
            startGameTime,
            endGameTime,
            days,
            snapshotCount,
            null,
            null,
            null,
            status,
            Array.Empty<InGameDaySummary>());
}
