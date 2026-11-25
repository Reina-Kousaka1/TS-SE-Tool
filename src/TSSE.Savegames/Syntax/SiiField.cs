namespace TSSE.Savegames.Syntax;

public sealed record SiiField(string Name, string RawValue, int LineNumber, string RawLine)
{
    public string Value => RawValue.Trim();
}
