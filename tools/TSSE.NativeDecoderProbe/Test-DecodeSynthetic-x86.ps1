# Fixed synthetic BSII buffer only; no arguments, save files, encryption, or output files.
$ErrorActionPreference = 'Stop'
if ($args.Count -ne 0) { throw 'This probe accepts no arguments.' }
if ([IntPtr]::Size -ne 4) { throw 'This isolated probe requires a 32-bit PowerShell process.' }
$dllPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\TS SE Tool\SII_Decrypt.dll')).Path
if ((Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash -ne 'F6393C253BBBBC8DDB7B2F39A22A9223FB2C80D927657B077B0DBC92532BFEBA') {
    throw 'The native image does not match the inspected legacy binary.'
}

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class SyntheticNativeDecodeProbe {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DecodeMemory(IntPtr input, UIntPtr inputSize, IntPtr output, ref UIntPtr outputSize);
    private static void WriteString(BinaryWriter writer, string value) {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }
    private static byte[] Fixture(bool includeExperimentalInt64Array) {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream)) {
            writer.Write(Encoding.ASCII.GetBytes("BSII"));
            writer.Write((uint)2);             // historical binary format version
            writer.Write((uint)0); writer.Write((byte)1); writer.Write((uint)1);
            WriteString(writer, "synthetic");  // structure name
            writer.Write((uint)0x25); WriteString(writer, "value"); // Int32 field
            writer.Write((uint)0x27); WriteString(writer, "xp"); // UInt32 field
            writer.Write((uint)0x27); WriteString(writer, "xp_value");
            writer.Write((uint)0x31); WriteString(writer, "balance"); // Int64 field
            writer.Write((uint)0x33); WriteString(writer, "wide_nil");
            writer.Write((uint)0x33); WriteString(writer, "wide_high");
            writer.Write((uint)0x33); WriteString(writer, "wide_low");
            writer.Write((uint)0x33); WriteString(writer, "wide_value");
            writer.Write((uint)0x01); WriteString(writer, "label");
            writer.Write((uint)0x39); WriteString(writer, "empty_ref");
            writer.Write((uint)0x3d); WriteString(writer, "anon_ref");
            writer.Write((uint)0x3b); WriteString(writer, "named_ref");
            writer.Write((uint)0x3a); WriteString(writer, "empty_refs");
            writer.Write((uint)0x3c); WriteString(writer, "refs");
            writer.Write((uint)0x05); WriteString(writer, "fuel");
            writer.Write((uint)0x06); WriteString(writer, "wear");
            writer.Write((uint)0x26); WriteString(writer, "signed_values");
            writer.Write((uint)0x28); WriteString(writer, "unsigned_values");
            if (includeExperimentalInt64Array) { writer.Write((uint)0x32); WriteString(writer, "long_values"); }
            writer.Write((uint)0x34); WriteString(writer, "wide_values");
            writer.Write((uint)0x35); WriteString(writer, "active");
            writer.Write((uint)0x36); WriteString(writer, "flags");
            writer.Write((uint)0x02); WriteString(writer, "labels");
            writer.Write((uint)0x03); WriteString(writer, "encoded");
            writer.Write((uint)0x04); WriteString(writer, "encoded_list");
            writer.Write((uint)0);             // end of field definitions
            writer.Write((uint)1);             // data block references structure 1
            writer.Write((byte)2); writer.Write(0x800000000000000bul); writer.Write(12ul); // synthetic named ID
            writer.Write((int)42);
            writer.Write(uint.MaxValue);
            writer.Write(uint.MaxValue - 1);
            writer.Write(long.MinValue);
            writer.Write(ulong.MaxValue);
            writer.Write(0xffffffff00000001ul);
            writer.Write(0x00000001fffffffful);
            writer.Write(9876543210ul);
            WriteString(writer, "route demo");
            writer.Write((byte)0);
            writer.Write((byte)0xff); writer.Write(0x9876ul);
            writer.Write((byte)2); writer.Write(0x800000000000000bul); writer.Write(12ul);
            writer.Write((uint)0);
            writer.Write((uint)4);
            writer.Write((byte)0);
            writer.Write((byte)0xff); writer.Write(0x9876ul);
            for (int i = 0; i < 2; i++) { writer.Write((byte)2); writer.Write(0x800000000000000bul); writer.Write(12ul); }
            writer.Write(0x3f000000u);
            writer.Write((uint)3); writer.Write(0x3f000000u); writer.Write(0x3fa00000u); writer.Write(0x80000000u);
            writer.Write((uint)2); writer.Write(int.MinValue); writer.Write(int.MaxValue);
            writer.Write((uint)3); writer.Write(0u); writer.Write(uint.MaxValue); writer.Write(uint.MaxValue - 1);
            if (includeExperimentalInt64Array) { writer.Write((uint)2); writer.Write(-6L); writer.Write(8L); }
            writer.Write((uint)3); writer.Write(0ul); writer.Write(0xffffffff00000001ul); writer.Write(9876543210ul);
            writer.Write((byte)255); writer.Write((uint)4);
            writer.Write((byte)0); writer.Write((byte)1); writer.Write((byte)2); writer.Write((byte)255);
            writer.Write((uint)3); WriteString(writer, ""); WriteString(writer, "road demo"); WriteString(writer, "abc");
            writer.Write(0x800000000000000bul);
            writer.Write((uint)4); writer.Write(0ul); writer.Write(12ul); writer.Write(0x800000000000000bul); writer.Write(12ul);
            writer.Write((uint)0); writer.Write((byte)0); // end marker
            return stream.ToArray();
        }
    }
    public static string Check(string path) {
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 0x00000100 | 0x00001000);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            IntPtr address = GetProcAddress(module, "DecodeMemory");
            if (address == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var decode = (DecodeMemory)Marshal.GetDelegateForFunctionPointer(address, typeof(DecodeMemory));
            byte[] unsupported = Fixture(true);
            var unsupportedHandle = GCHandle.Alloc(unsupported, GCHandleType.Pinned);
            try {
                UIntPtr unavailableSize = UIntPtr.Zero;
                int rejected = decode(unsupportedHandle.AddrOfPinnedObject(), (UIntPtr)(uint)unsupported.Length, IntPtr.Zero, ref unavailableSize);
                if (rejected != -1) throw new InvalidDataException("Unexpected Int64-array query result: " + rejected + ".");
            } finally { unsupportedHandle.Free(); }
            byte[] input = Fixture(false);
            var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            try {
                UIntPtr announced = UIntPtr.Zero;
                int first = decode(inputHandle.AddrOfPinnedObject(), (UIntPtr)(uint)input.Length, IntPtr.Zero, ref announced);
                ulong capacity = announced.ToUInt64();
                if (first != 0 || capacity == 0 || capacity > 1024 * 1024) throw new InvalidDataException("Size query result " + first + ", capacity " + capacity + ".");
                byte[] output = new byte[(int)capacity];
                for (int i = 0; i < output.Length; i++) output[i] = 0xa5;
                var outputHandle = GCHandle.Alloc(output, GCHandleType.Pinned);
                try {
                    UIntPtr zero = UIntPtr.Zero;
                    int tooSmall = decode(inputHandle.AddrOfPinnedObject(), (UIntPtr)(uint)input.Length, outputHandle.AddrOfPinnedObject(), ref zero);
                    if (tooSmall != 0 || zero.ToUInt64() != 0) throw new InvalidDataException("Unexpected zero-capacity behavior.");
                    foreach (byte b in output) if (b != 0xa5) throw new InvalidDataException("Zero-capacity call changed the output buffer.");
                    UIntPtr one = (UIntPtr)1;
                    int undersized = decode(inputHandle.AddrOfPinnedObject(), (UIntPtr)(uint)input.Length, outputHandle.AddrOfPinnedObject(), ref one);
                    if (undersized != 0 || one.ToUInt64() != 1) throw new InvalidDataException("Unexpected one-byte capacity behavior.");
                    for (int i = 1; i < output.Length; i++) if (output[i] != 0xa5) throw new InvalidDataException("One-byte call wrote beyond its declared capacity.");
                    UIntPtr produced = (UIntPtr)(uint)capacity;
                    int second = decode(inputHandle.AddrOfPinnedObject(), (UIntPtr)(uint)input.Length, outputHandle.AddrOfPinnedObject(), ref produced);
                    ulong length = produced.ToUInt64();
                    if (second != 0 || length == 0 || length > capacity) throw new InvalidDataException("Decode failed or returned an invalid length.");
                    return new UTF8Encoding(false, true).GetString(output, 0, (int)length);
                } finally { outputHandle.Free(); }
            } finally { inputHandle.Free(); }
        } finally { FreeLibrary(module); }
    }
}
'@

$decoded = [SyntheticNativeDecodeProbe]::Check($dllPath)
if ($decoded -notmatch '(?m)^SiiNunit\s*$' -or $decoded -notmatch '(?m)^\s*synthetic\s*:\s*a\.b\s*\{' -or $decoded -notmatch '(?m)^\s*value:\s*42\s*$' -or
    $decoded -notmatch '(?m)^\s*xp:\s*nil\s*$' -or $decoded -notmatch '(?m)^\s*xp_value:\s*4294967294\s*$' -or
    $decoded -notmatch '(?m)^\s*balance:\s*-9223372036854775808\s*$' -or
    $decoded -notmatch '(?m)^\s*wide_nil:\s*nil\s*$' -or $decoded -notmatch '(?m)^\s*wide_high:\s*nil\s*$' -or
    $decoded -notmatch '(?m)^\s*wide_low:\s*nil\s*$' -or $decoded -notmatch '(?m)^\s*wide_value:\s*9876543210\s*$' -or
    $decoded -notmatch '(?m)^\s*label:\s*"route demo"\s*$' -or
    $decoded -notmatch '(?m)^\s*empty_ref:\s*null\s*$' -or $decoded -notmatch '(?m)^\s*anon_ref:\s*_nameless\.9876\s*$' -or
    $decoded -notmatch '(?m)^\s*named_ref:\s*a\.b\s*$' -or $decoded -notmatch '(?m)^\s*empty_refs:\s*0\s*$' -or $decoded -notmatch '(?m)^\s*refs:\s*4\s*$' -or
    $decoded -notmatch '(?m)^\s*refs\[0\]:\s*null\s*$' -or $decoded -notmatch '(?m)^\s*refs\[1\]:\s*_nameless\.9876\s*$' -or
    $decoded -notmatch '(?m)^\s*refs\[2\]:\s*a\.b\s*$' -or $decoded -notmatch '(?m)^\s*refs\[3\]:\s*a\.b\s*$' -or
    $decoded -notmatch '(?m)^\s*fuel:\s*&3f000000\s*$' -or $decoded -notmatch '(?m)^\s*wear:\s*3\s*$' -or
    $decoded -notmatch '(?m)^\s*wear\[0\]:\s*&3f000000\s*$' -or $decoded -notmatch '(?m)^\s*wear\[1\]:\s*&3fa00000\s*$' -or
    $decoded -notmatch '(?m)^\s*wear\[2\]:\s*-?0\s*$' -or
    $decoded -notmatch '(?m)^\s*signed_values\[0\]:\s*-2147483648\s*$' -or
    $decoded -notmatch '(?m)^\s*signed_values\[1\]:\s*2147483647\s*$' -or
    $decoded -notmatch '(?m)^\s*unsigned_values\[1\]:\s*nil\s*$' -or
    $decoded -notmatch '(?m)^\s*unsigned_values\[2\]:\s*4294967294\s*$' -or
    $decoded -notmatch '(?m)^\s*wide_values\[1\]:\s*nil\s*$' -or
    $decoded -notmatch '(?m)^\s*wide_values\[2\]:\s*9876543210\s*$' -or
    $decoded -notmatch '(?m)^\s*active:\s*true\s*$' -or $decoded -notmatch '(?m)^\s*flags:\s*4\s*$' -or
    $decoded -notmatch '(?m)^\s*flags\[0\]:\s*false\s*$' -or $decoded -notmatch '(?m)^\s*flags\[1\]:\s*true\s*$' -or
    $decoded -notmatch '(?m)^\s*flags\[2\]:\s*true\s*$' -or $decoded -notmatch '(?m)^\s*flags\[3\]:\s*true\s*$' -or
    $decoded -notmatch '(?m)^\s*labels:\s*3\s*$' -or $decoded -notmatch '(?m)^\s*labels\[0\]:\s*""\s*$' -or
    $decoded -notmatch '(?m)^\s*labels\[1\]:\s*"road demo"\s*$' -or $decoded -notmatch '(?m)^\s*labels\[2\]:\s*abc\s*$' -or
    $decoded -notmatch '(?m)^\s*encoded:\s*a\s*$' -or $decoded -notmatch '(?m)^\s*encoded_list:\s*4\s*$' -or
    $decoded -notmatch '(?m)^\s*encoded_list\[0\]:\s*""\s*$' -or $decoded -notmatch '(?m)^\s*encoded_list\[1\]:\s*b\s*$' -or
    $decoded -notmatch '(?m)^\s*encoded_list\[2\]:\s*a\s*$' -or $decoded -notmatch '(?m)^\s*encoded_list\[3\]:\s*b\s*$') {
    throw ('The fixed synthetic decoded text did not contain the expected structure and value: ' + $decoded)
}
Write-Output 'Synthetic x86 DecodeMemory: Int64 array query rejected; undersized calls reported success; bounded decode contained expected values; no save file used.'
