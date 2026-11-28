using System.Buffers.Binary;
using TSSE.Core.Editing;
using TSSE.Savegames.Diagnostics;

namespace TSSE.Savegames.Editing;

/// <summary>
/// In-memory, fixed-width scalar edits for already parsed BSII v1/v2/v3 bytes.
/// This does not encode a ScsC container, access files, or validate game semantics.
/// </summary>
public sealed class ExperimentalBinaryScalarEditSession
{
    private const uint Int32Type = 0x25;
    private const uint UInt32Type = 0x27;
    private const uint Int64Type = 0x31;
    private readonly byte[] _originalBytes;
    private readonly EditHistory<byte[]> _history;
    private readonly int _originalBlockCount;

    public ExperimentalBinaryScalarEditSession(ReadOnlySpan<byte> payload)
    {
        _originalBytes = payload.ToArray();
        IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(_originalBytes);
        _originalBlockCount = blocks.Count;
        _history = new EditHistory<byte[]>(_originalBytes, IsValidCandidate);
    }

    public byte[] OriginalBytes => (byte[])_originalBytes.Clone();
    public byte[] PreviewBytes => (byte[])_history.Current.Clone();
    public bool IsDirty => !_originalBytes.AsSpan().SequenceEqual(_history.Current);
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    /// <summary>Reports exact binary value changes made through this session.</summary>
    public IReadOnlyList<ExperimentalBinaryScalarChange> Changes
    {
        get
        {
            IReadOnlyList<ExperimentalBinaryBlock> before = ExperimentalBinarySiiReader.Read(_originalBytes);
            IReadOnlyList<ExperimentalBinaryBlock> after = ExperimentalBinarySiiReader.Read(_history.Current);
            var changes = new List<ExperimentalBinaryScalarChange>();
            for (int blockIndex = 0; blockIndex < before.Count; blockIndex++)
            {
                ExperimentalBinaryBlock oldBlock = before[blockIndex];
                ExperimentalBinaryBlock newBlock = after[blockIndex];
                foreach ((string name, ExperimentalBinaryRawField oldField) in oldBlock.RawFields)
                {
                    ExperimentalBinaryRawField newField = newBlock.RawFields[name];
                    if (!oldField.Bytes.SequenceEqual(newField.Bytes))
                    {
                        changes.Add(new ExperimentalBinaryScalarChange(
                            oldBlock.Type,
                            oldBlock.Identifier!,
                            name,
                            oldField.TypeCode,
                            oldField.Bytes.ToArray(),
                            newField.Bytes.ToArray()));
                    }
                }
            }
            return changes.AsReadOnly();
        }
    }

    public long GetInt64(string blockType, ExperimentalBinaryBlockIdentifier identifier, string fieldName)
    {
        ExperimentalBinaryRawField field = FindField(_history.Current, blockType, identifier, fieldName, Int64Type, sizeof(long));
        return BinaryPrimitives.ReadInt64LittleEndian(field.Bytes.ToArray());
    }

    public uint GetUInt32(string blockType, ExperimentalBinaryBlockIdentifier identifier, string fieldName)
    {
        ExperimentalBinaryRawField field = FindField(_history.Current, blockType, identifier, fieldName, UInt32Type, sizeof(uint));
        return BinaryPrimitives.ReadUInt32LittleEndian(field.Bytes.ToArray());
    }

    public int GetInt32(string blockType, ExperimentalBinaryBlockIdentifier identifier, string fieldName)
    {
        ExperimentalBinaryRawField field = FindField(_history.Current, blockType, identifier, fieldName, Int32Type, sizeof(int));
        return BinaryPrimitives.ReadInt32LittleEndian(field.Bytes.ToArray());
    }

    public void SetInt64(string blockType, ExperimentalBinaryBlockIdentifier identifier, string fieldName, long value)
    {
        byte[] encoded = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(encoded, value);
        Apply([new(blockType, identifier, fieldName, Int64Type, encoded)]);
    }

    public void SetUInt32(string blockType, ExperimentalBinaryBlockIdentifier identifier, string fieldName, uint value)
    {
        byte[] encoded = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Apply([new(blockType, identifier, fieldName, UInt32Type, encoded)]);
    }

    public void SetInt32(string blockType, ExperimentalBinaryBlockIdentifier identifier, string fieldName, int value)
    {
        byte[] encoded = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(encoded, value);
        Apply([new(blockType, identifier, fieldName, Int32Type, encoded)]);
    }

    /// <summary>Applies one logical batch as a single undoable, fully revalidated snapshot.</summary>
    public void Apply(IReadOnlyList<ExperimentalBinaryScalarEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0) return;
        if (edits.Count > 512) throw new ArgumentOutOfRangeException(nameof(edits));

        byte[] candidate = (byte[])_history.Current.Clone();
        var targets = new List<ExperimentalBinaryScalarEdit>(edits.Count);
        foreach (ExperimentalBinaryScalarEdit edit in edits)
        {
            ArgumentNullException.ThrowIfNull(edit);
            if (targets.Any(existing => existing.BlockType == edit.BlockType &&
                existing.FieldName == edit.FieldName && SameIdentifier(existing.Identifier, edit.Identifier)))
                throw new InvalidOperationException("A BSII scalar cannot be edited more than once in one batch.");

            int expectedLength = edit.TypeCode switch
            {
                Int32Type or UInt32Type => sizeof(uint),
                Int64Type => sizeof(long),
                _ => throw new InvalidOperationException("Only existing fixed-width Int32, UInt32, and Int64 scalars can be edited.")
            };
            if (edit.EncodedValue.Length != expectedLength)
                throw new InvalidOperationException("The encoded BSII scalar length does not match its type.");

            ExperimentalBinaryRawField field = FindField(candidate, edit.BlockType, edit.Identifier,
                edit.FieldName, edit.TypeCode, expectedLength);
            edit.EncodedValue.Span.CopyTo(candidate.AsSpan(field.ByteOffset, expectedLength));
            targets.Add(edit);
        }

        if (_history.Current.AsSpan().SequenceEqual(candidate)) return;
        if (!_history.TryApply(candidate))
            throw new InvalidOperationException("The edited BSII payload did not pass complete revalidation.");
    }

    public bool Undo() => _history.Undo();
    public bool Redo() => _history.Redo();

    private static ExperimentalBinaryRawField FindField(
        byte[] payload,
        string blockType,
        ExperimentalBinaryBlockIdentifier identifier,
        string fieldName,
        uint expectedType,
        int expectedLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(payload);
        ExperimentalBinaryBlock[] blockMatches = blocks
            .Where(block => block.Type == blockType && SameIdentifier(block.Identifier, identifier))
            .ToArray();
        if (blockMatches.Length != 1)
            throw new InvalidOperationException("The selected BSII block is missing or ambiguous.");

        if (!blockMatches[0].RawFields.TryGetValue(fieldName, out ExperimentalBinaryRawField? field) || field is null ||
            field.TypeCode != expectedType || field.Bytes.Count != expectedLength)
            throw new InvalidOperationException("The selected BSII scalar is missing, ambiguous, or has an unsupported type.");
        if (field.ByteOffset < 0 || field.ByteOffset > payload.Length - expectedLength)
            throw new InvalidOperationException("The selected BSII scalar offset is outside the payload.");
        return field;
    }

    private bool IsValidCandidate(byte[] candidate)
    {
        try
        {
            return ExperimentalBinarySiiReader.Read(candidate).Count == _originalBlockCount;
        }
        catch (ExperimentalBinarySiiException)
        {
            return false;
        }
    }

    private static bool SameIdentifier(ExperimentalBinaryBlockIdentifier? left, ExperimentalBinaryBlockIdentifier right)
        => left is not null && left.Marker == right.Marker && left.RawParts.SequenceEqual(right.RawParts);
}

public sealed record ExperimentalBinaryScalarChange(
    string BlockType,
    ExperimentalBinaryBlockIdentifier Identifier,
    string FieldName,
    uint TypeCode,
    byte[] OriginalBytes,
    byte[] CurrentBytes);

public sealed class ExperimentalBinaryScalarEdit
{
    private readonly byte[] _encodedValue;

    public ExperimentalBinaryScalarEdit(
        string blockType,
        ExperimentalBinaryBlockIdentifier identifier,
        string fieldName,
        uint typeCode,
        byte[] encodedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(encodedValue);
        BlockType = blockType;
        Identifier = new ExperimentalBinaryBlockIdentifier(identifier.Marker, Array.AsReadOnly(identifier.RawParts.ToArray()));
        FieldName = fieldName;
        TypeCode = typeCode;
        _encodedValue = (byte[])encodedValue.Clone();
    }

    public string BlockType { get; }
    public ExperimentalBinaryBlockIdentifier Identifier { get; }
    public string FieldName { get; }
    public uint TypeCode { get; }
    public ReadOnlyMemory<byte> EncodedValue => _encodedValue;
}
