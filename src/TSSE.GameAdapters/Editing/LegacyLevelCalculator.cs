namespace TSSE.GameAdapters.Editing;

/// <summary>
/// Level thresholds transcribed from the legacy FormMethods.cs. These are
/// unverified against current game versions and do not enable file operations.
/// </summary>
public static class LegacyLevelCalculator
{
    private static readonly uint[] Ets2 =
        [200, 500, 700, 900, 1000, 1100, 1300, 1600, 1700, 2100, 2300, 2600, 2700, 2900, 3000, 3100, 3400, 3700, 4000, 4300, 4600, 4700, 4900, 5200, 5700, 5900, 6000, 6200, 6600, 6800];
    private static readonly uint[] Ats =
        [200, 500, 700, 900, 1100, 1300, 1500, 1700, 1900, 2100, 2300, 2500, 2700, 2900, 3100, 3300, 3500, 3700, 4000, 4300, 4600, 4900, 5200, 5500, 5800, 6100, 6400, 6700, 7000, 7300];

    public static LegacyLevelProgress GetProgress(LegacyLevelTable table, uint experience)
    {
        uint[] steps = Steps(table);
        ulong threshold = 0;
        for (int level = 0; level < steps.Length; level++)
        {
            threshold += steps[level];
            if (experience < threshold) return new(level, threshold);
        }
        ulong remaining = experience - threshold;
        ulong levelsAfterTable = remaining / steps[^1];
        return new(checked(steps.Length + (int)levelsAfterTable),
            threshold + (levelsAfterTable + 1) * steps[^1]);
    }

    /// <summary>Legacy editor sets XP to one less than the next level, for levels 0–150.</summary>
    public static uint ExperienceForLevelEnd(LegacyLevelTable table, int level)
    {
        uint[] steps = Steps(table);
        if (level is < 0 or > 150) throw new ArgumentOutOfRangeException(nameof(level));
        ulong threshold = 0;
        for (int i = 0; i <= level; i++)
            threshold += steps[Math.Min(i, steps.Length - 1)];
        return checked((uint)(threshold - 1));
    }

    private static uint[] Steps(LegacyLevelTable table) => table switch
    {
        LegacyLevelTable.Ets2 => Ets2,
        LegacyLevelTable.Ats => Ats,
        _ => throw new ArgumentOutOfRangeException(nameof(table))
    };
}

public enum LegacyLevelTable { Ets2, Ats }
public sealed record LegacyLevelProgress(int Level, ulong NextLevelExperience);
