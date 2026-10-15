using System.Text.Json;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// The format of a backup: a ZIP file which holds the data folder below DATA_ROOT and the
/// configuration folder below CONFIG_ROOT, each in the layout AI Studio keeps on the disk, and the
/// manifest as its last entry.
/// </summary>
/// <remarks>
/// What goes in follows DataBackupContents. The paths below the roots are the relative paths of
/// that list, so restoring a backup is unpacking it. The manifest comes last: an archive which lacks
/// it was not written completely.
/// </remarks>
public static class DataBackupFormat
{
    /// <summary>
    /// The version of the format this version of AI Studio writes. A restore refuses a backup of a
    /// later version, since it cannot know what such a backup holds.
    /// </summary>
    public const int FORMAT_VERSION = 1;

    /// <summary>
    /// The file extension of a backup, named like the one of plugin archives.
    /// </summary>
    public const string FILE_EXTENSION = ".mwbackup";

    /// <summary>
    /// The manifest, the last entry of every backup.
    /// </summary>
    public const string MANIFEST_ENTRY_NAME = "manifest.json";

    /// <summary>
    /// Where the data folder lies within a backup.
    /// </summary>
    public const string DATA_ROOT = "data/";

    /// <summary>
    /// Where the configuration folder lies within a backup.
    /// </summary>
    public const string CONFIG_ROOT = "config/";

    /// <summary>
    /// How the manifest is written: readable for anybody who opens the backup.
    /// </summary>
    public static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// File types which are compressed already. Compressing them once more costs time and makes the
    /// backup no smaller.
    /// </summary>
    public static readonly IReadOnlySet<string> ALREADY_COMPRESSED_EXTENSIONS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".tiff",
        ".mp3", ".m4a", ".m4b", ".aac", ".ogg", ".opus", ".flac", ".wma",
        ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".flv",
        ".zip", ".7z", ".gz", ".bz2", ".xz", ".rar",
        ".pdf", ".docx", ".xlsx", ".pptx", ".odt", ".odp",
        ".mwplugin", FILE_EXTENSION,
    };

    /// <summary>
    /// The name of the entry which holds a file of the data or configuration folder.
    /// </summary>
    /// <param name="location">The folder of the file.</param>
    /// <param name="relativePath">The path of the file within its folder, with forward slashes.</param>
    /// <returns>The name of the entry.</returns>
    public static string EntryName(DataBackupLocation location, string relativePath) => location switch
    {
        DataBackupLocation.CONFIG_DIRECTORY => CONFIG_ROOT + relativePath,
        _ => DATA_ROOT + relativePath,
    };
}