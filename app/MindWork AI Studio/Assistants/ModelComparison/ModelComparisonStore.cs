using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using AIStudio.Settings;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Stores the files of the Model Comparison assistant below the data directory.
/// </summary>
/// <remarks>
/// The store keeps no paths: it derives every path from IDs, so a backup works on another computer as well. It writes
/// each file atomically, guards each file with a lock of its own, and never deletes anything on its own; only the user
/// deletes. A file this version cannot read is left alone rather than overwritten or removed.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="storageOptions">Overrides the data directory, for tests.</param>
public sealed partial class ModelComparisonStore(ILogger<ModelComparisonStore> logger, ModelComparisonStorageOptions? storageOptions = null)
{
    /// <summary>
    /// The name of the folder below the data directory.
    /// </summary>
    public const string ROOT_DIRECTORY_NAME = "modelComparisons";

    /// <summary>
    /// The JSON options of all files of the assistant.
    /// </summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = ModelComparisonJson.PERSISTENCE;

    /// <summary>
    /// The lock of each file, by the ID which names it.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> fileLocks = [];

    /// <summary>
    /// Serializes the first start of the store.
    /// </summary>
    private readonly SemaphoreSlim initializationLock = new(1, 1);

    /// <summary>
    /// Whether the store cleaned up already.
    /// </summary>
    private bool initialized;

    /// <summary>
    /// Gets the folder of the assistant below the data directory.
    /// </summary>
    private string RootDirectory => Path.Combine(
        storageOptions?.DataDirectory ??
        SettingsManager.DataDirectory ??
        throw new InvalidOperationException("The AI Studio data directory is not initialized."),
        ROOT_DIRECTORY_NAME);

    /// <summary>
    /// Reads a file of the store.
    /// </summary>
    /// <typeparam name="T">The type of the file.</typeparam>
    /// <param name="path">The file.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What the file holds, or null when it does not exist or this version cannot read it.</returns>
    private async Task<T?> ReadJsonAsync<T>(string path, CancellationToken token) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            return await JsonSerializer.DeserializeAsync<T>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(exception, "Could not read the file '{Path}' of the Model Comparison assistant.", path);
            return null;
        }
    }

    /// <summary>
    /// Writes a file of the store so that readers see either the old or the new content, never a part of it.
    /// </summary>
    /// <typeparam name="T">The type of the file.</typeparam>
    /// <param name="targetPath">The file to write.</param>
    /// <param name="value">The new content.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task WriteJsonAtomicAsync<T>(string targetPath, T value, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = $"{targetPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(value, JSON_OPTIONS), new UTF8Encoding(false), token);
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
    /// Gets the lock of a file.
    /// </summary>
    /// <param name="id">The ID which names the file.</param>
    private SemaphoreSlim GetLock(Guid id) => this.fileLocks.GetOrAdd(id, _ => new(1, 1));

    /// <summary>
    /// Drops the lock of a file which does not exist anymore.
    /// </summary>
    /// <remarks>
    /// The semaphore is not disposed: another operation might still wait on it.
    /// </remarks>
    /// <param name="id">The ID which named the file.</param>
    private void ForgetLock(Guid id) => this.fileLocks.TryRemove(id, out _);

    /// <summary>
    /// Creates the folder of the assistant and removes temporary files a crash left behind.
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

            this.initialized = true;
        }
        finally
        {
            this.initializationLock.Release();
        }
    }
}