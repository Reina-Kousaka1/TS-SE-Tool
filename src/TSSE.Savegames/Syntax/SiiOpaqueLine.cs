namespace TSSE.Savegames.Syntax;

/// <summary>
/// Non-comment source syntax that the generic SII foundation did not classify as a block header or field.
/// The original full line remains unchanged in the document text.
/// </summary>
public sealed record SiiOpaqueLine(
    int LineNumber,
    string RawLine,
    string? ContainingBlockType,
    string? ContainingBlockId);
