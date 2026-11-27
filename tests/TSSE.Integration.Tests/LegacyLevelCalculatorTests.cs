using TSSE.GameAdapters.Editing;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyLevelCalculatorTests
{
    [Theory]
    [InlineData(LegacyLevelTable.Ets2, 0u, 0, 200ul)]
    [InlineData(LegacyLevelTable.Ets2, 199u, 0, 200ul)]
    [InlineData(LegacyLevelTable.Ets2, 200u, 1, 700ul)]
    [InlineData(LegacyLevelTable.Ets2, 3300u, 5, 4400ul)]
    [InlineData(LegacyLevelTable.Ats, 3300u, 4, 3400ul)]
    public void ProgressUsesLegacyGameSpecificThresholds(LegacyLevelTable table, uint xp, int level, ulong next)
    {
        Assert.Equal(new LegacyLevelProgress(level, next), LegacyLevelCalculator.GetProgress(table, xp));
    }

    [Theory]
    [InlineData(LegacyLevelTable.Ets2)]
    [InlineData(LegacyLevelTable.Ats)]
    public void LevelEndRoundTripsAndLargeXpDoesNotOverflow(LegacyLevelTable table)
    {
        foreach (int level in new[] { 0, 1, 29, 30, 150 })
        {
            uint xp = LegacyLevelCalculator.ExperienceForLevelEnd(table, level);
            Assert.Equal(level, LegacyLevelCalculator.GetProgress(table, xp).Level);
            Assert.Equal(level + 1, LegacyLevelCalculator.GetProgress(table, xp + 1).Level);
        }
        Assert.True(LegacyLevelCalculator.GetProgress(table, uint.MaxValue).NextLevelExperience > uint.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyLevelCalculator.ExperienceForLevelEnd(table, 151));
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyLevelCalculator.ExperienceForLevelEnd(table, -1));
    }
}
