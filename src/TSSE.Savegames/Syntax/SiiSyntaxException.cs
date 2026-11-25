namespace TSSE.Savegames.Syntax;

public sealed class SiiSyntaxException : FormatException
{
    public SiiSyntaxException(string message, int lineNumber)
        : base($"{message} (line {lineNumber}).")
    {
        LineNumber = lineNumber;
    }

    public int LineNumber { get; }
}
