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
    private const int MaximumStructures = 1024, MaximumBlocks = 4096, MaximumFieldsPerStructure = 256, MaximumNameBytes = 128;

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
                    if (type is not (0x25 or 0x27 or 0x31)) throw cursor.Error(ExperimentalBinarySiiError.UnsupportedType);
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
                foreach (Field field in structure.Fields)
                {
                    switch (field.Type)
                    {
                        case 0x25: values.Add(field.Name, cursor.Int32()); break;
                        case 0x27: unsignedValues.Add(field.Name, new ExperimentalBinaryUInt32(cursor.UInt32())); break;
                        case 0x31: longValues.Add(field.Name, cursor.Int64()); break;
                        default: throw cursor.Error(ExperimentalBinarySiiError.UnsupportedType);
                    }
                }
                blocks.Add(new ExperimentalBinaryBlock(structure.Name, id, new ReadOnlyDictionary<string, int>(values))
                {
                    UInt32Fields = new ReadOnlyDictionary<string, ExperimentalBinaryUInt32>(unsignedValues),
                    Int64Fields = new ReadOnlyDictionary<string, long>(longValues)
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
}
/// <summary>Raw 0x27 bytes are retained; the legacy decoder renders all bits set as nil.</summary>
public readonly record struct ExperimentalBinaryUInt32(uint Raw)
{
    public bool IsLegacyNil => Raw == uint.MaxValue;
    public uint? LegacyValue => IsLegacyNil ? null : Raw;
}
public enum ExperimentalBinarySiiError { InvalidHeader, UnsupportedVersion, Truncated, UnsupportedType, UnsupportedIdentifier, UnsupportedName, InvalidStructure, Ambiguous, LimitExceeded, TrailingData }
public sealed class ExperimentalBinarySiiException(ExperimentalBinarySiiError error, int offset) : FormatException($"Unsupported or invalid experimental binary SII ({error}) at byte {offset}.")
{
    public ExperimentalBinarySiiError Error { get; } = error;
    public int Offset { get; } = offset;
}
