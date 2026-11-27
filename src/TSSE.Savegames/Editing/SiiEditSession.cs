using TSSE.Core.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Editing;

/// <summary>
/// In-memory syntax editing for synthetic fixtures. This does not locate, load,
/// validate for any game version, or write a save file.
/// </summary>
public sealed class SiiEditSession
{
    private readonly EditHistory<SiiSyntaxDocument> _history;

    public SiiEditSession(SiiSyntaxDocument initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _history = new EditHistory<SiiSyntaxDocument>(initial, static _ => true);
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
            if (!targets.Add((edit.BlockType, edit.BlockId, edit.FieldName)))
                throw new ArgumentException("An edit batch cannot target the same field twice.", nameof(edits));
            candidate = candidate.WithFieldValue(edit.BlockType, edit.BlockId, edit.FieldName, edit.RawValue);
        }

        if (!string.Equals(candidate.ToText(), _history.Current.ToText(), StringComparison.Ordinal))
            _history.TryApply(candidate);
    }

    public bool Undo() => _history.Undo();
    public bool Redo() => _history.Redo();
}

/// <summary>An explicit syntax target, not a validated game-specific mapping.</summary>
public sealed record SiiFieldEdit(string BlockType, string? BlockId, string FieldName, string RawValue);
