using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace TSSE.Savegames.Diagnostics;

/// <summary>Inspects PE headers in memory without loading or executing native code.</summary>
public static class NativeDecoderHeaderProbe
{
    public const int MaximumImageBytes = 16 * 1024 * 1024;

    public static NativeDecoderHeaderResult Inspect(ReadOnlySpan<byte> image, Architecture target)
    {
        if (image.Length == 0 || image.Length > MaximumImageBytes)
            return new(NativeDecoderHeaderStatus.InvalidSize, null, false);
        try
        {
            using var stream = new MemoryStream(image.ToArray(), writable: false);
            using var reader = new PEReader(stream);
            PEHeaders headers = reader.PEHeaders;
            if (!headers.IsDll || headers.PEHeader is null)
                return new(NativeDecoderHeaderStatus.NotNativeDll, null, false);
            if (headers.CorHeader is not null)
                return new(NativeDecoderHeaderStatus.NotNativeDll, null, false);

            Architecture? architecture = (headers.CoffHeader.Machine, headers.PEHeader.Magic) switch
            {
                (Machine.I386, PEMagic.PE32) => Architecture.X86,
                (Machine.Amd64, PEMagic.PE32Plus) => Architecture.X64,
                (Machine.Arm64, PEMagic.PE32Plus) => Architecture.Arm64,
                _ => null
            };
            return architecture is null
                ? new(NativeDecoderHeaderStatus.UnsupportedMachine, null, false)
                : new(NativeDecoderHeaderStatus.NativePeHeadersOnly, architecture, architecture == target);
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or ArgumentException or InvalidOperationException)
        {
            return new(NativeDecoderHeaderStatus.InvalidImage, null, false);
        }
    }
}

public enum NativeDecoderHeaderStatus { InvalidSize, InvalidImage, NotNativeDll, UnsupportedMachine, NativePeHeadersOnly }

/// <summary>A header match does not verify exports, ABI, trust, OS compatibility, or permission to load.</summary>
public sealed record NativeDecoderHeaderResult(NativeDecoderHeaderStatus Status, Architecture? Architecture, bool MatchesTargetArchitecture)
{
    public bool NativeLoadAuthorized => false;
}
