namespace TSSE.Core.IO;

public interface IAtomicFileReplacer
{
    void Replace(string stagedPath, string targetPath, string backupPath);
}

public sealed class PlatformAtomicFileReplacer : IAtomicFileReplacer
{
    public void Replace(string stagedPath, string targetPath, string backupPath) =>
        File.Replace(stagedPath, targetPath, backupPath);
}
