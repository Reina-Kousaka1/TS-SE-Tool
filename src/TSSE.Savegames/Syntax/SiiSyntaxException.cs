namespace TSSE.Savegames.Syntax;

public sealed class SiiSyntaxException : FormatException
{
    public SiiSyntaxException(
        string message,
        int lineNumber,
        SiiSyntaxErrorCode errorCode = SiiSyntaxErrorCode.InvalidSyntax,
        int? columnNumber = null)
        : base(columnNumber is { } column
            ? $"{message} (line {lineNumber}, column {column})."
            : $"{message} (line {lineNumber}).")
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        ErrorCode = errorCode;
    }

    public int LineNumber { get; }
    public int? ColumnNumber { get; }
    public SiiSyntaxErrorCode ErrorCode { get; }
}
