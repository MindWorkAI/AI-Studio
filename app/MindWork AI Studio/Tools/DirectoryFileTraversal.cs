namespace AIStudio.Tools;

/// <summary>
/// Enumerates files with the directory rules shared by folder-based features.
/// </summary>
internal static class DirectoryFileTraversal
{
    private const FileAttributes ALWAYS_SKIPPED_DIRECTORY_ATTRIBUTES =
        FileAttributes.ReparsePoint | FileAttributes.Offline | FileAttributes.System;

    /// <summary>
    /// Enumerates files below an explicitly selected root. The root itself is always read; the
    /// options apply only to its descendants.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(
        string rootPath,
        bool includeSubdirectories,
        bool includeHiddenSubdirectories,
        Func<string, bool>? includeDirectory = null,
        Action<string, Exception>? reportInaccessibleDirectory = null)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            var currentPath = pendingDirectories.Pop();
            string[] files;
            string[] subdirectories;
            try
            {
                files = Directory.GetFiles(currentPath);
                subdirectories = includeSubdirectories ? Directory.GetDirectories(currentPath) : [];
            }
            catch (Exception exception)
            {
                reportInaccessibleDirectory?.Invoke(currentPath, exception);
                continue;
            }

            foreach (var file in files)
                yield return file;

            foreach (var subdirectory in subdirectories)
            {
                if (includeDirectory is not null && !includeDirectory(subdirectory))
                    continue;

                try
                {
                    var directory = new DirectoryInfo(subdirectory);
                    var attributes = directory.Attributes;
                    if ((attributes & ALWAYS_SKIPPED_DIRECTORY_ATTRIBUTES) != 0)
                        continue;

                    if (!includeHiddenSubdirectories && IsHidden(directory, attributes))
                        continue;
                }
                catch (Exception exception)
                {
                    reportInaccessibleDirectory?.Invoke(subdirectory, exception);
                    continue;
                }

                pendingDirectories.Push(subdirectory);
            }
        }
    }

    /// <summary>
    /// Whether an existing path is inside a hidden descendant of the root. Hidden files directly
    /// in a visible folder are deliberately not treated as hidden directories.
    /// </summary>
    public static bool IsInHiddenSubdirectory(string rootPath, string path)
    {
        try
        {
            var root = new DirectoryInfo(Path.GetFullPath(rootPath));
            var current = Directory.Exists(path)
                ? new DirectoryInfo(Path.GetFullPath(path))
                : new FileInfo(Path.GetFullPath(path)).Directory;

            while (current is not null && !string.Equals(current.FullName, root.FullName, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsBelowRoot(root.FullName, current.FullName))
                    return false;

                if (IsHidden(current, current.Attributes))
                    return true;

                current = current.Parent;
            }
        }
        catch
        {
            // A watcher event can race with a rename or deletion. Let the caller refresh
            // conservatively when the former directory attributes can no longer be inspected.
        }

        return false;
    }

    private static bool IsHidden(DirectoryInfo directory, FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Hidden)
               || directory.Name.StartsWith(".", StringComparison.Ordinal);
    }

    private static bool IsBelowRoot(string rootPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        return relativePath is not ".."
               && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !Path.IsPathRooted(relativePath);
    }
}
