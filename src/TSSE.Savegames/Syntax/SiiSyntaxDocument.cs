using System.Collections.ObjectModel;
using System.Text;

namespace TSSE.Savegames.Syntax;

/// <summary>
/// Lossless line-oriented SII syntax foundation. Values remain raw text; this class does not
/// claim game-version compatibility or understand game-specific field semantics.
/// </summary>
public sealed class SiiSyntaxDocument
{
    private readonly IReadOnlyList<SourceLine> _lines;
    private readonly SiiSyntaxParseLimits _limits;

    private SiiSyntaxDocument(
        IReadOnlyList<SourceLine> lines,
        IReadOnlyList<SiiBlock> blocks,
        IReadOnlyList<SiiOpaqueLine> opaqueLines,
        SiiSyntaxParseLimits limits)
    {
        _lines = lines;
        _limits = limits;
        Blocks = new ReadOnlyCollection<SiiBlock>(blocks.ToList());
        OpaqueLines = new ReadOnlyCollection<SiiOpaqueLine>(opaqueLines.ToList());
    }

    public IReadOnlyList<SiiBlock> Blocks { get; }

    /// <summary>Unclassified source lines. These remain present in <see cref="ToText"/>.</summary>
    public IReadOnlyList<SiiOpaqueLine> OpaqueLines { get; }

    public static SiiSyntaxDocument Parse(string text)
        => Parse(text, SiiSyntaxParseLimits.Default);

    /// <summary>
    /// Reads at most the configured character limit plus one detection character before parsing.
    /// The reader remains owned by the caller.
    /// </summary>
    public static SiiSyntaxDocument ParseReader(TextReader reader)
        => ParseReader(reader, SiiSyntaxParseLimits.Default);

    public static SiiSyntaxDocument ParseReader(TextReader reader, SiiSyntaxParseLimits limits)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(limits);

        var buffer = new char[Math.Min(8 * 1024, limits.MaximumCharacters)];
        var text = new StringBuilder(Math.Min(16 * 1024, limits.MaximumCharacters));
        int lineNumber = 1;
        int completedLines = 0;
        int lineLength = 0;
        bool previousWasCarriageReturn = false;
        bool lastCharacterWasLineTerminator = false;
        while (true)
        {
            int remaining = limits.MaximumCharacters - text.Length;
            int readLimit = remaining < buffer.Length ? remaining + 1 : buffer.Length;
            int read = reader.Read(buffer, 0, readLimit);
            if (read == 0)
                break;
            if (read > remaining)
                throw new SiiSyntaxException("Document exceeds the configured character limit", 1, SiiSyntaxErrorCode.DocumentLimitExceeded);

            for (int index = 0; index < read; index++)
            {
                char current = buffer[index];
                if (current == '\r')
                {
                    if (completedLines >= limits.MaximumLines)
                        throw new SiiSyntaxException("Document exceeds the configured line limit", lineNumber, SiiSyntaxErrorCode.LineLimitExceeded);

                    completedLines++;
                    lineNumber++;
                    lineLength = 0;
                    previousWasCarriageReturn = true;
                    lastCharacterWasLineTerminator = true;
                }
                else if (current == '\n')
                {
                    if (!previousWasCarriageReturn)
                    {
                        if (completedLines >= limits.MaximumLines)
                            throw new SiiSyntaxException("Document exceeds the configured line limit", lineNumber, SiiSyntaxErrorCode.LineLimitExceeded);

                        completedLines++;
                        lineNumber++;
                    }

                    lineLength = 0;
                    previousWasCarriageReturn = false;
                    lastCharacterWasLineTerminator = true;
                }
                else
                {
                    if (completedLines >= limits.MaximumLines)
                        throw new SiiSyntaxException("Document exceeds the configured line limit", lineNumber, SiiSyntaxErrorCode.LineLimitExceeded);

                    previousWasCarriageReturn = false;
                    lastCharacterWasLineTerminator = false;
                    lineLength++;
                    if (lineLength > limits.MaximumLineLength)
                        throw new SiiSyntaxException("Line exceeds the configured length limit", lineNumber, SiiSyntaxErrorCode.LineLengthExceeded);
                }
            }

            text.Append(buffer, 0, read);
        }

        int lineCount = text.Length == 0 || !lastCharacterWasLineTerminator
            ? completedLines + 1
            : completedLines;
        if (lineCount > limits.MaximumLines)
            throw new SiiSyntaxException("Document exceeds the configured line limit", limits.MaximumLines + 1, SiiSyntaxErrorCode.LineLimitExceeded);

        return Parse(text.ToString(), limits);
    }

    public static SiiSyntaxDocument Parse(string text, SiiSyntaxParseLimits limits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(limits);

        if (text.Length > limits.MaximumCharacters)
            throw new SiiSyntaxException("Document exceeds the configured character limit", 1, SiiSyntaxErrorCode.DocumentLimitExceeded);

        List<SourceLine> lines = SplitLines(text, limits);
        var builders = new Stack<BlockBuilder>();
        var closedBlocks = new List<(int StartLine, int StartColumn, SiiBlock Block)>();
        var opaqueLines = new List<SiiOpaqueLine>();
        string? pendingHeader = null;
        int fieldCount = 0;

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string line = lines[lineIndex].Text;
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || IsWholeLineComment(trimmed))
            {
                pendingHeader = null;
                continue;
            }

            bool sawStructuralBrace = false;
            bool inQuotedString = false;
            bool escaped = false;
            int headerSegmentStart = 0;

            for (int charIndex = 0; charIndex < line.Length; charIndex++)
            {
                char current = line[charIndex];
                if (inQuotedString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inQuotedString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inQuotedString = true;
                    continue;
                }

                if (current == '{')
                {
                    sawStructuralBrace = true;
                    if (builders.Count >= limits.MaximumNestingDepth)
                        throw new SiiSyntaxException("Document exceeds the configured nesting limit", lineIndex + 1, SiiSyntaxErrorCode.NestingLimitExceeded, charIndex + 1);

                    string header = line[headerSegmentStart..charIndex].Trim();
                    if (header.Length == 0)
                        header = pendingHeader ?? string.Empty;
                    if (header.Length == 0)
                        throw new SiiSyntaxException("Block opening has no header", lineIndex + 1, SiiSyntaxErrorCode.MissingBlockHeader, charIndex + 1);

                    (string type, string? id) = ParseHeader(header, lineIndex + 1, Math.Max(1, headerSegmentStart + 1));
                    BlockBuilder? parent = builders.TryPeek(out BlockBuilder? parentBuilder) ? parentBuilder : null;
                    builders.Push(new BlockBuilder(
                        type,
                        id,
                        lineIndex,
                        charIndex,
                        builders.Count,
                        parent is null ? null : parent.StartLine + 1,
                        parent is null ? null : parent.StartColumn + 1));
                    pendingHeader = null;
                    headerSegmentStart = charIndex + 1;
                }
                else if (current == '}')
                {
                    sawStructuralBrace = true;
                    if (builders.Count == 0)
                        throw new SiiSyntaxException("Unexpected block closing brace", lineIndex + 1, SiiSyntaxErrorCode.UnexpectedClosingBrace, charIndex + 1);

                    if (closedBlocks.Count >= limits.MaximumBlocks)
                        throw new SiiSyntaxException("Document exceeds the configured block limit", lineIndex + 1, SiiSyntaxErrorCode.BlockLimitExceeded, charIndex + 1);

                    if (!string.IsNullOrWhiteSpace(line[headerSegmentStart..charIndex]))
                        AddOpaqueLine(opaqueLines, lineIndex + 1, line, builders.Peek());

                    BlockBuilder closed = builders.Pop();
                    closedBlocks.Add((closed.StartLine, closed.StartColumn, new SiiBlock(
                        closed.Type,
                        closed.Id,
                        closed.StartLine + 1,
                        lineIndex + 1,
                        closed.StartColumn + 1,
                        closed.NestingDepth,
                        closed.ParentStartLine,
                        closed.ParentStartColumn,
                        new ReadOnlyCollection<SiiField>(closed.Fields))));
                    headerSegmentStart = charIndex + 1;
                }
            }

            if (inQuotedString)
                throw new SiiSyntaxException("Unterminated quoted value", lineIndex + 1, SiiSyntaxErrorCode.UnterminatedQuotedValue, line.Length + 1);

            if (sawStructuralBrace)
            {
                if (!string.IsNullOrWhiteSpace(line[headerSegmentStart..]))
                {
                    BlockBuilder? owner = builders.TryPeek(out BlockBuilder? currentBuilder) ? currentBuilder : null;
                    AddOpaqueLine(opaqueLines, lineIndex + 1, line, owner);
                }
                continue;
            }

            if (lineIndex + 1 < lines.Count && lines[lineIndex + 1].Text.Trim() == "{")
            {
                pendingHeader = trimmed;
                continue;
            }

            if (builders.Count > 0 && TryParseField(line, lineIndex + 1, out SiiField field))
            {
                if (fieldCount >= limits.MaximumFields)
                    throw new SiiSyntaxException("Document exceeds the configured field limit", lineIndex + 1, SiiSyntaxErrorCode.FieldLimitExceeded);

                builders.Peek().Fields.Add(field);
                fieldCount++;
            }
            else
            {
                BlockBuilder? owner = builders.TryPeek(out BlockBuilder? currentBuilder) ? currentBuilder : null;
                AddOpaqueLine(opaqueLines, lineIndex + 1, line, owner);
            }
        }

        if (builders.Count > 0)
        {
            BlockBuilder open = builders.Peek();
            throw new SiiSyntaxException($"Block '{open.Type}' was not closed", lines.Count, SiiSyntaxErrorCode.UnclosedBlock);
        }

        if (closedBlocks.Count == 0)
            throw new SiiSyntaxException("Document contains no SII blocks", 1, SiiSyntaxErrorCode.NoBlocks);

        List<SiiBlock> blocks = closedBlocks
            .OrderBy(item => item.StartLine)
            .ThenBy(item => item.StartColumn)
            .Select(item => item.Block)
            .ToList();
        return new SiiSyntaxDocument(new ReadOnlyCollection<SourceLine>(lines), blocks, opaqueLines, limits);
    }

    public SiiSyntaxDocument WithFieldValue(string blockType, string? blockId, string fieldName, string rawValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);

        if (rawValue.Contains('\r') || rawValue.Contains('\n') ||
            !string.Equals(rawValue, rawValue.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A replacement value must be a single unpadded SII value.", nameof(rawValue));

        if (OpaqueLines.Count > 0)
            throw new InvalidOperationException("The document contains unclassified syntax and cannot be edited safely.");

        List<SiiBlock> matchingBlocks = Blocks
            .Where(block => block.Type == blockType && block.Id == blockId)
            .ToList();
        if (matchingBlocks.Count != 1)
            throw new InvalidOperationException("The requested block is missing or ambiguous.");

        List<SiiField> matchingFields = matchingBlocks[0].Fields
            .Where(field => field.Name == fieldName)
            .ToList();
        if (matchingFields.Count != 1)
            throw new InvalidOperationException("The requested field is missing or ambiguous.");

        SiiField fieldToChange = matchingFields[0];
        int lineIndex = fieldToChange.LineNumber - 1;
        string originalLine = _lines[lineIndex].Text;
        int colonIndex = originalLine.IndexOf(':');
        if (colonIndex < 0)
            throw new InvalidOperationException("The field line is no longer valid.");

        string afterColon = originalLine[(colonIndex + 1)..];
        int leadingWhitespace = 0;
        while (leadingWhitespace < afterColon.Length && char.IsWhiteSpace(afterColon[leadingWhitespace]))
            leadingWhitespace++;

        int trailingWhitespace = 0;
        while (trailingWhitespace < afterColon.Length - leadingWhitespace &&
               char.IsWhiteSpace(afterColon[afterColon.Length - 1 - trailingWhitespace]))
            trailingWhitespace++;

        string prefix = originalLine[..(colonIndex + 1)] + afterColon[..leadingWhitespace];
        string suffix = trailingWhitespace == 0 ? string.Empty : afterColon[^trailingWhitespace..];
        var candidateLines = _lines.ToList();
        candidateLines[lineIndex] = candidateLines[lineIndex] with
        {
            Text = prefix + rawValue + suffix
        };

        string candidateText = Render(candidateLines);
        SiiSyntaxDocument candidate = Parse(candidateText, _limits);
        if (!HasSameBlockLayout(Blocks, candidate.Blocks))
            throw new InvalidOperationException("The replacement would alter the SII block structure.");

        SiiBlock candidateBlock = candidate.Blocks.Single(block => block.Type == blockType && block.Id == blockId);
        if (candidateBlock.Fields.Single(field => field.Name == fieldName).Value != rawValue)
            throw new InvalidOperationException("The replacement did not roundtrip as one SII field value.");

        return candidate;
    }

    public string ToText() => Render(_lines);

    private static bool HasSameBlockLayout(IReadOnlyList<SiiBlock> left, IReadOnlyList<SiiBlock> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i].Type != right[i].Type || left[i].Id != right[i].Id ||
                left[i].StartLine != right[i].StartLine || left[i].EndLine != right[i].EndLine ||
                left[i].StartColumn != right[i].StartColumn || left[i].NestingDepth != right[i].NestingDepth ||
                left[i].ParentStartLine != right[i].ParentStartLine || left[i].ParentStartColumn != right[i].ParentStartColumn)
                return false;
        }

        return true;
    }

    private static (string Type, string? Id) ParseHeader(string header, int lineNumber, int columnNumber)
    {
        int colon = header.IndexOf(':');
        string type = (colon < 0 ? header : header[..colon]).Trim();
        string? id = colon < 0 ? null : header[(colon + 1)..].Trim();
        if (type.Length == 0 || (colon >= 0 && string.IsNullOrWhiteSpace(id)))
            throw new SiiSyntaxException("Invalid block header", lineNumber, SiiSyntaxErrorCode.InvalidBlockHeader, columnNumber);

        return (type, id);
    }

    private static bool TryParseField(string line, int lineNumber, out SiiField field)
    {
        field = null!;
        int colon = line.IndexOf(':');
        if (colon < 0)
            return false;

        string name = line[..colon].Trim();
        if (name.Length == 0 || name.Contains('{') || name.Contains('}'))
            return false;

        field = new SiiField(name, line[(colon + 1)..], lineNumber, line);
        return true;
    }

    private static bool IsWholeLineComment(string trimmed) =>
        trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal);

    private static List<SourceLine> SplitLines(string text, SiiSyntaxParseLimits limits)
    {
        var result = new List<SourceLine>();
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\r' or '\n'))
                continue;

            if (index - start > limits.MaximumLineLength)
                throw new SiiSyntaxException("Line exceeds the configured length limit", result.Count + 1, SiiSyntaxErrorCode.LineLengthExceeded);
            if (result.Count >= limits.MaximumLines)
                throw new SiiSyntaxException("Document exceeds the configured line limit", result.Count + 1, SiiSyntaxErrorCode.LineLimitExceeded);

            string terminator;
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                terminator = "\r\n";
                result.Add(new SourceLine(text[start..index], terminator));
                index++;
            }
            else
            {
                terminator = text[index].ToString();
                result.Add(new SourceLine(text[start..index], terminator));
            }

            start = index + 1;
        }

        if (start < text.Length || result.Count == 0 || text.Length > 0 && text[^1] is not ('\r' or '\n'))
        {
            if (text.Length - start > limits.MaximumLineLength)
                throw new SiiSyntaxException("Line exceeds the configured length limit", result.Count + 1, SiiSyntaxErrorCode.LineLengthExceeded);
            if (result.Count >= limits.MaximumLines)
                throw new SiiSyntaxException("Document exceeds the configured line limit", result.Count + 1, SiiSyntaxErrorCode.LineLimitExceeded);

            result.Add(new SourceLine(text[start..], string.Empty));
        }

        return result;
    }

    private static string Render(IEnumerable<SourceLine> lines)
    {
        var builder = new StringBuilder();
        foreach (SourceLine line in lines)
            builder.Append(line.Text).Append(line.Terminator);
        return builder.ToString();
    }

    private static void AddOpaqueLine(
        ICollection<SiiOpaqueLine> opaqueLines,
        int lineNumber,
        string rawLine,
        BlockBuilder? containingBlock) =>
        opaqueLines.Add(new SiiOpaqueLine(
            lineNumber,
            rawLine,
            containingBlock?.Type,
            containingBlock?.Id));

    private sealed record SourceLine(string Text, string Terminator);

    private sealed class BlockBuilder(
        string type,
        string? id,
        int startLine,
        int startColumn,
        int nestingDepth,
        int? parentStartLine,
        int? parentStartColumn)
    {
        public string Type { get; } = type;
        public string? Id { get; } = id;
        public int StartLine { get; } = startLine;
        public int StartColumn { get; } = startColumn;
        public int NestingDepth { get; } = nestingDepth;
        public int? ParentStartLine { get; } = parentStartLine;
        public int? ParentStartColumn { get; } = parentStartColumn;
        public List<SiiField> Fields { get; } = [];
    }
}
