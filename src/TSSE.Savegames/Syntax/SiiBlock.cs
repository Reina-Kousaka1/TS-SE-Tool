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
    public int StartLine { get; }
    public int EndLine { get; }
    /// <summary>One-based column of the opening brace on <see cref="StartLine"/>.</summary>
    public int StartColumn { get; }
    public int NestingDepth { get; }
    public int? ParentStartLine { get; }
    public int? ParentStartColumn { get; }
    public IReadOnlyList<SiiField> Fields { get; }
}
