using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacySkillPreviewTests
{
    private const string Input = "economy : e {\n bank: b\n experience_points: 0\n adr: 000\n long_dist: 2\n heavy: 0\n fragile: 0\n urgent: 0\n mechanical: 0\n mod: \"未知\"\n}\nbank : b {\n money_account: 10\n}\n";

    [Fact]
    public void ResetSkillsIsAtomicAndUndoRestoresExactSource()
    {
        string input = Input.Replace("adr: 000", "adr: 3", StringComparison.Ordinal);
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(input), "e");
        editor.ResetSkills();
        Assert.Equal((byte)0, editor.GetSkill("adr"));
        Assert.Equal((byte)0, editor.GetSkill("long_dist"));
        Assert.Contains("mod: \"未知\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
        Assert.True(editor.Redo());
        Assert.Equal((byte)0, editor.GetSkill("long_dist"));
    }

    [Fact]
    public void InvalidLaterSkillRejectsEntireReset()
    {
        string input = Input.Replace("mechanical: 0", "mechanical: 7", StringComparison.Ordinal);
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(input), "e");
        Assert.Throws<InvalidOperationException>(() => editor.ResetSkills());
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void SkillEditsPreserveOtherFieldsAndSupportUndoRedo()
    {
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(Input), "e");
        editor.SetSkill("adr", 0);
        Assert.Equal(Input, editor.PreviewText);
        editor.SetSkill("adr", 63);
        editor.SetSkill("long_dist", 6);
        Assert.Equal((byte)63, editor.GetSkill("adr"));
        Assert.Equal((byte)6, editor.GetSkill("long_dist"));
        Assert.Contains("mod: \"未知\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal((byte)2, editor.GetSkill("long_dist"));
        Assert.True(editor.Undo());
        Assert.Equal(Input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal((byte)63, editor.GetSkill("adr"));
    }

    [Theory]
    [InlineData("adr", 64)]
    [InlineData("heavy", 7)]
    public void InvalidNewValuesDoNotChangeHistory(string name, byte value)
    {
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(Input), "e");
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetSkill(name, value));
        Assert.Equal(Input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }

    [Theory]
    [InlineData("adr: 000", "adr: 64", "adr")]
    [InlineData("heavy: 0", "heavy: 7", "heavy")]
    [InlineData("heavy: 0", "heavy: 0\n heavy: 1", "heavy")]
    [InlineData("heavy: 0", "other: 0", "heavy")]
    public void UnsupportedOriginalValueCannotBeOverwritten(string oldValue, string replacement, string name)
    {
        string input = Input.Replace(oldValue, replacement, StringComparison.Ordinal);
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(input), "e");
        Assert.Throws<InvalidOperationException>(() => editor.SetSkill(name, 0));
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void CandidateCoachSkillsEditOnlyExistingFieldsAndUndoToExactSource()
    {
        string coachFields = " bus_experience_points: 12\n long_duty: 1\n tight_schedule: 3\n satisfaction_decrease: 0\n low_acceleration: 2\n satisfaction_increase: 1\n ecodriving: 3\n";
        string input = Input.Replace(" experience_points: 0\n", " experience_points: 0\n" + coachFields, StringComparison.Ordinal);
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(input), "e");

        Assert.Equal((byte)3, editor.GetCoachSkill("tight_schedule"));
        editor.SetCoachSkill("long_duty", 3);
        Assert.Equal((byte)3, editor.GetCoachSkill("long_duty"));
        string afterSingleEdit = editor.PreviewText;
        editor.ResetCoachSkills();

        Assert.All(new[] { "long_duty", "tight_schedule", "satisfaction_decrease", "low_acceleration", "satisfaction_increase", "ecodriving" },
            skill => Assert.Equal((byte)0, editor.GetCoachSkill(skill)));
        Assert.Contains("bus_experience_points: 12", editor.PreviewText, StringComparison.Ordinal);
        Assert.Contains("mod: \"未知\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(afterSingleEdit, editor.PreviewText);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal(afterSingleEdit, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal((byte)0, editor.GetCoachSkill("ecodriving"));
    }

    [Fact]
    public void CandidateCoachSkillFieldsAreNeverAddedAndInvalidValuesFailBeforeMutation()
    {
        var missing = new LegacyProfilePreview(SiiSyntaxDocument.Parse(Input), "e");
        Assert.Throws<InvalidOperationException>(() => missing.SetCoachSkill("long_duty", 1));
        Assert.Throws<InvalidOperationException>(() => missing.ResetCoachSkills());
        Assert.Equal(Input, missing.PreviewText);

        string invalid = Input.Replace(" experience_points: 0\n", " experience_points: 0\n long_duty: 4\n ecodriving: 0\n", StringComparison.Ordinal);
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(invalid), "e");
        Assert.Throws<InvalidOperationException>(() => editor.ResetCoachSkills());
        Assert.Equal(invalid, editor.PreviewText);
        Assert.False(editor.CanUndo);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetCoachSkill("long_duty", 4));
    }
}
