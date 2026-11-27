using System.Text;

namespace TSSE.Savegames.Syntax;

/// <summary>
/// Strict, in-memory plain-text SII decoding for synthetic fixtures. This is
/// not a game-container decoder and cannot access or write files.
/// </summary>
public sealed class SiiEncodedText
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    private readonly Encoding _encoding;
    private readonly byte[] _preamble;

    private SiiEncodedText(Encoding encoding, byte[] preamble, SiiSyntaxDocument syntax)
    {
        _encoding = encoding;
        _preamble = preamble;
        Syntax = syntax;
    }

    public SiiSyntaxDocument Syntax { get; }
    public SiiTextEncoding EncodingKind => _encoding.CodePage switch
    {
        65001 => _preamble.Length == 0 ? SiiTextEncoding.Utf8 : SiiTextEncoding.Utf8Bom,
        1200 => SiiTextEncoding.Utf16LittleEndianBom,
        1201 => SiiTextEncoding.Utf16BigEndianBom,
        _ => throw new InvalidOperationException("Unsupported encoding state.")
    };

    public static SiiEncodedText Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes)
            throw new InvalidDataException("The encoded text exceeds the byte limit.");
        if (bytes.StartsWith(new byte[] { 0xff, 0xfe, 0x00, 0x00 }) ||
            bytes.StartsWith(new byte[] { 0x00, 0x00, 0xfe, 0xff }))
            throw new InvalidDataException("UTF-32 is not supported.");

        Encoding encoding;
        byte[] preamble;
        if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
        {
            encoding = new UTF8Encoding(false, true);
            preamble = [0xef, 0xbb, 0xbf];
        }
        else if (bytes.StartsWith(new byte[] { 0xff, 0xfe }))
        {
            encoding = new UnicodeEncoding(false, false, true);
            preamble = [0xff, 0xfe];
        }
        else if (bytes.StartsWith(new byte[] { 0xfe, 0xff }))
        {
            encoding = new UnicodeEncoding(true, false, true);
            preamble = [0xfe, 0xff];
        }
        else
        {
            encoding = new UTF8Encoding(false, true);
            preamble = [];
        }

        string text;
        try
        {
            text = encoding.GetString(bytes[preamble.Length..]);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("The text encoding is invalid or unsupported.", ex);
        }
        if (text.Contains('\0'))
            throw new InvalidDataException("NUL characters are not supported in SII text.");
        return new SiiEncodedText(encoding, preamble, SiiSyntaxDocument.Parse(text));
    }

    /// <summary>Changes an explicit syntax value in memory; this does not validate game semantics.</summary>
    public SiiEncodedText WithFieldValue(string blockType, string? blockId, string fieldName, string rawValue)
    {
        SiiSyntaxDocument candidate = Syntax.WithFieldValue(blockType, blockId, fieldName, rawValue);
        if (candidate.ToText().Contains('\0'))
            throw new InvalidDataException("NUL characters are not supported in SII text.");
        var result = new SiiEncodedText(_encoding, _preamble, candidate);
        _ = result.ToBytes();
        return result;
    }

    public byte[] ToBytes()
        => Encode(Syntax);

    internal byte[] Encode(SiiSyntaxDocument syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        string text = syntax.ToText();
        if (text.Contains('\0'))
            throw new InvalidDataException("NUL characters are not supported in SII text.");
        int payloadLength;
        try
        {
            payloadLength = _encoding.GetByteCount(text);
        }
        catch (EncoderFallbackException ex)
        {
            throw new InvalidDataException("The text cannot be encoded without loss.", ex);
        }
        if ((long)_preamble.Length + payloadLength > MaximumBytes)
            throw new InvalidDataException("The encoded text exceeds the byte limit.");
        var result = new byte[_preamble.Length + payloadLength];
        _preamble.CopyTo(result, 0);
        _encoding.GetBytes(text, 0, text.Length, result, _preamble.Length);
        return result;
    }
}

public enum SiiTextEncoding { Utf8, Utf8Bom, Utf16LittleEndianBom, Utf16BigEndianBom }
