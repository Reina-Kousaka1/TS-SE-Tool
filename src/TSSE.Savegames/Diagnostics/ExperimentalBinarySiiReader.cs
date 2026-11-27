using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Read-only experiment for a narrow synthetic historical BSII subset. It does
/// not open files, decode encryption, write, or establish game compatibility.
/// Unknown versions, field types and IDs fail without returning partial data.
/// </summary>
public static class ExperimentalBinarySiiReader
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    private const int MaximumStructures = 1024, MaximumBlocks = 4096, MaximumFieldsPerStructure = 256, MaximumNameBytes = 128, MaximumStringBytes = 65536, MaximumArrayItems = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static IReadOnlyList<ExperimentalBinaryBlock> Read(ReadOnlySpan<byte> input)
    {
        if (input.Length > MaximumBytes) throw new ExperimentalBinarySiiException(ExperimentalBinarySiiError.LimitExceeded, 0);
        var cursor = new Cursor(input);
        if (!cursor.Take(4).SequenceEqual("BSII"u8)) throw cursor.Error(ExperimentalBinarySiiError.InvalidHeader);
        if (cursor.UInt32() != 2) throw cursor.Error(ExperimentalBinarySiiError.UnsupportedVersion);
        var structures = new Dictionary<uint, Structure>();
        var blocks = new List<ExperimentalBinaryBlock>();
        var ids = new HashSet<ulong>();
        while (!cursor.End)
        {
            uint kind = cursor.UInt32();
            if (kind == 0)
            {
                byte valid = cursor.Byte();
                if (valid == 0)
                {
                    if (!cursor.End) throw cursor.Error(ExperimentalBinarySiiError.TrailingData);
                    if (blocks.Count == 0) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
                    return blocks.AsReadOnly();
                }
                if (valid != 1 || structures.Count >= MaximumStructures) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
                uint id = cursor.UInt32();
                if (id == 0 || structures.ContainsKey(id)) throw cursor.Error(ExperimentalBinarySiiError.Ambiguous);
                string name = cursor.Name();
                var fields = new List<Field>();
                var fieldNames = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    uint type = cursor.UInt32();
                    if (type == 0) break;
                    if (type is not (0x01 or 0x05 or 0x06 or 0x25 or 0x26 or 0x27 or 0x28 or 0x31 or 0x32 or 0x33 or 0x34 or 0x39 or 0x3a or 0x3b or 0x3c or 0x3d)) throw cursor.Error(ExperimentalBinarySiiError.UnsupportedType);
                    if (fields.Count >= MaximumFieldsPerStructure) throw cursor.Error(ExperimentalBinarySiiError.LimitExceeded);
                    string field = cursor.Name();
                    if (!fieldNames.Add(field)) throw cursor.Error(ExperimentalBinarySiiError.Ambiguous);
                    fields.Add(new Field(field, type));
                }
                structures.Add(id, new Structure(name, fields.AsReadOnly()));
            }
            else
            {
                if (blocks.Count >= MaximumBlocks) throw cursor.Error(ExperimentalBinarySiiError.LimitExceeded);
                if (!structures.TryGetValue(kind, out Structure? structure)) throw cursor.Error(ExperimentalBinarySiiError.InvalidStructure);
                if (cursor.Byte() != 0xff) throw cursor.Error(ExperimentalBinarySiiError.UnsupportedIdentifier);
                ulong id = cursor.UInt64();
                if (!ids.Add(id)) throw cursor.Error(ExperimentalBinarySiiError.Ambiguous);
                var values = new Dictionary<string, int>(StringComparer.Ordinal);
                var unsignedValues = new Dictionary<string, ExperimentalBinaryUInt32>(StringComparer.Ordinal);
                var longValues = new Dictionary<string, long>(StringComparer.Ordinal);
                var unsignedLongValues = new Dictionary<string, ExperimentalBinaryUInt64>(StringComparer.Ordinal);
                var stringValues = new Dictionary<string, string>(StringComparer.Ordinal);
                var references = new Dictionary<string, ExperimentalBinaryReference>(StringComparer.Ordinal);
                var referenceArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryReference>>(StringComparer.Ordinal);
                var floats = new Dictionary<string, ExperimentalBinaryFloat32>(StringComparer.Ordinal);
                var floatArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>(StringComparer.Ordinal);
                var intArrays = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
                var uintArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>(StringComparer.Ordinal);
                var longArrays = new Dictionary<string, IReadOnlyList<long>>(StringComparer.Ordinal);
                var ulongArrays = new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>(StringComparer.Ordinal);
                foreach (Field field in structure.Fields)
                {
                    switch (field.Type)
                    {
                        case 0x25: values.Add(field.Name, cursor.Int32()); break;
                        case 0x27: unsignedValues.Add(field.Name, new ExperimentalBinaryUInt32(cursor.UInt32())); break;
                        case 0x31: longValues.Add(field.Name, cursor.Int64()); break;
                        case 0x33: unsignedLongValues.Add(field.Name, new ExperimentalBinaryUInt64(cursor.UInt64())); break;
                        case 0x01: stringValues.Add(field.Name, cursor.Text()); break;
                        case 0x05: floats.Add(field.Name, new ExperimentalBinaryFloat32(cursor.UInt32())); break;
                        case 0x06: floatArrays.Add(field.Name, cursor.FloatArray()); break;
                        case 0x26: intArrays.Add(field.Name, cursor.Int32Array()); break;
                        case 0x28: uintArrays.Add(field.Name, cursor.UInt32Array()); break;
                        case 0x32: longArrays.Add(field.Name, cursor.Int64Array()); break;
                        case 0x34: ulongArrays.Add(field.Name, cursor.UInt64Array()); break;
                        case 0x39 or 0x3b or 0x3d: references.Add(field.Name, cursor.Reference(field.Type)); break;
                        case 0x3a or 0x3c: referenceArrays.Add(field.Name, cursor.ReferenceArray(field.Type)); break;
                        default: throw cursor.Error(ExperimentalBinarySiiError.UnsupportedType);
                    }
                }
                blocks.Add(new ExperimentalBinaryBlock(structure.Name, id, new ReadOnlyDictionary<string, int>(values))
                {
                    UInt32Fields = new ReadOnlyDictionary<string, ExperimentalBinaryUInt32>(unsignedValues),
                    Int64Fields = new ReadOnlyDictionary<string, long>(longValues),
                    UInt64Fields = new ReadOnlyDictionary<string, ExperimentalBinaryUInt64>(unsignedLongValues),
                    StringFields = new ReadOnlyDictionary<string, string>(stringValues),
                    ReferenceFields = new ReadOnlyDictionary<string, ExperimentalBinaryReference>(references),
                    ReferenceArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryReference>>(referenceArrays),
                    Float32Fields = new ReadOnlyDictionary<string, ExperimentalBinaryFloat32>(floats),
                    Float32ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>(floatArrays),
                    Int32ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<int>>(intArrays),
                    UInt32ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>(uintArrays),
                    Int64ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<long>>(longArrays),
                    UInt64ArrayFields = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>(ulongArrays)
                });
            }
        }
        throw cursor.Error(ExperimentalBinarySiiError.Truncated);
    }

    private sealed record Field(string Name, uint Type);
    private sealed record Structure(string Name, IReadOnlyList<Field> Fields);

    private ref struct Cursor(ReadOnlySpan<byte> input)
    {
        private readonly ReadOnlySpan<byte> _input = input;
        private int _position = 0;
        public bool End => _position == _input.Length;
        public ExperimentalBinarySiiException Error(ExperimentalBinarySiiError error) => new(error, _position);
        public ReadOnlySpan<byte> Take(int count)
        {
            if (count < 0 || count > _input.Length - _position) throw Error(ExperimentalBinarySiiError.Truncated);
            ReadOnlySpan<byte> value = _input.Slice(_position, count);
            _position += count;
            return value;
        }
        public byte Byte() => Take(1)[0];
        public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
        public int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        public long Int64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));
        public ulong UInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
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

public sealed record ExperimentalBinaryBlock(string Type, ulong NamelessId, IReadOnlyDictionary<string, int> Int32Fields)
{
    public IReadOnlyDictionary<string, ExperimentalBinaryUInt32> UInt32Fields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryUInt32>(new Dictionary<string, ExperimentalBinaryUInt32>());
    public IReadOnlyDictionary<string, long> Int64Fields { get; init; } = new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());
    public IReadOnlyDictionary<string, ExperimentalBinaryUInt64> UInt64Fields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryUInt64>(new Dictionary<string, ExperimentalBinaryUInt64>());
    public IReadOnlyDictionary<string, string> StringFields { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    public IReadOnlyDictionary<string, ExperimentalBinaryReference> ReferenceFields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryReference>(new Dictionary<string, ExperimentalBinaryReference>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryReference>> ReferenceArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryReference>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryReference>>());
    public IReadOnlyDictionary<string, ExperimentalBinaryFloat32> Float32Fields { get; init; } = new ReadOnlyDictionary<string, ExperimentalBinaryFloat32>(new Dictionary<string, ExperimentalBinaryFloat32>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>> Float32ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryFloat32>>());
    public IReadOnlyDictionary<string, IReadOnlyList<int>> Int32ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<int>>(new Dictionary<string, IReadOnlyList<int>>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>> UInt32ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt32>>());
    public IReadOnlyDictionary<string, IReadOnlyList<long>> Int64ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<long>>(new Dictionary<string, IReadOnlyList<long>>());
    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>> UInt64ArrayFields { get; init; } = new ReadOnlyDictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>(new Dictionary<string, IReadOnlyList<ExperimentalBinaryUInt64>>());
}
/// <summary>Bit-preserving IEEE-754 view; non-finite values are not declared valid game values.</summary>
public readonly record struct ExperimentalBinaryFloat32(uint RawBits)
{
    public float Value => BitConverter.Int32BitsToSingle(unchecked((int)RawBits));
    public bool IsFinite => float.IsFinite(Value);
}
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
public sealed class ExperimentalBinarySiiException(ExperimentalBinarySiiError error, int offset) : FormatException($"Unsupported or invalid experimental binary SII ({error}) at byte {offset}.")
{
    public ExperimentalBinarySiiError Error { get; } = error;
    public int Offset { get; } = offset;
}
