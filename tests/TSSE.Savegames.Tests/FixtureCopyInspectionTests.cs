using System.Text;
using System.Text.Json;
using TSSE.FixtureInspector;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class FixtureCopyInspectionTests
{
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
    public void UnsupportedContainerIsReportedWithoutDecoding()
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
}
