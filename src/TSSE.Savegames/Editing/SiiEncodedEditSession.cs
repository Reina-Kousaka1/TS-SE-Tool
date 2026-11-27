using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Editing;

/// <summary>
/// Byte-preserving, in-memory editing for synthetic plain-text SII fixtures.
/// It does not read or write files or validate game-specific semantics.
/// </summary>
public sealed class SiiEncodedEditSession
{
    private readonly SiiEncodedText _encoding;
    private readonly SiiEditSession _session;

    public SiiEncodedEditSession(ReadOnlySpan<byte> bytes)
    {
        _encoding = SiiEncodedText.Parse(bytes);
        _session = new SiiEditSession(_encoding.Syntax, candidate => _ = _encoding.Encode(candidate));
    }

    public byte[] PreviewBytes => _encoding.Encode(_session.CurrentSyntax);
    public string PreviewText => _session.PreviewText;
    public IReadOnlyList<SiiFieldChange> Changes => _session.Changes;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;
    public void Apply(IReadOnlyList<SiiFieldEdit> edits) => _session.Apply(edits);
    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();
}
