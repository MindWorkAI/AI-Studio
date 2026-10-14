using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Settings;

namespace AIStudio.Tools;

/// <summary>
/// Stores a path below the data directory relative to it, and every other path as it is.
/// </summary>
/// <remarks>
/// AI Studio keeps files of its own below the data directory: the transcripts of a chat, the copies of
/// the tokenizers, and more. A stored path to such a file outlives the computer it was written on. A
/// backup brings it to another computer, where the user name, the OS, or the data directory itself may
/// differ. Relative to the data directory, the path still names the same file there. In memory, the
/// path stays absolute, so the code which works with it never notices.<br/><br/>
///
/// A relative path is written with forward slashes, which every OS reads. A path to a file of the user
/// stays absolute: it lies outside of the data directory, and a backup does not bring that file along.
/// A path written before AI Studio stored such paths relative is absolute as well, and is read as it
/// is. While the data directory is not known yet, every path stays as it is.
/// </remarks>
public sealed class DataDirectoryRelativePathConverter : JsonConverter<string>
{
    private const char STORED_SEPARATOR = '/';

    #region Overrides of JsonConverter<string>

    /// <inheritdoc />
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ToAbsolute(reader.GetString() ?? string.Empty, SettingsManager.DataDirectory);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(ToStored(value, SettingsManager.DataDirectory));

    #endregion

    /// <summary>
    /// Turns a path into the form in which it is stored.
    /// </summary>
    /// <param name="path">The absolute path, as AI Studio works with it.</param>
    /// <param name="dataDirectory">The data directory, or null while it is not known yet.</param>
    /// <returns>The path relative to the data directory when it lies below it, otherwise the path as it is.</returns>
    internal static string ToStored(string path, string? dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(dataDirectory) || !Path.IsPathFullyQualified(path) || !PathTools.IsInsideDirectory(dataDirectory, path))
            return path;

        return Path.GetRelativePath(Path.GetFullPath(dataDirectory), Path.GetFullPath(path)).Replace(Path.DirectorySeparatorChar, STORED_SEPARATOR);
    }

    /// <summary>
    /// Turns a stored path back into the absolute path AI Studio works with.
    /// </summary>
    /// <remarks>
    /// A relative path which leads out of the data directory was not written by AI Studio. Rather than
    /// resolving it against whatever directory the process runs in, it is read as no path at all.
    /// </remarks>
    /// <param name="storedPath">The path as it was stored.</param>
    /// <param name="dataDirectory">The data directory, or null while it is not known yet.</param>
    /// <returns>The absolute path.</returns>
    internal static string ToAbsolute(string storedPath, string? dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || string.IsNullOrWhiteSpace(dataDirectory) || Path.IsPathRooted(storedPath))
            return storedPath;

        var path = Path.Join(dataDirectory, storedPath.Replace(STORED_SEPARATOR, Path.DirectorySeparatorChar));
        return PathTools.IsInsideDirectory(dataDirectory, path) ? path : string.Empty;
    }
}