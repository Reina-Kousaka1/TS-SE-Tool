using System.Runtime.InteropServices;
using TSSE.Savegames.Diagnostics;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class NativeDecoderHeaderProbeTests
{
    [Fact]
    public void BundledLegacyDllHasX86HeadersAndDoesNotMatch64BitTargets()
    {
        byte[] binary = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-native-header.dll"));
        NativeDecoderHeaderResult x86 = NativeDecoderHeaderProbe.Inspect(binary, Architecture.X86);
        Assert.Equal(NativeDecoderHeaderStatus.NativePeHeadersOnly, x86.Status);
        Assert.Equal(Architecture.X86, x86.Architecture);
        Assert.True(x86.MatchesTargetArchitecture);
        Assert.False(x86.NativeLoadAuthorized);
        Assert.False(NativeDecoderHeaderProbe.Inspect(binary, Architecture.X64).MatchesTargetArchitecture);
        Assert.False(NativeDecoderHeaderProbe.Inspect(binary, Architecture.Arm64).MatchesTargetArchitecture);
    }

    [Fact]
    public void ManagedAssembliesAreNotAcceptedAsNativeDecoderDlls()
    {
        byte[] managed = File.ReadAllBytes(typeof(NativeDecoderHeaderProbe).Assembly.Location);
        var result = NativeDecoderHeaderProbe.Inspect(managed, Architecture.X64);
        Assert.Equal(NativeDecoderHeaderStatus.NotNativeDll, result.Status);
        Assert.False(result.MatchesTargetArchitecture);
        Assert.False(result.NativeLoadAuthorized);
    }

    [Fact]
    public void EmptyOversizedAndTruncatedImagesFailClosed()
    {
        Assert.Equal(NativeDecoderHeaderStatus.InvalidSize, NativeDecoderHeaderProbe.Inspect([], Architecture.X64).Status);
        Assert.Equal(NativeDecoderHeaderStatus.InvalidSize, NativeDecoderHeaderProbe.Inspect(new byte[NativeDecoderHeaderProbe.MaximumImageBytes + 1], Architecture.X64).Status);
        var truncated = NativeDecoderHeaderProbe.Inspect("MZ"u8, Architecture.X64);
        Assert.Equal(NativeDecoderHeaderStatus.InvalidImage, truncated.Status);
        Assert.False(truncated.NativeLoadAuthorized);
    }
}
