using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Compares explicitly supplied in-memory syntax documents. Results describe
/// raw syntax, not game semantics, and can contain sensitive identifiers.
/// This class does not open, write, log, or discover files.
/// </summary>
public static class SiiSyntaxComparison
{
    public static IReadOnlyList<SiiSyntaxDifference> Compare(SiiSyntaxDocument before, SiiSyntaxDocument after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var left = Index(before);
        var right = Index(after);
        var result = new List<SiiSyntaxDifference>();
        foreach (var entry in left)
        {
            if (!right.TryGetValue(entry.Key, out var current))
            {
                result.Add(new(SiiSyntaxDifferenceKind.BlockRemoved, entry.Key.Type, entry.Key.Id, null));
                continue;
            }
            if (entry.Value.Parent != current.Parent)
                result.Add(new(SiiSyntaxDifferenceKind.BlockMoved, entry.Key.Type, entry.Key.Id, null));
            foreach (var field in entry.Value.Fields)
            {
                if (!current.Fields.TryGetValue(field.Key, out string? value))
                    result.Add(new(SiiSyntaxDifferenceKind.FieldRemoved, entry.Key.Type, entry.Key.Id, field.Key));
                else if (!string.Equals(field.Value, value, StringComparison.Ordinal))
                    result.Add(new(SiiSyntaxDifferenceKind.FieldChanged, entry.Key.Type, entry.Key.Id, field.Key));
            }
            foreach (var field in current.Fields)
                if (!entry.Value.Fields.ContainsKey(field.Key))
                    result.Add(new(SiiSyntaxDifferenceKind.FieldAdded, entry.Key.Type, entry.Key.Id, field.Key));
        }
        foreach (var entry in right)
            if (!left.ContainsKey(entry.Key))
                result.Add(new(SiiSyntaxDifferenceKind.BlockAdded, entry.Key.Type, entry.Key.Id, null));
        return result.AsReadOnly();
    }

    private static Dictionary<(string Type, string? Id), BlockSnapshot> Index(SiiSyntaxDocument document)
    {
        if (document.OpaqueLines.Count != 0)
            throw new InvalidOperationException("Unclassified syntax cannot be compared safely.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var positions = document.Blocks.ToDictionary(block => (block.StartLine, block.StartColumn));
        var result = new Dictionary<(string Type, string? Id), BlockSnapshot>();
        foreach (SiiBlock block in document.Blocks)
        {
            if (block.Id is not null && !ids.Add(block.Id))
                throw new InvalidOperationException("A block identifier is ambiguous.");
            (string Type, string? Id)? parent = null;
            if (block.ParentStartLine is int line && block.ParentStartColumn is int column)
            {
                if (!positions.TryGetValue((line, column), out SiiBlock? owner))
                    throw new InvalidOperationException("A parent block is missing.");
                parent = (owner.Type, owner.Id);
            }
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (SiiField field in block.Fields)
                if (!fields.TryAdd(field.Name, field.Value))
                    throw new InvalidOperationException("A field name is ambiguous.");
            if (!result.TryAdd((block.Type, block.Id), new(parent, fields)))
                throw new InvalidOperationException("A block identity is ambiguous.");
        }
        return result;
    }

    private sealed record BlockSnapshot((string Type, string? Id)? Parent, Dictionary<string, string> Fields);
}

public enum SiiSyntaxDifferenceKind { BlockAdded, BlockRemoved, BlockMoved, FieldAdded, FieldRemoved, FieldChanged }

/// <summary>Raw names and identifiers may be sensitive; field values are intentionally omitted.</summary>
public sealed record SiiSyntaxDifference(SiiSyntaxDifferenceKind Kind, string BlockType, string? BlockId, string? FieldName);
