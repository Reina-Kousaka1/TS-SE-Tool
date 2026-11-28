using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>Read-only inspection of explicitly selected, legacy-shaped profile scalars.</summary>
/// <remarks>This maps known historical field names only. It does not establish current game compatibility.</remarks>
public static class LegacyProfileReadInspector
{
    public static ProfileValues InspectText(SiiSyntaxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        SiiBlock economy = Single(document.Blocks, "economy");
        string bankId = Value(economy, "bank");
        if (bankId == "nil" || string.IsNullOrWhiteSpace(bankId))
            throw new InvalidDataException("The selected economy bank reference is null or empty.");
        SiiBlock bank = Single(document.Blocks, "bank", bankId);
        if (!long.TryParse(Value(bank, "money_account"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long balance) ||
            !uint.TryParse(Value(economy, "experience_points"), NumberStyles.None, CultureInfo.InvariantCulture, out uint experience))
            throw new InvalidDataException("Known profile scalars are missing or not supported decimal integers.");
        int? coachExperience = null;
        SiiField[] coachFields = economy.Fields.Where(field => field.Name == "bus_experience_points").ToArray();
        if (coachFields.Length > 1 || coachFields.Length == 1 &&
            !int.TryParse(coachFields[0].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            throw new InvalidDataException("The optional historical Coaches experience scalar is ambiguous or invalid.");
        if (coachFields.Length == 1)
            coachExperience = int.Parse(coachFields[0].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return new ProfileValues(balance, experience, coachExperience);
    }

    public static ProfileValues InspectBinary(ReadOnlySpan<byte> payload)
        => InspectBinary(ExperimentalBinarySiiReader.Read(payload));

    public static ProfileValues InspectBinary(IReadOnlyList<ExperimentalBinaryBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ExperimentalBinaryBlock economy = Single(blocks, "economy");
        if (!economy.ReferenceFields.TryGetValue("bank", out ExperimentalBinaryReference? bankRef) || bankRef is null || bankRef.IsNullMarker)
            throw new InvalidDataException("The selected economy block has no supported bank reference.");
        ExperimentalBinaryBlock bank = Single(blocks, "bank", bankRef);
        if (!economy.UInt32Fields.TryGetValue("experience_points", out ExperimentalBinaryUInt32 experience) || experience.IsLegacyNil ||
            !bank.Int64Fields.TryGetValue("money_account", out long balance))
            throw new InvalidDataException("Known profile scalars are missing, nil, or not supported by the binary reader.");
        int? coachExperience = null;
        if (economy.RawFields.ContainsKey("bus_experience_points"))
        {
            if (!economy.Int32Fields.TryGetValue("bus_experience_points", out int coachValue))
                throw new InvalidDataException("The optional historical Coaches experience scalar has an unsupported representation.");
            coachExperience = coachValue;
        }
        return new ProfileValues(balance, experience.Raw, coachExperience);
    }

    private static SiiBlock Single(IReadOnlyList<SiiBlock> blocks, string type, string? id = null)
    {
        SiiBlock[] found = blocks.Where(block => block.Type == type && (id is null || block.Id == id)).ToArray();
        return found.Length == 1 ? found[0] : throw new InvalidDataException("The required profile block is missing or ambiguous.");
    }

    private static ExperimentalBinaryBlock Single(IReadOnlyList<ExperimentalBinaryBlock> blocks, string type)
    {
        ExperimentalBinaryBlock[] found = blocks.Where(block => block.Type == type).ToArray();
        return found.Length == 1 ? found[0] : throw new InvalidDataException("The required profile block is missing or ambiguous.");
    }

    private static ExperimentalBinaryBlock Single(IReadOnlyList<ExperimentalBinaryBlock> blocks, string type, ExperimentalBinaryReference reference)
    {
        ExperimentalBinaryBlock[] found = blocks.Where(block => block.Type == type && block.Identifier is { } id &&
            id.Marker == reference.Marker && id.RawParts.SequenceEqual(reference.RawParts)).ToArray();
        return found.Length == 1 ? found[0] : throw new InvalidDataException("The referenced profile block is missing or ambiguous.");
    }

    private static string Value(SiiBlock block, string field)
    {
        SiiField[] found = block.Fields.Where(item => item.Name == field).ToArray();
        return found.Length == 1 ? found[0].Value : throw new InvalidDataException("A required profile field is missing or ambiguous.");
    }
}
