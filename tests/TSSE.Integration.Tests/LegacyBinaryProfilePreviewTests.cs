using System.Text;
using TSSE.Core.Editing;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Diagnostics;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyBinaryProfilePreviewTests
{
    [Fact]
    public void ReadOnlyInspectorReadsKnownSyntheticBinaryProfileValues()
    {
        byte[] payload = BinaryProfileFixture();

        ProfileValues values = LegacyProfileReadInspector.InspectBinary(payload);

        Assert.Equal(new ProfileValues(123456789, 42, 7), values);
    }

    [Fact]
    public void BinaryCoachCounterInspectorReadsOnlySourceObservedInt32Fields()
    {
        byte[] payload = BinaryProfileFixture();
        IReadOnlyDictionary<string, long?> counters = LegacyCoachProfileInspector.InspectBinaryCounters(
            ExperimentalBinarySiiReader.Read(payload));

        Assert.Equal(7L, counters["bus_experience_points"]);
        Assert.Null(counters["bus_total_distance"]);
        Assert.Equal(8, counters.Count);
    }

    [Fact]
    public void BinaryCandidateCoachSkillInspectionReportsOnlyRawFieldTypeCodes()
    {
        IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(BinaryProfileFixture(includeCoachSkill: true));

        IReadOnlyDictionary<string, uint?> types = LegacyCoachProfileInspector.InspectBinaryCandidateSkillTypeCodes(blocks);

        Assert.Equal(0x25u, types["long_duty"]);
        Assert.Null(types["tight_schedule"]);
        Assert.Equal(6, types.Count);
    }

    [Fact]
    public void EditsSyntheticBinaryProfileScalarsAsOneUndoableChangeAndRetainsUnknownBytes()
    {
        byte[] original = BinaryProfileFixture();
        ExperimentalBinaryBlock economy = Assert.Single(
            ExperimentalBinarySiiReader.Read(original), block => block.Type == "economy");
        var preview = new LegacyBinaryProfilePreview(original, economy.Identifier!);

        Assert.Equal(new ProfileValues(123456789, 42, 7), preview.Current);
        preview.Apply(new ProfileValues(-987654321, uint.MaxValue - 1, int.MinValue));

        Assert.Equal(new ProfileValues(-987654321, uint.MaxValue - 1, int.MinValue), preview.Current);
        Assert.Equal(3, preview.Changes.Count);
        Assert.True(preview.IsDirty);
        IReadOnlyList<ExperimentalBinaryBlock> parsed = ExperimentalBinarySiiReader.Read(preview.PreviewBytes);
        Assert.Equal("preserve bank unknown field", Assert.Single(parsed, block => block.Type == "bank").StringFields["mod_blob"]);
        Assert.Equal("preserve economy unknown field", Assert.Single(parsed, block => block.Type == "economy").StringFields["mod_blob"]);

        Assert.True(preview.Undo());
        Assert.False(preview.IsDirty);
        Assert.Equal(original, preview.PreviewBytes);
        Assert.True(preview.Redo());
        Assert.Equal(new ProfileValues(-987654321, uint.MaxValue - 1, int.MinValue), preview.Current);
    }

    [Fact]
    public void SupportsObservedUnsignedBinaryCoachExperienceWithoutWideningOrChangingItsType()
    {
        byte[] original = BinaryProfileFixture(coachExperienceTypeCode: 0x27);
        var preview = CreatePreview(original);

        Assert.Equal(7L, preview.Current.CoachExperience);
        Assert.Equal(7L, LegacyProfileReadInspector.InspectBinary(original).CoachExperience);
        Assert.Equal(7L, LegacyCoachProfileInspector.InspectBinaryCounters(
            ExperimentalBinarySiiReader.Read(original))["bus_experience_points"]);

        long maximumNonNil = (long)uint.MaxValue - 1;
        preview.Apply(preview.Current with { CoachExperience = maximumNonNil });

        Assert.Equal(maximumNonNil, preview.Current.CoachExperience);
        Assert.Equal(0x27u, Assert.Single(ExperimentalBinarySiiReader.Read(preview.PreviewBytes), block => block.Type == "economy")
            .RawFields["bus_experience_points"].TypeCode);
        Assert.True(preview.Undo());
        Assert.Equal(original, preview.PreviewBytes);
        Assert.True(preview.Redo());
        Assert.Equal(maximumNonNil, preview.Current.CoachExperience);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            preview.Apply(preview.Current with { CoachExperience = uint.MaxValue }));
    }

    [Fact]
    public void EditsOnlyPresentObservedUnsignedCoachSkillFieldsAndResetsAsOneHistoryEntry()
    {
        var preview = CreatePreview(BinaryProfileFixture(includeCoachSkill: true, coachSkillTypeCode: 0x27));

        Assert.Equal((byte)2, preview.CoachSkills["long_duty"]);
        Assert.Single(preview.CoachSkills);
        Assert.Throws<InvalidOperationException>(() => preview.SetCoachSkill("tight_schedule", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetCoachSkill("long_duty", 4));
        Assert.Equal((byte)2, preview.CoachSkills["long_duty"]);

        preview.SetCoachSkill("long_duty", 3);
        preview.ResetCoachSkills();

        Assert.Equal((byte)0, preview.CoachSkills["long_duty"]);
        Assert.Single(preview.Changes);
        Assert.True(preview.Undo());
        Assert.Equal((byte)3, preview.CoachSkills["long_duty"]);
        Assert.True(preview.Undo());
        Assert.Equal((byte)2, preview.CoachSkills["long_duty"]);
    }

    [Fact]
    public void RefusesToCreateMissingCoachFieldAndRejectsLegacyNilExperience()
    {
        var withoutCoach = CreatePreview(BinaryProfileFixture(includeCoachExperience: false));
        byte[] before = withoutCoach.PreviewBytes;

        Assert.Null(withoutCoach.Current.CoachExperience);
        Assert.Throws<InvalidOperationException>(() =>
            withoutCoach.Apply(new ProfileValues(1, 2, 3)));
        Assert.Equal(before, withoutCoach.PreviewBytes);
        Assert.Empty(withoutCoach.Changes);

        byte[] nilExperience = BinaryProfileFixture(experience: uint.MaxValue);
        ExperimentalBinaryBlock economy = Assert.Single(
            ExperimentalBinarySiiReader.Read(nilExperience), block => block.Type == "economy");
        Assert.Throws<InvalidOperationException>(() =>
            new LegacyBinaryProfilePreview(nilExperience, economy.Identifier!));
    }

    private static LegacyBinaryProfilePreview CreatePreview(byte[] bytes)
    {
        ExperimentalBinaryBlock economy = Assert.Single(
            ExperimentalBinarySiiReader.Read(bytes), block => block.Type == "economy");
        return new LegacyBinaryProfilePreview(bytes, economy.Identifier!);
    }

    private static byte[] BinaryProfileFixture(
        bool includeCoachExperience = true,
        bool includeCoachSkill = false,
        uint experience = 42,
        uint coachExperienceTypeCode = 0x25,
        uint coachSkillTypeCode = 0x25,
        long coachExperience = 7)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("BSII"u8);
        writer.Write(3u);

        Structure(writer, 1, "bank", (0x31, "money_account"), (0x01, "mod_blob"));
        writer.Write(1u);
        WriteIdentity(writer, 0x77);
        writer.Write(123456789L);
        WriteString(writer, "preserve bank unknown field");

        var economyFields = new List<(uint Type, string Name)>
        {
            (0x3b, "bank"),
            (0x27, "experience_points")
        };
        if (includeCoachExperience)
            economyFields.Add((coachExperienceTypeCode, "bus_experience_points"));
        if (includeCoachSkill)
            economyFields.Add((coachSkillTypeCode, "long_duty"));
        economyFields.Add((0x01, "mod_blob"));
        Structure(writer, 2, "economy", economyFields.ToArray());
        writer.Write(2u);
        WriteIdentity(writer, 0x88);
        WriteIdentity(writer, 0x77); // Existing bank reference uses the raw bank block identity.
        writer.Write(experience);
        if (includeCoachExperience)
        {
            if (coachExperienceTypeCode == 0x25)
                writer.Write(checked((int)coachExperience));
            else if (coachExperienceTypeCode == 0x27)
                writer.Write(checked((uint)coachExperience));
            else
                throw new ArgumentOutOfRangeException(nameof(coachExperienceTypeCode));
        }
        if (includeCoachSkill)
            writer.Write(2);
        WriteString(writer, "preserve economy unknown field");

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

    private static void WriteIdentity(BinaryWriter writer, ulong rawValue)
    {
        writer.Write((byte)0xff);
        writer.Write(rawValue);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }
}
