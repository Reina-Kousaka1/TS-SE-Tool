using System.Security.Cryptography;
using TSSE.Core.IO;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Writes a newly named plain-text SII copy after bounded parse, structural comparison,
/// encoding-preserving serialization, and validation. Existing files are never replaced.
/// ScsC and BSII serialization are intentionally unsupported.
/// </summary>
public static class SiiTextSaveCopyWriter
{
    public static SiiTextSaveCopyResult WriteNewCopy(
        string sourcePath,
        string destinationPath,
        SiiSyntaxDocument editedDocument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(editedDocument);

        string source;
        string destination;
        try
        {
            source = Path.GetFullPath(sourcePath);
            destination = Path.GetFullPath(destinationPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new(SiiTextSaveCopyStatus.PathRejected);
        }

        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(source, destination, pathComparison))
            return new(SiiTextSaveCopyStatus.SourceAndDestinationSame);

        byte[]? sourceBytes = null;
        byte[]? secondRead = null;
        byte[]? encoded = null;
        string? stagedPath = null;
        bool stagedFileCreated = false;
        try
        {
            string? outputDirectory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
                return new(SiiTextSaveCopyStatus.PathRejected);

            if (FileSystemPathSafety.ContainsReparsePointInPath(source) ||
                FileSystemPathSafety.ContainsReparsePointInPath(destination))
                return new(SiiTextSaveCopyStatus.LinkedPathRejected);

            if (Directory.Exists(destination) || File.Exists(destination))
                return new(SiiTextSaveCopyStatus.DestinationAlreadyExists);

            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (sourceStream.Length is <= 0 or > SiiEncodedText.MaximumBytes)
                return new(SiiTextSaveCopyStatus.SourceSizeRejected);

            sourceBytes = ReadExactly(sourceStream);
            sourceStream.Position = 0;
            secondRead = ReadExactly(sourceStream);
            if (!sourceBytes.AsSpan().SequenceEqual(secondRead) || sourceStream.ReadByte() != -1)
                return new(SiiTextSaveCopyStatus.SourceChangedDuringRead);

            SiiEncodedText original;
            try
            {
                original = SiiEncodedText.Parse(sourceBytes);
            }
            catch (Exception ex) when (ex is InvalidDataException or SiiSyntaxException or ArgumentException)
            {
                return new(SiiTextSaveCopyStatus.UnsupportedInput);
            }

            IReadOnlyList<SiiSyntaxDifference> differences;
            try
            {
                differences = SiiSyntaxComparison.Compare(original.Syntax, editedDocument);
            }
            catch (InvalidOperationException)
            {
                return new(SiiTextSaveCopyStatus.InvalidCandidate);
            }

            if (differences.Count == 0)
                return new(SiiTextSaveCopyStatus.NoChanges);
            if (differences.Any(change => change.Kind != SiiSyntaxDifferenceKind.FieldChanged))
                return new(SiiTextSaveCopyStatus.StructureChanged);

            SiiSyntaxDocument expected = original.Syntax;
            try
            {
                foreach (SiiSyntaxDifference change in differences)
                {
                    if (change.FieldName is null)
                        return new(SiiTextSaveCopyStatus.InvalidCandidate);
                    SiiBlock block = editedDocument.Blocks.Single(item =>
                        item.Type == change.BlockType && item.Id == change.BlockId);
                    SiiField field = block.Fields.Single(item => item.Name == change.FieldName);
                    expected = expected.WithFieldValue(change.BlockType, change.BlockId, change.FieldName, field.Value);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
            {
                return new(SiiTextSaveCopyStatus.InvalidCandidate);
            }

            if (!string.Equals(expected.ToText(), editedDocument.ToText(), StringComparison.Ordinal))
                return new(SiiTextSaveCopyStatus.StructureChanged);

            try
            {
                encoded = original.WithSyntax(editedDocument).ToBytes();
                SiiEncodedText roundtrip = SiiEncodedText.Parse(encoded);
                if (encoded.Length == 0 || roundtrip.Syntax.OpaqueLines.Count != 0 ||
                    !string.Equals(roundtrip.Syntax.ToText(), editedDocument.ToText(), StringComparison.Ordinal) ||
                    SiiSyntaxComparison.Compare(editedDocument, roundtrip.Syntax).Count != 0)
                    return new(SiiTextSaveCopyStatus.ValidationFailed);
            }
            catch (Exception ex) when (ex is InvalidDataException or SiiSyntaxException or InvalidOperationException or ArgumentException)
            {
                return new(SiiTextSaveCopyStatus.ValidationFailed);
            }

            string fileName = Path.GetFileName(destination);
            stagedPath = Path.Combine(outputDirectory, $".{fileName}.tsse-{Guid.NewGuid():N}.tmp");
            using (var stagedStream = new FileStream(
                stagedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.WriteThrough))
            {
                stagedFileCreated = true;
                stagedStream.Write(encoded);
                stagedStream.Flush(flushToDisk: true);
            }

            byte[] stagedBytes = File.ReadAllBytes(stagedPath);
            try
            {
                if (!stagedBytes.AsSpan().SequenceEqual(encoded))
                    return new(SiiTextSaveCopyStatus.ValidationFailed);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(stagedBytes);
            }

            try
            {
                File.Move(stagedPath, destination, overwrite: false);
                stagedFileCreated = false;
                return new(SiiTextSaveCopyStatus.Success, encoded.Length);
            }
            catch (IOException) when (File.Exists(destination) || Directory.Exists(destination))
            {
                return new(SiiTextSaveCopyStatus.DestinationAlreadyExists);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            return new(SiiTextSaveCopyStatus.IoFailure);
        }
        finally
        {
            if (stagedFileCreated && stagedPath is not null)
            {
                try { File.Delete(stagedPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
            }
            if (sourceBytes is not null) CryptographicOperations.ZeroMemory(sourceBytes);
            if (secondRead is not null) CryptographicOperations.ZeroMemory(secondRead);
            if (encoded is not null) CryptographicOperations.ZeroMemory(encoded);
        }
    }

    private static byte[] ReadExactly(FileStream stream)
    {
        if (stream.Length is <= 0 or > SiiEncodedText.MaximumBytes)
            throw new InvalidDataException("The plain-text source exceeds the configured size bounds.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

public sealed record SiiTextSaveCopyResult(SiiTextSaveCopyStatus Status, int? BytesWritten = null);

public enum SiiTextSaveCopyStatus
{
    Success,
    PathRejected,
    LinkedPathRejected,
    SourceAndDestinationSame,
    DestinationAlreadyExists,
    SourceSizeRejected,
    SourceChangedDuringRead,
    UnsupportedInput,
    InvalidCandidate,
    NoChanges,
    StructureChanged,
    ValidationFailed,
    IoFailure
}
