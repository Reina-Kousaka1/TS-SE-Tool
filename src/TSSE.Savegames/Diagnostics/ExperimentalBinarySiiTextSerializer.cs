using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Experimental conversion of supported BSII v1-v3 payloads to plain SiiNunit text.
/// This is not a binary/container writer, does not access files, and does not authorize save export.
/// Unsupported or ambiguous values fail closed instead of being omitted.
/// </summary>
public static class ExperimentalBinarySiiTextSerializer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Converts one validated BSII payload and reparses the complete result before returning it.</summary>
    public static byte[] Serialize(ReadOnlySpan<byte> payload)
    {
        IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(payload);
        var text = new StringBuilder("SiiNunit\n{\n");
        var expectedFieldLineCounts = new List<int>(blocks.Count);
        foreach (ExperimentalBinaryBlock block in blocks)
        {
            string id = FormatBlockIdentifier(block.Identifier);
            if (string.IsNullOrWhiteSpace(block.Type) || string.IsNullOrWhiteSpace(id) ||
                id.Contains('\r') || id.Contains('\n') || id.Contains('{') || id.Contains('}'))
                throw new ExperimentalBinaryTextSerializationException("InvalidBlockIdentity");
            if (block.FieldDefinitions.Count != block.RawFields.Count)
                throw new ExperimentalBinaryTextSerializationException("FieldDefinitionMismatch");

            text.Append(block.Type).Append(" : ").Append(id).Append(" {\n");
            int fieldLineCount = 0;
            foreach (ExperimentalBinaryFieldDefinition definition in block.FieldDefinitions)
            {
                if (!block.RawFields.TryGetValue(definition.Name, out ExperimentalBinaryRawField? raw) ||
                    raw is null || raw.TypeCode != definition.TypeCode)
                    throw new ExperimentalBinaryTextSerializationException("FieldDefinitionMismatch");
                IReadOnlyList<string> values = DecodeField(block, definition, raw);
                if (IsArrayType(definition.TypeCode))
                {
                    fieldLineCount = checked(fieldLineCount + 1 + values.Count);
                    text.Append(' ').Append(definition.Name).Append(": ")
                        .Append(values.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
                    for (int index = 0; index < values.Count; index++)
                        text.Append(' ').Append(definition.Name).Append('[')
                            .Append(index.ToString(CultureInfo.InvariantCulture)).Append("]: ")
                            .Append(values[index]).Append('\n');
                }
                else
                {
                    if (values.Count != 1)
                        throw new ExperimentalBinaryTextSerializationException("InvalidScalarValueCount");
                    fieldLineCount++;
                    text.Append(' ').Append(definition.Name).Append(": ").Append(values[0]).Append('\n');
                }
                if (text.Length > SiiSyntaxParseLimits.Default.MaximumCharacters)
                    throw new ExperimentalBinaryTextSerializationException("OutputLimitExceeded");
            }
            expectedFieldLineCounts.Add(fieldLineCount);
            text.Append("}\n\n");
        }
        text.Append('}');

        byte[] result;
        try { result = StrictUtf8.GetBytes(text.ToString()); }
        catch (EncoderFallbackException exception)
        {
            throw new ExperimentalBinaryTextSerializationException("InvalidTextEncoding", exception);
        }
        if (result.Length == 0 || result.Length > SiiEncodedText.MaximumBytes)
            throw new ExperimentalBinaryTextSerializationException("OutputLimitExceeded");

        try
        {
            SiiSyntaxDocument parsed = SiiEncodedText.Parse(result).Syntax;
            // The syntax model represents the SiiNunit container as the outermost block.
            if (parsed.OpaqueLines.Count != 0 || parsed.Blocks.Count != blocks.Count + 1 ||
                parsed.Blocks[0].Type != "SiiNunit" || parsed.Blocks[0].Id is not null)
                throw new ExperimentalBinaryTextSerializationException("SyntaxValidationFailed");
            for (int i = 0; i < blocks.Count; i++)
            {
                SiiBlock parsedBlock = parsed.Blocks[i + 1];
                if (parsedBlock.Type != blocks[i].Type || parsedBlock.Id != FormatBlockIdentifier(blocks[i].Identifier) ||
                    parsedBlock.Fields.Count != expectedFieldLineCounts[i])
                    throw new ExperimentalBinaryTextSerializationException("SyntaxValidationFailed");
            }
        }
        catch (SiiSyntaxException exception)
        {
            throw new ExperimentalBinaryTextSerializationException("SyntaxValidationFailed", exception);
        }

        return result;
    }

    private static IReadOnlyList<string> DecodeField(
        ExperimentalBinaryBlock block,
        ExperimentalBinaryFieldDefinition definition,
        ExperimentalBinaryRawField raw)
    {
        var cursor = new ValueCursor(raw.Bytes.ToArray());
        var values = new List<string>();
        uint type = definition.TypeCode;
        if (type is 0x02 or 0x04 or 0x06 or 0x08 or 0x0a or 0x12 or 0x18 or 0x1a or
            0x26 or 0x28 or 0x2a or 0x2c or 0x32 or 0x34 or 0x36 or 0x3a or 0x3c or 0x3e)
        {
            uint count = cursor.UInt32();
            if (count > 65_536)
                throw new ExperimentalBinaryTextSerializationException("ArrayLimitExceeded");
            for (uint i = 0; i < count; i++) values.Add(DecodeValue(ref cursor, block, definition, type, arrayElement: true));
        }
        else
        {
            values.Add(DecodeValue(ref cursor, block, definition, type, arrayElement: false));
        }
        if (!cursor.End)
            throw new ExperimentalBinaryTextSerializationException("TrailingFieldBytes");
        return values.AsReadOnly();
    }

    private static string DecodeValue(
        ref ValueCursor cursor,
        ExperimentalBinaryBlock block,
        ExperimentalBinaryFieldDefinition definition,
        uint type,
        bool arrayElement)
    {
        switch (type)
        {
            case 0x01: return FormatString(cursor.String());
            case 0x02: return FormatString(cursor.String(), arrayElement: true);
            case 0x03: return FormatEncodedString(cursor.UInt64());
            case 0x04: return FormatEncodedString(cursor.UInt64());
            case 0x05: return Float(cursor.UInt32());
            case 0x06: return Float(cursor.UInt32());
            case 0x07: return Vector(cursor.UInt32(), cursor.UInt32());
            case 0x08: return Vector(cursor.UInt32(), cursor.UInt32());
            case 0x09: return Vector(cursor.UInt32(), cursor.UInt32(), cursor.UInt32());
            case 0x0a: return Vector(cursor.UInt32(), cursor.UInt32(), cursor.UInt32());
            case 0x11: return IntVector(cursor.Int32(), cursor.Int32(), cursor.Int32());
            case 0x12: return IntVector(cursor.Int32(), cursor.Int32(), cursor.Int32());
            case 0x17: return Quaternion(cursor.UInt32(), cursor.UInt32(), cursor.UInt32(), cursor.UInt32());
            case 0x18: return Quaternion(cursor.UInt32(), cursor.UInt32(), cursor.UInt32(), cursor.UInt32());
            case 0x19: return Placement(ref cursor, block.BinaryVersion);
            case 0x1a: return Placement(ref cursor, block.BinaryVersion);
            case 0x25: return cursor.Int32().ToString(CultureInfo.InvariantCulture);
            case 0x26: return cursor.Int32().ToString(CultureInfo.InvariantCulture);
            case 0x27: return UInt32(cursor.UInt32(), nilSentinel: true);
            case 0x28: return UInt32(cursor.UInt32(), nilSentinel: true);
            case 0x2f: return UInt32(cursor.UInt32(), nilSentinel: true);
            case 0x29: return Int16(cursor.Int16(), nilSentinel: !arrayElement);
            case 0x2a: return cursor.Int16().ToString(CultureInfo.InvariantCulture);
            case 0x2b: return UInt16(cursor.UInt16(), nilSentinel: !arrayElement);
            case 0x2c: return cursor.UInt16().ToString(CultureInfo.InvariantCulture);
            case 0x31: return cursor.Int64().ToString(CultureInfo.InvariantCulture);
            case 0x32: return cursor.Int64().ToString(CultureInfo.InvariantCulture);
            case 0x33: return UInt64(cursor.UInt64(), nilSentinel: !arrayElement);
            case 0x34: return cursor.UInt64().ToString(CultureInfo.InvariantCulture);
            case 0x35: return Boolean(cursor.Byte());
            case 0x36: return Boolean(cursor.Byte());
            case 0x37:
            {
                uint ordinal = cursor.UInt32();
                if (definition.Name is "lashing_method" or "lashing_gear_type") return "none";
                if (!definition.OrdinalValues.TryGetValue(ordinal, out string? text))
                    throw new ExperimentalBinaryTextSerializationException("UnresolvedOrdinal");
                return FormatString(text);
            }
            case 0x39: case 0x3a: case 0x3b: case 0x3c: case 0x3d: case 0x3e:
                return Reference(cursor.Byte(), ref cursor);
            default:
                throw new ExperimentalBinaryTextSerializationException("UnsupportedFieldType");
        }
    }

    private static string Reference(byte marker, ref ValueCursor cursor)
    {
        if (marker == 0) return "null";
        if (marker == 0xff) return ExperimentalBinarySiiReader.NamelessText(cursor.UInt64());
        var parts = new ulong[marker];
        for (int i = 0; i < parts.Length; i++) parts[i] = cursor.UInt64();
        var reference = new ExperimentalBinaryReference(0, marker, Array.AsReadOnly(parts));
        return reference.TryGetLegacyText(out string? text) && text is not null
            ? text : throw new ExperimentalBinaryTextSerializationException("InvalidReference");
    }

    private static string FormatBlockIdentifier(ExperimentalBinaryBlockIdentifier? identifier)
    {
        if (identifier is null) throw new ExperimentalBinaryTextSerializationException("InvalidBlockIdentity");
        if (identifier.IsNameless)
        {
            if (identifier.RawParts.Count != 1)
                throw new ExperimentalBinaryTextSerializationException("InvalidBlockIdentity");
            return ExperimentalBinarySiiReader.NamelessText(identifier.RawParts[0]);
        }
        if (identifier.Marker == 0) return "null";
        return identifier.TryGetLegacyText(out string? text) && text is not null
            ? text : throw new ExperimentalBinaryTextSerializationException("InvalidBlockIdentity");
    }

    private static string FormatEncodedString(ulong raw)
    {
        var encoded = new ExperimentalBinaryEncodedString(raw);
        return encoded.TryGetLegacyText(out string? text) && text is not null
            ? text.Length == 0 ? "\"\"" : text
            : throw new ExperimentalBinaryTextSerializationException("InvalidEncodedString");
    }

    private static string FormatString(string value, bool arrayElement = false)
    {
        if (value.Contains('\0')) throw new ExperimentalBinaryTextSerializationException("UnsupportedStringCharacter");
        bool simple = value.Length > 0 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_');
        if (simple || value.Length == 0) return simple ? value : "\"\"";
        if (arrayElement && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return value;
        if (!arrayElement && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)) return value;

        byte[] utf8;
        try { utf8 = StrictUtf8.GetBytes(value); }
        catch (EncoderFallbackException exception)
        {
            throw new ExperimentalBinaryTextSerializationException("InvalidTextEncoding", exception);
        }
        var output = new StringBuilder("\"");
        foreach (byte current in utf8)
        {
            if (current is >= 0x20 and <= 0x7e && current is not (byte)'"' and not (byte)'\\')
                output.Append((char)current);
            else
                output.Append("\\x").Append(current.ToString("x2", CultureInfo.InvariantCulture));
        }
        return output.Append('"').ToString();
    }

    private static string Float(uint bits) => "&" + bits.ToString("x8", CultureInfo.InvariantCulture);
    private static string Vector(params uint[] values) => "(" + string.Join(", ", values.Select(Float)) + ")";
    private static string Quaternion(uint w, uint x, uint y, uint z) => $"({Float(w)}; {Float(x)}, {Float(y)}, {Float(z)})";
    private static string IntVector(params int[] values) => "(" + string.Join(", ", values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + ")";
    private static string UInt32(uint value, bool nilSentinel) => nilSentinel && value == uint.MaxValue ? "nil" : value.ToString(CultureInfo.InvariantCulture);
    private static string UInt16(ushort value, bool nilSentinel) => nilSentinel && value == ushort.MaxValue ? "nil" : value.ToString(CultureInfo.InvariantCulture);
    private static string Int16(short value, bool nilSentinel) => nilSentinel && value == short.MaxValue ? "nil" : value.ToString(CultureInfo.InvariantCulture);
    private static string UInt64(ulong value, bool nilSentinel) => nilSentinel && value == ulong.MaxValue ? "nil" : value.ToString(CultureInfo.InvariantCulture);
    private static string Boolean(byte value) => value == 0 ? "false" : "true";

    private static string Placement(ref ValueCursor cursor, uint version)
    {
        int componentCount = version == 1 ? 7 : version is 2 or 3 ? 8 : throw new ExperimentalBinaryTextSerializationException("UnsupportedVersion");
        var bits = new uint[componentCount];
        for (int i = 0; i < bits.Length; i++) bits[i] = cursor.UInt32();
        if (componentCount == 8)
        {
            float packedBias = BitConverter.Int32BitsToSingle(unchecked((int)bits[3]));
            double biasValue = packedBias;
            if (!float.IsFinite(packedBias) || biasValue < -9_223_372_036_854_775_808d || biasValue >= 9_223_372_036_854_775_808d)
                throw new ExperimentalBinaryTextSerializationException("InvalidPlacementBias");
            long bias = (long)packedBias;
            long xBits = ((bias & 0xfff) - 2048) << 9;
            long zBits = (((bias >> 12) & 0xfff) - 2048) << 9;
            bits[0] = BitConverter.SingleToUInt32Bits(BitConverter.Int32BitsToSingle(unchecked((int)bits[0])) + xBits);
            bits[2] = BitConverter.SingleToUInt32Bits(BitConverter.Int32BitsToSingle(unchecked((int)bits[2])) + zBits);
        }
        return "(" + string.Join(", ", new[] { Float(bits[0]), Float(bits[1]), Float(bits[2]) }) + ") (" +
            Float(bits[componentCount == 7 ? 3 : 4]) + "; " +
            string.Join(", ", Enumerable.Range(componentCount == 7 ? 4 : 5, 3).Select(index => Float(bits[index]))) + ")";
    }

    private static bool IsArrayType(uint type) => type is
        0x02 or 0x04 or 0x06 or 0x08 or 0x0a or 0x12 or 0x18 or 0x1a or
        0x26 or 0x28 or 0x2a or 0x2c or 0x32 or 0x34 or 0x36 or 0x3a or 0x3c or 0x3e;

    private ref struct ValueCursor(byte[] bytes)
    {
        private readonly byte[] _bytes = bytes;
        private int _offset;
        public bool End => _offset == _bytes.Length;
        public byte Byte() => Take(1)[0];
        public short Int16() => BinaryPrimitives.ReadInt16LittleEndian(Take(sizeof(short)));
        public ushort UInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(sizeof(ushort)));
        public int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Take(sizeof(int)));
        public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(sizeof(uint)));
        public long Int64() => BinaryPrimitives.ReadInt64LittleEndian(Take(sizeof(long)));
        public ulong UInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(sizeof(ulong)));
        public string String()
        {
            uint length = UInt32();
            if (length > 65_536 || length > _bytes.Length - _offset)
                throw new ExperimentalBinaryTextSerializationException("InvalidStringLength");
            try { return StrictUtf8.GetString(Take(checked((int)length))); }
            catch (DecoderFallbackException exception)
            {
                throw new ExperimentalBinaryTextSerializationException("InvalidTextEncoding", exception);
            }
        }
        private ReadOnlySpan<byte> Take(int count)
        {
            if (count < 0 || count > _bytes.Length - _offset)
                throw new ExperimentalBinaryTextSerializationException("TruncatedFieldValue");
            ReadOnlySpan<byte> value = _bytes.AsSpan(_offset, count);
            _offset += count;
            return value;
        }
    }
}

public sealed class ExperimentalBinaryTextSerializationException : FormatException
{
    public ExperimentalBinaryTextSerializationException(string error, Exception? innerException = null)
        : base($"Experimental BSII text serialization failed ({error}).", innerException) => Error = error;
    public string Error { get; }
}
