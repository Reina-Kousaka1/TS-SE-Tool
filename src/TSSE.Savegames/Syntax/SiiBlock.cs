namespace TSSE.Savegames.Syntax;

public sealed class SiiBlock
{
    internal SiiBlock(
        string type,
        string? id,
        int startLine,
        int endLine,
        int startColumn,
        int nestingDepth,
        int? parentStartLine,
        int? parentStartColumn,
        IReadOnlyList<SiiField> fields)
    {
        Type = type;
        Id = id;
        StartLine = startLine;
        EndLine = endLine;
        StartColumn = startColumn;
        NestingDepth = nestingDepth;
        ParentStartLine = parentStartLine;
        ParentStartColumn = parentStartColumn;
        Fields = fields;
    }

    public string Type { get; }
    public string? Id { get; }
    /// <summary>One-based line containing this block's opening brace.</summary>
    public int StartLine { get; }
    /// <summary>One-based line containing this block's closing brace.</summary>
    public int EndLine { get; }
    /// <summary>One-based column of the opening brace on <see cref="StartLine"/>.</summary>
    public int StartColumn { get; }
    /// <summary>Zero-based nesting depth; top-level blocks have depth zero.</summary>
    public int NestingDepth { get; }
    /// <summary>One-based line containing the parent block's opening brace, or null for a top-level block.</summary>
    public int? ParentStartLine { get; }
    /// <summary>One-based column of the parent block's opening brace, or null for a top-level block.</summary>
    public int? ParentStartColumn { get; }
    public IReadOnlyList<SiiField> Fields { get; }
}
