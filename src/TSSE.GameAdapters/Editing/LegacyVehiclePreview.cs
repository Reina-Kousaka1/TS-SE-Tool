using System.Globalization;
using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>
/// Experimental syntax preview for legacy-shaped synthetic vehicle fixtures.
/// This is not a game-version adapter and cannot access or write files.
/// </summary>
public sealed class LegacyVehiclePreview
{
    private static readonly string[] TruckFields =
        ["fuel_relative", "engine_wear", "transmission_wear", "cabin_wear", "chassis_wear"];
    private static readonly string[] TrailerFields =
        ["cargo_damage", "trailer_body_wear", "chassis_wear"];
    private readonly SiiEditSession _session;
    private readonly string _blockType;
    private readonly string _blockId;

    public LegacyVehiclePreview(SiiSyntaxDocument document, LegacyVehicleKind kind, string blockId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        if (document.OpaqueLines.Count != 0)
            throw new InvalidOperationException("Unclassified syntax cannot be safely edited.");
        _blockType = kind switch
        {
            LegacyVehicleKind.Truck => "vehicle",
            LegacyVehicleKind.Trailer => "trailer",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        _blockId = blockId;
        _session = new SiiEditSession(document);
        _ = FindBlock(document);
    }

    public string PreviewText => _session.PreviewText;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;

    public float GetValue(string fieldName)
    {
        CheckField(fieldName);
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(_session.PreviewText);
        SiiField[] fields = FindBlock(document).Fields.Where(field => field.Name == fieldName).ToArray();
        if (fields.Length != 1 || !TryParseRatio(fields[0].Value, out float value))
            throw new InvalidOperationException("The requested field is missing, ambiguous, or unsupported.");
        return value;
    }

    public void SetValue(string fieldName, float value)
    {
        CheckField(fieldName);
        if (!float.IsFinite(value) || value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "The ratio must be finite and between zero and one.");
        if (GetValue(fieldName) == value)
            return;
        _session.Apply([new(_blockType, _blockId, fieldName, value.ToString("R", CultureInfo.InvariantCulture))]);
    }

    /// <summary>Resets only the named scalar wear fields, as one in-memory edit.</summary>
    public void RepairKnownBodyFields()
    {
        string[] fields = _blockType == "vehicle"
            ? ["engine_wear", "transmission_wear", "cabin_wear", "chassis_wear"]
            : ["trailer_body_wear", "chassis_wear"];
        var edits = new List<SiiFieldEdit>();
        foreach (string field in fields)
        {
            if (GetValue(field) != 0)
                edits.Add(new(_blockType, _blockId, field, "0"));
        }
        _session.Apply(edits);
    }

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private void CheckField(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        string[] allowed = _blockType == "vehicle" ? TruckFields : TrailerFields;
        if (!allowed.Contains(fieldName, StringComparer.Ordinal))
            throw new ArgumentException("The field is not supported by this preview.", nameof(fieldName));
    }

    private SiiBlock FindBlock(SiiSyntaxDocument document)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Type == _blockType && block.Id == _blockId).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("The requested block is missing or ambiguous.");
    }

    private static bool TryParseRatio(string input, out float value)
    {
        bool parsed;
        if (input.Length == 9 && input[0] == '&' &&
            uint.TryParse(input.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint bits))
        {
            value = BitConverter.Int32BitsToSingle(unchecked((int)bits));
            parsed = true;
        }
        else
        {
            parsed = float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
        return parsed && float.IsFinite(value) && value >= 0 && value <= 1;
    }
}

public enum LegacyVehicleKind
{
    Truck,
    Trailer
}
