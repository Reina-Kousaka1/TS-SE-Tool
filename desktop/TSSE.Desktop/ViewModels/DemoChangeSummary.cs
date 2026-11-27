using TSSE.Savegames.Editing;

namespace TSSE.Desktop.ViewModels;

internal static class DemoChangeSummary
{
    public static string Format(IReadOnlyList<SiiFieldChange> changes, string empty) =>
        changes.Count == 0
            ? empty
            : string.Join(Environment.NewLine, changes.Select(change =>
                $"{change.BlockType} : {change.BlockId} / {change.FieldName}: {change.OriginalValue} → {change.CurrentValue}"));
}
