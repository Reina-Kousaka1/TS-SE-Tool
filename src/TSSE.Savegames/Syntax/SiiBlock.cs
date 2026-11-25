namespace TSSE.Savegames.Syntax;

public sealed class SiiBlock
{
    internal SiiBlock(string type, string? id, int startLine, int endLine, IReadOnlyList<SiiField> fields)
    {
        Type = type;
        Id = id;
        StartLine = startLine;
        EndLine = endLine;
        Fields = fields;
    }

    public string Type { get; }
    public string? Id { get; }
    public int StartLine { get; }
    public int EndLine { get; }
    public IReadOnlyList<SiiField> Fields { get; }
}
