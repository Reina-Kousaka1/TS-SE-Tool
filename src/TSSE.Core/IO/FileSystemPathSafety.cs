namespace TSSE.Core.IO;

internal static class FileSystemPathSafety
{
    internal static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    internal static bool IsReparsePointIfPresent(string path)
    {
        try
        {
            return IsReparsePoint(path);
        }
        catch (FileNotFoundException)
        {
            return IsDanglingSymbolicLink(path);
        }
        catch (DirectoryNotFoundException)
        {
            return IsDanglingSymbolicLink(path);
        }
    }

    private static bool IsDanglingSymbolicLink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
