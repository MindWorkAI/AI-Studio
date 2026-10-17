namespace AIStudio.Tools.DataBackup;

/// <summary>
/// What an entry of the backup contents holds, and with it, whether it belongs into a backup.
/// </summary>
public enum DataBackupEntryKind
{
    /// <summary>
    /// The settings. A backup leaves out what a configuration plugin of the organization manages.
    /// </summary>
    SETTINGS,

    /// <summary>
    /// The chats of the workspaces, with their transcripts.
    /// </summary>
    WORKSPACES,

    /// <summary>
    /// The temporary chats, with their transcripts.
    /// </summary>
    TEMPORARY_CHATS,

    /// <summary>
    /// The plugins of the user. A backup leaves out the plugins of the organization.
    /// </summary>
    PLUGINS,

    /// <summary>
    /// The copies of the tokenizers the user chose for a provider.
    /// </summary>
    TOKENIZERS,

    /// <summary>
    /// The projects of the Visual Briefing Assistant.
    /// </summary>
    VISUAL_BRIEFINGS,

    /// <summary>
    /// The SQLite index of the data sources: which files are embedded, and their text chunks.
    /// </summary>
    DATA_SOURCE_INDEX,

    /// <summary>
    /// The vector stores, which hold the embeddings of the data sources.
    /// </summary>
    VECTOR_STORES,

    /// <summary>
    /// The copies of the settings which every version of AI Studio keeps for itself.
    /// </summary>
    SETTINGS_OF_OTHER_VERSIONS,

    /// <summary>
    /// The plugins which come with AI Studio, such as the language packs.
    /// </summary>
    BUILT_IN_PLUGINS,

    /// <summary>
    /// The plugins which the organization deploys, or tries out for one session.
    /// </summary>
    ORGANIZATION_PLUGINS,

    /// <summary>
    /// Files AI Studio needs only for a moment, while it works on something, and removes again.
    /// </summary>
    TEMPORARY_FILES,

    /// <summary>
    /// Tools AI Studio downloaded, which depend on the OS, such as Pandoc.
    /// </summary>
    DOWNLOADED_TOOLS,

    /// <summary>
    /// The keys with which the user interface protects its data. Only the Flatpak keeps them in the data
    /// folder, since the home folder is read-only there, and AI Studio creates new ones when they are missing.
    /// </summary>
    INTERFACE_KEYS,

    /// <summary>
    /// The log files.
    /// </summary>
    LOGS,
}