using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using TSSE.Savegames.Diagnostics;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class NativeDecoderExportProbeTests
{
    [Fact]
    public void BundledLegacyImageContainsExpectedNamedDirectExports()
    {
        byte[] binary = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-native-header.dll"));
        var result = NativeDecoderExportProbe.Inspect(binary);
        Assert.Equal(NativeDecoderExportStatus.Inspected, result.Status);
        Assert.Equal(NativeExportKind.DirectRva, result.GetMemoryFormat);
        Assert.Equal(NativeExportKind.DirectRva, result.DecodeMemory);
        Assert.Equal(NativeExportKind.DirectRva, result.DecryptAndDecodeMemory);
        Assert.False(result.NativeLoadAuthorized);
    }

    [Fact]
    public void ExcessiveExportCountsAndBrokenImagesFailClosed()
    {
        byte[] binary = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-native-header.dll"));
        using (var pe = new PEReader(new MemoryStream(binary, writable: false)))
        {
            int rva = pe.PEHeaders.PEHeader!.ExportTableDirectory.RelativeVirtualAddress;
            SectionHeader section = pe.PEHeaders.SectionHeaders.Single(item => rva >= item.VirtualAddress && rva < item.VirtualAddress + item.SizeOfRawData);
            int offset = section.PointerToRawData + rva - section.VirtualAddress;
            BinaryPrimitives.WriteInt32LittleEndian(binary.AsSpan(offset + 24, 4), 4097);
        }
        var result = NativeDecoderExportProbe.Inspect(binary);
        Assert.Equal(NativeDecoderExportStatus.InvalidImage, result.Status);
        Assert.Null(result.GetMemoryFormat);
        Assert.False(result.NativeLoadAuthorized);
        Assert.Equal(NativeDecoderExportStatus.InvalidImage, NativeDecoderExportProbe.Inspect("MZ"u8).Status);
        Assert.Equal(NativeDecoderExportStatus.InvalidImage, NativeDecoderExportProbe.Inspect([]).Status);
    }

    [Fact]
    public void AChangedExportNameIsReportedMissing()
    {
        byte[] binary = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-native-header.dll"));
        using (var pe = new PEReader(new MemoryStream(binary, writable: false)))
        {
            int Offset(int rva)
            {
                SectionHeader section = pe.PEHeaders.SectionHeaders.Single(item => rva >= item.VirtualAddress && rva < item.VirtualAddress + item.SizeOfRawData);
                return section.PointerToRawData + rva - section.VirtualAddress;
            }
            int table = Offset(pe.PEHeaders.PEHeader!.ExportTableDirectory.RelativeVirtualAddress);
            int count = BinaryPrimitives.ReadInt32LittleEndian(binary.AsSpan(table + 24, 4));
            int pointers = Offset(BinaryPrimitives.ReadInt32LittleEndian(binary.AsSpan(table + 32, 4)));
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                int name = Offset(BinaryPrimitives.ReadInt32LittleEndian(binary.AsSpan(pointers + i * 4, 4)));
                byte[] target = Encoding.ASCII.GetBytes("GetMemoryFormat\0");
                if (!binary.AsSpan(name, target.Length).SequenceEqual(target)) continue;
                binary[name] = (byte)'X';
                found++;
            }
            Assert.Equal(1, found);
        }
        var result = NativeDecoderExportProbe.Inspect(binary);
        Assert.Equal(NativeDecoderExportStatus.Inspected, result.Status);
        Assert.Equal(NativeExportKind.Missing, result.GetMemoryFormat);
        Assert.Equal(NativeExportKind.DirectRva, result.DecodeMemory);
        Assert.False(result.NativeLoadAuthorized);
    }
}
