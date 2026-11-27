# Development-only, synthetic-buffer probe. Does not accept paths, decode or write saves.
$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 4) { throw 'This isolated probe requires a 32-bit PowerShell process.' }
$dllPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\TS SE Tool\SII_Decrypt.dll')).Path
$expectedHash = 'F6393C253BBBBC8DDB7B2F39A22A9223FB2C80D927657B077B0DBC92532BFEBA'
if ((Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'The native image does not match the inspected legacy binary.'
}

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class SyntheticNativeFormatProbe {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetMemoryFormat(IntPtr buffer, UIntPtr size);
    public static int[] Check(string path) {
        // Limit dependency search to the DLL directory and system defaults.
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 0x00000100 | 0x00001000);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            IntPtr address = GetProcAddress(module, "GetMemoryFormat");
            if (address == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var query = (GetMemoryFormat)Marshal.GetDelegateForFunctionPointer(address, typeof(GetMemoryFormat));
            byte[][] samples = {
                System.Text.Encoding.ASCII.GetBytes("SiiNunit\n{\n sample : demo {\n value: 1\n }\n}\n"),
                System.Text.Encoding.ASCII.GetBytes("NOT_A_SII_SYNTHETIC_BUFFER"),
                new byte[] { 0x53, 0x69 }
            };
            int[] results = new int[samples.Length];
            for (int i = 0; i < samples.Length; i++) {
                var handle = GCHandle.Alloc(samples[i], GCHandleType.Pinned);
                try { results[i] = query(handle.AddrOfPinnedObject(), (UIntPtr)(uint)samples[i].Length); }
                finally { handle.Free(); }
            }
            return results;
        } finally { FreeLibrary(module); }
    }
}
'@

$actual = [SyntheticNativeFormatProbe]::Check($dllPath)
if ($actual.Length -ne 3 -or $actual[0] -ne 1 -or $actual[1] -ne 10 -or $actual[2] -ne 11) {
    throw ('Unexpected synthetic format results: ' + ($actual -join ','))
}
Write-Output 'Synthetic x86 GetMemoryFormat: plaintext=1, unknown=10, short=11; no container decoded.'
