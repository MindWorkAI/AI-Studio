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

    private bool IsNotExcluded(string directoryPath, Action<string>? reportExcludedFolder)
    {
        if (!FileExclusions.ExcludesFolder(directoryPath, exclusions))
            return true;

        reportExcludedFolder?.Invoke(directoryPath);
        return false;
    }
}