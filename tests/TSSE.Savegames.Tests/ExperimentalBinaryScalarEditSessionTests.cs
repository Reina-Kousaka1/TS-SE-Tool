using System.Buffers.Binary;
using System.Text;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Editing;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class ExperimentalBinaryScalarEditSessionTests
{
    [Fact]
    public void EditsOnlyExistingTypedProfileScalarsAndPreservesOpaqueValuesThroughUndoRedo()
    {
        byte[] original = ProfileFixture();
        var session = new ExperimentalBinaryScalarEditSession(original);
        IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(original);
        ExperimentalBinaryBlock bank = Assert.Single(blocks, block => block.Type == "bank");
        ExperimentalBinaryBlock economy = Assert.Single(blocks, block => block.Type == "economy");

        session.SetInt64("bank", bank.Identifier!, "money_account", long.MaxValue);
        session.SetUInt32("economy", economy.Identifier!, "experience_points", uint.MaxValue);
        session.SetInt32("economy", economy.Identifier!, "bus_experience_points", int.MinValue);

        Assert.Equal(long.MaxValue, session.GetInt64("bank", bank.Identifier!, "money_account"));
        Assert.Equal(uint.MaxValue, session.GetUInt32("economy", economy.Identifier!, "experience_points"));
        Assert.Equal(int.MinValue, session.GetInt32("economy", economy.Identifier!, "bus_experience_points"));
        Assert.Equal(3, session.Changes.Count);
        Assert.True(session.IsDirty);

        IReadOnlyList<ExperimentalBinaryBlock> edited = ExperimentalBinarySiiReader.Read(session.PreviewBytes);
        Assert.Equal(bank.RawFields["mod_blob"].Bytes, Assert.Single(edited, block => block.Type == "bank").RawFields["mod_blob"].Bytes);
        Assert.Equal(economy.RawFields["mod_blob"].Bytes, Assert.Single(edited, block => block.Type == "economy").RawFields["mod_blob"].Bytes);

        Assert.True(session.Undo());
        Assert.True(session.Undo());
        Assert.True(session.Undo());
        Assert.False(session.IsDirty);
        Assert.Equal(original, session.PreviewBytes);
        Assert.True(session.Redo());
        Assert.True(session.Redo());
        Assert.True(session.Redo());
        Assert.Equal(long.MaxValue, session.GetInt64("bank", bank.Identifier!, "money_account"));
    }

    [Fact]
    public void RejectsMissingFieldsWrongTypesAndChangedIdentifiersWithoutMutation()
    {
        var session = new ExperimentalBinaryScalarEditSession(ProfileFixture());
        ExperimentalBinaryBlock bank = Assert.Single(
            ExperimentalBinarySiiReader.Read(session.PreviewBytes), block => block.Type == "bank");
        byte[] before = session.PreviewBytes;

        Assert.Throws<InvalidOperationException>(() =>
            session.SetUInt32("bank", bank.Identifier!, "money_account", 5));
        Assert.Throws<InvalidOperationException>(() =>
            session.SetInt64("bank", bank.Identifier!, "missing", 5));
        Assert.Throws<InvalidOperationException>(() =>
            session.SetInt64("bank", new ExperimentalBinaryBlockIdentifier(0xff, Array.AsReadOnly(new[] { 0x99ul })), "money_account", 5));

        Assert.Equal(before, session.PreviewBytes);
        Assert.Empty(session.Changes);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void RejectsAnInvalidBatchWithoutApplyingItsEarlierValidValues()
    {
        byte[] original = ProfileFixture();
        var session = new ExperimentalBinaryScalarEditSession(original);
        ExperimentalBinaryBlock bank = Assert.Single(
            ExperimentalBinarySiiReader.Read(original), block => block.Type == "bank");
        byte[] newBalance = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(newBalance, long.MinValue);

        Assert.Throws<InvalidOperationException>(() => session.Apply(
        [
            new ExperimentalBinaryScalarEdit("bank", bank.Identifier!, "money_account", 0x31, newBalance),
            new ExperimentalBinaryScalarEdit("bank", bank.Identifier!, "missing", 0x31, newBalance)
        ]));

        Assert.Equal(original, session.PreviewBytes);
        Assert.Empty(session.Changes);
        Assert.False(session.CanUndo);
    }

    private static byte[] ProfileFixture()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("BSII"u8);
        writer.Write(3u);

        Structure(writer, 1, "bank", (0x31, "money_account"), (0x01, "mod_blob"));
        writer.Write(1u);
        writer.Write((byte)0xff);
        writer.Write(0x77ul);
        writer.Write(123456789L);
        WriteString(writer, "preserve-bank-blob");

        Structure(writer, 2, "economy", (0x27, "experience_points"), (0x25, "bus_experience_points"), (0x01, "mod_blob"));
        writer.Write(2u);
        writer.Write((byte)0xff);
        writer.Write(0x88ul);
        writer.Write(42u);
        writer.Write(7);
        WriteString(writer, "preserve-economy-blob");

        writer.Write(0u);
        writer.Write((byte)0);
        return stream.ToArray();
    }

    private static void Structure(BinaryWriter writer, uint id, string name, params (uint Type, string Name)[] fields)
    {
        writer.Write(0u);
        writer.Write((byte)1);
        writer.Write(id);
        WriteString(writer, name);
        foreach ((uint type, string fieldName) in fields)
        {
            writer.Write(type);
            WriteString(writer, fieldName);
        }
        writer.Write(0u);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }
}
