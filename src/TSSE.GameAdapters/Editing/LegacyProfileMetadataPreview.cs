using System.Globalization;
using System.Text;
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
            return DecodeName(Value("profile_name"));
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
        string encoded = EncodeName(name);
        if (Name != name) _session.Apply([new("user_profile", _id, "profile_name", encoded)]);
    }

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static bool IsSimpleAscii(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
    private static bool IsAllowedAscii(char c) => IsSimpleAscii(c) || c is ' ' or '-' or '.';

    private static string EncodeName(string name)
    {
        if (name.Length is < 1 or > 64 || string.IsNullOrWhiteSpace(name) ||
            name.Any(c => char.IsControl(c) || c < 128 && !IsAllowedAscii(c)))
            throw new ArgumentException("The name contains unsupported characters or exceeds the preview limit.", nameof(name));
        byte[] bytes;
        try { bytes = StrictUtf8.GetBytes(name); }
        catch (EncoderFallbackException ex) { throw new ArgumentException("The name contains invalid Unicode.", nameof(name), ex); }
        if (name.All(IsSimpleAscii)) return name;
        var result = new StringBuilder("\"");
        foreach (byte value in bytes)
        {
            if (value < 128) result.Append((char)value);
            else result.Append("\\x").Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }
        return result.Append('"').ToString();
    }

    private static string DecodeName(string raw)
    {
        if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"')
        {
            if (raw.Length is >= 1 and <= 64 && raw.All(IsSimpleAscii)) return raw;
            throw new InvalidOperationException("The profile name encoding is unsupported.");
        }
        var bytes = new List<byte>();
        for (int i = 1; i < raw.Length - 1; i++)
        {
            char c = raw[i];
            if (c == '\\')
            {
                if (i + 3 >= raw.Length - 1 || raw[i + 1] != 'x' ||
                    !byte.TryParse(raw.AsSpan(i + 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte value) || value < 128)
                    throw new InvalidOperationException("The profile name escape is unsupported.");
                bytes.Add(value);
                i += 3;
            }
            else if (c < 128 && IsAllowedAscii(c)) bytes.Add((byte)c);
            else throw new InvalidOperationException("The profile name encoding is unsupported.");
        }
        try
        {
            string name = StrictUtf8.GetString(bytes.ToArray());
            _ = EncodeName(name);
            return name;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("The profile name encoding is invalid.", ex);
        }
    }

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
