using System.Text.Json;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// What remains to do after the runtime applied a restore, or after a backup or the preparation of a
/// restore stopped halfway: read the report of the runtime, and remove what nobody needs any longer.
/// </summary>
/// <remarks>
/// Nothing here touches a restore which the runtime has not finished, cf. IsRestoreUnfinished: its
/// journal and the data it replaced are the only way back, and the runtime continues at every start.
/// </remarks>
public static class DataRestoreFollowUp
{
    /// <summary>
    /// Whether a restore is prepared completely and waits for the next start.
    /// </summary>
    /// <param name="dataDirectory">The data folder.</param>
    /// <returns>True when the next start applies a restore.</returns>
    public static bool IsRestorePending(string dataDirectory) => IsMarkedReady(dataDirectory) && !HasJournal(dataDirectory);

    /// <summary>
    /// Whether the runtime has not finished a restore: it failed to move a failed restore back, and
    /// tries again at the next start.
    /// </summary>
    /// <param name="dataDirectory">The data folder.</param>
    /// <returns>True while the runtime still needs the pending and the previous directories.</returns>
    public static bool IsRestoreUnfinished(string dataDirectory) => IsMarkedReady(dataDirectory) && HasJournal(dataDirectory);

    /// <summary>
    /// Reads how the runtime applied a restore at this start. Throws a JsonException when the report
    /// cannot be read.
    /// </summary>
    /// <param name="dataDirectory">The data folder.</param>
    /// <param name="token">Cancels reading.</param>
    /// <returns>The report, or null when the runtime applied no restore.</returns>
    public static async Task<DataRestoreReport?> ReadReportAsync(string dataDirectory, CancellationToken token)
    {
        var reportPath = Path.Join(dataDirectory, DataRestoreFormat.RESULT_FILE_NAME);
        if (!File.Exists(reportPath))
            return null;

        await using var reportStream = File.OpenRead(reportPath);
        return await JsonSerializer.DeserializeAsync<DataRestoreReport>(reportStream, DataBackupFormat.JSON_OPTIONS, token);
    }

    /// <summary>
    /// Removes the report of the runtime, once the user saw it.
    /// </summary>
    /// <param name="dataDirectory">The data folder.</param>
    public static void RemoveReport(string dataDirectory) => File.Delete(Path.Join(dataDirectory, DataRestoreFormat.RESULT_FILE_NAME));

    /// <summary>
    /// Removes what a backup or the preparation of a restore left behind when it stopped halfway, e.g.,
    /// because AI Studio crashed. Call it only while neither runs.
    /// </summary>
    /// <remarks>
    /// A pending directory with READY_FILE_NAME stays: the restore waits for the next start, or the
    /// runtime has not finished it.
    /// </remarks>
    /// <param name="dataDirectory">The data folder.</param>
    /// <param name="configDirectory">The configuration folder.</param>
    public static void RemoveLeftovers(string dataDirectory, string configDirectory)
    {
        DeleteDirectory(DataRestoreFormat.DirectoryIn(dataDirectory, DataBackupFormat.STAGING_DIRECTORY));
        if (IsMarkedReady(dataDirectory))
            return;

        DeleteDirectory(DataRestoreFormat.DirectoryIn(dataDirectory, DataRestoreFormat.PENDING_DIRECTORY));
        DeleteDirectory(DataRestoreFormat.DirectoryIn(configDirectory, DataRestoreFormat.PENDING_DIRECTORY));
    }

    /// <summary>
    /// Removes the data which a restore replaced. Call it once the restored settings loaded: until
    /// then, these data are the way back.
    /// </summary>
    /// <param name="dataDirectory">The data folder.</param>
    /// <param name="configDirectory">The configuration folder.</param>
    /// <returns>Whether there were such data.</returns>
    public static bool RemoveReplacedData(string dataDirectory, string configDirectory)
    {
        if (IsMarkedReady(dataDirectory))
            return false;

        var removedData = DeleteDirectory(DataRestoreFormat.DirectoryIn(dataDirectory, DataRestoreFormat.PREVIOUS_DIRECTORY));
        var removedConfiguration = DeleteDirectory(DataRestoreFormat.DirectoryIn(configDirectory, DataRestoreFormat.PREVIOUS_DIRECTORY));
        return removedData || removedConfiguration;
    }

    private static bool IsMarkedReady(string dataDirectory) => File.Exists(Path.Join(DataRestoreFormat.DirectoryIn(dataDirectory, DataRestoreFormat.PENDING_DIRECTORY), DataRestoreFormat.READY_FILE_NAME));

    private static bool HasJournal(string dataDirectory) => File.Exists(Path.Join(DataRestoreFormat.DirectoryIn(dataDirectory, DataRestoreFormat.PENDING_DIRECTORY), DataRestoreFormat.JOURNAL_FILE_NAME));

    /// <summary>
    /// Deletes a directory with everything in it.
    /// </summary>
    /// <returns>Whether the directory existed.</returns>
    private static bool DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return false;

        Directory.Delete(path, true);
        return true;
    }
}