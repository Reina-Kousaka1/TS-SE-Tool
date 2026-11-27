using TSSE.Core.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Editing;

/// <summary>
/// In-memory syntax editing for synthetic fixtures. This does not locate, load,
/// validate for any game version, or write a save file.
/// </summary>
public sealed class SiiEditSession
{
    public const int MaximumPreviewCharacters = 2 * 1024 * 1024;
    public const int MaximumHistoryStates = 16;
    private readonly EditHistory<SiiSyntaxDocument> _history;

    public SiiEditSession(SiiSyntaxDocument initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        if (initial.ToText().Length > MaximumPreviewCharacters)
            throw new ArgumentException("The document exceeds the in-memory preview limit.", nameof(initial));
        _history = new EditHistory<SiiSyntaxDocument>(initial, static doc => doc.ToText().Length <= MaximumPreviewCharacters, MaximumHistoryStates);
    }

    public string PreviewText => _history.Current.ToText();
    public bool IsDirty => _history.IsDirty;
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Applies all requested syntax edits as one history entry, or none if any
    /// target is ambiguous, missing, or unsafe. Exceptions leave history intact.
    /// </summary>
    public void Apply(IReadOnlyList<SiiFieldEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        SiiSyntaxDocument candidate = _history.Current;
        var targets = new HashSet<(string, string?, string)>();
        foreach (SiiFieldEdit edit in edits)
        {
            ArgumentNullException.ThrowIfNull(edit);
            ArgumentNullException.ThrowIfNull(edit.RawValue);
            if (edit.RawValue.Length > MaximumPreviewCharacters)
                throw new InvalidOperationException("The replacement exceeds the in-memory preview limit.");
            if (!targets.Add((edit.BlockType, edit.BlockId, edit.FieldName)))
                throw new ArgumentException("An edit batch cannot target the same field twice.", nameof(edits));
            candidate = candidate.WithFieldValue(edit.BlockType, edit.BlockId, edit.FieldName, edit.RawValue);
            if (candidate.ToText().Length > MaximumPreviewCharacters)
                throw new InvalidOperationException("The document exceeds the in-memory preview limit.");
        }

        if (!string.Equals(candidate.ToText(), _history.Current.ToText(), StringComparison.Ordinal))
        {
            if (!_history.TryApply(candidate))
                throw new InvalidOperationException("The document exceeds the in-memory preview limit.");
        }
    }

    public bool Undo() => _history.Undo();
    public bool Redo() => _history.Redo();
}

/// <summary>An explicit syntax target, not a validated game-specific mapping.</summary>
public sealed record SiiFieldEdit(string BlockType, string? BlockId, string FieldName, string RawValue);
