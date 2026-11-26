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

        string current = NormalizeKnownSystemPathAliases(Path.GetFullPath(path));
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

    private static string NormalizeKnownSystemPathAliases(string path)
    {
        if (!OperatingSystem.IsMacOS())
            return path;

        string root = Path.GetPathRoot(path)!;
        foreach (string aliasName in new[] { "etc", "tmp", "var" })
        {
            string aliasPath = Path.Combine(root, aliasName);
            if (!string.Equals(path, aliasPath, StringComparison.Ordinal) &&
                !path.StartsWith(aliasPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;

            string physicalPath = Path.Combine(root, "private", aliasName);
            return physicalPath + path[aliasPath.Length..];
        }

        return path;
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
