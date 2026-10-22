using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// What AI Studio keeps in its data and configuration folders, and which of it belongs into a backup.
/// </summary>
/// <remarks>
/// This is a positive list. A backup takes along only the entries marked as included, and restoring
/// one replaces only those. Everything else in these folders stays out of a backup and survives a
/// restore: the leftovers of a plugin installation, a file somebody else put there, or a folder a
/// future version adds before anybody listed it here.<br/><br/>
///
/// The entries which stay out are listed, too. The folder panel on the information page tells users
/// who back up these folders with a tool of their own what they may leave out, and nobody has to guess
/// whether a folder was forgotten or left out on purpose. Whoever adds a file or folder to the data or
/// configuration folder adds it here, one way or the other.<br/><br/>
///
/// The runtime, which applies a restore, keeps its own list of the included entries and refuses to
/// replace anything else (REPLACEABLE_ELEMENTS in runtime/src/data_restore.rs). An entry included here
/// goes there as well.
/// </remarks>
public static class DataBackupContents
{
    /// <summary>
    /// The separator of the relative paths, the same on every OS.
    /// </summary>
    public const char SEPARATOR = '/';

    /// <summary>
    /// The folder of the plugins, within the data folder.
    /// </summary>
    public const string PLUGINS_FOLDER = "plugins/";

    /// <summary>
    /// Every file and folder AI Studio keeps directly in its data or configuration folder, and those
    /// below the folders of the plugins and the databases which need a decision of their own.
    /// </summary>
    public static readonly IReadOnlyList<DataBackupEntry> ENTRIES =
    [
        new(DataBackupLocation.CONFIG_DIRECTORY, "settings.json", DataBackupEntryKind.SETTINGS),
        new(DataBackupLocation.DATA_DIRECTORY, "workspaces/", DataBackupEntryKind.WORKSPACES),
        new(DataBackupLocation.DATA_DIRECTORY, "tempChats/", DataBackupEntryKind.TEMPORARY_CHATS),
        new(DataBackupLocation.DATA_DIRECTORY, PLUGINS_FOLDER, DataBackupEntryKind.PLUGINS),
        new(DataBackupLocation.DATA_DIRECTORY, "tokenizers/", DataBackupEntryKind.TOKENIZERS),
        new(DataBackupLocation.DATA_DIRECTORY, "visualBriefings/", DataBackupEntryKind.VISUAL_BRIEFINGS),
        new(DataBackupLocation.DATA_DIRECTORY, "databases/sqlite/", DataBackupEntryKind.DATA_SOURCE_INDEX),
        new(DataBackupLocation.DATA_DIRECTORY, "databases/vector_database/stores/", DataBackupEntryKind.VECTOR_STORES),
        new(DataBackupLocation.DATA_DIRECTORY, "mediaTranscripts/", DataBackupEntryKind.MEDIA_TRANSCRIPTS),

        new(DataBackupLocation.CONFIG_DIRECTORY, "settings.v*.json", DataBackupEntryKind.SETTINGS_OF_OTHER_VERSIONS),
        new(DataBackupLocation.DATA_DIRECTORY, "plugins/.internal/", DataBackupEntryKind.BUILT_IN_PLUGINS),
        new(DataBackupLocation.DATA_DIRECTORY, "plugins/.config/", DataBackupEntryKind.ORGANIZATION_PLUGINS),
        new(DataBackupLocation.DATA_DIRECTORY, "plugins/.config-tests/", DataBackupEntryKind.ORGANIZATION_PLUGINS),
        new(DataBackupLocation.DATA_DIRECTORY, "plugins/.lock", DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, ".plugin-staging/", DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, ".plugin-install-backups/", DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, ".plugin-delete-backups/", DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, "media-staging/", DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, "mailAttachments/", DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, "mindwork_ai_studio.pid", DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, DataBackupFormat.STAGING_DIRECTORY, DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, DataRestoreFormat.PENDING_DIRECTORY, DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.CONFIG_DIRECTORY, DataRestoreFormat.PENDING_DIRECTORY, DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, DataRestoreFormat.PREVIOUS_DIRECTORY, DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.CONFIG_DIRECTORY, DataRestoreFormat.PREVIOUS_DIRECTORY, DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, DataRestoreFormat.RESULT_FILE_NAME, DataBackupEntryKind.TEMPORARY_FILES),
        new(DataBackupLocation.DATA_DIRECTORY, "pandoc/", DataBackupEntryKind.DOWNLOADED_TOOLS),
        new(DataBackupLocation.DATA_DIRECTORY, "data-protection-keys/", DataBackupEntryKind.INTERFACE_KEYS),
        new(DataBackupLocation.DATA_DIRECTORY, "events.log", DataBackupEntryKind.LOGS),

        // The runtime hides the log of the startup on Linux and macOS:
        new(DataBackupLocation.DATA_DIRECTORY, OperatingSystem.IsWindows() ? "AI Studio Events.log" : ".AI Studio Events.log", DataBackupEntryKind.LOGS),
    ];

    /// <summary>
    /// Whether a path lies within an entry: below it for a folder, or the entry itself for a file.
    /// </summary>
    /// <param name="entryPath">The path of the entry, as the list holds it.</param>
    /// <param name="relativePath">The path to check, relative to the same folder and with forward slashes.</param>
    /// <returns>True when the entry covers the path.</returns>
    public static bool Covers(string entryPath, string relativePath) => entryPath.EndsWith(SEPARATOR)
        ? relativePath.StartsWith(entryPath, PathTools.COMPARISON)
        : relativePath.Equals(entryPath, PathTools.COMPARISON);

    /// <summary>
    /// Finds the plugins which the organization deployed into the folder of the user's plugins.
    /// </summary>
    /// <remarks>
    /// The config server keeps the plugins of an organization in folders of their own, which the list
    /// leaves out as a whole. A device management may put them anywhere, though, so their path proves
    /// nothing: such a plugin declares itself as deployed by the organization instead. A backup leaves
    /// it out, because the organization brings it back, and a user with a backup tool of their own
    /// needs to know which folders these are.
    /// </remarks>
    /// <param name="plugins">The plugins AI Studio found.</param>
    /// <param name="dataDirectory">The data folder.</param>
    /// <returns>The folders of these plugins within the data folder, in the form of the list, ordered.</returns>
    public static IReadOnlyList<string> FindOrganizationPluginsAmongUserPlugins(IEnumerable<IAvailablePlugin> plugins, string dataDirectory)
    {
        var excludedPluginFolders = ENTRIES
            .Where(entry => !entry.IsIncluded && entry.IsFolder && entry.Location is DataBackupLocation.DATA_DIRECTORY && entry.RelativePath.StartsWith(PLUGINS_FOLDER, StringComparison.Ordinal))
            .Select(entry => entry.RelativePath)
            .ToList();

        //
        // Relative to the data folder and with forward slashes, the plugin folders take the form of
        // the list. That is the form in which AI Studio stores such paths, too:
        //
        return plugins
            .Where(plugin => plugin.IsManagedByConfigServer)
            .Select(plugin => DataDirectoryRelativePathConverter.ToStored(plugin.LocalPath, dataDirectory).TrimEnd(SEPARATOR) + SEPARATOR)
            .Where(folder => folder.Length > PLUGINS_FOLDER.Length
                             && folder.StartsWith(PLUGINS_FOLDER, PathTools.COMPARISON)
                             && !excludedPluginFolders.Any(excluded => folder.StartsWith(excluded, PathTools.COMPARISON)))
            .Distinct(PathTools.COMPARER)
            .Order(PathTools.COMPARER)
            .ToList();
    }
}