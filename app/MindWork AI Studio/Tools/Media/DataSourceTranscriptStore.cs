using System.Text.RegularExpressions;

using AIStudio.Settings;

namespace AIStudio.Tools.Media;

/// <summary>
/// Keeps the transcripts of the audio and video files of one data source below the data directory.
/// </summary>
/// <remarks>
/// A data source never writes into the folders of the user: they may be read-only, a network share,
/// or the read-only home folder of the Flatpak, and a transcript which cannot be stored would be paid
/// for again with every run. The transcripts live in 'mediaTranscripts/&lt;data source id&gt;/&lt;key&gt;.md'
/// instead, where the key is the SHA-256 hash of the content of the media file. Renaming or moving a
/// recording, or a sync tool which only touches its write time, finds the transcript again. The path
/// follows from the id and the key, so nothing stores it.
/// </remarks>
/// <param name="dataDirectory">The data directory of AI Studio.</param>
/// <param name="dataSourceId">The id of the data source, a GUID.</param>
internal sealed partial class DataSourceTranscriptStore(string dataDirectory, string dataSourceId) : IMediaTranscriptLocation
{
    /// <summary>
    /// The folder below the data directory which holds the transcripts of all data sources.
    /// </summary>
    public const string FOLDER_NAME = "mediaTranscripts";

    private const string FILE_EXTENSION = ".md";

    private readonly string directoryPath = Path.Join(dataDirectory, FOLDER_NAME, NormalizeId(dataSourceId));

    /// <summary>
    /// The store of a data source in the data directory of the running app.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <returns>The store.</returns>
    public static DataSourceTranscriptStore ForDataSource(string dataSourceId) => new(SettingsManager.DataDirectory ?? throw new InvalidOperationException("The data directory is not known yet."), dataSourceId);

    /// <summary>
    /// The store of a data source in the data directory of the running app, or null when there is none to clean up.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <returns>The store, or null while the data directory is not known or for an id which is no GUID.</returns>
    public static DataSourceTranscriptStore? TryForDataSource(string dataSourceId) => string.IsNullOrWhiteSpace(SettingsManager.DataDirectory) || !Guid.TryParse(dataSourceId, out _)
        ? null
        : new(SettingsManager.DataDirectory, dataSourceId);

    /// <inheritdoc />
    /// <remarks>
    /// Reads the whole media file to hash it, in pieces. Callers ask only for files which are about
    /// to be read anyway because they are new or changed.
    /// </remarks>
    public async Task<MediaTranscriptSlot> ResolveAsync(string mediaPath, CancellationToken token)
    {
        var key = await FileContentHash.ComputeAsync(mediaPath, token);
        return new(this.GetPath(key), key);
    }

    /// <summary>
    /// Whether a text is a key of this store, i.e. a lowercase SHA-256 hash.
    /// </summary>
    /// <param name="key">The text to check.</param>
    /// <returns>True for a key.</returns>
    public static bool IsKey(string key) => KeyPattern().IsMatch(key);

    /// <summary>
    /// The path of the transcript with the given key.
    /// </summary>
    /// <param name="key">The key of the transcript.</param>
    /// <returns>The path, whether or not the transcript exists.</returns>
    public string GetPath(string key)
    {
        if (!IsKey(key))
            throw new ArgumentException("The key of a media transcript has to be a lowercase SHA-256 hash.", nameof(key));

        return Path.Join(this.directoryPath, key + FILE_EXTENSION);
    }

    /// <summary>
    /// The keys of all transcripts of this data source.
    /// </summary>
    /// <returns>The keys; empty when the data source has no transcripts.</returns>
    public IReadOnlyList<string> ListKeys()
    {
        return this.EnumerateOwnFiles()
            .Select(file => Path.GetFileName(file.FullName))
            .Where(fileName => TranscriptFileNamePattern().IsMatch(fileName))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToList();
    }

    /// <summary>
    /// Deletes the transcript with the given key.
    /// </summary>
    /// <param name="key">The key of the transcript.</param>
    /// <returns>True when a transcript was deleted.</returns>
    public bool Delete(string key)
    {
        var file = new FileInfo(this.GetPath(key));
        if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return false;

        file.Delete();
        return true;
    }

    /// <summary>
    /// Deletes the temporary files which an interrupted write left behind.
    /// </summary>
    /// <remarks>
    /// Call it only while nothing writes a transcript of this data source, e.g. at the end of a run.
    /// </remarks>
    /// <returns>The number of deleted files.</returns>
    public int DeleteTemporaryFiles()
    {
        var deleted = 0;
        foreach (var file in this.EnumerateOwnFiles().Where(file => TemporaryFileNamePattern().IsMatch(file.Name)))
        {
            file.Delete();
            deleted++;
        }

        return deleted;
    }

    /// <summary>
    /// Deletes every transcript of this data source, together with temporary files a crash left behind.
    /// </summary>
    /// <remarks>
    /// Removes only what this store wrote. Anything else in the folder stays, and the folder with it.
    /// </remarks>
    public void DeleteAll()
    {
        foreach (var file in this.EnumerateOwnFiles())
            file.Delete();

        var directory = new DirectoryInfo(this.directoryPath);
        if (directory.Exists && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint) && !directory.EnumerateFileSystemInfos().Any())
            directory.Delete();
    }

    /// <summary>
    /// Copies the transcripts of all data sources, e.g. for a backup.
    /// </summary>
    /// <remarks>
    /// Copies only transcripts, file by file and in pieces. Temporary files of an interrupted write,
    /// links, and anything else in the folders stay behind.
    /// </remarks>
    /// <param name="dataDirectory">The data directory to copy from.</param>
    /// <param name="targetDirectory">The folder which receives the transcripts below 'mediaTranscripts/', in the same layout.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The number of copied transcripts.</returns>
    public static async Task<int> CopyAllAsync(string dataDirectory, string targetDirectory, CancellationToken token)
    {
        var root = new DirectoryInfo(Path.Join(dataDirectory, FOLDER_NAME));
        if (!root.Exists || root.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return 0;

        var copied = 0;
        foreach (var dataSourceFolder in root.EnumerateDirectories("*", new EnumerationOptions { RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
        {
            if (!Guid.TryParse(dataSourceFolder.Name, out _))
                continue;

            var store = new DataSourceTranscriptStore(dataDirectory, dataSourceFolder.Name);
            foreach (var file in store.EnumerateOwnFiles().Where(file => TranscriptFileNamePattern().IsMatch(file.Name)))
            {
                token.ThrowIfCancellationRequested();
                var targetPath = Path.Join(targetDirectory, FOLDER_NAME, dataSourceFolder.Name, file.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                await using var source = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65_536, FileOptions.Asynchronous);
                await source.CopyToAsync(target, token);
                copied++;
            }
        }

        return copied;
    }

    /// <summary>
    /// The files this store wrote: transcripts and the temporary files of interrupted writes. Links are left out.
    /// </summary>
    private IEnumerable<FileInfo> EnumerateOwnFiles()
    {
        var directory = new DirectoryInfo(this.directoryPath);
        if (!directory.Exists || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return [];

        return directory
            .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true })
            .Where(file => TranscriptFileNamePattern().IsMatch(file.Name) || TemporaryFileNamePattern().IsMatch(file.Name))
            .ToList();
    }

    private static string NormalizeId(string dataSourceId) => Guid.TryParse(dataSourceId, out var id)
        ? id.ToString("D")
        : throw new ArgumentException("The id of a data source with media transcripts has to be a GUID.", nameof(dataSourceId));

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^[0-9a-f]{64}\.md$")]
    private static partial Regex TranscriptFileNamePattern();

    [GeneratedRegex(@"^[0-9a-f]{64}\.md\.[0-9a-f]{32}\.tmp$")]
    private static partial Regex TemporaryFileNamePattern();
}