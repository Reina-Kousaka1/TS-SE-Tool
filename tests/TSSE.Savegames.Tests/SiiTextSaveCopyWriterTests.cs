using System.Text;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiTextSaveCopyWriterTests
{
    [Fact]
    public void WritesValidatedTextToNewPathPreservingEncodingAndSourceBytes()
    {
        string directory = CreateTestDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.sii");
            string destinationPath = Path.Combine(directory, "edited-copy.sii");
            byte[] sourceBytes = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(Original)];
            File.WriteAllBytes(sourcePath, sourceBytes);
            SiiSyntaxDocument edited = SiiEncodedText.Parse(sourceBytes).Syntax
                .WithFieldValue("economy", "e", "experience_points", "42");

            SiiTextSaveCopyResult result = SiiTextSaveCopyWriter.WriteNewCopy(sourcePath, destinationPath, edited);

            Assert.Equal(SiiTextSaveCopyStatus.Success, result.Status);
            Assert.True(result.BytesWritten is > 0);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
            SiiEncodedText written = SiiEncodedText.Parse(File.ReadAllBytes(destinationPath));
            Assert.Equal(SiiTextEncoding.Utf8Bom, written.EncodingKind);
            Assert.Equal(edited.ToText(), written.Syntax.ToText());

            byte[] firstOutput = File.ReadAllBytes(destinationPath);
            SiiTextSaveCopyResult duplicate = SiiTextSaveCopyWriter.WriteNewCopy(sourcePath, destinationPath, edited);
            Assert.Equal(SiiTextSaveCopyStatus.DestinationAlreadyExists, duplicate.Status);
            Assert.Equal(firstOutput, File.ReadAllBytes(destinationPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RefusesSamePathNoChangesAndStructuralChangesWithoutCreatingOutput()
    {
        string directory = CreateTestDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.sii");
            string destinationPath = Path.Combine(directory, "new-copy.sii");
            byte[] sourceBytes = Encoding.UTF8.GetBytes(Original);
            File.WriteAllBytes(sourcePath, sourceBytes);
            SiiSyntaxDocument original = SiiEncodedText.Parse(sourceBytes).Syntax;

            Assert.Equal(SiiTextSaveCopyStatus.SourceAndDestinationSame,
                SiiTextSaveCopyWriter.WriteNewCopy(sourcePath, sourcePath, original).Status);
            Assert.Equal(SiiTextSaveCopyStatus.NoChanges,
                SiiTextSaveCopyWriter.WriteNewCopy(sourcePath, destinationPath, original).Status);

            SiiSyntaxDocument structural = SiiSyntaxDocument.Parse(Original + "extra : x {\n value: 1\n}\n");
            Assert.Equal(SiiTextSaveCopyStatus.StructureChanged,
                SiiTextSaveCopyWriter.WriteNewCopy(sourcePath, destinationPath, structural).Status);

            const string reorderedText = "SiiNunit\n{\n bank : b {\n  money_account: 100\n }\n economy : e {\n  experience_points: 42\n  bank: b\n }\n}\n";
            SiiSyntaxDocument reordered = SiiSyntaxDocument.Parse(reorderedText);
            Assert.Equal(SiiTextSaveCopyStatus.StructureChanged,
                SiiTextSaveCopyWriter.WriteNewCopy(sourcePath, destinationPath, reordered).Status);

            Assert.False(File.Exists(destinationPath));
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsScsCInputAndDoesNotCreateAnEmptyOutput()
    {
        string directory = CreateTestDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "wrapped.sii");
            string destinationPath = Path.Combine(directory, "copy.sii");
            byte[] wrapped = "ScsC"u8.ToArray();
            File.WriteAllBytes(sourcePath, wrapped);
            SiiSyntaxDocument candidate = SiiSyntaxDocument.Parse(Original)
                .WithFieldValue("economy", "e", "experience_points", "42");

            Assert.Equal(SiiTextSaveCopyStatus.UnsupportedInput,
                SiiTextSaveCopyWriter.WriteNewCopy(sourcePath, destinationPath, candidate).Status);
            Assert.False(File.Exists(destinationPath));
            Assert.Equal(wrapped, File.ReadAllBytes(sourcePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TSSE-SiiCopy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private const string Original = "SiiNunit\n{\n bank : b {\n  money_account: 100\n }\n economy : e {\n  bank: b\n  experience_points: 5\n }\n}\n";
}
