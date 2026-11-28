using System.Buffers.Binary;
using TSSE.Core.Editing;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Editing;

namespace TSSE.GameAdapters.Editing;

/// <summary>
/// In-memory profile scalar preview for explicitly selected BSII blocks with an existing bank link.
/// It does not discover profiles, verify container authentication, encode ScsC, or validate game semantics.
/// </summary>
public sealed class LegacyBinaryProfilePreview
{
    private readonly ExperimentalBinaryScalarEditSession _session;
    private readonly ExperimentalBinaryBlockIdentifier _economyId;
    private readonly ExperimentalBinaryBlockIdentifier _bankId;
    private readonly bool _hasCoachExperience;
    private readonly uint? _coachExperienceTypeCode;

    public LegacyBinaryProfilePreview(ReadOnlySpan<byte> binaryPayload, ExperimentalBinaryBlockIdentifier economyId)
    {
        ArgumentNullException.ThrowIfNull(economyId);
        _economyId = SnapshotIdentifier(economyId);
        _session = new ExperimentalBinaryScalarEditSession(binaryPayload);

        IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(binaryPayload);
        ExperimentalBinaryBlock economy = FindBlock(blocks, "economy", _economyId);
        if (!economy.RawFields.TryGetValue("bank", out ExperimentalBinaryRawField? bankField) || bankField is null ||
            !economy.ReferenceFields.TryGetValue("bank", out ExperimentalBinaryReference? bankReference) || bankReference is null ||
            bankReference.IsNullMarker || bankField.TypeCode is not (0x39 or 0x3b or 0x3d))
            throw new InvalidOperationException("The selected economy block has no supported unique bank reference.");

        ExperimentalBinaryBlock[] bankMatches = blocks
            .Where(block => block.Type == "bank" && SameIdentifier(block.Identifier, bankReference))
            .ToArray();
        if (bankMatches.Length != 1)
            throw new InvalidOperationException("The economy bank reference is missing or ambiguous.");
        _bankId = SnapshotIdentifier(bankMatches[0].Identifier!);
        _ = RequireField(economy, "experience_points", 0x27, sizeof(uint));
        _ = RequireField(bankMatches[0], "money_account", 0x31, sizeof(long));

        if (economy.RawFields.TryGetValue("bus_experience_points", out ExperimentalBinaryRawField? coachField))
        {
            if (coachField is null || coachField.TypeCode is not (0x25 or 0x27) || coachField.Bytes.Count != sizeof(uint))
                throw new InvalidOperationException("The existing Coaches experience field has an unsupported type or width.");
            _hasCoachExperience = true;
            _coachExperienceTypeCode = coachField.TypeCode;
        }

        _ = Current;
    }

    public ProfileValues Current
    {
        get
        {
            IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(_session.PreviewBytes);
            ExperimentalBinaryBlock economy = FindBlock(blocks, "economy", _economyId);
            if (!economy.UInt32Fields.TryGetValue("experience_points", out ExperimentalBinaryUInt32 experience) || experience.IsLegacyNil)
                throw new InvalidOperationException("The selected experience value is missing, ambiguous, or encoded as nil.");

            long? coachExperience = null;
            if (_hasCoachExperience)
            {
                coachExperience = _coachExperienceTypeCode switch
                {
                    0x25 when economy.Int32Fields.TryGetValue("bus_experience_points", out int signedValue) => signedValue,
                    0x27 when economy.UInt32Fields.TryGetValue("bus_experience_points", out ExperimentalBinaryUInt32 unsignedValue) && !unsignedValue.IsLegacyNil => unsignedValue.Raw,
                    0x27 => null,
                    _ => throw new InvalidOperationException("The selected Coaches experience value is missing or ambiguous.")
                };
            }

            long balance = _session.GetInt64("bank", _bankId, "money_account");
            return new ProfileValues(balance, experience.Raw, coachExperience);
        }
    }

    public byte[] PreviewBytes => _session.PreviewBytes;
    public IReadOnlyList<ExperimentalBinaryScalarChange> Changes => _session.Changes;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;

    /// <summary>Reads present candidate Coaches skill fields only when their BSII values match the observed UInt32 representation.</summary>
    public IReadOnlyDictionary<string, byte> CoachSkills
    {
        get
        {
            IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(_session.PreviewBytes);
            ExperimentalBinaryBlock economy = FindBlock(blocks, "economy", _economyId);
            var skills = new Dictionary<string, byte>(StringComparer.Ordinal);
            foreach (string name in LegacyCoachSkillCatalog.FieldNames)
            {
                if (!economy.RawFields.TryGetValue(name, out ExperimentalBinaryRawField? raw)) continue;
                if (raw is null || raw.TypeCode != 0x27 || raw.Bytes.Count != sizeof(uint) ||
                    !economy.UInt32Fields.TryGetValue(name, out ExperimentalBinaryUInt32 value) || value.IsLegacyNil ||
                    value.Raw > LegacyCoachSkillCatalog.MaximumRank)
                    throw new InvalidOperationException($"The existing binary Coaches skill '{name}' has an unsupported representation or value.");
                skills.Add(name, checked((byte)value.Raw));
            }
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, byte>(skills);
        }
    }

    /// <summary>Changes a present candidate Coaches skill in the in-memory preview; it never adds a field.</summary>
    public void SetCoachSkill(string name, byte rank)
    {
        if (!LegacyCoachSkillCatalog.FieldNames.Contains(name, StringComparer.Ordinal))
            throw new ArgumentException("Unknown candidate Coaches skill.", nameof(name));
        if (rank > LegacyCoachSkillCatalog.MaximumRank)
            throw new ArgumentOutOfRangeException(nameof(rank));
        if (!CoachSkills.ContainsKey(name))
            throw new InvalidOperationException("The selected profile does not contain this Coaches skill field.");
        _session.SetUInt32("economy", _economyId, name, rank);
    }

    /// <summary>Resets existing candidate Coaches skill fields to zero as one undoable in-memory batch.</summary>
    public void ResetCoachSkills()
    {
        IReadOnlyDictionary<string, byte> current = CoachSkills;
        var edits = current.Where(pair => pair.Value != 0)
            .Select(pair => new ExperimentalBinaryScalarEdit("economy", _economyId, pair.Key, 0x27, EncodeUInt32(0)))
            .ToArray();
        _session.Apply(edits);
    }

    public void Apply(ProfileValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ProfileValues current = Current;
        if (values.Experience == uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(values), "The UInt32 nil sentinel is not a valid experience value.");
        if (!_hasCoachExperience && values.CoachExperience is not null)
            throw new InvalidOperationException("The selected profile has no existing Coaches experience field.");

        var edits = new List<ExperimentalBinaryScalarEdit>(3);
        if (values.Balance != current.Balance)
            edits.Add(new("bank", _bankId, "money_account", 0x31, EncodeInt64(values.Balance)));
        if (values.Experience != current.Experience)
            edits.Add(new("economy", _economyId, "experience_points", 0x27, EncodeUInt32(values.Experience)));
        if (_hasCoachExperience && values.CoachExperience is long coachExperience && coachExperience != current.CoachExperience)
        {
            switch (_coachExperienceTypeCode)
            {
                case 0x25 when coachExperience is >= int.MinValue and <= int.MaxValue:
                    edits.Add(new("economy", _economyId, "bus_experience_points", 0x25, EncodeInt32((int)coachExperience)));
                    break;
                case 0x27 when coachExperience is >= 0 and < uint.MaxValue:
                    edits.Add(new("economy", _economyId, "bus_experience_points", 0x27, EncodeUInt32((uint)coachExperience)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(values), "The Coaches experience value is outside the existing field's supported range.");
            }
        }
        _session.Apply(edits);
    }

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private static ExperimentalBinaryBlock FindBlock(
        IReadOnlyList<ExperimentalBinaryBlock> blocks,
        string type,
        ExperimentalBinaryBlockIdentifier identifier)
    {
        ExperimentalBinaryBlock[] matches = blocks
            .Where(block => block.Type == type && SameIdentifier(block.Identifier, identifier))
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException("The selected profile block is missing or ambiguous.");
    }

    private static ExperimentalBinaryRawField RequireField(
        ExperimentalBinaryBlock block,
        string name,
        uint typeCode,
        int byteCount)
    {
        if (!block.RawFields.TryGetValue(name, out ExperimentalBinaryRawField? field) || field is null ||
            field.TypeCode != typeCode || field.Bytes.Count != byteCount)
            throw new InvalidOperationException("A required profile scalar is missing or has an unsupported type.");
        return field;
    }

    private static bool SameIdentifier(ExperimentalBinaryBlockIdentifier? left, ExperimentalBinaryBlockIdentifier? right)
        => left is not null && right is not null && left.Marker == right.Marker && left.RawParts.SequenceEqual(right.RawParts);

    private static bool SameIdentifier(ExperimentalBinaryBlockIdentifier? blockId, ExperimentalBinaryReference reference)
        => blockId is not null && blockId.Marker == reference.Marker && blockId.RawParts.SequenceEqual(reference.RawParts);

    private static ExperimentalBinaryBlockIdentifier SnapshotIdentifier(ExperimentalBinaryBlockIdentifier identifier)
        => new(identifier.Marker, Array.AsReadOnly(identifier.RawParts.ToArray()));

    private static byte[] EncodeInt64(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] EncodeUInt32(uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] EncodeInt32(int value)
    {
        byte[] bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }
}
