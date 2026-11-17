using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using AIStudio.Settings;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Stores the preparations of the BeyondFacts Lab as files below the data directory.
/// </summary>
/// <remarks>
/// Each preparation lives in a folder named by its ID; the conversation about whether the lab fits a task, which
/// belongs to no preparation, lives in a folder of its own. The store keeps no paths: it derives every path from
/// IDs, so a backup works on another computer as well. It writes each file atomically, guards each preparation
/// with a lock of its own, and never deletes anything on its own; only the person deletes a preparation.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="storageOptions">Overrides the data directory, for tests.</param>
public sealed partial class BeyondFactsLabStore(ILogger<BeyondFactsLabStore> logger, BflStorageOptions? storageOptions = null)
{
    /// <summary>
    /// The name of the folder below the data directory.
    /// </summary>
    public const string ROOT_DIRECTORY_NAME = "beyondFactsLab";

    /// <summary>
    /// The longest name of a preparation the store keeps.
    /// </summary>
    public const int MAX_NAME_LENGTH = 200;

    /// <summary>
    /// The file of a preparation which holds its name and dates.
    /// </summary>
    private const string PROJECT_FILE_NAME = "project.json";

    /// <summary>
    /// The file which holds the preparation selected last.
    /// </summary>
    private const string SELECTION_FILE_NAME = "selection.json";

    /// <summary>
    /// The JSON options of all files of the lab.
    /// </summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = BeyondFactsLabJson.PERSISTENCE;

    /// <summary>
    /// The lock of each preparation.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> projectLocks = [];

    /// <summary>
    /// Serializes the first start of the store.
    /// </summary>
    private readonly SemaphoreSlim initializationLock = new(1, 1);

    /// <summary>
    /// Serializes writing the selection.
    /// </summary>
    private readonly SemaphoreSlim selectionLock = new(1, 1);

    /// <summary>
    /// Whether the store cleaned up and read the selection already.
    /// </summary>
    private bool initialized;

    /// <summary>
    /// Gets the folder of the lab below the data directory.
    /// </summary>
    private string RootDirectory => Path.Combine(
        storageOptions?.DataDirectory ??
        SettingsManager.DataDirectory ??
        throw new InvalidOperationException("The AI Studio data directory is not initialized."),
        ROOT_DIRECTORY_NAME);

    /// <summary>
    /// Gets the folder whose files replace the prompts of the lab while the person allows it, see BflPromptOverrides.
    /// </summary>
    public string PromptOverridesDirectory => Path.Combine(this.RootDirectory, BflPromptOverrides.DIRECTORY_NAME);

    /// <summary>
    /// Writes a text file so that readers see either the old or the new content, never a part of it.
    /// </summary>
    /// <param name="targetPath">The file to write.</param>
    /// <param name="content">The new content.</param>
    /// <param name="token">The cancellation token.</param>
    private static Task WriteTextAtomicAsync(string targetPath, string content, CancellationToken token) => WriteAtomicAsync(targetPath, temporaryPath => File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false), token), token);

    /// <summary>
    /// Writes a binary file so that readers see either the old or the new content, never a part of it.
    /// </summary>
    /// <param name="targetPath">The file to write.</param>
    /// <param name="content">The new content.</param>
    /// <param name="token">The cancellation token.</param>
    private static Task WriteBytesAtomicAsync(string targetPath, ReadOnlyMemory<byte> content, CancellationToken token) => WriteAtomicAsync(targetPath, temporaryPath => File.WriteAllBytesAsync(temporaryPath, content, token), token);

    /// <summary>
    /// Writes a file beside its target first and moves it there once it is complete.
    /// </summary>
    /// <param name="targetPath">The file to write.</param>
    /// <param name="writeTemporaryFile">Writes the new content into the file of the path given.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task WriteAtomicAsync(string targetPath, Func<string, Task> writeTemporaryFile, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = $"{targetPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await writeTemporaryFile(temporaryPath);
            await using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4_096, true))
                await stream.FlushAsync(token);

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>
    /// Deletes a file when it exists, without failing.
    /// </summary>
    /// <param name="path">The file to delete.</param>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleaning up a temporary file is best effort; the next start tries again.
        }
    }

    /// <summary>
    /// Removes control characters from a name, trims it, and cuts it to the longest name the store keeps.
    /// </summary>
    /// <param name="name">The name as entered or as read from a file.</param>
    /// <returns>The cleaned name, empty when nothing remains.</returns>
    internal static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var sanitized = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return sanitized.Length <= MAX_NAME_LENGTH ? sanitized : sanitized[..MAX_NAME_LENGTH].TrimEnd();
    }

    /// <summary>
    /// Gets the lock of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private SemaphoreSlim GetLock(Guid projectId) => this.projectLocks.GetOrAdd(projectId, _ => new(1, 1));

    /// <summary>
    /// Drops the lock of a preparation which does not exist anymore.
    /// </summary>
    /// <remarks>
    /// The semaphore is not disposed: another operation might still wait on it.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    private void ForgetLock(Guid projectId) => this.projectLocks.TryRemove(projectId, out _);

    /// <summary>
    /// Gets the folder of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private string ProjectDirectory(Guid projectId) => Path.Combine(this.RootDirectory, projectId.ToString("D"));

    /// <summary>
    /// Gets the file which holds the name and the dates of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private string ProjectPath(Guid projectId) => Path.Combine(this.ProjectDirectory(projectId), PROJECT_FILE_NAME);

    /// <summary>
    /// Gets the file which holds the preparation selected last.
    /// </summary>
    private string SelectionPath() => Path.Combine(this.RootDirectory, SELECTION_FILE_NAME);

    /// <summary>
    /// Creates the folder of the lab, removes temporary files a crash left behind, and reads the selection.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    private async Task InitializeAsync(CancellationToken token)
    {
        if (this.initialized)
            return;

        await this.initializationLock.WaitAsync(token);
        try
        {
            if (this.initialized)
                return;

            Directory.CreateDirectory(this.RootDirectory);
            foreach (var temporaryPath in Directory.EnumerateFiles(this.RootDirectory, "*.tmp-*", SearchOption.AllDirectories))
                TryDeleteFile(temporaryPath);

            await this.LoadSelectionAsync(token);
            this.initialized = true;
        }
        finally
        {
            this.initializationLock.Release();
        }
    }
}