using System.Text;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class LegacyDecodeSafetyCheckTests
{
    private static readonly byte[] Valid = Encoding.UTF8.GetBytes("root {\n unknown_mod_field: \"保持\"\n}\n");

    [Fact]
    public void SuccessfulCallsYieldSyntaxEvidenceOnly()
    {
        var result = LegacyDecodeSafetyCheck.Inspect(0, 0, (uint)Valid.Length, (uint)Valid.Length, Valid);
        Assert.Equal(LegacyDecodeCheckStatus.SyntaxOnly, result.Status);
        Assert.True(result.Report!.ByteRoundTripEqual);
        Assert.False(result.GameCompatibilityEstablished);
        Assert.False(result.SaveWritingEnabled);
    }

    [Fact]
    public void FailedSecondCallNeverAcceptsPlausibleOutput()
    {
        var result = LegacyDecodeSafetyCheck.Inspect(0, 12, (uint)Valid.Length, (uint)Valid.Length, Valid);
        Assert.Equal(LegacyDecodeCheckStatus.DecodeFailed, result.Status);
        Assert.Null(result.Report);
    }

    [Fact]
    public void FailedQueryAndInconsistentOrExcessiveSizesFailClosed()
    {
        Assert.Equal(LegacyDecodeCheckStatus.SizeQueryFailed, LegacyDecodeSafetyCheck.Inspect(-1, 0, (uint)Valid.Length, (uint)Valid.Length, Valid).Status);
        Assert.Equal(LegacyDecodeCheckStatus.InvalidSize, LegacyDecodeSafetyCheck.Inspect(0, 0, 0, 0, []).Status);
        Assert.Equal(LegacyDecodeCheckStatus.InvalidSize, LegacyDecodeSafetyCheck.Inspect(0, 0, (uint)SiiEncodedText.MaximumBytes + 1, 1, Valid).Status);
        Assert.Equal(LegacyDecodeCheckStatus.SizeMismatch, LegacyDecodeSafetyCheck.Inspect(0, 0, (uint)Valid.Length, (uint)Valid.Length - 1, Valid).Status);
        Assert.Equal(LegacyDecodeCheckStatus.SizeMismatch, LegacyDecodeSafetyCheck.Inspect(0, 0, (uint)Valid.Length + 1, (uint)Valid.Length + 1, Valid).Status);
    }

    [Theory]
    [InlineData("root {\n field: 1\n")]
    [InlineData("root {\n unknown syntax\n}\n")]
    [InlineData("root {\n field: 1\n field: 2\n}\n")]
    [InlineData("ScsC synthetic prefix")]
    public void UnsupportedOrAmbiguousOutputIsRejected(string text)
    {
        byte[] output = Encoding.UTF8.GetBytes(text);
        var result = LegacyDecodeSafetyCheck.Inspect(0, 0, (uint)output.Length, (uint)output.Length, output);
        Assert.Equal(LegacyDecodeCheckStatus.OutputRejected, result.Status);
        Assert.False(result.GameCompatibilityEstablished);
        Assert.False(result.SaveWritingEnabled);
    }
}
