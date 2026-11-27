using System.Text;
using TSSE.Savegames.Editing;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiEncodedEditSessionTests
{
    private const string Input = "SiiNunit\r\n{\r\n economy : demo {\r\n  amount: 1\r\n  xp: 2\r\n  mod: \"未知 🚛\"\r\n }\r\n}\r\n";

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    public void BatchUndoRedoPreservesEncodingPreambleAndUnknownBytes(string kind)
    {
        Encoding encoding = kind switch
        {
            "utf8" => new UTF8Encoding(false, true),
            "utf8-bom" => new UTF8Encoding(true, true),
            "utf16-le" => new UnicodeEncoding(false, true, true),
            "utf16-be" => new UnicodeEncoding(true, true, true),
            _ => throw new ArgumentException("Unknown test encoding", nameof(kind))
        };
        byte[] initial = Bytes(encoding, Input);
        var session = new SiiEncodedEditSession(initial);
        Assert.Equal(initial, session.PreviewBytes);
        session.Apply([new("economy", "demo", "amount", "20"), new("economy", "demo", "xp", "30")]);
        string changed = Input.Replace("amount: 1", "amount: 20", StringComparison.Ordinal)
            .Replace("xp: 2", "xp: 30", StringComparison.Ordinal);
        Assert.Equal(Bytes(encoding, changed), session.PreviewBytes);
        Assert.True(session.Undo());
        Assert.Equal(initial, session.PreviewBytes);
        Assert.Empty(session.Changes);
        Assert.True(session.Redo());
        Assert.Equal(Bytes(encoding, changed), session.PreviewBytes);
        session.Apply([new("economy", "demo", "amount", "1"), new("economy", "demo", "xp", "2")]);
        Assert.Equal(initial, session.PreviewBytes);
        Assert.False(session.IsDirty);
        Assert.True(session.Undo());
        Assert.True(session.IsDirty);
        Assert.True(session.Redo());
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void UnencodableBatchDoesNotChangeBytesOrDiscardRedo()
    {
        byte[] initial = Bytes(new UTF8Encoding(true, true), Input);
        var session = new SiiEncodedEditSession(initial);
        session.Apply([new("economy", "demo", "amount", "20")]);
        Assert.True(session.Undo());
        Assert.Throws<InvalidDataException>(() => session.Apply([
            new("economy", "demo", "amount", "5"),
            new("economy", "demo", "xp", "\ud800")
        ]));
        Assert.Equal(initial, session.PreviewBytes);
        Assert.Empty(session.Changes);
        Assert.True(session.CanRedo);
        Assert.True(session.Redo());
        Assert.Contains("amount: 20", session.PreviewText, StringComparison.Ordinal);
    }

    private static byte[] Bytes(Encoding encoding, string text) =>
        encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
}
