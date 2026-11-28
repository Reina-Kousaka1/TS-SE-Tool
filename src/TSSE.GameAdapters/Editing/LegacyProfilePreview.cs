using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>
/// Experimental in-memory legacy-shaped profile editing for synthetic fixtures.
/// This does not establish compatibility with any ETS2 or ATS save version.
/// It never opens or writes files.
/// </summary>
public sealed class LegacyProfilePreview
{
    private static readonly string[] SkillNames = ["adr", "long_dist", "heavy", "fragile", "urgent", "mechanical"];
    // Names reported for the Coaches career by an independent 1.62 implementation.
    // Presence and range are still checked against the selected document; fields are never added.
    private readonly SiiEditSession _session;
    private readonly string _economyId;
    private readonly string _bankId;

    public LegacyProfilePreview(SiiSyntaxDocument document, string economyId)
        : this(new SiiEditSession(document), economyId)
    {
    }

    internal LegacyProfilePreview(SiiEditSession session, string economyId)
    {
        ArgumentNullException.ThrowIfNull(session);
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(session.PreviewText);
        ArgumentException.ThrowIfNullOrWhiteSpace(economyId);
        if (document.OpaqueLines.Count != 0)
            throw new InvalidOperationException("Unclassified syntax cannot be safely edited.");
        _economyId = economyId;
        SiiBlock economy = FindBlock(document, "economy", economyId);
        _bankId = FindValue(economy, "bank");
        if (string.IsNullOrWhiteSpace(_bankId) || _bankId == "nil")
            throw new InvalidOperationException("The economy bank reference is missing.");
        _session = session;
        _ = Current;
    }

    public ProfileValues Current
    {
        get
        {
            SiiSyntaxDocument document = SiiSyntaxDocument.Parse(_session.PreviewText);
            string balance = FindValue(FindBlock(document, "bank", _bankId), "money_account");
            SiiBlock economy = FindBlock(document, "economy", _economyId);
            string experience = FindValue(economy, "experience_points");
            string? coachExperience = FindOptionalValue(economy, "bus_experience_points");
            if (!long.TryParse(balance, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long money) ||
                !uint.TryParse(experience, NumberStyles.None, CultureInfo.InvariantCulture, out uint xp) ||
                xp == uint.MaxValue ||
                coachExperience is not null && !int.TryParse(coachExperience, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                throw new InvalidOperationException("Profile values are not supported decimal integers.");
            int? coachXp = coachExperience is null ? null : int.Parse(coachExperience, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            return new ProfileValues(money, xp, coachXp);
        }
    }

    public string PreviewText => _session.PreviewText;
    public IReadOnlyList<SiiFieldChange> Changes => _session.Changes;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;

    /// <summary>Reads one explicitly selected legacy-shaped skill from synthetic SII.</summary>
    public byte GetSkill(string name)
    {
        if (!SkillNames.Contains(name, StringComparer.Ordinal))
            throw new ArgumentException("Unknown synthetic skill.", nameof(name));
        string raw = FindValue(FindBlock(SiiSyntaxDocument.Parse(_session.PreviewText), "economy", _economyId), name);
        if (!byte.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out byte value) || value > SkillMaximum(name))
            throw new InvalidOperationException("The skill value is missing, ambiguous, or unsupported.");
        return value;
    }

    public void SetSkill(string name, byte value)
    {
        byte current = GetSkill(name);
        if (value > SkillMaximum(name)) throw new ArgumentOutOfRangeException(nameof(value));
        if (value != current)
            _session.Apply([new("economy", _economyId, name, value.ToString(CultureInfo.InvariantCulture))]);
    }

    private static byte SkillMaximum(string name) => name == "adr" ? (byte)63 : (byte)6;

    /// <summary>Reads one candidate Coaches skill only when the selected document already contains it.</summary>
    public byte GetCoachSkill(string name)
    {
        if (!LegacyCoachSkillCatalog.FieldNames.Contains(name, StringComparer.Ordinal))
            throw new ArgumentException("Unknown candidate Coaches skill.", nameof(name));
        SiiBlock economy = FindBlock(SiiSyntaxDocument.Parse(_session.PreviewText), "economy", _economyId);
        string raw = FindValue(economy, name);
        if (!byte.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out byte value) || value > LegacyCoachSkillCatalog.MaximumRank)
            throw new InvalidOperationException("The Coaches skill is missing, ambiguous, or outside its supported 0–3 range.");
        return value;
    }

    /// <summary>Changes one existing candidate Coaches skill in memory; it never creates a missing field.</summary>
    public void SetCoachSkill(string name, byte value)
    {
        if (!LegacyCoachSkillCatalog.FieldNames.Contains(name, StringComparer.Ordinal))
            throw new ArgumentException("Unknown candidate Coaches skill.", nameof(name));
        if (value > LegacyCoachSkillCatalog.MaximumRank) throw new ArgumentOutOfRangeException(nameof(value));
        byte current = GetCoachSkill(name);
        if (value != current)
            _session.Apply([new("economy", _economyId, name, value.ToString(CultureInfo.InvariantCulture))]);
    }

    /// <summary>Resets present candidate Coaches skill fields atomically, preserving all absent and unknown fields.</summary>
    public void ResetCoachSkills()
    {
        SiiBlock economy = FindBlock(SiiSyntaxDocument.Parse(_session.PreviewText), "economy", _economyId);
        var edits = new List<SiiFieldEdit>();
        foreach (string name in LegacyCoachSkillCatalog.FieldNames)
        {
            if (FindOptionalValue(economy, name) is null) continue;
            byte value = GetCoachSkill(name);
            if (value != 0) edits.Add(new("economy", _economyId, name, "0"));
        }
        if (edits.Count != 0)
        {
            _session.Apply(edits);
            return;
        }

        if (LegacyCoachSkillCatalog.FieldNames.Any(name => FindOptionalValue(economy, name) is not null)) return;
        throw new InvalidOperationException("The selected profile has no modeled Coaches skill fields.");
    }

    /// <summary>Resets all supported synthetic skill fields in one undoable edit.</summary>
    public void ResetSkills()
    {
        var edits = new List<SiiFieldEdit>();
        foreach (string name in SkillNames)
            if (GetSkill(name) != 0)
                edits.Add(new("economy", _economyId, name, "0"));
        _session.Apply(edits);
    }

    public void Apply(ProfileValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Experience == uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(values), "The UInt32 nil sentinel is not a valid experience value.");
        ProfileValues current = Current;
        var changes = new List<SiiFieldEdit>();
        if (values.Balance != current.Balance)
            changes.Add(new("bank", _bankId, "money_account", values.Balance.ToString(CultureInfo.InvariantCulture)));
        if (values.Experience != current.Experience)
            changes.Add(new("economy", _economyId, "experience_points", values.Experience.ToString(CultureInfo.InvariantCulture)));
        if (values.CoachExperience is int coachExperience)
        {
            if (current.CoachExperience is null)
                throw new InvalidOperationException("The selected profile has no existing Coaches experience field.");
            if (values.CoachExperience != current.CoachExperience)
                changes.Add(new("economy", _economyId, "bus_experience_points", coachExperience.ToString(CultureInfo.InvariantCulture)));
        }
        _session.Apply(changes);
    }

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private static SiiBlock FindBlock(SiiSyntaxDocument document, string type, string id)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Id == id).ToArray();
        return matches.Length == 1 && matches[0].Type == type
            ? matches[0] : throw new InvalidOperationException("The requested block is missing or ambiguous.");
    }

    private static string FindValue(SiiBlock block, string name)
    {
        SiiField[] matches = block.Fields.Where(field => field.Name == name).ToArray();
        return matches.Length == 1 ? matches[0].Value : throw new InvalidOperationException("The requested field is missing or ambiguous.");
    }

    private static string? FindOptionalValue(SiiBlock block, string name)
    {
        SiiField[] matches = block.Fields.Where(field => field.Name == name).ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0].Value,
            _ => throw new InvalidOperationException("The requested field is ambiguous.")
        };
    }
}
