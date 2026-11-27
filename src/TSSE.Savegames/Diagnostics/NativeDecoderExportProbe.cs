using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace TSSE.Savegames.Diagnostics;

/// <summary>Bounded PE export inspection; never loads or executes the image.</summary>
public static class NativeDecoderExportProbe
{
    private const int MaximumExports = 4096;
    private const int MaximumNameBytes = 128;
    private static readonly string[] Wanted = ["GetMemoryFormat", "DecodeMemory", "DecryptAndDecodeMemory"];

    public static NativeDecoderExportResult Inspect(ReadOnlySpan<byte> image)
    {
        var header = NativeDecoderHeaderProbe.Inspect(image, Architecture.X86);
        if (header.Status != NativeDecoderHeaderStatus.NativePeHeadersOnly)
            return new(NativeDecoderExportStatus.InvalidImage, null, null, null);
        try
        {
            using var reader = new PEReader(new MemoryStream(image.ToArray(), writable: false));
            DirectoryEntry directory = reader.PEHeaders.PEHeader!.ExportTableDirectory;
            if (directory.RelativeVirtualAddress == 0 && directory.Size == 0)
                return new(NativeDecoderExportStatus.NoExports, null, null, null);
            if (directory.RelativeVirtualAddress <= 0 || directory.Size < 40)
                throw new BadImageFormatException();
            BlobReader table = reader.GetSectionData(directory.RelativeVirtualAddress).GetReader();
            if (table.RemainingBytes < directory.Size) throw new BadImageFormatException();
            table.Offset = 20;
            int addressCount = table.ReadInt32();
            int nameCount = table.ReadInt32();
            int addressesRva = table.ReadInt32();
            int namesRva = table.ReadInt32();
            int ordinalsRva = table.ReadInt32();
            if (addressCount is < 1 or > MaximumExports || nameCount is < 1 or > MaximumExports ||
                addressesRva <= 0 || namesRva <= 0 || ordinalsRva <= 0)
                throw new BadImageFormatException();
            BlobReader addresses = reader.GetSectionData(addressesRva).GetReader();
            BlobReader names = reader.GetSectionData(namesRva).GetReader();
            BlobReader ordinals = reader.GetSectionData(ordinalsRva).GetReader();
            if (addresses.RemainingBytes < addressCount * 4 || names.RemainingBytes < nameCount * 4 || ordinals.RemainingBytes < nameCount * 2)
                throw new BadImageFormatException();
            var found = new Dictionary<string, NativeExportKind>(StringComparer.Ordinal);
            for (int i = 0; i < nameCount; i++)
            {
                int nameRva = names.ReadInt32();
                int ordinal = ordinals.ReadUInt16();
                if (nameRva <= 0 || ordinal >= addressCount) throw new BadImageFormatException();
                string name = ReadName(reader, nameRva);
                if (!Wanted.Contains(name, StringComparer.Ordinal)) continue;
                addresses.Offset = ordinal * 4;
                int targetRva = addresses.ReadInt32();
                if (targetRva <= 0) throw new BadImageFormatException();
                bool forwarded = (long)targetRva >= directory.RelativeVirtualAddress &&
                    (long)targetRva < (long)directory.RelativeVirtualAddress + directory.Size;
                if (forwarded)
                {
                    string target = ReadName(reader, targetRva);
                    if ((long)targetRva + target.Length >= (long)directory.RelativeVirtualAddress + directory.Size)
                        throw new BadImageFormatException();
                }
                if (!forwarded && reader.GetSectionData(targetRva).Length == 0) throw new BadImageFormatException();
                if (!found.TryAdd(name, forwarded ? NativeExportKind.Forwarded : NativeExportKind.DirectRva))
                    throw new BadImageFormatException();
            }
            return new(NativeDecoderExportStatus.Inspected,
                found.GetValueOrDefault(Wanted[0], NativeExportKind.Missing),
                found.GetValueOrDefault(Wanted[1], NativeExportKind.Missing),
                found.GetValueOrDefault(Wanted[2], NativeExportKind.Missing));
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return new(NativeDecoderExportStatus.InvalidImage, null, null, null);
        }
    }

    private static string ReadName(PEReader reader, int rva)
    {
        BlobReader data = reader.GetSectionData(rva).GetReader();
        var bytes = new List<byte>();
        while (data.RemainingBytes > 0 && bytes.Count < MaximumNameBytes)
        {
            byte value = data.ReadByte();
            if (value == 0 && bytes.Count != 0) return Encoding.ASCII.GetString(bytes.ToArray());
            if (value == 0) throw new BadImageFormatException();
            if (value is < 33 or > 126) throw new BadImageFormatException();
            bytes.Add(value);
        }
        throw new BadImageFormatException();
    }
}

public enum NativeDecoderExportStatus { InvalidImage, NoExports, Inspected }
public enum NativeExportKind { Missing, DirectRva, Forwarded }

/// <summary>Names and RVAs do not establish function signatures, calling conventions, trust or permission to load.</summary>
public sealed record NativeDecoderExportResult(NativeDecoderExportStatus Status, NativeExportKind? GetMemoryFormat,
    NativeExportKind? DecodeMemory, NativeExportKind? DecryptAndDecodeMemory)
{
    public bool NativeLoadAuthorized => false;
}
