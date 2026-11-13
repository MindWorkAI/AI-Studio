namespace AIStudio.Tools;

/// <summary>
/// Enumerates files with the directory rules shared by folder-based features.
/// </summary>
/// <remarks>
/// Hidden files and folders follow the convention of a shell: a file or folder is hidden when its
/// name begins with a dot or when the operating system marks it as hidden. Unless they are asked
/// for, hidden folders are not entered and hidden files are not returned, just as * in a shell
/// does not match .env. A caller can still ask for single hidden files, e.g., because a file
/// pattern begins with a dot itself, as .gitignore does.
///
/// The metadata of operating systems, such as Thumbs.db or __MACOSX, is never returned, cf.
/// FileExclusions.
/// </remarks>
internal static class DirectoryFileTraversal
{
    /// <summary>
    /// What the name of a hidden file or folder begins with on macOS and Linux.
    /// </summary>
    public const char HIDDEN_NAME_PREFIX = '.';

    private const FileAttributes ALWAYS_SKIPPED_DIRECTORY_ATTRIBUTES =
        FileAttributes.ReparsePoint | FileAttributes.Offline | FileAttributes.System;

    /// <summary>
    /// Enumerates files below an explicitly selected root. The root itself is always read, even
    /// when it is hidden; the options apply to everything in it.
    /// </summary>
    /// <param name="rootPath">The selected folder.</param>
    /// <param name="includeSubdirectories">Whether files in subfolders are returned as well.</param>
    /// <param name="includeHiddenFilesAndFolders">Whether hidden files are returned and hidden folders are entered.</param>
    /// <param name="includeHiddenFile">Asked for each hidden file while hidden files and folders are left out: whether this one is wanted anyway.</param>
    /// <param name="includeDirectory">Whether a subfolder is entered at all.</param>
    /// <param name="reportInaccessibleDirectory">Told about every folder which could not be read.</param>
    /// <returns>The full paths of the files.</returns>
    public static IEnumerable<string> EnumerateFiles(
        string rootPath,
        bool includeSubdirectories,
        bool includeHiddenFilesAndFolders,
        Func<string, bool>? includeHiddenFile = null,
        Func<string, bool>? includeDirectory = null,
        Action<string, Exception>? reportInaccessibleDirectory = null)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            var currentPath = pendingDirectories.Pop();
            FileInfo[] files;
            DirectoryInfo[] subdirectories;
            try
            {
                //
                // Unlike plain paths, the entries carry the attributes the enumeration read anyway,
                // so telling hidden files apart costs no extra call per file on Windows:
                //
                var currentDirectory = new DirectoryInfo(currentPath);
                files = currentDirectory.GetFiles();
                subdirectories = includeSubdirectories ? currentDirectory.GetDirectories() : [];
            }
            catch (Exception exception)
            {
                reportInaccessibleDirectory?.Invoke(currentPath, exception);
                continue;
            }

            foreach (var file in files)
            {
                if (FileExclusions.IsAlwaysExcludedFile(file.Name))
                    continue;

                if (!includeHiddenFilesAndFolders && IsHiddenFileLeftOut(file, includeHiddenFile))
                    continue;

                yield return file.FullName;
            }

            foreach (var subdirectory in subdirectories)
            {
                if (FileExclusions.IsAlwaysExcludedFolder(subdirectory.Name))
                    continue;

                try
                {
                    if ((subdirectory.Attributes & ALWAYS_SKIPPED_DIRECTORY_ATTRIBUTES) != 0)
                        continue;

                    if (!includeHiddenFilesAndFolders && IsHidden(subdirectory))
                        continue;
                }
                catch (Exception exception)
                {
                    reportInaccessibleDirectory?.Invoke(subdirectory.FullName, exception);
                    continue;
                }

                //
                // Asked last, so that the caller hears only about folders which would have been
                // entered otherwise, e.g., to tell the user which folders a list left out:
                //
                if (includeDirectory is not null && !includeDirectory(subdirectory.FullName))
                    continue;

                pendingDirectories.Push(subdirectory.FullName);
            }
        }
    }

    /// <summary>
    /// Whether a path, e.g., one a file system watcher reports, is a hidden file or folder.
    /// </summary>
    /// <remarks>
    /// Once a file or folder is gone, only its name can tell whether it was hidden.
    /// </remarks>
    public static bool IsHiddenPath(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (HasHiddenName(name))
            return true;

        try
        {
            FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return entry.Exists && entry.Attributes.HasFlag(FileAttributes.Hidden);
        }
        catch
        {
            // The path vanished meanwhile, so its name is all there is to tell:
            return false;
        }
    }

    /// <summary>
    /// Whether a file or folder is hidden. The same rule applies to both.
    /// </summary>
    private static bool IsHidden(FileSystemInfo entry) => HasHiddenName(entry.Name) || entry.Attributes.HasFlag(FileAttributes.Hidden);

    private static bool HasHiddenName(string name) => name.StartsWith(HIDDEN_NAME_PREFIX);

    private static bool IsHiddenFileLeftOut(FileInfo file, Func<string, bool>? includeHiddenFile)
    {
        try
        {
            if (!IsHidden(file))
                return false;
        }
        catch
        {
            // The file vanished meanwhile. Whoever reads it next finds out and says so:
            return false;
        }

        return includeHiddenFile?.Invoke(file.FullName) is not true;
    }
}