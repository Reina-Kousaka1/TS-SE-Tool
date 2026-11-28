using System.Text;
using System.Text.Json;
using TSSE.FixtureInspector;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class FixtureCopyInspectionTests
{
    [Fact]
    public void ComparisonUsesOnlyExplicitCopiesAndHidesNamesUnlessRequested()
    {
        string before = Path.GetTempFileName();
        string after = Path.GetTempFileName();
        const string left = "root {\n item : private_id {\n  private_field: 1\n }\n}\n";
        const string right = "root {\n item : private_id {\n  private_field: 2\n }\n}\n";
        try
        {
            File.WriteAllText(before, left);
            File.WriteAllText(after, right);
            using var output = new StringWriter();
            Assert.Equal(0, FixtureCopyInspection.Run(["--compare-approved-test-copies", before, after], output));
            string text = output.ToString();
            Assert.DoesNotContain(before, text, StringComparison.Ordinal);
            Assert.DoesNotContain(after, text, StringComparison.Ordinal);
            Assert.DoesNotContain("private_id", text, StringComparison.Ordinal);
            Assert.DoesNotContain("private_field", text, StringComparison.Ordinal);
            using var json = JsonDocument.Parse(text);
            Assert.Equal(1, json.RootElement.GetProperty("DifferenceCounts").GetProperty("FieldChanged").GetInt32());
            Assert.False(json.RootElement.GetProperty("ByteIdentical").GetBoolean());
            Assert.True(json.RootElement.GetProperty("SyntaxComparisonIgnoresFormattingAndComments").GetBoolean());
            Assert.False(json.RootElement.GetProperty("GameCompatibilityEstablished").GetBoolean());
            Assert.False(json.RootElement.GetProperty("SaveWritingEnabled").GetBoolean());
            using var detailed = new StringWriter();
            Assert.Equal(0, FixtureCopyInspection.Run(["--compare-approved-test-copies", before, after, "--include-field-names"], detailed));
            Assert.Contains("private_field", detailed.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("private_id", detailed.ToString(), StringComparison.Ordinal);
            Assert.Equal(left, File.ReadAllText(before));
            Assert.Equal(right, File.ReadAllText(after));
        }
        finally { File.Delete(before); File.Delete(after); }
    }

    [Fact]
    public void ComparisonDistinguishesExactBytesFromSyntaxOnlyAgreement()
    {
        string before = Path.GetTempFileName();
        string after = Path.GetTempFileName();
        const string original = "root {\n value: 1\n}\n";
        try
        {
            File.WriteAllText(before, original);
            File.WriteAllText(after, original);
            using var identicalOutput = new StringWriter();
            Assert.Equal(0, FixtureCopyInspection.Run(["--compare-approved-test-copies", before, after], identicalOutput));
            using var identical = JsonDocument.Parse(identicalOutput.ToString());
            Assert.True(identical.RootElement.GetProperty("ByteIdentical").GetBoolean());
            Assert.Empty(identical.RootElement.GetProperty("DifferenceCounts").EnumerateObject());
            File.WriteAllText(after, "# synthetic comment\nroot {\n value:  1  \n}\n");
            using var formattedOutput = new StringWriter();
            Assert.Equal(0, FixtureCopyInspection.Run(["--compare-approved-test-copies", before, after], formattedOutput));
            using var formatted = JsonDocument.Parse(formattedOutput.ToString());
            Assert.False(formatted.RootElement.GetProperty("ByteIdentical").GetBoolean());
            Assert.Empty(formatted.RootElement.GetProperty("DifferenceCounts").EnumerateObject());
            Assert.False(formatted.RootElement.GetProperty("GameCompatibilityEstablished").GetBoolean());
            Assert.False(formatted.RootElement.GetProperty("SaveWritingEnabled").GetBoolean());
        }
        finally { File.Delete(before); File.Delete(after); }
    }

    [Theory]
    [InlineData("ScsC synthetic")]
    [InlineData("root {\n item : x {\n  v: 1\n  v: 2\n }\n}\n")]
    public void ComparisonRejectsUnsupportedOrAmbiguousInputsWithoutDisclosingPaths(string content)
    {
        string before = Path.GetTempFileName();
        string after = Path.GetTempFileName();
        try
        {
            File.WriteAllText(before, "root {\n value: 1\n}\n");
            File.WriteAllText(after, content);
            using var output = new StringWriter();
            Assert.Equal(1, FixtureCopyInspection.Run(["--compare-approved-test-copies", before, after], output));
            Assert.DoesNotContain(before, output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(after, output.ToString(), StringComparison.Ordinal);
            Assert.Equal(content, File.ReadAllText(after));
        }
        finally { File.Delete(before); File.Delete(after); }
    }

    [Fact]
    public void ChangedStreamIsRejectedRatherThanReportedAsAStableRoundtrip()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("root {\n value: 1\n}\n");
        using var stream = new ChangeOnRewindStream(bytes);
        Assert.Throws<IOException>(() => FixtureCopyInspection.ObserveTwice(stream));
    }

    [Fact]
    public void OversizedInputDoesNotClaimAStableFullRead()
    {
        using var stream = new MemoryStream(new byte[SiiEncodedText.MaximumBytes + 1]);
        FixtureObservation result = FixtureCopyInspection.ObserveTwice(stream);
        Assert.Null(result.ObservedSameBytesAcrossReads);
        Assert.True(result.Report.ByteLengthIsLowerBound);
    }

    private sealed class ChangeOnRewindStream : MemoryStream
    {
        private readonly byte[] _bytes;
        public ChangeOnRewindStream(byte[] bytes) : base(bytes, writable: true) => _bytes = bytes;
        public override long Position
        {
            get => base.Position;
            set
            {
                if (value == 0 && base.Position > 0) _bytes[15] = (byte)'2';
                base.Position = value;
            }
        }
    }

    [Fact]
    public void SymbolicLinkIsNotInspected()
    {
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") return;
        string target = Path.GetTempFileName();
        string link = target + ".link";
        bool linkCreated = false;
        try
        {
            File.WriteAllText(target, "root {\n value: synthetic\n}\n");
            File.CreateSymbolicLink(link, target);
            linkCreated = true;
            using var output = new StringWriter();
            Assert.Equal(1, FixtureCopyInspection.Run(["--approved-test-copy", link], output));
            Assert.Equal(1, FixtureCopyInspection.Run(["--compare-approved-test-copies", target, link], output));
            Assert.DoesNotContain(target, output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(link, output.ToString(), StringComparison.Ordinal);
            Assert.Equal("root {\n value: synthetic\n}\n", File.ReadAllText(target));
        }
        finally { if (linkCreated) File.Delete(link); File.Delete(target); }
    }

    [Fact]
    public void ExplicitCopyIsInspectedReadOnlyWithoutDisclosingPathOrContent()
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes("root {\n secret_test_value: \"synthetic\"\n}\n");
            File.WriteAllBytes(path, bytes);
            using var output = new StringWriter();
            Assert.Equal(0, FixtureCopyInspection.Run(["--approved-test-copy", path], output));
            string text = output.ToString();
            Assert.DoesNotContain(path, text, StringComparison.Ordinal);
            Assert.DoesNotContain("secret_test_value", text, StringComparison.Ordinal);
            using JsonDocument json = JsonDocument.Parse(text);
            Assert.False(json.RootElement.GetProperty("GameCompatibilityEstablished").GetBoolean());
            Assert.False(json.RootElement.GetProperty("SaveWritingEnabled").GetBoolean());
            Assert.True(json.RootElement.GetProperty("ObservedSameBytesAcrossReads").GetBoolean());
            Assert.True(json.RootElement.GetProperty("Report").GetProperty("ByteRoundTripEqual").GetBoolean());
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RequiresExplicitApprovalFlagAndDoesNotDiscloseFailedPath()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var output = new StringWriter();
        Assert.Equal(2, FixtureCopyInspection.Run([missing], output));
        Assert.Equal(1, FixtureCopyInspection.Run(["--approved-test-copy", missing], output));
        Assert.DoesNotContain(missing, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedContainerIsReportedAsUnsupported()
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] bytes = "ScsC synthetic only"u8.ToArray();
            File.WriteAllBytes(path, bytes);
            using var output = new StringWriter();
            Assert.Equal(0, FixtureCopyInspection.Run(["--approved-test-copy", path], output));
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            JsonElement report = json.RootElement.GetProperty("Report");
            Assert.Equal("UnsupportedTextOrContainer", report.GetProperty("Status").GetString());
            Assert.Equal("EncryptedPrefix", report.GetProperty("SignatureHint").GetString());
            Assert.Equal(JsonValueKind.Null, report.GetProperty("ByteRoundTripEqual").ValueKind);
            Assert.False(json.RootElement.GetProperty("GameCompatibilityEstablished").GetBoolean());
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void BinaryProfilePreviewExercisesMoneyXpAndExistingCoachSkillWithoutWritingTheCopy()
    {
        string path = Path.GetTempFileName();
        byte[] original = BinaryProfileFixture();
        try
        {
            File.WriteAllBytes(path, original);
            using var output = new StringWriter();

            Assert.Equal(0, FixtureCopyInspection.Run(["--exercise-binary-profile-preview", path], output));

            string reportText = output.ToString();
            Assert.DoesNotContain(path, reportText, StringComparison.Ordinal);
            Assert.DoesNotContain("money_account", reportText, StringComparison.Ordinal);
            using JsonDocument json = JsonDocument.Parse(reportText);
            JsonElement report = json.RootElement;
            Assert.True(report.GetProperty("ProfileScalarsChangedOnlyInMemory").GetBoolean());
            Assert.True(report.GetProperty("MoneyXpUndoRedoRestoredExactPayload").GetBoolean());
            Assert.Equal(1, report.GetProperty("ExistingCoachSkillCount").GetInt32());
            Assert.True(report.GetProperty("ExistingCoachSkillUndoRedoRestoredExactPayload").GetBoolean());
            Assert.True(report.GetProperty("SourceBytesStableAcrossPreview").GetBoolean());
            Assert.False(report.GetProperty("GameCompatibilityEstablished").GetBoolean());
            Assert.False(report.GetProperty("SaveWritingEnabled").GetBoolean());
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    private static byte[] BinaryProfileFixture()
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

        Structure(writer, 2, "economy", (0x3b, "bank"), (0x27, "experience_points"),
            (0x27, "bus_experience_points"), (0x27, "long_duty"), (0x01, "mod_blob"));
        writer.Write(2u);
        WriteIdentity(writer, 0x88);
        WriteIdentity(writer, 0x77);
        writer.Write(42u);
        writer.Write(7u);
        writer.Write(2u);
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
