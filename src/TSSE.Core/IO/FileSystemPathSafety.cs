namespace TSSE.Core.IO;

/// <summary>Best-effort checks for symbolic links and other filesystem reparse points.</summary>
/// <remarks>These path-based checks are not atomic and cannot prevent a path change after inspection.</remarks>
public static class FileSystemPathSafety
{
    /// <summary>Checks whether an existing path itself is a filesystem reparse point.</summary>
    public static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    /// <summary>Checks for a reparse point, including a dangling symbolic link.</summary>
    /// <remarks>Returns false when the path and its link target are both absent.</remarks>
    public static bool IsReparsePointIfPresent(string path)
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

    /// <summary>Checks the target and each existing lexical parent path for a reparse point.</summary>
    public static bool ContainsReparsePointInPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string current = Path.GetFullPath(path);
        while (true)
        {
            if (IsReparsePointIfPresent(current))
                return true;

            string? parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
                return false;

            current = parent;
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
