using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Bounded read-only BSII v1/v2/v3 decoder for in-memory bytes. It does not open
/// files, decode the outer encrypted container, write, or establish game
/// compatibility. Known values are retained as typed values and exact raw bytes.
/// </summary>
public static class ExperimentalBinarySiiReader
{
    public const int MaximumBytes = 32 * 1024 * 1024;
    public const int MaximumBlocks = 65_536;
    private const int MaximumStructures = 1024, MaximumFieldsPerStructure = 512, MaximumTotalFieldValues = 1_000_000, MaximumNameBytes = 128, MaximumStringBytes = 65536, MaximumArrayItems = 65_536;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static IReadOnlyList<ExperimentalBinaryBlock> Read(ReadOnlySpan<byte> input)
    {
        if (input.Length > MaximumBytes) throw new ExperimentalBinarySiiException(ExperimentalBinarySiiError.LimitExceeded, 0);
        var cursor = new Cursor(input);
        if (!cursor.Take(4).SequenceEqual("BSII"u8)) throw cursor.Error(ExperimentalBinarySiiError.InvalidHeader);
        uint version = cursor.UInt32();
        if (version is not (1 or 2 or 3)) throw cursor.Error(ExperimentalBinarySiiError.UnsupportedVersion);
        var structures = new Dictionary<uint, Structure>();
        var blocks = new List<ExperimentalBinaryBlock>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int totalFieldValues = 0;
        while (!cursor.End)
        {
            uint kind = cursor.UInt32();
            if (kind == 0)
            {
                byte valid = cursor.Byte();
                if (valid == 0)
                {
                    if (blocks.Count == 0) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
                    if (!cursor.End) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
                    return blocks.AsReadOnly();
                }
                if (valid != 1) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
                if (structures.Count >= MaximumStructures) throw cursor.Error(ExperimentalBinarySiiError.LimitExceeded);
                uint id = cursor.UInt32();
                if (id == 0 || structures.ContainsKey(id)) throw cursor.Error(ExperimentalBinarySiiError.Ambiguous);
                string name = cursor.Name();
                var fields = new List<Field>();
                var fieldNames = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    uint type = cursor.UInt32();
                    if (type == 0) break;
                    if (!IsKnownType(type))
                    {
                        string unsupportedFieldName = cursor.Name();
                        throw cursor.Error(ExperimentalBinarySiiError.UnsupportedType, type, name, unsupportedFieldName);
                    }
                    if (fields.Count >= MaximumFieldsPerStructure) throw cursor.Error(ExperimentalBinarySiiError.LimitExceeded);
                    string field = cursor.Name();
                    if (!fieldNames.Add(field)) throw cursor.Error(ExperimentalBinarySiiError.Ambiguous);
                    IReadOnlyDictionary<uint, string> ordinalValues = type == 0x37 ? cursor.OrdinalValues() : EmptyOrdinalValues;
                    fields.Add(new Field(field, type, ordinalValues));
                }
                IReadOnlyList<Field> fieldDefinitions = fields.AsReadOnly();
                IReadOnlyList<ExperimentalBinaryFieldDefinition> publicFieldDefinitions = Array.AsReadOnly(fieldDefinitions
                    .Select(field => new ExperimentalBinaryFieldDefinition(field.Name, field.Type, field.OrdinalValues))
                    .ToArray());
                structures.Add(id, new Structure(name, fieldDefinitions, publicFieldDefinitions));
            }
            else
            {
                if (blocks.Count >= MaximumBlocks) throw cursor.Error(ExperimentalBinarySiiError.LimitExceeded);
                if (!structures.TryGetValue(kind, out Structure? structure)) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
                if (structure.Fields.Count > MaximumTotalFieldValues - totalFieldValues)
                    throw cursor.Error(ExperimentalBinarySiiError.LimitExceeded);
                totalFieldValues += structure.Fields.Count;
                ExperimentalBinaryReference rawId = cursor.Reference(0);
                string identity;
                ulong? nameless = null;
                if (rawId.IsNamelessMarker)
                {
                    nameless = rawId.RawParts[0];
                    identity = NamelessText(nameless.Value);
                }
                else if (rawId.IsNullMarker || !rawId.TryGetLegacyText(out string? named) || named is null)
                    throw cursor.Error(ExperimentalBinarySiiError.UnsupportedIdentifier);
                else identity = named;
                if (!ids.Add(identity)) throw cursor.Error(ExperimentalBinarySiiError.Ambiguous);
                var values = new Dictionary<string, int>(StringComparer.Ordinal);
                var unsignedValues = new Dictionary<string, ExperimentalBinaryUInt32>(StringComparer.Ordinal);
                var longValues = new Dictionary<string, long>(StringComparer.Ordinal);
                var unsignedLongValues = new Dictionary<string, ExperimentalBinaryUInt64>(StringComparer.Ordinal);
                var stringValues = new Dictionary<string, string>(StringComparer.Ordinal);
                var references = new Dictionary<string, ExperimentalBinaryReference>(StringComparer.Ordinal);
                var referenceArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryReference>>(StringComparer.Ordinal);
                var floats = new Dictionary<string, ExperimentalBinaryFloat32>(StringComparer.Ordinal);
                var floatArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>(StringComparer.Ordinal);
                var vector2Arrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryFloat32Vector2>>(StringComparer.Ordinal);
                var intArrays = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
                var uintArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>(StringComparer.Ordinal);
                var longArrays = new Dictionary<string, IReadOnlyList<long>>(StringComparer.Ordinal);
                var ulongArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>(StringComparer.Ordinal);
                var booleans = new Dictionary<string, ExperimentalBinaryBool>(StringComparer.Ordinal);
                var booleanArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryBool>>(StringComparer.Ordinal);
                var stringArrays = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                var encodedStrings = new Dictionary<string, ExperimentalBinaryEncodedString>(StringComparer.Ordinal);
                var encodedStringArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryEncodedString>>(StringComparer.Ordinal);
                var shortValues = new Dictionary<string, ushort>(StringComparer.Ordinal);
                var shortArrays = new Dictionary<string, IReadOnlyList<ushort>>(StringComparer.Ordinal);
                var signedShortValues = new Dictionary<string, short>(StringComparer.Ordinal);
                var signedShortArrays = new Dictionary<string, IReadOnlyList<short>>(StringComparer.Ordinal);
                var plainUnsignedValues = new Dictionary<string, uint>(StringComparer.Ordinal);
                var ordinals = new Dictionary<string, ExperimentalBinaryOrdinal>(StringComparer.Ordinal);
                var rawFields = new Dictionary<string, ExperimentalBinaryRawField>(StringComparer.Ordinal);
                foreach (Field field in structure.Fields)
                {
                    int valueStart = cursor.Position;
                    switch (field.Type)
                    {
                        case 0x25: values.Add(field.Name, cursor.Int32()); break;
                        case 0x27: unsignedValues.Add(field.Name, new ExperimentalBinaryUInt32(cursor.UInt32())); break;
                        case 0x31: longValues.Add(field.Name, cursor.Int64()); break;
                        case 0x33: unsignedLongValues.Add(field.Name, new ExperimentalBinaryUInt64(cursor.UInt64())); break;
                        case 0x01: stringValues.Add(field.Name, cursor.Text()); break;
                        case 0x02: stringArrays.Add(field.Name, cursor.TextArray()); break;
                        case 0x03: encodedStrings.Add(field.Name, new ExperimentalBinaryEncodedString(cursor.UInt64())); break;
                        case 0x04: encodedStringArrays.Add(field.Name, cursor.EncodedStringArray()); break;
                        case 0x05: floats.Add(field.Name, new ExperimentalBinaryFloat32(cursor.UInt32())); break;
                        case 0x06: floatArrays.Add(field.Name, cursor.FloatArray()); break;
                        case 0x08: vector2Arrays.Add(field.Name, cursor.FloatVector2Array()); break;
                        case 0x26: intArrays.Add(field.Name, cursor.Int32Array()); break;
                        case 0x28: uintArrays.Add(field.Name, cursor.UInt32Array()); break;
                        case 0x32: longArrays.Add(field.Name, cursor.Int64Array()); break;
                        case 0x34: ulongArrays.Add(field.Name, cursor.UInt64Array()); break;
                        case 0x35: booleans.Add(field.Name, new ExperimentalBinaryBool(cursor.Byte())); break;
                        case 0x36: booleanArrays.Add(field.Name, cursor.BoolArray()); break;
                        case 0x07: case 0x09: case 0x11: case 0x17: case 0x19:
                            cursor.Skip(FixedValueSize(field.Type, version)); break;
                        case 0x0a: case 0x12: case 0x18: case 0x1a:
                            cursor.SkipFixedArray(FixedValueSize(field.Type, version)); break;
                        case 0x2b: shortValues.Add(field.Name, cursor.UInt16()); break;
                        case 0x2c: shortArrays.Add(field.Name, cursor.UInt16Array()); break;
                        case 0x29: signedShortValues.Add(field.Name, cursor.Int16()); break;
                        case 0x2a: signedShortArrays.Add(field.Name, cursor.Int16Array()); break;
                        case 0x2f: plainUnsignedValues.Add(field.Name, cursor.UInt32()); break;
                        case 0x37: ordinals.Add(field.Name, new ExperimentalBinaryOrdinal(cursor.UInt32(), field.OrdinalValues)); break;
                        case 0x39 or 0x3b or 0x3d: references.Add(field.Name, cursor.Reference(field.Type)); break;
                        case 0x3a or 0x3c or 0x3e: referenceArrays.Add(field.Name, cursor.ReferenceArray(field.Type)); break;
                        default: throw cursor.Error(ExperimentalBinarySiiError.UnsupportedType);
                    }
                    rawFields.Add(field.Name, new ExperimentalBinaryRawField(field.Type, valueStart, cursor.CopyRange(valueStart, cursor.Position)));
                }
                blocks.Add(new ExperimentalBinaryBlock(structure.Name, nameless, new ReadOnlyDictionary<string, int>(values))
                {
                    BinaryVersion = version,
                    FieldDefinitions = structure.PublicFields,
                    Identifier = new ExperimentalBinaryBlockIdentifier(rawId.Marker, rawId.RawParts),
                    UInt32Fields = new ReadOnlyDictionary<string, ExperimentalBinaryUInt32>(unsignedValues),
                    Int64Fields = new ReadOnlyDictionary<string, long>(longValues),
                    UInt64Fields = new ReadOnlyDictionary<string, ExperimentalBinaryUInt64>(unsignedLongValues),
                    StringFields = new ReadOnlyDictionary<string, string>(stringValues),
                    ReferenceFields = new ReadOnlyDictionary<string, ExperimentalBinaryReference>(references),
                    ReferenceArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryReference>>(referenceArrays),
                    Float32Fields = new ReadOnlyDictionary<string, ExperimentalBinaryFloat32>(floats),
                    Float32ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>(floatArrays),
                    Float32Vector2ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32Vector2>>(vector2Arrays),
                    Int32ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<int>>(intArrays),
                    UInt32ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>(uintArrays),
                    Int64ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<long>>(longArrays),
                    UInt64ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>(ulongArrays),
                    BoolFields = new ReadOnlyDictionary<string, ExperimentalBinaryBool>(booleans),
                    BoolArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryBool>>(booleanArrays),
                    StringArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<string>>(stringArrays),
                    EncodedStringFields = new ReadOnlyDictionary<string, ExperimentalBinaryEncodedString>(encodedStrings),
                    EncodedStringArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryEncodedString>>(encodedStringArrays),
                    UInt16Fields = new ReadOnlyDictionary<string, ushort>(shortValues),
                    UInt16ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ushort>>(shortArrays),
                    Int16Fields = new ReadOnlyDictionary<string, short>(signedShortValues),
                    Int16ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<short>>(signedShortArrays),
                    UInt32PlainFields = new ReadOnlyDictionary<string, uint>(plainUnsignedValues),
                    OrdinalFields = new ReadOnlyDictionary<string, ExperimentalBinaryOrdinal>(ordinals),
                    RawFields = new ReadOnlyDictionary<string, ExperimentalBinaryRawField>(rawFields)
                });
            }
        }
        if (blocks.Count == 0) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
        throw cursor.Error(ExperimentalBinarySiiError.Truncated);
    }

    private static readonly IReadOnlyDictionary<uint, string> EmptyOrdinalValues = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>());
    private static bool IsKnownType(uint type) => type is
        0x01 or 0x02 or 0x03 or 0x04 or 0x05 or 0x06 or
        0x07 or 0x08 or 0x09 or 0x0a or 0x11 or 0x12 or 0x17 or 0x18 or 0x19 or 0x1a or
        0x25 or 0x26 or 0x27 or 0x28 or 0x29 or 0x2a or 0x2b or 0x2c or 0x2f or
        0x31 or 0x32 or 0x33 or 0x34 or 0x35 or 0x36 or 0x37 or
        0x39 or 0x3a or 0x3b or 0x3c or 0x3d or 0x3e;
    private static int FixedValueSize(uint type, uint version) => type switch
    {
        0x07 => 8,
        0x09 or 0x0a or 0x11 or 0x12 => 12,
        0x17 or 0x18 => 16,
        0x19 or 0x1a => version == 1 ? 28 : 32,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    private sealed record Field(string Name, uint Type, IReadOnlyDictionary<uint, string> OrdinalValues);
    private sealed record Structure(
        string Name,
        IReadOnlyList<Field> Fields,
        IReadOnlyList<ExperimentalBinaryFieldDefinition> PublicFields);

    internal static string NamelessText(ulong id)
    {
        string hex = id.ToString("x", CultureInfo.InvariantCulture);
        int first = hex.Length % 4;
        if (first == 0) first = 4;
        var result = new StringBuilder("_nameless.").Append(hex.AsSpan(0, first));
        for (int i = first; i < hex.Length; i += 4) result.Append('.').Append(hex.AsSpan(i, 4));
        return result.ToString();
    }

    private ref struct Cursor(ReadOnlySpan<byte> input)
    {
        private readonly ReadOnlySpan<byte> _input = input;
        private int _position = 0;
        public bool End => _position == _input.Length;
        public int Position => _position;
        public ExperimentalBinarySiiException Error(
            ExperimentalBinarySiiError error,
            uint? unsupportedTypeCode = null,
            string? structureName = null,
            string? fieldName = null)
            => new(error, _position, unsupportedTypeCode, structureName, fieldName);
        public ReadOnlySpan<byte> Take(int count)
        {
            if (count < 0 || count > _input.Length - _position) throw Error(ExperimentalBinarySiiError.Truncated);
            ReadOnlySpan<byte> value = _input.Slice(_position, count);
            _position += count;
            return value;
        }
        public byte Byte() => Take(1)[0];
        public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
        public ushort UInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public short Int16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
        public int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        public long Int64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));
        public ulong UInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
        public void Skip(int count) => _ = Take(count);
        public void SkipFixedArray(int elementSize)
        {
            uint count = UInt32();
            if (count > MaximumArrayItems) throw Error(ExperimentalBinarySiiError.LimitExceeded);
            if ((long)count * elementSize > _input.Length - _position) throw Error(ExperimentalBinarySiiError.Truncated);
            Skip(checked((int)count * elementSize));
        }
        public byte[] CopyRange(int start, int end) => _input.Slice(start, end - start).ToArray();
        public IReadOnlyDictionary<uint, string> OrdinalValues()
        {
            uint count = UInt32();
            if (count > MaximumArrayItems) throw Error(ExperimentalBinarySiiError.LimitExceeded);
            var values = new Dictionary<uint, string>();
            for (uint i = 0; i < count; i++)
            {
                uint value = UInt32();
                string name = Text();
                if (!values.TryAdd(value, name)) throw Error(ExperimentalBinarySiiError.Ambiguous);
            }
            return new ReadOnlyDictionary<uint, string>(values);
        }
        public ExperimentalBinaryReference Reference(uint type)
        {
            byte marker = Byte();
            int count = marker == 0xff ? 1 : marker;
            var parts = new ulong[count];
            for (int i = 0; i < count; i++) parts[i] = UInt64();
            return new ExperimentalBinaryReference(type, marker, Array.AsReadOnly(parts));
        }
        public IReadOnlyList<ExperimentalBinaryReference> ReferenceArray(uint type)
        {
            uint count = UInt32();
            if (count > MaximumArrayItems) throw Error(ExperimentalBinarySiiError.LimitExceeded);
            if (count > _input.Length - _position) throw Error(ExperimentalBinarySiiError.Truncated);
            var values = new List<ExperimentalBinaryReference>((int)count);
            for (uint i = 0; i < count; i++) values.Add(Reference(type));
            return values.AsReadOnly();
        }
        public IReadOnlyList<ExperimentalBinaryFloat32> FloatArray()
        {
            int count = FixedArrayCount(4);
            var values = new List<ExperimentalBinaryFloat32>(count);
            for (int i = 0; i < count; i++) values.Add(new ExperimentalBinaryFloat32(UInt32()));
            return values.AsReadOnly();
        }
        public IReadOnlyList<ExperimentalBinaryFloat32Vector2> FloatVector2Array()
        {
            int count = FixedArrayCount(2 * sizeof(uint));
            var values = new List<ExperimentalBinaryFloat32Vector2>(count);
            for (int i = 0; i < count; i++)
                values.Add(new ExperimentalBinaryFloat32Vector2(new ExperimentalBinaryFloat32(UInt32()), new ExperimentalBinaryFloat32(UInt32())));
            return values.AsReadOnly();
        }
        public IReadOnlyList<int> Int32Array()
        {
            int count = FixedArrayCount(4); var result = new List<int>(count);
            for (int i = 0; i < count; i++) result.Add(Int32());
            return result.AsReadOnly();
        }
        public IReadOnlyList<ExperimentalBinaryUInt32> UInt32Array()
        {
            int count = FixedArrayCount(4); var result = new List<ExperimentalBinaryUInt32>(count);
            for (int i = 0; i < count; i++) result.Add(new ExperimentalBinaryUInt32(UInt32()));
            return result.AsReadOnly();
        }
        public IReadOnlyList<long> Int64Array()
        {
            int count = FixedArrayCount(8); var result = new List<long>(count);
            for (int i = 0; i < count; i++) result.Add(Int64());
            return result.AsReadOnly();
        }
        public IReadOnlyList<ExperimentalBinaryUInt64> UInt64Array()
        {
            int count = FixedArrayCount(8); var result = new List<ExperimentalBinaryUInt64>(count);
            for (int i = 0; i < count; i++) result.Add(new ExperimentalBinaryUInt64(UInt64()));
            return result.AsReadOnly();
        }
        public IReadOnlyList<ExperimentalBinaryBool> BoolArray()
        {
            int count = FixedArrayCount(1); var result = new List<ExperimentalBinaryBool>(count);
            for (int i = 0; i < count; i++) result.Add(new ExperimentalBinaryBool(Byte()));
            return result.AsReadOnly();
        }
        public IReadOnlyList<ushort> UInt16Array()
        {
            int count = FixedArrayCount(2); var result = new List<ushort>(count);
            for (int i = 0; i < count; i++) result.Add(UInt16());
            return result.AsReadOnly();
        }
        public IReadOnlyList<short> Int16Array()
        {
            int count = FixedArrayCount(sizeof(short)); var result = new List<short>(count);
            for (int i = 0; i < count; i++) result.Add(Int16());
            return result.AsReadOnly();
        }
        public IReadOnlyList<string> TextArray()
        {
            int count = FixedArrayCount(4); var result = new List<string>(count);
            for (int i = 0; i < count; i++) result.Add(Text());
            return result.AsReadOnly();
        }
        public IReadOnlyList<ExperimentalBinaryEncodedString> EncodedStringArray()
        {
            int count = FixedArrayCount(8); var result = new List<ExperimentalBinaryEncodedString>(count);
            for (int i = 0; i < count; i++) result.Add(new ExperimentalBinaryEncodedString(UInt64()));
            return result.AsReadOnly();
        }
        private int FixedArrayCount(int elementSize)
        {
            uint count = UInt32();
            if (count > MaximumArrayItems) throw Error(ExperimentalBinarySiiError.LimitExceeded);
            if ((long)count * elementSize > _input.Length - _position) throw Error(ExperimentalBinarySiiError.Truncated);
            return (int)count;
        }
        public string Text()
        {
            uint length = UInt32();
            if (length > MaximumStringBytes) throw Error(ExperimentalBinarySiiError.LimitExceeded);
            try { return StrictUtf8.GetString(Take((int)length)); }
            catch (DecoderFallbackException) { throw Error(ExperimentalBinarySiiError.InvalidStringEncoding); }
        }
        public string Name()
        {
            uint length = UInt32();
            if (length is 0 or > MaximumNameBytes) throw Error(ExperimentalBinarySiiError.LimitExceeded);
            ReadOnlySpan<byte> bytes = Take((int)length);
            foreach (byte value in bytes)
                if (!(value is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'_'))
                    throw Error(ExperimentalBinarySiiError.UnsupportedName);
            return Encoding.ASCII.GetString(bytes);
        }
    }
}

public sealed record ExperimentalBinaryBlock(string Type, ulong? NamelessId, IReadOnlyDictionary<string, int> Int32Fields)
{
    public uint BinaryVersion { get; init; }
    public IReadOnlyList<ExperimentalBinaryFieldDefinition> FieldDefinitions { get; init; } = Array.Empty<ExperimentalBinaryFieldDefinition>();
    /// <summary>Original block marker and parts; null only for caller-constructed records.</summary>
    public ExperimentalBinaryBlockIdentifier? Identifier { get; init; }
    public IReadOnlyDictionary<string, ExperimentalBinaryUInt32> UInt32Fields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryUInt32>(new Dictionary<string, ExperimentalBinaryUInt32>());
    public IReadOnlyDictionary<string, long> Int64Fields { get; init; } = new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());
    public IReadOnlyDictionary<string, ExperimentalBinaryUInt64> UInt64Fields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryUInt64>(new Dictionary<string, ExperimentalBinaryUInt64>());
    public IReadOnlyDictionary<string, string> StringFields { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    public IReadOnlyDictionary<string, ExperimentalBinaryReference> ReferenceFields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryReference>(new Dictionary<string, ExperimentalBinaryReference>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryReference>> ReferenceArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryReference>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryReference>>());
    public IReadOnlyDictionary<string, ExperimentalBinaryFloat32> Float32Fields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryFloat32>(new Dictionary<string, ExperimentalBinaryFloat32>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>> Float32ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32Vector2>> Float32Vector2ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32Vector2>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryFloat32Vector2>>());
    public IReadOnlyDictionary<string, IReadOnlyList<int>> Int32ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<int>>(new Dictionary<string, IReadOnlyList<int>>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>> UInt32ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>());
    public IReadOnlyDictionary<string, IReadOnlyList<long>> Int64ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<long>>(new Dictionary<string, IReadOnlyList<long>>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>> UInt64ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>());
    public IReadOnlyDictionary<string, ExperimentalBinaryBool> BoolFields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryBool>(new Dictionary<string, ExperimentalBinaryBool>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryBool>> BoolArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryBool>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryBool>>());
    public IReadOnlyDictionary<string, IReadOnlyList<string>> StringArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>());
    public IReadOnlyDictionary<string, ExperimentalBinaryEncodedString> EncodedStringFields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryEncodedString>(new Dictionary<string, ExperimentalBinaryEncodedString>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryEncodedString>> EncodedStringArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryEncodedString>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryEncodedString>>());
    public IReadOnlyDictionary<string, ushort> UInt16Fields { get; init; } = new ReadOnlyDictionary<string, ushort>(new Dictionary<string, ushort>());
    public IReadOnlyDictionary<string, IReadOnlyList<ushort>> UInt16ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ushort>>(new Dictionary<string, IReadOnlyList<ushort>>());
    public IReadOnlyDictionary<string, short> Int16Fields { get; init; } = new ReadOnlyDictionary<string, short>(new Dictionary<string, short>());
    public IReadOnlyDictionary<string, IReadOnlyList<short>> Int16ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<short>>(new Dictionary<string, IReadOnlyList<short>>());
    public IReadOnlyDictionary<string, uint> UInt32PlainFields { get; init; } = new ReadOnlyDictionary<string, uint>(new Dictionary<string, uint>());
    public IReadOnlyDictionary<string, ExperimentalBinaryOrdinal> OrdinalFields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryOrdinal>(new Dictionary<string, ExperimentalBinaryOrdinal>());
    /// <summary>Original encoded payload for every field, including opaque vector and placement values.</summary>
    public IReadOnlyDictionary<string, ExperimentalBinaryRawField> RawFields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryRawField>(new Dictionary<string, ExperimentalBinaryRawField>());
}

/// <summary>Ordered field declaration from the block's BSII structure definition.</summary>
public sealed record ExperimentalBinaryFieldDefinition(string Name, uint TypeCode, IReadOnlyDictionary<uint, string> OrdinalValues);

/// <summary>Exact encoded bytes for one BSII field value; bytes are exposed through a read-only collection.</summary>
public sealed record ExperimentalBinaryRawField
{
    public ExperimentalBinaryRawField(uint typeCode, int byteOffset, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (byteOffset < 0) throw new ArgumentOutOfRangeException(nameof(byteOffset));
        TypeCode = typeCode;
        ByteOffset = byteOffset;
        Bytes = Array.AsReadOnly((byte[])bytes.Clone());
    }

    public uint TypeCode { get; }
    /// <summary>Absolute byte offset in the original BSII payload.</summary>
    public int ByteOffset { get; }
    public IReadOnlyList<byte> Bytes { get; }
}

/// <summary>Ordinal integer value and the source definition's optional symbolic name.</summary>
public readonly record struct ExperimentalBinaryOrdinal(uint Raw, IReadOnlyDictionary<uint, string> Names)
{
    public string? Name => Names is not null && Names.TryGetValue(Raw, out string? value) ? value : null;
}
/// <summary>A packed value with its high bit intact; text is a legacy interpretation only.</summary>
public readonly record struct ExperimentalBinaryEncodedString(ulong Raw)
{
    public bool LegacyTextMasksHighBit => (Raw & 0x8000000000000000ul) != 0;
    public bool TryGetLegacyText(out string? text)
    {
        if ((Raw & 0x7ffffffffffffffful) == 0) { text = string.Empty; return true; }
        return new ExperimentalBinaryReference(0, 1, Array.AsReadOnly(new[] { Raw })).TryGetLegacyText(out text);
    }
}
/// <summary>Original byte and the historical zero/nonzero interpretation.</summary>
public readonly record struct ExperimentalBinaryBool(byte Raw)
{
    public bool LegacyValue => Raw != 0;
}
/// <summary>Raw block identity; a legacy text match does not establish game semantics.</summary>
public sealed record ExperimentalBinaryBlockIdentifier(byte Marker, IReadOnlyList<ulong> RawParts)
{
    public bool IsNameless => Marker == 0xff;
    public bool TryGetLegacyText(out string? text) => new ExperimentalBinaryReference(0, Marker, RawParts).TryGetLegacyText(out text);
}
/// <summary>Bit-preserving IEEE-754 view; non-finite values are not declared valid game values.</summary>
public readonly record struct ExperimentalBinaryFloat32(uint RawBits)
{
    public float Value => BitConverter.Int32BitsToSingle(unchecked((int)RawBits));
    public bool IsFinite => float.IsFinite(Value);
}

public readonly record struct ExperimentalBinaryFloat32Vector2(ExperimentalBinaryFloat32 X, ExperimentalBinaryFloat32 Y);
/// <summary>Preserves reference type, marker and every original 64-bit part; it does not resolve ownership.</summary>
public sealed record ExperimentalBinaryReference(uint TypeCode, byte Marker, IReadOnlyList<ulong> RawParts)
{
    public bool IsNullMarker => Marker == 0;
    public bool IsNamelessMarker => Marker == 0xff;

    /// <summary>Attempts only the legacy textual interpretation; identical text may originate from different raw bits.</summary>
    public bool TryGetLegacyText(out string? text)
    {
        text = null;
        if (Marker == 0 || Marker == 0xff || RawParts.Count != Marker) return false;
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz_";
        var parts = new List<string>(RawParts.Count);
        foreach (ulong raw in RawParts)
        {
            ulong value = raw & 0x7ffffffffffffffful;
            var part = new StringBuilder();
            while (value != 0)
            {
                int digit = (int)(value % 38);
                if (digit == 0 || part.Length == 12) return false;
                part.Append(alphabet[digit - 1]);
                value /= 38;
            }
            if (part.Length == 0) return false;
            parts.Add(part.ToString());
        }
        text = string.Join('.', parts);
        return true;
    }
}
/// <summary>Raw 0x27 bytes are retained; the legacy decoder renders all bits set as nil.</summary>
public readonly record struct ExperimentalBinaryUInt32(uint Raw)
{
    public bool IsLegacyNil => Raw == uint.MaxValue;
    public uint? LegacyValue => IsLegacyNil ? null : Raw;
}
/// <summary>Raw 0x33 bits are preserved; the legacy decoder prints nil when either 32-bit half is all ones.</summary>
public readonly record struct ExperimentalBinaryUInt64(ulong Raw)
{
    public bool LegacyRendersNil => (Raw & uint.MaxValue) == uint.MaxValue || (Raw >> 32) == uint.MaxValue;
    public ulong? LegacyValue => LegacyRendersNil ? null : Raw;
}
public enum ExperimentalBinarySiiError { InvalidHeader, UnsupportedVersion, Truncated, UnsupportedType, UnsupportedIdentifier, UnsupportedName, InvalidStringEncoding, InvalidStructure, Ambiguous, LimitExceeded, TrailingData }
public sealed class ExperimentalBinarySiiException(
    ExperimentalBinarySiiError error,
    int offset,
    uint? unsupportedTypeCode = null,
    string? structureName = null,
    string? fieldName = null)
    : FormatException($"Unsupported or invalid experimental binary SII ({error}) at byte {offset}" +
        (unsupportedTypeCode is uint type ? $"; type 0x{type:x8}" : string.Empty) +
        (structureName is null ? string.Empty : $"; structure '{structureName}'") +
        (fieldName is null ? string.Empty : $"; field '{fieldName}'") + ".")
{
    public ExperimentalBinarySiiError Error { get; } = error;
    public int Offset { get; } = offset;
    public uint? UnsupportedTypeCode { get; } = unsupportedTypeCode;
    public string? StructureName { get; } = structureName;
    public string? FieldName { get; } = fieldName;
}
