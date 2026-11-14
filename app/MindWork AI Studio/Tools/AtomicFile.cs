using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudio.Tools;

/// <summary>
/// Writes files so that readers see either the old or the new content, never a part of it.
/// </summary>
/// <remarks>
/// The new content goes into a temporary file next to the target first, which then replaces the target by a rename.
/// The temporary file has to live in the directory of the target: a rename across a file system boundary falls back
/// to copying, which is exactly what this avoids, and the temporary directory of the operating system is such another
/// file system under Flatpak. The temporary file is named after its target, followed by a GUID and ".tmp", so that
/// a crash leaves a file behind which DeleteTemporaryFiles, DataSourceTranscriptStore, and MediaSidecarTranscripts
/// recognize.<br/><br/>
/// This works only where AI Studio may create files next to the target: in its data and configuration directories,
/// or in a folder the user chose as a whole, e.g., the output folder of the Batch Processing assistant. It does not
/// work for a single file the user chose in a save or an open dialog or dropped onto AI Studio: under Flatpak, the
/// document portal shares only that one file, so nothing can be created next to it. Prepare such a file in the
/// temporary directory and write it into the chosen file with FileMode.Create, as PresentationFile and
/// PluginShareService do. Keep in mind, too, that the Flatpak sees the home directory read-only (--filesystem=home:ro).
/// <br/><br/>
/// The rename protects against a crash of AI Studio. Against a power failure, the content has to reach the disk
/// before the rename, which flushToDisk asks for. That costs time on every write, especially on macOS and on SD cards,
/// so only files which are hard to get back use it.
/// </remarks>
public static partial class AtomicFile
{
    private const int BUFFER_SIZE = 65_536;

    /// <summary>
    /// Writes a file, unless the writer decides to discard what it wrote.
    /// </summary>
    /// <param name="targetPath">The file to write. Its directory has to exist.</param>
    /// <param name="write">Writes the new content into the stream and returns whether it replaces the target.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="overwrite">Whether an existing target is replaced; otherwise, an existing target fails the write with an IOException.</param>
    /// <param name="flushToDisk">Whether the content has to reach the disk before it replaces the target.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    /// <returns>True when the new content replaced the target, false when the writer discarded it.</returns>
    public static async Task<bool> WriteOrDiscardAsync(string targetPath, Func<Stream, CancellationToken, Task<bool>> write, CancellationToken token, bool overwrite = true, bool flushToDisk = false, ILogger? logger = null)
    {
        var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            bool keep;
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous))
            {
                keep = await write(stream, token);
                if (keep && flushToDisk)
                    await FlushToDiskAsync(stream, token);
            }

            if (!keep)
            {
                TryDelete(temporaryPath, logger);
                return false;
            }

            File.Move(temporaryPath, targetPath, overwrite);
            return true;
        }
        catch
        {
            // Deleting never throws, so the original error is the one which reaches the caller:
            TryDelete(temporaryPath, logger);
            throw;
        }
    }

    /// <summary>
    /// Writes a file.
    /// </summary>
    /// <param name="targetPath">The file to write. Its directory has to exist.</param>
    /// <param name="write">Writes the new content into the stream.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="overwrite">Whether an existing target is replaced; otherwise, an existing target fails the write with an IOException.</param>
    /// <param name="flushToDisk">Whether the content has to reach the disk before it replaces the target.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    public static Task WriteAsync(string targetPath, Func<Stream, CancellationToken, Task> write, CancellationToken token, bool overwrite = true, bool flushToDisk = false, ILogger? logger = null) => WriteOrDiscardAsync(targetPath, async (stream, writeToken) =>
    {
        await write(stream, writeToken);
        return true;
    }, token, overwrite, flushToDisk, logger);

    /// <summary>
    /// Writes a text file.
    /// </summary>
    /// <remarks>
    /// The file starts with the byte order mark of the encoding, if it has one, just like with File.WriteAllTextAsync:
    /// Encoding.UTF8 writes one, new UTF8Encoding(false) does not.
    /// </remarks>
    /// <param name="targetPath">The file to write. Its directory has to exist.</param>
    /// <param name="content">The new content.</param>
    /// <param name="encoding">The encoding of the file.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="overwrite">Whether an existing target is replaced; otherwise, an existing target fails the write with an IOException.</param>
    /// <param name="flushToDisk">Whether the content has to reach the disk before it replaces the target.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    public static Task WriteTextAsync(string targetPath, string content, Encoding encoding, CancellationToken token, bool overwrite = true, bool flushToDisk = false, ILogger? logger = null) =>
        WriteAsync(targetPath, (stream, writeToken) => WriteTextAsync(stream, content, encoding, writeToken), token, overwrite, flushToDisk, logger);

    /// <summary>
    /// Writes a text into the stream of a file, for a writer passed to WriteAsync or WriteOrDiscardAsync.
    /// </summary>
    /// <remarks>
    /// The stream gets the byte order mark of the encoding, if it has one, just like a file written by File.WriteAllTextAsync.
    /// </remarks>
    /// <param name="stream">The stream of the file.</param>
    /// <param name="content">The text.</param>
    /// <param name="encoding">The encoding of the file.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task WriteTextAsync(Stream stream, string content, Encoding encoding, CancellationToken token)
    {
        await using var writer = new StreamWriter(stream, encoding, BUFFER_SIZE, leaveOpen: true);
        await writer.WriteAsync(content.AsMemory(), token);
        await writer.FlushAsync(token);
    }

    /// <summary>
    /// Writes a JSON file, as UTF-8 without a byte order mark.
    /// </summary>
    /// <remarks>
    /// The value goes into the file piece by piece, so that the complete JSON is never in memory at once. The file
    /// holds the same bytes as when the value was serialized into a string and written by File.WriteAllTextAsync.
    /// </remarks>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="targetPath">The file to write. Its directory has to exist.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="options">The JSON options.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="overwrite">Whether an existing target is replaced; otherwise, an existing target fails the write with an IOException.</param>
    /// <param name="flushToDisk">Whether the content has to reach the disk before it replaces the target.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    public static Task WriteJsonAsync<T>(string targetPath, T value, JsonSerializerOptions options, CancellationToken token, bool overwrite = true, bool flushToDisk = false, ILogger? logger = null) =>
        WriteAsync(targetPath, (stream, writeToken) => JsonSerializer.SerializeAsync(stream, value, options, writeToken), token, overwrite, flushToDisk, logger);

    /// <summary>
    /// Deletes the temporary files which a crash left behind anywhere below a directory.
    /// </summary>
    /// <remarks>
    /// Besides the temporary files of this class, it also deletes those which AI Studio named ".tmp-" followed by a
    /// GUID before, so that the leftovers of older versions disappear as well. Call it only for a directory which
    /// belongs to AI Studio alone.
    /// </remarks>
    /// <param name="rootDirectory">The directory to clean up.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    public static void DeleteTemporaryFiles(string rootDirectory, ILogger? logger = null)
    {
        foreach (var path in Directory.EnumerateFiles(rootDirectory, "*.tmp*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(path);
            if (TemporaryFileName().IsMatch(fileName) || PreviousTemporaryFileName().IsMatch(fileName))
                TryDelete(path, logger);
        }
    }

    /// <summary>
    /// Makes sure the content of a file reached the disk.
    /// </summary>
    /// <remarks>
    /// FlushAsync hands the buffered content to the operating system only; just Flush(true) waits for the disk, and
    /// that only synchronously. It runs on the thread pool, so that it does not block the caller, e.g., the renderer.
    /// </remarks>
    /// <param name="stream">The stream of the file.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task FlushToDiskAsync(FileStream stream, CancellationToken token)
    {
        await stream.FlushAsync(token);
        await Task.Run(() => stream.Flush(flushToDisk: true), token);
    }

    /// <summary>
    /// Deletes a file, without failing.
    /// </summary>
    /// <param name="path">The file to delete; a file which does not exist is fine.</param>
    /// <param name="logger">Where to report a file which cannot be removed.</param>
    private static void TryDelete(string path, ILogger? logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(exception, "Could not remove the temporary file '{TemporaryPath}'.", path);
        }
    }

    [GeneratedRegex(@"\.[0-9a-f]{32}\.tmp$")]
    private static partial Regex TemporaryFileName();

    [GeneratedRegex(@"\.tmp-[0-9a-f]{32}$")]
    private static partial Regex PreviousTemporaryFileName();
}