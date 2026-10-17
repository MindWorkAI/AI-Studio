using AIStudio.Settings.DataModel;

namespace AIStudio.Tools;

/// <summary>
/// Which files below a folder a folder-based feature works with.
/// </summary>
/// <remarks>
/// The file patterns select files, while the exclusion list and the rule for hidden files and
/// folders leave some out again, cf. FileNamePatterns, FileExclusions, and DirectoryFileTraversal.
/// Data sources, their information dialog, and the batch processing assistant all decide through
/// this class, so the files the dialog counts are the files which get indexed.
/// </remarks>
/// <param name="filePatterns">The parsed file patterns.</param>
/// <param name="exclusions">The parsed list of excluded files and folders.</param>
/// <param name="includeSubdirectories">Whether files in subfolders are selected as well.</param>
/// <param name="includeHiddenFilesAndFolders">Whether hidden files and the files in hidden folders are selected as well.</param>
/// <param name="maxFileSizeMegabytes">The largest file the feature works with, in megabytes, cf. IsTooLarge.</param>
internal sealed class FolderFileSelection(IReadOnlyList<string> filePatterns, IReadOnlyList<string> exclusions, bool includeSubdirectories, bool includeHiddenFilesAndFolders, int maxFileSizeMegabytes)
{
    /// <summary>
    /// The size limit of files unless the user or the organization chooses another one.
    /// </summary>
    /// <remarks>
    /// A safety net, not a brake on costs: large scanned PDFs, often 100 to 500 MB, still pass. It
    /// catches the images of virtual machines, ISO files, database dumps, and huge logs, which end
    /// up in a selection by * or by an unknown extension. There is no value for "no limit"; the
    /// limit can be raised instead.
    /// </remarks>
    public const int DEFAULT_MAX_FILE_SIZE_MEGABYTES = 1024;

    /// <summary>
    /// The smallest size limit which can be set.
    /// </summary>
    public const int SMALLEST_MAX_FILE_SIZE_MEGABYTES = 1;

    private const long BYTES_PER_MEGABYTE = 1024 * 1024;

    /// <summary>
    /// Every file below a folder, hidden ones included, whatever its size. Only what nobody ever
    /// wants is left out.
    /// </summary>
    public static readonly FolderFileSelection EVERY_FILE = new(["*"], [], true, true, int.MaxValue);

    /// <summary>
    /// The files a local folder data source indexes.
    /// </summary>
    public static FolderFileSelection Of(DataSourceLocalDirectory dataSource) => new(
        FileNamePatterns.Parse(dataSource.EffectiveFilePatterns),
        FileExclusions.Parse(dataSource.EffectiveExcludePatterns),
        dataSource.IncludeSubdirectories,
        dataSource.IncludeHiddenFilesAndFolders,
        dataSource.EffectiveMaxFileSizeMegabytes);

    /// <summary>
    /// The size limit in bytes.
    /// </summary>
    public long MaxFileSizeBytes => MegabytesToBytes(maxFileSizeMegabytes);

    public static long MegabytesToBytes(int megabytes) => megabytes * BYTES_PER_MEGABYTE;

    /// <summary>
    /// Enumerates the files below a folder which are neither hidden nor in an excluded or a hidden
    /// folder. Whether one of them is selected tells Selects.
    /// </summary>
    /// <param name="rootPath">The folder.</param>
    /// <param name="reportExcludedFolder">Told about every folder the exclusion list leaves out.</param>
    /// <param name="includeDirectory">Whether the caller wants a folder entered, e.g., not the output folder of a batch run.</param>
    /// <param name="reportInaccessibleDirectory">Told about every folder which could not be read.</param>
    /// <returns>The full paths of the files.</returns>
    public IEnumerable<string> EnumerateCandidates(
        string rootPath,
        Action<string>? reportExcludedFolder = null,
        Func<string, bool>? includeDirectory = null,
        Action<string, Exception>? reportInaccessibleDirectory = null) => DirectoryFileTraversal.EnumerateFiles(
        rootPath,
        includeSubdirectories,
        includeHiddenFilesAndFolders,
        includeHiddenFile: filePath => FileNamePatterns.NamesHiddenFile(filePath, filePatterns),
        includeDirectory: directoryPath => this.IsNotExcluded(directoryPath, reportExcludedFolder) && includeDirectory?.Invoke(directoryPath) is not false,
        reportInaccessibleDirectory: reportInaccessibleDirectory);

    /// <summary>
    /// Whether a file which EnumerateCandidates found is selected: the exclusion list does not name
    /// it, and one of the file patterns does. Exclusion comes first.
    /// </summary>
    public bool Selects(string filePath) => !FileExclusions.ExcludesFile(filePath, exclusions) && FileNamePatterns.Matches(filePath, filePatterns);

    /// <summary>
    /// Whether a selected file is larger than the limit. A file of exactly the limit is not.
    /// </summary>
    /// <remarks>
    /// Kept apart from Selects on purpose: a file the user selected but which is too large is no
    /// intentional exclusion, so the caller tells the user about it.
    /// </remarks>
    public bool IsTooLarge(FileInfo file) => file.Length > this.MaxFileSizeBytes;

    /// <summary>
    /// Whether a path below the folder lies where EnumerateCandidates never looks: in a hidden or
    /// an excluded folder, or as a hidden or an excluded file itself.
    /// </summary>
    /// <remarks>
    /// Meant for the file system watcher. It judges each folder between the root and the path by
    /// its name, so it also works for a path which is gone, e.g., after a rename. Whether the
    /// operating system hid something can be told only while it exists. A path which is gone can
    /// have been a file or a folder, so it is left out only when it would be either way.
    /// </remarks>
    /// <param name="rootPath">The folder.</param>
    /// <param name="path">The path below it.</param>
    public bool LeavesOut(string rootPath, string path)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);
        //
        // The folder itself, or a path outside of it, is nothing this selection has a say about:
        //
        if (relativePath is "." or ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
            return false;

        var names = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (!includeSubdirectories && names.Length > 1)
            return true;

        var folderPath = rootPath;
        foreach (var folderName in names[..^1])
        {
            folderPath = Path.Combine(folderPath, folderName);
            if (this.LeavesOutFolder(folderPath))
                return true;
        }

        if (Directory.Exists(path))
            return this.LeavesOutFolder(path);

        if (File.Exists(path))
            return this.LeavesOutFile(path);

        return this.LeavesOutFolder(path) && this.LeavesOutFile(path);
    }

    /// <summary>
    /// Whether a file which is gone now could have been selected.
    /// </summary>
    /// <remarks>
    /// A selector reads the content of a file, which is gone. So any file which a selector may
    /// have taken counts, i.e., every one but media, which never matches a selector.
    /// </remarks>
    public bool CouldHaveSelected(string filePath) => this.Selects(filePath)
                                                     || (filePatterns.Any(FileNamePatterns.IsSelector)
                                                         && !FileExclusions.ExcludesFile(filePath, exclusions)
                                                         && !FileNamePatterns.IsMediaFile(filePath));

    private bool LeavesOutFolder(string folderPath) => FileExclusions.ExcludesFolder(folderPath, exclusions)
                                                       || (!includeHiddenFilesAndFolders && DirectoryFileTraversal.IsHiddenPath(folderPath));

    private bool LeavesOutFile(string filePath) => FileExclusions.ExcludesFile(filePath, exclusions)
                                                   || (!includeHiddenFilesAndFolders && DirectoryFileTraversal.IsHiddenPath(filePath) && !FileNamePatterns.NamesHiddenFile(filePath, filePatterns));

    private bool IsNotExcluded(string directoryPath, Action<string>? reportExcludedFolder)
    {
        if (!FileExclusions.ExcludesFolder(directoryPath, exclusions))
            return true;

        reportExcludedFolder?.Invoke(directoryPath);
        return false;
    }
}