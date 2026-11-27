using System.Globalization;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>Strict bounded reader for explicitly named legacy-shaped synthetic arrays.</summary>
internal static class LegacyReferenceArray
{
    internal static IReadOnlyList<string> Read(SiiSyntaxDocument document, string type, string id, string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (document.OpaqueLines.Count != 0) throw new InvalidOperationException("Unclassified syntax prevents safe inspection.");
        SiiBlock[] blocks = document.Blocks.Where(block => block.Type == type && block.Id == id).ToArray();
        if (blocks.Length != 1) throw new InvalidOperationException("The selected block is missing or ambiguous.");
        SiiField[] counts = blocks[0].Fields.Where(item => item.Name == name).ToArray();
        if (counts.Length != 1 || !int.TryParse(counts[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count is < 0 or > 1024)
            throw new InvalidOperationException("The array count is missing, ambiguous, or unsupported.");
        SiiField[] entries = blocks[0].Fields.Where(item => item.Name.StartsWith(name + "[", StringComparison.Ordinal)).ToArray();
        if (entries.Length != count) throw new InvalidOperationException("The array is incomplete or ambiguous.");
        var result = new List<string>(count);
        for (int index = 0; index < count; index++)
        {
            string fieldName = name + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            SiiField[] matching = entries.Where(item => item.Name == fieldName).ToArray();
            if (matching.Length != 1 || string.IsNullOrWhiteSpace(matching[0].Value))
                throw new InvalidOperationException("An array index is missing, ambiguous, or empty.");
            result.Add(matching[0].Value);
        }
        return result.AsReadOnly();
    }
}
