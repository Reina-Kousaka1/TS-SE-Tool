using System.Text;
using TSSE.Savegames.Diagnostics;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class ExperimentalBinarySiiReaderTests
{
    [Fact]
    public void ReadsOnlyTheSupportedSyntheticStructureWithoutChangingInput()
    {
        byte[] input = Fixture();
        byte[] before = (byte[])input.Clone();
        var block = Assert.Single(ExperimentalBinarySiiReader.Read(input));
        Assert.Equal("synthetic", block.Type);
        Assert.Equal(0x1234ul, block.NamelessId);
        Assert.Equal(42, block.Int32Fields["value"]);
        Assert.Equal(uint.MaxValue, block.UInt32Fields["xp"].Raw);
        Assert.True(block.UInt32Fields["xp"].IsLegacyNil);
        Assert.Null(block.UInt32Fields["xp"].LegacyValue);
        Assert.Equal(uint.MaxValue - 1, block.UInt32Fields["xp_value"].LegacyValue);
        Assert.False(block.UInt32Fields["xp_value"].IsLegacyNil);
        Assert.Equal(long.MinValue, block.Int64Fields["balance"]);
        Assert.Equal(before, input);
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
    public void UnsupportedVersionTypeIdentifierAndTrailingBytesFailClosed()
    {
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedVersion, Reject(Fixture(version: 3)).Error);
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedType, Reject(Fixture(fieldType: 0x3f)).Error);
        Assert.Equal(ExperimentalBinarySiiError.UnsupportedIdentifier, Reject(Fixture(idKind: 1)).Error);
        Assert.Equal(ExperimentalBinarySiiError.TrailingData, Reject([.. Fixture(), 0]).Error);
        Assert.Equal(ExperimentalBinarySiiError.LimitExceeded, Reject(new byte[ExperimentalBinarySiiReader.MaximumBytes + 1]).Error);
    }

    [Fact]
    public void DuplicateFieldNamesAndDuplicateBlockIdsAreRejected()
    {
        Assert.Equal(ExperimentalBinarySiiError.Ambiguous, Reject(Fixture(duplicateField: true)).Error);
        Assert.Equal(ExperimentalBinarySiiError.Ambiguous, Reject(Fixture(duplicateBlock: true)).Error);
    }

    private static ExperimentalBinarySiiException Reject(byte[] input) =>
        Assert.Throws<ExperimentalBinarySiiException>(() => ExperimentalBinarySiiReader.Read(input));

    private static byte[] Fixture(uint version = 2, uint fieldType = 0x25, byte idKind = 0xff, bool duplicateField = false, bool duplicateBlock = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        void Name(string value) { byte[] bytes = Encoding.ASCII.GetBytes(value); writer.Write((uint)bytes.Length); writer.Write(bytes); }
        void Data() { writer.Write((uint)1); writer.Write(idKind); writer.Write(0x1234ul); writer.Write(42); writer.Write(uint.MaxValue); writer.Write(uint.MaxValue - 1); writer.Write(long.MinValue); }
        writer.Write("BSII"u8);
        writer.Write(version);
        writer.Write((uint)0); writer.Write((byte)1); writer.Write((uint)1); Name("synthetic");
        writer.Write(fieldType); Name("value");
        if (duplicateField) { writer.Write((uint)0x25); Name("value"); }
        writer.Write((uint)0x27); Name("xp");
        writer.Write((uint)0x27); Name("xp_value");
        writer.Write((uint)0x31); Name("balance");
        writer.Write((uint)0);
        Data();
        if (duplicateBlock) Data();
        writer.Write((uint)0); writer.Write((byte)0);
        return stream.ToArray();
    }
}
