using System.Globalization;
using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>Separate in-memory preview of synthetic legacy-shaped profile metadata.</summary>
public sealed class LegacyProfileMetadataPreview
{
    private readonly SiiEditSession _session;
    private readonly string _id;

    public LegacyProfileMetadataPreview(SiiSyntaxDocument document, string id)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (document.OpaqueLines.Count != 0) throw new InvalidOperationException("Unclassified syntax cannot be edited.");
        _id = id;
        _session = new SiiEditSession(document);
        _ = Name;
        _ = CachedExperience;
        _ = CachedDistance;
    }

    public string Name
    {
        get
        {
            string raw = Value("profile_name");
            string name = raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"' ? raw[1..^1] : raw;
            if (!IsSupportedName(name)) throw new InvalidOperationException("The profile name is unsupported.");
            return name;
        }
    }
    public uint CachedExperience => ReadUInt("cached_experience");
    public uint CachedDistance => ReadUInt("cached_distance");
    public string PreviewText => _session.PreviewText;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;

    /// <summary>Changes only the synthetic metadata name; it does not rename directories or other files.</summary>
    public void SetName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!IsSupportedName(name)) throw new ArgumentException("Only 1–64 ASCII letters, digits or underscores are supported.", nameof(name));
        if (Name != name) _session.Apply([new("user_profile", _id, "profile_name", name)]);
    }

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private static bool IsSupportedName(string name) => name.Length is >= 1 and <= 64 &&
        name.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private uint ReadUInt(string field) => uint.TryParse(Value(field), NumberStyles.None, CultureInfo.InvariantCulture, out uint value)
        ? value : throw new InvalidOperationException("The cached value is invalid or unsupported.");

    private string Value(string name)
    {
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(_session.PreviewText);
        SiiBlock[] blocks = document.Blocks.Where(block => block.Id == _id).ToArray();
        if (blocks.Length != 1 || blocks[0].Type != "user_profile")
            throw new InvalidOperationException("The profile block is missing or ambiguous.");
        SiiField[] fields = blocks[0].Fields.Where(field => field.Name == name).ToArray();
        return fields.Length == 1 ? fields[0].Value : throw new InvalidOperationException("The profile field is missing or ambiguous.");
    }
}
