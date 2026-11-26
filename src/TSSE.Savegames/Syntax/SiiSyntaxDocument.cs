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
        SiiSyntaxParseLimits limits)
    {
        _lines = lines;
        _limits = limits;
        Blocks = new ReadOnlyCollection<SiiBlock>(blocks.ToList());
    }

    public IReadOnlyList<SiiBlock> Blocks { get; }

    public static SiiSyntaxDocument Parse(string text)
        => Parse(text, SiiSyntaxParseLimits.Default);

    public static SiiSyntaxDocument Parse(string text, SiiSyntaxParseLimits limits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(limits);

        if (text.Length > limits.MaximumCharacters)
            throw new SiiSyntaxException("Document exceeds the configured character limit", 1);

        List<SourceLine> lines = SplitLines(text, limits);
        var builders = new Stack<BlockBuilder>();
        var closedBlocks = new List<(int StartLine, int StartColumn, SiiBlock Block)>();
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
                        throw new SiiSyntaxException("Document exceeds the configured nesting limit", lineIndex + 1);

                    string header = line[headerSegmentStart..charIndex].Trim();
                    if (header.Length == 0)
                        header = pendingHeader ?? string.Empty;
                    if (header.Length == 0)
                        throw new SiiSyntaxException("Block opening has no header", lineIndex + 1);

                    (string type, string? id) = ParseHeader(header, lineIndex + 1);
                    builders.Push(new BlockBuilder(type, id, lineIndex, charIndex));
                    pendingHeader = null;
                    headerSegmentStart = charIndex + 1;
                }
                else if (current == '}')
                {
                    sawStructuralBrace = true;
                    if (builders.Count == 0)
                        throw new SiiSyntaxException("Unexpected block closing brace", lineIndex + 1);

                    if (closedBlocks.Count >= limits.MaximumBlocks)
                        throw new SiiSyntaxException("Document exceeds the configured block limit", lineIndex + 1);

                    BlockBuilder closed = builders.Pop();
                    closedBlocks.Add((closed.StartLine, closed.StartColumn, new SiiBlock(
                        closed.Type,
                        closed.Id,
                        closed.StartLine + 1,
                        lineIndex + 1,
                        new ReadOnlyCollection<SiiField>(closed.Fields))));
                    headerSegmentStart = charIndex + 1;
                }
            }

            if (inQuotedString)
                throw new SiiSyntaxException("Unterminated quoted value", lineIndex + 1);

            if (sawStructuralBrace)
                continue;

            if (lineIndex + 1 < lines.Count && lines[lineIndex + 1].Text.Trim() == "{")
            {
                pendingHeader = trimmed;
                continue;
            }

            if (builders.Count > 0 && TryParseField(line, lineIndex + 1, out SiiField field))
            {
                if (fieldCount >= limits.MaximumFields)
                    throw new SiiSyntaxException("Document exceeds the configured field limit", lineIndex + 1);

                builders.Peek().Fields.Add(field);
                fieldCount++;
            }
        }

        if (builders.Count > 0)
        {
            BlockBuilder open = builders.Peek();
            throw new SiiSyntaxException($"Block '{open.Type}' was not closed", lines.Count);
        }

        if (closedBlocks.Count == 0)
            throw new SiiSyntaxException("Document contains no SII blocks", 1);

        List<SiiBlock> blocks = closedBlocks
            .OrderBy(item => item.StartLine)
            .ThenBy(item => item.StartColumn)
            .Select(item => item.Block)
            .ToList();
        return new SiiSyntaxDocument(new ReadOnlyCollection<SourceLine>(lines), blocks, limits);
    }

    public SiiSyntaxDocument WithFieldValue(string blockType, string? blockId, string fieldName, string rawValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);

        if (rawValue.Contains('\r') || rawValue.Contains('\n') ||
            !string.Equals(rawValue, rawValue.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A replacement value must be a single unpadded SII value.", nameof(rawValue));

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
                left[i].StartLine != right[i].StartLine || left[i].EndLine != right[i].EndLine)
                return false;
        }

        return true;
    }

    private static (string Type, string? Id) ParseHeader(string header, int lineNumber)
    {
        int colon = header.IndexOf(':');
        string type = (colon < 0 ? header : header[..colon]).Trim();
        string? id = colon < 0 ? null : header[(colon + 1)..].Trim();
        if (type.Length == 0 || (colon >= 0 && string.IsNullOrWhiteSpace(id)))
            throw new SiiSyntaxException("Invalid block header", lineNumber);

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
                throw new SiiSyntaxException("Line exceeds the configured length limit", result.Count + 1);
            if (result.Count >= limits.MaximumLines)
                throw new SiiSyntaxException("Document exceeds the configured line limit", result.Count + 1);

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
                throw new SiiSyntaxException("Line exceeds the configured length limit", result.Count + 1);
            if (result.Count >= limits.MaximumLines)
                throw new SiiSyntaxException("Document exceeds the configured line limit", result.Count + 1);

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

    private sealed record SourceLine(string Text, string Terminator);

    private sealed class BlockBuilder(string type, string? id, int startLine, int startColumn)
    {
        public string Type { get; } = type;
        public string? Id { get; } = id;
        public int StartLine { get; } = startLine;
        public int StartColumn { get; } = startColumn;
        public List<SiiField> Fields { get; } = [];
    }
}
