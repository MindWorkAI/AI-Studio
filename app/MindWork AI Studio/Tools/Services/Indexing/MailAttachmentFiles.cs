using AIStudio.Settings;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The files an attachment is written to while the runtime reads its text.
/// </summary>
/// <remarks>
/// The runtime reads documents from disk only. An attachment therefore lands in a file of its own
/// for as long as its text is read, and is deleted right after. Their names are random: the
/// runtime logs the path it reads, and the name of an attachment may name a person.
///
/// Whatever a crash left behind is deleted when AI Studio starts again, before the first mailbox
/// is synced. That is why the files live in a directory of their own within the data directory of
/// AI Studio, rather than in the temporary directory of the OS: deleting everything in it touches
/// no file of another program.
/// </remarks>
internal static class MailAttachmentFiles
{
    private const string DIRECTORY_NAME = "mailAttachments";

    /// <summary>
    /// Makes up the path of a file for one attachment, and creates the directory it lies in.
    /// </summary>
    /// <param name="extension">The extension of the attachment, checked against the known document types beforehand, without a dot.</param>
    /// <returns>The path, where no file exists yet.</returns>
    /// <exception cref="InvalidOperationException">The data directory is not known yet.</exception>
    public static string CreatePath(string extension)
    {
        var directory = GetDirectory() ?? throw new InvalidOperationException("The data directory is not known yet.");
        Directory.CreateDirectory(directory);
        return Path.Join(directory, $"{Guid.NewGuid():N}.{extension}");
    }

    /// <summary>
    /// Deletes the file of one attachment, should it exist.
    /// </summary>
    /// <param name="path">The path from CreatePath.</param>
    /// <param name="logger">The logger, for a file which cannot be deleted. The next start deletes it.</param>
    public static void Delete(string path, ILogger logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The temporary file '{Path}' of a mail attachment could not be deleted. It is deleted on the next start.", path);
        }
    }

    /// <summary>
    /// Deletes every file an earlier session of AI Studio left behind.
    /// </summary>
    /// <remarks>
    /// Only to be called before any mailbox is synced, since it deletes the files of a running sync as well.
    /// </remarks>
    /// <param name="logger">The logger.</param>
    public static void DeleteLeftovers(ILogger logger)
    {
        if (GetDirectory() is not { } directory || !Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, true);
            logger.LogInformation("Deleted the temporary files of mail attachments an earlier session left behind.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The temporary files of mail attachments in '{Directory}' could not be deleted.", directory);
        }
    }

    private static string? GetDirectory() => string.IsNullOrWhiteSpace(SettingsManager.DataDirectory)
        ? null
        : Path.Join(SettingsManager.DataDirectory, DIRECTORY_NAME);
}