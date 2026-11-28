using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiEditSessionTests
{
    [Theory]
    [InlineData("1 # note")]
    [InlineData("1 // note")]
    [InlineData("\"quoted \\\" text\" # note")]
    public void PossibleInlineCommentsCannotBeSilentlyDiscarded(string oldValue)
    {
        string input = "root {\n value: " + oldValue + "\n other: 0\n}\n";
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(input));
        session.Apply([new("root", null, "other", "1")]);
        Assert.True(session.Undo());
        Assert.Throws<InvalidOperationException>(() => session.Apply([
            new("root", null, "other", "2"), new("root", null, "value", "3")
        ]));
        Assert.Equal(input, session.PreviewText);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void QuotedCommentMarkersCanBeEditedButUnquotedMarkersCannotBeIntroduced()
    {
        const string input = "root {\n value: \"# //\"\n}\n";
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(input));
        Assert.Throws<InvalidOperationException>(() => session.Apply([new("root", null, "value", "1 # note")]));
        session.Apply([new("root", null, "value", "\"new # //\"")]);
        Assert.Contains("\"new # //\"", session.PreviewText, StringComparison.Ordinal);
        Assert.True(session.Undo());
        Assert.Equal(input, session.PreviewText);
    }

    [Fact]
    public void UnencodableReplacementRejectsWholeBatchAndPreservesRedo()
    {
      foreach (string value in new[] { "\"bad\0value\"", "\"bad\ud800value\"", "\"bad\udc00value\"", "\"bad\ud800\ud800\"" })
      {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        session.Apply([new("bank", "a", "money_account", "2")]);
        Assert.True(session.Undo());
        Assert.Throws<InvalidDataException>(() => session.Apply([
            new("bank", "a", "money_account", "3"),
            new("economy", "b", "experience_points", value)
        ]));
        Assert.Equal(Original, session.PreviewText);
        Assert.Empty(session.Changes);
        Assert.True(session.CanRedo);
        Assert.True(session.Redo());
        Assert.Contains("mod_extra: \"未知\"", session.PreviewText, StringComparison.Ordinal);
      }
    }

    [Fact]
    public void ReplacementAcceptsValidSupplementaryUnicode()
    {
        const string input = "root {\n name: \"old\"\n}\n";
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(input));
        session.Apply([new("root", null, "name", "\"🚌\"")]);
        Assert.Contains("\"🚌\"", session.PreviewText, StringComparison.Ordinal);
        Assert.True(session.Undo());
        Assert.Equal(input, session.PreviewText);
    }

    [Fact]
    public void RevertAllIsExactUndoableAndNoOpPreservesRedo()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        Assert.False(session.RevertAll());
        session.Apply([new("bank", "a", "money_account", "-8")]);
        session.Apply([new("economy", "b", "experience_points", "30")]);
        string changed = session.PreviewText;
        Assert.True(session.RevertAll());
        Assert.Equal(Original, session.PreviewText);
        Assert.Empty(session.Changes);
        Assert.False(session.IsDirty);
        Assert.True(session.Undo());
        Assert.Equal(changed, session.PreviewText);
        Assert.True(session.Redo());
        Assert.Equal(Original, session.PreviewText);
        Assert.True(session.Undo());
        Assert.True(session.Undo());
        Assert.True(session.Undo());
        Assert.False(session.RevertAll());
        Assert.True(session.CanRedo);
        Assert.True(session.Redo());
    }

    [Fact]
    public void RevertAllAtHistoryLimitFailsWithoutChangingState()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        for (int i = 1; i < SiiEditSession.MaximumHistoryStates; i++)
            session.Apply([new("bank", "a", "money_account", i.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        string before = session.PreviewText;
        Assert.Throws<TSSE.Core.Editing.EditHistoryLimitException>(() => session.RevertAll());
        Assert.Equal(before, session.PreviewText);
        Assert.True(session.IsDirty);
        Assert.True(session.Undo());
    }

    [Fact]
    public void CrossTypeDuplicateTargetRejectsWholeBatchAndPreservesRedo()
    {
        const string original = "root {\n safe : a {\n  value: 1\n }\n vehicle : b {\n  value: 2\n }\n mod : b {\n  unknown: preserved\n }\n}\n";
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(original));
        session.Apply([new("safe", "a", "value", "3")]);
        Assert.True(session.Undo());
        Assert.Throws<InvalidOperationException>(() => session.Apply([
            new("safe", "a", "value", "4"), new("vehicle", "b", "value", "5")
        ]));
        Assert.Equal(original, session.PreviewText);
        Assert.Empty(session.Changes);
        Assert.True(session.CanRedo);
        Assert.True(session.Redo());
        Assert.Contains("value: 3", session.PreviewText, StringComparison.Ordinal);
        Assert.Contains("unknown: preserved", session.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void NetChangesFollowUndoRedoAndReturnToOriginalValue()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        session.Apply([new("economy", "b", "experience_points", "30"), new("bank", "a", "money_account", "-8")]);
        Assert.Equal(new[] { "money_account", "experience_points" }, session.Changes.Select(change => change.FieldName));
        SiiFieldChange money = session.Changes[0];
        Assert.Equal("42", money.OriginalValue);
        Assert.Equal("-8", money.CurrentValue);
        Assert.True(session.Undo());
        Assert.Empty(session.Changes);
        Assert.True(session.Redo());
        Assert.Collection(session.Changes,
            change => Assert.Equal("money_account", change.FieldName),
            change => Assert.Equal("experience_points", change.FieldName));
        session.Apply([new("bank", "a", "money_account", "42")]);
        Assert.Equal("experience_points", Assert.Single(session.Changes).FieldName);
        session.Apply([new("economy", "b", "experience_points", "5")]);
        Assert.Empty(session.Changes);
        Assert.Equal(Original, session.PreviewText);
        Assert.False(session.IsDirty);
        Assert.True(session.CanUndo);
        Assert.True(session.Undo());
        Assert.True(session.IsDirty);
        Assert.True(session.Redo());
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void SemanticallyEqualButTextuallyDifferentValueRemainsDirty()
    {
        const string input = "root {\n value: 001\n}\n";
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(input));
        session.Apply([new("root", null, "value", "1")]);
        Assert.True(session.IsDirty);
        Assert.Single(session.Changes);
        session.Apply([new("root", null, "value", "001")]);
        Assert.False(session.IsDirty);
        Assert.Empty(session.Changes);
    }

    [Fact]
    public void FailedBatchDoesNotReportUnappliedChanges()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(Original));
        Assert.Throws<InvalidOperationException>(() => session.Apply([
            new("bank", "a", "money_account", "1"), new("economy", "b", "missing", "2")
        ]));
        Assert.Empty(session.Changes);
        Assert.Equal(Original, session.PreviewText);
    }
    [Fact]
    public void HistoryLimitRejectsNewEditAndPreservesUndo()
    {
        var session = new SiiEditSession(SiiSyntaxDocument.Parse("root {\n value: 0\n}\n"));
        for (int i = 1; i < SiiEditSession.MaximumHistoryStates; i++)
            session.Apply([new("root", null, "value", i.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        string before = session.PreviewText;
        Assert.Throws<TSSE.Core.Editing.EditHistoryLimitException>(() =>
            session.Apply([new("root", null, "value", "100")]));
        Assert.Equal(before, session.PreviewText);
        Assert.True(session.Undo());
        session.Apply([new("root", null, "value", "200")]);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void OversizedReplacementIsRejectedWithoutChangingText()
    {
        const string original = "root {\n value: 0\n}\n";
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(original));
        Assert.Throws<InvalidOperationException>(() =>
            session.Apply([new("root", null, "value", new string('x', SiiEditSession.MaximumPreviewCharacters + 1))]));
        Assert.Equal(original, session.PreviewText);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void EditsMultiMegabyteDocumentWithinBoundAndKeepsUndoExact()
    {
        string filler = string.Concat(Enumerable.Repeat("#" + new string('x', 60_000) + "\n", 50));
        string original = "root {\n value: 0\n}\n" + filler;
        Assert.InRange(original.Length, 2 * 1024 * 1024 + 1, SiiEditSession.MaximumPreviewCharacters);
        var session = new SiiEditSession(SiiSyntaxDocument.Parse(original));

        session.Apply([new("root", null, "value", "1")]);

        Assert.StartsWith("root {\n value: 1\n}\n", session.PreviewText, StringComparison.Ordinal);
        Assert.True(session.Undo());
        Assert.Equal(original, session.PreviewText);
    }
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
