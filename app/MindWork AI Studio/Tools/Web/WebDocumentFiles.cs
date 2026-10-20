using AIStudio.Settings;

namespace AIStudio.Tools.Web;

/// <summary>
/// The files a document from the web is downloaded to while the runtime reads its text.
/// </summary>
/// <remarks>
/// The runtime reads documents from disk only. A document therefore lands in a file of its own for
/// as long as its text is read, and is deleted right after, whether reading succeeded or not. The
/// download streams into the file, so the document never sits in memory as a whole.<br/><br/>
/// The names are random and carry no extension: the runtime logs the path it reads, and the name of
/// a document may say more than it should. Without an extension, the runtime decides the format by
/// the content alone, which is the one thing a server cannot get wrong.<br/><br/>
/// The files live in a directory of their own within the data directory, as the files of mail
/// attachments do, so deleting what a crash left behind touches no file of another program. That
/// happens before every download, for files older than any download and extraction may take, so a
/// read which is still running keeps its file.
/// </remarks>
/// <param name="directory">The directory the files lie in.</param>
public sealed class WebDocumentFiles(string directory)
{
    public const string DIRECTORY_NAME = "webDocuments";

    /// <summary>
    /// The age from which a file counts as left behind. Well beyond the longest download and the
    /// longest extraction together.
    /// </summary>
    private static readonly TimeSpan LEFTOVER_AGE = TimeSpan.FromHours(1);

    /// <summary>
    /// The files within the data directory of AI Studio, or null while it is not known yet.
    /// </summary>
    public static WebDocumentFiles? InDataDirectory() => string.IsNullOrWhiteSpace(SettingsManager.DataDirectory)
        ? null
        : new(Path.Join(SettingsManager.DataDirectory, DIRECTORY_NAME));

    /// <summary>
    /// Makes up the path of a file for one document, and creates the directory it lies in.
    /// </summary>
    /// <returns>The path, where no file exists yet.</returns>
    public string CreatePath()
    {
        Directory.CreateDirectory(directory);
        return Path.Join(directory, Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Deletes the file of one document, should it exist.
    /// </summary>
    /// <param name="path">The path from CreatePath.</param>
    /// <param name="logger">The logger, for a file which cannot be deleted. A later download deletes it.</param>
    public static void Delete(string path, ILogger logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The temporary file '{Path}' of a web document could not be deleted. A later download deletes it.", path);
        }
    }

    /// <summary>
    /// Deletes the files an earlier download left behind, such as after a crash.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <returns>How many files were deleted.</returns>
    public int DeleteLeftovers(ILogger logger)
    {
        if (!Directory.Exists(directory))
            return 0;

        var deleted = 0;
        var threshold = DateTime.UtcNow - LEFTOVER_AGE;
        foreach (var file in new DirectoryInfo(directory).EnumerateFiles())
        {
            if (file.LastWriteTimeUtc >= threshold)
                continue;

            try
            {
                file.Delete();
                deleted++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The temporary file '{Path}' of a web document could not be deleted.", file.FullName);
            }
        }

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} temporary files of web documents an earlier download left behind.", deleted);

        return deleted;
    }
}