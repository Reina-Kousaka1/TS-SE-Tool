using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiEditSessionTests
{
    private const string Original = "SiiNunit\r\n{\r\n bank : a {\r\n  money_account: 42\r\n  mod_extra: \"未知\"\r\n }\r\n economy : b {\r\n  experience_points: 5\r\n }\r\n}\r\n";

    [Fact]
    public void BatchEditsUndoAndRedoWithoutLosingUnknownData()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        session.Apply([
            new("bank", "a", "money_account", "-8"),
            new("economy", "b", "experience_points", "30")
        ]);
        string expected = Original.Replace("money_account: 42", "money_account: -8", StringComparison.Ordinal)
            .Replace("experience_points: 5", "experience_points: 30", StringComparison.Ordinal);
        Assert.Equal(expected, session.PreviewText);
        Assert.True(session.IsDirty);
        Assert.True(session.Undo());
        Assert.Equal(Original, session.PreviewText);
        Assert.False(session.IsDirty);
        Assert.True(session.Redo());
        Assert.Equal(expected, session.PreviewText);
    }

    [Fact]
    public void FailureInLaterEditLeavesTheWholeBatchAndRedoUntouched()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        session.Apply([new("bank", "a", "money_account", "2")]);
        Assert.True(session.Undo());
        Assert.Throws<InvalidOperationException>(() => session.Apply([
            new("bank", "a", "money_account", "3"),
            new("economy", "b", "missing", "4")
        ]));
        Assert.Equal(Original, session.PreviewText);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void DuplicateTargetsAndNoOpCannotDiscardRedo()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        session.Apply([new("bank", "a", "money_account", "2")]);
        Assert.True(session.Undo());
        Assert.Throws<ArgumentException>(() => session.Apply([
            new("bank", "a", "money_account", "3"),
            new("bank", "a", "money_account", "4")
        ]));
        session.Apply([new("bank", "a", "money_account", "42")]);
        Assert.Equal(Original, session.PreviewText);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void UnclassifiedSyntaxRejectsEditsWithoutDiscardingText()
    {
        const string input = "root {\n value: 1\n unexpected syntax\n}\n";
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(input));
        Assert.Throws<InvalidOperationException>(() => session.Apply([new("root", null, "value", "2")]));
        Assert.Equal(input, session.PreviewText);
        Assert.False(session.IsDirty);
    }
}
