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
            string experience = FindValue(FindBlock(document, "economy", _economyId), "experience_points");
            if (!long.TryParse(balance, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long money) ||
                !uint.TryParse(experience, NumberStyles.None, CultureInfo.InvariantCulture, out uint xp))
                throw new InvalidOperationException("Profile values are not supported decimal integers.");
            return new ProfileValues(money, xp);
        }
    }

    public string PreviewText => _session.PreviewText;
    public IReadOnlyList<SiiFieldChange> Changes => _session.Changes;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;

    public void Apply(ProfileValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ProfileValues current = Current;
        var changes = new List<SiiFieldEdit>();
        if (values.Balance != current.Balance)
            changes.Add(new("bank", _bankId, "money_account", values.Balance.ToString(CultureInfo.InvariantCulture)));
        if (values.Experience != current.Experience)
            changes.Add(new("economy", _economyId, "experience_points", values.Experience.ToString(CultureInfo.InvariantCulture)));
        _session.Apply(changes);
    }

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private static SiiBlock FindBlock(SiiSyntaxDocument document, string type, string id)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Type == type && block.Id == id).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("The requested block is missing or ambiguous.");
    }

    private static string FindValue(SiiBlock block, string name)
    {
        SiiField[] matches = block.Fields.Where(field => field.Name == name).ToArray();
        return matches.Length == 1 ? matches[0].Value : throw new InvalidOperationException("The requested field is missing or ambiguous.");
    }
}
