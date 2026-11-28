using System.Text;
using TSSE.Savegames.Diagnostics;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class ExperimentalBinarySiiReaderTests
{
    [Fact]
    public void ExcessiveStructureCountReportsLimitExceeded()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("BSII"u8);
            writer.Write(2u);
            for (uint id = 1; id <= 1025; id++)
            {
                writer.Write(0u);
                writer.Write((byte)1);
                writer.Write(id);
                byte[] name = Encoding.ASCII.GetBytes("synthetic");
                writer.Write((uint)name.Length);
                writer.Write(name);
                writer.Write(0u);
            }
            writer.Write(0u);
            writer.Write((byte)0);
        }

        var exception = Assert.Throws<ExperimentalBinarySiiException>(() =>
            ExperimentalBinarySiiReader.Read(stream.ToArray()));
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, exception.Error);
    }

    [Fact]
    public void ExcessiveBlockCountReportsLimitExceeded()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("BSII"u8);
            writer.Write(2u);
            writer.Write(0u);
            writer.Write((byte)1);
            writer.Write(1u);
            byte[] name = Encoding.ASCII.GetBytes("synthetic");
            writer.Write((uint)name.Length);
            writer.Write(name);
            writer.Write(0u);
            for (ulong id = 1; id <= (ulong)ExperimentalBinarySiiReader.MaximumBlocks + 1; id++)
            {
                writer.Write(1u);
                writer.Write((byte)0xff);
                writer.Write(id);
            }
            writer.Write(0u);
            writer.Write((byte)0);
        }

        var exception = Assert.Throws<ExperimentalBinarySiiException>(() =>
            ExperimentalBinarySiiReader.Read(stream.ToArray()));
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, exception.Error);
    }

    [Fact]
    public void ExcessiveFieldCountReportsLimitExceeded()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("BSII"u8);
            writer.Write(2u);
            writer.Write(0u);
            writer.Write((byte)1);
            writer.Write(1u);
            WriteName(writer, "synthetic");
            for (var index = 0; index < 513; index++)
            {
                writer.Write(0x25u);
                WriteName(writer, $"field_{index}");
            }
        }

        var exception = Assert.Throws<ExperimentalBinarySiiException>(() =>
            ExperimentalBinarySiiReader.Read(stream.ToArray()));
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, exception.Error);
    }

    private static void WriteName(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }

    [Fact]
    public void ReadsOnlyTheSupportedSyntheticStructureWithoutChangingInput()
    {
        byte[] input = Fixture();
        byte[] before = (byte[])input.Clone();
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(input));
        Assert.Equal("synthetic", block.Type);
        Assert.Equal(0x1234ul, block.NamelessId);
        Assert.True(block.Identifier!.IsNameless);
        Assert.Equal(42, block.Int32Fields["value"]);
        Assert.Equal(uint.MaxValue, block.UInt32Fields["xp"].Raw);
        Assert.True(block.UInt32Fields["xp"].IsLegacyNil);
        Assert.Null(block.UInt32Fields["xp"].LegacyValue);
        Assert.Equal(uint.MaxValue - 1, block.UInt32Fields["xp_value"].LegacyValue);
        Assert.False(block.UInt32Fields["xp_value"].IsLegacyNil);
        Assert.Equal(long.MinValue, block.Int64Fields["balance"]);
        Assert.Equal("route demo", block.StringFields["label"]);
        Assert.True(block.UInt64Fields["wide_nil"].LegacyRendersNil);
        Assert.True(block.UInt64Fields["wide_high"].LegacyRendersNil);
        Assert.True(block.UInt64Fields["wide_low"].LegacyRendersNil);
        Assert.Equal(0xffffffff00000001ul, block.UInt64Fields["wide_high"].Raw);
        Assert.Equal(0x00000001fffffffful, block.UInt64Fields["wide_low"].Raw);
        Assert.Equal(9876543210ul, block.UInt64Fields["wide_value"].LegacyValue);
        Assert.True(block.ReferenceFields["empty_ref"].IsNullMarker);
        Assert.Empty(block.ReferenceFields["empty_ref"].RawParts);
        Assert.True(block.ReferenceFields["anon_ref"].IsNamelessMarker);
        Assert.Equal(0x9876ul, Assert.Single(block.ReferenceFields["anon_ref"].RawParts));
        var named = block.ReferenceFields["named_ref"];
        Assert.Equal(0x3bu, named.TypeCode);
        Assert.Equal(0x800000000000000bul, named.RawParts[0]);
        Assert.True(named.TryGetLegacyText(out string? text));
        Assert.Equal("a.b", text);
        Assert.Empty(block.ReferenceArrayFields["empty_refs"]);
        var refs = block.ReferenceArrayFields["refs"];
        Assert.Equal(4, refs.Count);
        Assert.True(refs[0].IsNullMarker);
        Assert.True(refs[1].IsNamelessMarker);
        Assert.Equal(0x9876ul, Assert.Single(refs[1].RawParts));
        Assert.True(refs[2].TryGetLegacyText(out string? arrayText));
        Assert.Equal("a.b", arrayText);
        Assert.Equal(refs[2].RawParts, refs[3].RawParts);
        Assert.Equal(0x3cu, refs[2].TypeCode);
        Assert.Equal(0x3f000000u, block.Float32Fields["fuel"].RawBits);
        Assert.Equal(0.5f, block.Float32Fields["fuel"].Value);
        Assert.Equal(new uint[] { 0x3f000000, 0x3fa00000, 0x80000000 }, block.Float32ArrayFields["wear"].Select(value => value.RawBits));
        Assert.Equal(new[] { int.MinValue, int.MaxValue }, block.Int32ArrayFields["signed_values"]);
        Assert.Equal(new uint[] { 0, uint.MaxValue, uint.MaxValue - 1 }, block.UInt32ArrayFields["unsigned_values"].Select(value => value.Raw));
        Assert.True(block.UInt32ArrayFields["unsigned_values"][1].IsLegacyNil);
        Assert.Equal(new[] { long.MinValue, long.MaxValue }, block.Int64ArrayFields["long_values"]);
        Assert.True(block.UInt64ArrayFields["wide_values"][1].LegacyRendersNil);
        Assert.Equal(0xffffffff00000001ul, block.UInt64ArrayFields["wide_values"][1].Raw);
        Assert.Equal((byte)255, block.BoolFields["active"].Raw);
        Assert.True(block.BoolFields["active"].LegacyValue);
        Assert.Equal(new byte[] { 0, 1, 2, 255 }, block.BoolArrayFields["flags"].Select(value => value.Raw));
        Assert.Equal(new[] { false, true, true, true }, block.BoolArrayFields["flags"].Select(value => value.LegacyValue));
        Assert.Equal(new[] { "", "road demo", "abc" }, block.StringArrayFields["labels"]);
        var encoded = block.EncodedStringFields["encoded"];
        Assert.Equal(0x800000000000000bul, encoded.Raw);
        Assert.True(encoded.LegacyTextMasksHighBit);
        Assert.True(encoded.TryGetLegacyText(out string? encodedText));
        Assert.Equal("a", encodedText);
        var encodedArray = block.EncodedStringArrayFields["encoded_list"];
        Assert.Equal(new ulong[] { 0, 12, 0x800000000000000bul, 12 }, encodedArray.Select(value => value.Raw));
        Assert.True(encodedArray[0].TryGetLegacyText(out string? empty));
        Assert.Equal("", empty);
        Assert.Equal(before, input);
    }

    [Fact]
    public void NamedBlockIdentityPreservesRawPartsAndRejectsLegacyTextCollisions()
    {
        ulong[] named = [0x800000000000000bul, 12];
        byte[] input = Fixture(namedBlockParts: named);
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(input));
        Assert.Null(block.NamelessId);
        var identifier = Assert.IsType<ExperimentalBinaryBlockIdentifier>(block.Identifier);
        Assert.False(identifier.IsNameless);
        Assert.Equal(named, identifier.RawParts);
        Assert.True(identifier.TryGetLegacyText(out string? text));
        Assert.Equal("a.b", text);
        Assert.Equal(ExperimentalBinarySiiError.Ambiguous,
            Reject(Fixture(duplicateBlock: true, namedBlockParts: [11, 12], secondNamedBlockParts: named)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Ambiguous,
            Reject(Fixture(duplicateBlock: true, secondNamedBlockParts: [Encode("_nameless"), Encode("1234")])).Error);
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedIdentifier, Reject(Fixture(namedBlockParts: [38])).Error);
        for (int length = 0; length < input.Length; length++)
        {
            byte[] prefix = input[..length];
            Assert.Throws<ExperimentalBinarySiiException>(() => ExperimentalBinarySiiReader.Read(prefix));
        }
    }

    [Fact]
    public void ReferenceIndexResolvesOnlyExactRawIdentity()
    {
        ulong[] named = [0x800000000000000bul, 12];
        var blocks = ExperimentalBinarySiiReader.Read(Fixture(namedBlockParts: named));
        var block = Assert.Single(blocks);
        var index = new ExperimentalBinaryReferenceIndex(blocks);
        var exact = index.Inspect(block.ReferenceFields["named_ref"]);
        Assert.Equal(ExperimentalReferenceMatchKind.ExactRawIdentity, exact.Kind);
        Assert.Equal(0, exact.TargetIndex);
        Assert.Equal(ExperimentalReferenceMatchKind.NullMarker, index.Inspect(block.ReferenceFields["empty_ref"]).Kind);
        Assert.Equal(ExperimentalReferenceMatchKind.Missing, index.Inspect(block.ReferenceFields["anon_ref"]).Kind);
        var displayOnly = index.Inspect(new ExperimentalBinaryReference(0x39, 2, new ulong[] { 11, 12 }));
        Assert.Equal(ExperimentalReferenceMatchKind.LegacyTextOnly, displayOnly.Kind);
        Assert.Null(displayOnly.TargetIndex);
        Assert.Equal(ExperimentalReferenceMatchKind.Unsupported, index.Inspect(new ExperimentalBinaryReference(0x39, 1, new ulong[] { 38 })).Kind);
        Assert.Equal(ExperimentalReferenceMatchKind.Unsupported, index.Inspect(new ExperimentalBinaryReference(0x39, 0xff, Array.Empty<ulong>())).Kind);
        var ambiguous = new ExperimentalBinaryReferenceIndex([block, block]).Inspect(block.ReferenceFields["named_ref"]);
        Assert.Equal(ExperimentalReferenceMatchKind.Ambiguous, ambiguous.Kind);
        Assert.Null(ambiguous.TargetIndex);
    }

    [Fact]
    public void ReferenceIndexSnapshotsExternallyProvidedIdentifiers()
    {
        ulong[] parts = [11, 12];
        var block = new ExperimentalBinaryBlock("example", null, new Dictionary<string, int>())
        {
            Identifier = new ExperimentalBinaryBlockIdentifier(2, parts)
        };
        var index = new ExperimentalBinaryReferenceIndex([block]);
        parts[0] = 13;
        Assert.Equal(ExperimentalReferenceMatchKind.ExactRawIdentity,
            index.Inspect(new ExperimentalBinaryReference(0x39, 2, new ulong[] { 11, 12 })).Kind);
        Assert.Equal(ExperimentalReferenceMatchKind.Missing,
            index.Inspect(new ExperimentalBinaryReference(0x39, 2, new ulong[] { 13, 12 })).Kind);
    }

    [Fact]
    public void ReferenceIndexTreatsNamelessAndTextCollisionsConservatively()
    {
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(Fixture()));
        var index = new ExperimentalBinaryReferenceIndex([block]);
        var anonymous = new ExperimentalBinaryReference(0x39, 0xff, new ulong[] { 0x1234 });
        Assert.Equal(0, index.Inspect(anonymous).TargetIndex);
        Assert.Equal(ExperimentalReferenceMatchKind.ExactRawIdentity, index.Inspect(anonymous).Kind);
        Assert.Equal(ExperimentalReferenceMatchKind.Unsupported,
            index.Inspect(new ExperimentalBinaryReference(0x3e, 0xff, new ulong[] { 0x1234 })).Kind);
        var named = new ExperimentalBinaryReference(0x39, 2, new ulong[] { Encode("_nameless"), Encode("1234") });
        var collision = index.Inspect(named);
        Assert.Equal(ExperimentalReferenceMatchKind.LegacyTextOnly, collision.Kind);
        Assert.Null(collision.TargetIndex);
        var namedBlock = new ExperimentalBinaryBlock("example", null, new Dictionary<string, int>())
        {
            Identifier = new ExperimentalBinaryBlockIdentifier(2, new ulong[] { Encode("_nameless"), Encode("1234") })
        };
        Assert.Equal(ExperimentalReferenceMatchKind.Ambiguous,
            new ExperimentalBinaryReferenceIndex([block, namedBlock]).Inspect(anonymous).Kind);
        Assert.Throws<ArgumentException>(() => new ExperimentalBinaryReferenceIndex(new ExperimentalBinaryBlock[ExperimentalBinarySiiReader.MaximumBlocks + 1]));
        Assert.Throws<ArgumentException>(() => new ExperimentalBinaryReferenceIndex([new ExperimentalBinaryBlock("example", null, new Dictionary<string, int>())]));
    }

    private static ulong Encode(string text)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz_";
        ulong value = 0;
        for (int i = text.Length - 1; i >= 0; i--) value = checked(value * 38 + (ulong)(alphabet.IndexOf(text[i]) + 1));
        return value;
    }

    [Fact]
    public void FloatBitsPreserveNegativeZeroNanAndInfinityWithoutDeclaringThemValid()
    {
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(floatBits: [0x80000000, 0x7fc01234, 0x7f800000, 0x7f800001])));
        var values = block.Float32ArrayFields["wear"];
        Assert.Equal(0x80000000u, values[0].RawBits);
        Assert.Equal(0f, values[0].Value);
        Assert.Equal(0x7fc01234u, values[1].RawBits);
        Assert.True(float.IsNaN(values[1].Value));
        Assert.False(values[1].IsFinite);
        Assert.Equal(0x7f800000u, values[2].RawBits);
        Assert.False(values[2].IsFinite);
        Assert.Equal(0x7f800001u, values[3].RawBits);
        Assert.False(values[3].IsFinite);
        Assert.Equal(0x7f800001u, values[3].RawBits);
    }

    [Fact]
    public void FloatArrayCountIsBoundedBeforeAllocation()
    {
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(floatArrayCount: 65_537)).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(floatArrayCount: uint.MaxValue)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Truncated, Reject(Fixture(floatArrayCount: 65_536)).Error);
        Assert.Empty(Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(floatBits: []))).Float32ArrayFields["wear"]);
    }

    [Fact]
    public void IntegerArraysRejectExcessiveOrTruncatedLengths()
    {
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(integerArrayCount: 65_537)).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(integerArrayCount: uint.MaxValue)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Truncated, Reject(Fixture(integerArrayCount: 65_536)).Error);
        Assert.Empty(Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(integerArrayCount: 0))).Int32ArrayFields["signed_values"]);
    }

    [Fact]
    public void BooleanArraysRejectExcessiveOrTruncatedLengths()
    {
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(boolArrayCount: 65_537)).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(boolArrayCount: uint.MaxValue)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Truncated, Reject(Fixture(boolArrayCount: 65_536)).Error);
        Assert.Empty(Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(boolArrayCount: 0))).BoolArrayFields["flags"]);
    }

    [Fact]
    public void InvalidLegacyReferenceTextDoesNotDiscardOriginalBits()
    {
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(referencePart: 38)));
        var reference = block.ReferenceFields["named_ref"];
        Assert.Equal(38ul, reference.RawParts[0]);
        Assert.False(reference.TryGetLegacyText(out string? text));
        Assert.Null(text);
    }

    [Fact]
    public void Utf8StringsAreStrictBoundedAndPreserveUnicode()
    {
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(textBytes: Encoding.UTF8.GetBytes("Straße 😀"))));
        Assert.Equal("Straße 😀", block.StringFields["label"]);
        Assert.Equal(ExperimentalBinarySiiError.InvalidStringEncoding, Reject(Fixture(textBytes: [0xc3, 0x28])).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(textBytes: new byte[65537])).Error);
        Assert.Equal("", Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(textBytes: []))).StringFields["label"]);
    }

    [Fact]
    public void StringArraysRejectExcessiveCountsAndInvalidUtf8()
    {
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(stringArrayCount: 65_537)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Truncated, Reject(Fixture(stringArrayCount: 65_536)).Error);
        Assert.Empty(Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(stringArrayCount: 0))).StringArrayFields["labels"]);
        Assert.Equal(ExperimentalBinarySiiError.InvalidStringEncoding, Reject(Fixture(arrayStringBytes: [0xc3, 0x28])).Error);
        Assert.Equal("世界 😀", Assert.Single(ExperimentalBinarySiiReader.Read(Fixture(arrayStringBytes: Encoding.UTF8.GetBytes("世界 😀")))).StringArrayFields["labels"][1]);
        var invalid = new ExperimentalBinaryEncodedString(38);
        Assert.False(invalid.TryGetLegacyText(out string? text));
        Assert.Null(text);
        Assert.Equal(38ul, invalid.Raw);
        var highOnly = new ExperimentalBinaryEncodedString(0x8000000000000000ul);
        Assert.True(highOnly.LegacyTextMasksHighBit);
        Assert.True(highOnly.TryGetLegacyText(out string? empty));
        Assert.Equal("", empty);
        Assert.Equal(0x8000000000000000ul, highOnly.Raw);
    }

    [Fact]
    public void EveryTruncatedPrefixIsRejectedWithoutPartialResult()
    {
        byte[] input = Fixture();
        for (int length = 0; length < input.Length; length++)
        {
            byte[] prefix = input[..length];
            Assert.Throws<ExperimentalBinarySiiException>(() => ExperimentalBinarySiiReader.Read(prefix));
        }
    }

    [Fact]
    public void UnsupportedVersionTypeIdentifierAndIncompletePayloadFailClosed()
    {
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedVersion, Reject(Fixture(version: 4)).Error);
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedType, Reject(Fixture(fieldType: 0x3f)).Error);
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedType, Reject(Fixture(fieldType: 0x08)).Error);
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedType, Reject(Fixture(fieldType: 0x3e)).Error);
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedIdentifier, Reject(Fixture(idKind: 0)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Truncated, Reject([.. Fixture(), 0]).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(new byte[ExperimentalBinarySiiReader.MaximumBytes + 1]).Error);
    }

    [Fact]
    public void V3ReadsOrdinalAndAllDocumentedOpaqueAndShortValueTypesLosslessly()
    {
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(ModernFixture()));

        Assert.Equal(3u, block.BinaryVersion);
        Assert.Equal((ushort)513, block.UInt16Fields["small"]);
        Assert.Equal(new ushort[] { 1, ushort.MaxValue }, block.UInt16ArrayFields["small_list"]);
        Assert.Equal(uint.MaxValue, block.UInt32PlainFields["wide32"]);
        Assert.Equal(2u, block.OrdinalFields["mode"].Raw);
        Assert.Equal("two", block.OrdinalFields["mode"].Name);
        Assert.Null((block.OrdinalFields["mode"] with { Raw = 99 }).Name);
        Assert.Equal("vector2", block.FieldDefinitions[0].Name);
        Assert.Equal("mode", block.FieldDefinitions[^2].Name);
        Assert.Equal(new byte[] { 0, 0, 0x80, 0x3f, 0, 0, 0, 0x40 }, block.RawFields["vector2"].Bytes);
        Assert.Equal((uint)0x07, block.RawFields["vector2"].TypeCode);
        Assert.Equal(12, block.RawFields["vector3"].Bytes.Count);
        Assert.Equal(16, block.RawFields["vector3_list"].Bytes.Count);
        Assert.Equal(12, block.RawFields["int_vector3"].Bytes.Count);
        Assert.Equal(16, block.RawFields["int_vector3_list"].Bytes.Count);
        Assert.Equal(16, block.RawFields["quaternion"].Bytes.Count);
        Assert.Equal(20, block.RawFields["quaternion_list"].Bytes.Count);
        Assert.Equal(32, block.RawFields["placement"].Bytes.Count);
        Assert.Equal(36, block.RawFields["placements"].Bytes.Count);
        // RawFields preserves the encoded array count followed by its two ushort values.
        Assert.Equal(8, block.RawFields["small_list"].Bytes.Count);
    }

    [Fact]
    public void V3RequiresTerminatorAndBoundsOpaqueArraysBeforeSkipping()
    {
        byte[] complete = ModernFixture();
        Assert.Equal(ExperimentalBinarySiiError.Truncated, Reject(complete[..^5]).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded,
            Reject(ModernFixture(vector3ArrayCount: 65_537, writeVector3ArrayData: false)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Truncated,
            Reject(ModernFixture(vector3ArrayCount: 65_536, writeVector3ArrayData: false, stopAfterVector3ArrayCount: true)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Ambiguous, Reject(ModernFixture(duplicateOrdinalValues: true)).Error);
    }

    [Theory]
    [InlineData(1, 28, 32)]
    [InlineData(2, 32, 36)]
    [InlineData(3, 32, 36)]
    public void SupportsAllDocumentedVersionsAndPlacementWidths(uint version, int scalarWidth, int arrayWidth)
    {
        ExperimentalBinaryBlock block = Assert.Single(ExperimentalBinarySiiReader.Read(ModernFixture(version: version)));

        Assert.Equal(version, block.BinaryVersion);
        Assert.Equal(8, block.RawFields["vector2"].Bytes.Count);
        Assert.Equal(12, block.RawFields["vector3"].Bytes.Count);
        Assert.Equal(scalarWidth, block.RawFields["placement"].Bytes.Count);
        Assert.Equal(arrayWidth, block.RawFields["placements"].Bytes.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EachDocumentedVersionRequiresAnExplicitTerminator(uint version)
        => Assert.Equal(ExperimentalBinarySiiError.Truncated,
            Reject(ModernFixture(version: version)[..^5]).Error);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RejectsDataAfterTerminatorForEveryDocumentedVersion(uint version)
        => Assert.Equal(ExperimentalBinarySiiError.InvalidStructure,
            Reject([.. ModernFixture(version: version), 0]).Error);

    [Fact]
    public void DuplicateFieldNamesAndDuplicateBlockIdsAreRejected()
    {
        Assert.Equal(ExperimentalBinarySiiError.Ambiguous, Reject(Fixture(duplicateField: true)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Ambiguous, Reject(Fixture(duplicateBlock: true)).Error);
    }

    [Fact]
    public void ExcessiveReferenceArrayCountIsRejectedBeforeAllocation()
    {
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(referenceArrayCount: 65_537)).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(Fixture(referenceArrayCount: uint.MaxValue)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Truncated, Reject(Fixture(referenceArrayCount: 65_536)).Error);
    }

    private static ExperimentalBinarySiiException Reject(byte[] input) =>
        Assert.Throws<ExperimentalBinarySiiException>(() => ExperimentalBinarySiiReader.Read(input));

    private static byte[] ModernFixture(
        uint version = 3,
        uint? vector3ArrayCount = null,
        bool writeVector3ArrayData = true,
        bool stopAfterVector3ArrayCount = false,
        bool duplicateOrdinalValues = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        void Name(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }
        void Field(uint type, string name)
        {
            writer.Write(type);
            Name(name);
        }

        writer.Write("BSII"u8);
        writer.Write(version);
        writer.Write(0u);
        writer.Write((byte)1);
        writer.Write(1u);
        Name("modern");
        Field(0x07, "vector2");
        Field(0x09, "vector3");
        Field(0x0a, "vector3_list");
        Field(0x11, "int_vector3");
        Field(0x12, "int_vector3_list");
        Field(0x17, "quaternion");
        Field(0x18, "quaternion_list");
        Field(0x19, "placement");
        Field(0x1a, "placements");
        Field(0x2b, "small");
        Field(0x2c, "small_list");
        Field(0x2f, "wide32");
        Field(0x37, "mode");
        writer.Write(duplicateOrdinalValues ? 3u : 2u);
        writer.Write(1u); Name("one");
        writer.Write(2u); Name("two");
        if (duplicateOrdinalValues) { writer.Write(1u); Name("duplicate"); }
        writer.Write(0u);

        writer.Write(1u);
        writer.Write((byte)0xff);
        writer.Write(0x77ul);
        writer.Write(0x3f800000u); writer.Write(0x40000000u);
        writer.Write(0x3f800000u); writer.Write(0x40000000u); writer.Write(0x40400000u);
        writer.Write(vector3ArrayCount ?? 1);
        if (stopAfterVector3ArrayCount) return stream.ToArray();
        if (writeVector3ArrayData) { writer.Write(0x40400000u); writer.Write(0x40800000u); writer.Write(0x40a00000u); }
        writer.Write(1); writer.Write(2); writer.Write(3);
        writer.Write(1u); writer.Write(1); writer.Write(2); writer.Write(3);
        writer.Write(0x3f800000u); writer.Write(0x40000000u); writer.Write(0x40400000u); writer.Write(0x40800000u);
        writer.Write(1u); writer.Write(0x3f800000u); writer.Write(0x40000000u); writer.Write(0x40400000u); writer.Write(0x40800000u);
        int placementFloatCount = version == 1 ? 7 : 8;
        for (int i = 0; i < placementFloatCount; i++) writer.Write((uint)(0x3f000000 + i));
        writer.Write(1u);
        for (int i = 0; i < placementFloatCount; i++) writer.Write((uint)(0x40000000 + i));
        writer.Write((ushort)513);
        writer.Write(2u); writer.Write((ushort)1); writer.Write(ushort.MaxValue);
        writer.Write(uint.MaxValue);
        writer.Write(2u);
        writer.Write(1u);
        writer.Write((byte)1); writer.Write(11ul);
        writer.Write(0u); writer.Write((byte)0);
        return stream.ToArray();
    }

    private static byte[] Fixture(uint version = 2, uint fieldType = 0x25, byte idKind = 0xff, bool duplicateField = false, bool duplicateBlock = false, byte[]? textBytes = null, ulong referencePart = 0x800000000000000bul, uint? referenceArrayCount = null, uint[]? floatBits = null, uint? floatArrayCount = null, uint? integerArrayCount = null, ulong[]? namedBlockParts = null, ulong[]? secondNamedBlockParts = null, uint? boolArrayCount = null, uint? stringArrayCount = null, byte[]? arrayStringBytes = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        void Name(string value) { byte[] bytes = Encoding.ASCII.GetBytes(value); writer.Write((uint)bytes.Length); writer.Write(bytes); }
        void Data(ulong[]? namedParts)
        {
            writer.Write((uint)1);
            if (namedParts is null) { writer.Write(idKind); writer.Write(0x1234ul); }
            else { writer.Write((byte)namedParts.Length); foreach (ulong part in namedParts) writer.Write(part); }
            writer.Write(42);
            writer.Write(uint.MaxValue); writer.Write(uint.MaxValue - 1); writer.Write(long.MinValue);
            writer.Write(ulong.MaxValue); writer.Write(0xffffffff00000001ul); writer.Write(0x00000001fffffffful); writer.Write(9876543210ul);
            byte[] content = textBytes ?? Encoding.UTF8.GetBytes("route demo");
            writer.Write((uint)content.Length); writer.Write(content);
            writer.Write((byte)0);
            writer.Write((byte)0xff); writer.Write(0x9876ul);
            writer.Write((byte)2); writer.Write(referencePart); writer.Write(12ul);
            writer.Write((uint)0);
            writer.Write(referenceArrayCount ?? 4);
            if (referenceArrayCount is null)
            {
                writer.Write((byte)0);
                writer.Write((byte)0xff); writer.Write(0x9876ul);
                for (int i = 0; i < 2; i++) { writer.Write((byte)2); writer.Write(referencePart); writer.Write(12ul); }
            }
            writer.Write(0x3f000000u);
            uint[] floats = floatBits ?? [0x3f000000, 0x3fa00000, 0x80000000];
            writer.Write(floatArrayCount ?? (uint)floats.Length);
            if (floatArrayCount is null) foreach (uint bits in floats) writer.Write(bits);
            writer.Write(integerArrayCount ?? 2);
            if (integerArrayCount is null) { writer.Write(int.MinValue); writer.Write(int.MaxValue); }
            writer.Write((uint)3); writer.Write(0u); writer.Write(uint.MaxValue); writer.Write(uint.MaxValue - 1);
            writer.Write((uint)2); writer.Write(long.MinValue); writer.Write(long.MaxValue);
            writer.Write((uint)3); writer.Write(0ul); writer.Write(0xffffffff00000001ul); writer.Write(9876543210ul);
            writer.Write((byte)255); writer.Write(boolArrayCount ?? 4);
            if (boolArrayCount is null) { writer.Write((byte)0); writer.Write((byte)1); writer.Write((byte)2); writer.Write((byte)255); }
            writer.Write(stringArrayCount ?? 3);
            if (stringArrayCount is null)
            {
                writer.Write(0u);
                byte[] arrayText = arrayStringBytes ?? Encoding.UTF8.GetBytes("road demo");
                writer.Write((uint)arrayText.Length); writer.Write(arrayText);
                Name("abc");
            }
            writer.Write(0x800000000000000bul);
            writer.Write(4u); writer.Write(0ul); writer.Write(12ul); writer.Write(0x800000000000000bul); writer.Write(12ul);
        }
        writer.Write("BSII"u8);
        writer.Write(version);
        writer.Write((uint)0); writer.Write((byte)1); writer.Write((uint)1); Name("synthetic");
        writer.Write(fieldType); Name("value");
        if (duplicateField) { writer.Write((uint)0x25); Name("value"); }
        writer.Write((uint)0x27); Name("xp");
        writer.Write((uint)0x27); Name("xp_value");
        writer.Write((uint)0x31); Name("balance");
        writer.Write((uint)0x33); Name("wide_nil");
        writer.Write((uint)0x33); Name("wide_high");
        writer.Write((uint)0x33); Name("wide_low");
        writer.Write((uint)0x33); Name("wide_value");
        writer.Write((uint)0x01); Name("label");
        writer.Write((uint)0x39); Name("empty_ref");
        writer.Write((uint)0x3d); Name("anon_ref");
        writer.Write((uint)0x3b); Name("named_ref");
        writer.Write((uint)0x3a); Name("empty_refs");
        writer.Write((uint)0x3c); Name("refs");
        writer.Write((uint)0x05); Name("fuel");
        writer.Write((uint)0x06); Name("wear");
        writer.Write((uint)0x26); Name("signed_values");
        writer.Write((uint)0x28); Name("unsigned_values");
        writer.Write((uint)0x32); Name("long_values");
        writer.Write((uint)0x34); Name("wide_values");
        writer.Write((uint)0x35); Name("active");
        writer.Write((uint)0x36); Name("flags");
        writer.Write((uint)0x02); Name("labels");
        writer.Write((uint)0x03); Name("encoded");
        writer.Write((uint)0x04); Name("encoded_list");
        writer.Write((uint)0);
        Data(namedBlockParts);
        if (duplicateBlock) Data(secondNamedBlockParts ?? namedBlockParts);
        writer.Write((uint)0); writer.Write((byte)0);
        return stream.ToArray();
    }
}
