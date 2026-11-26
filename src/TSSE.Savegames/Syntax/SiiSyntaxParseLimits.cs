namespace TSSE.Savegames.Syntax;

/// <summary>
/// Defensive resource bounds for parsing an untrusted or damaged SII text document.
/// These generic limits do not establish compatibility with any game or save version.
/// </summary>
public sealed record SiiSyntaxParseLimits
{
    public static SiiSyntaxParseLimits Default { get; } = new(
        maximumCharacters: 64 * 1024 * 1024,
        maximumLines: 1_000_000,
        maximumBlocks: 250_000,
        maximumFields: 1_000_000,
        maximumNestingDepth: 256,
        maximumLineLength: 1024 * 1024);

    public SiiSyntaxParseLimits(
        int maximumCharacters,
        int maximumLines,
        int maximumBlocks,
        int maximumFields,
        int maximumNestingDepth,
        int maximumLineLength)
    {
        MaximumCharacters = RequirePositive(maximumCharacters, nameof(maximumCharacters));
        MaximumLines = RequirePositive(maximumLines, nameof(maximumLines));
        MaximumBlocks = RequirePositive(maximumBlocks, nameof(maximumBlocks));
        MaximumFields = RequirePositive(maximumFields, nameof(maximumFields));
        MaximumNestingDepth = RequirePositive(maximumNestingDepth, nameof(maximumNestingDepth));
        MaximumLineLength = RequirePositive(maximumLineLength, nameof(maximumLineLength));

        if (MaximumLineLength > MaximumCharacters)
            throw new ArgumentOutOfRangeException(nameof(maximumLineLength), "The line limit cannot exceed the document limit.");
    }

    public int MaximumCharacters { get; }
    public int MaximumLines { get; }
    public int MaximumBlocks { get; }
    public int MaximumFields { get; }
    public int MaximumNestingDepth { get; }
    public int MaximumLineLength { get; }

    private static int RequirePositive(int value, string parameterName) =>
        value > 0 ? value : throw new ArgumentOutOfRangeException(parameterName, "The parse limit must be positive.");
}
