namespace AIStudio.Settings.DataModel;

/// <summary>
/// How the chats are stored.
/// </summary>
/// <remarks>
/// The names of the values are stored in the settings file, so they stay as they are, also where they
/// tell less than they could: older versions of AI Studio read the same file.
/// </remarks>
public enum WorkspaceStorageBehavior
{
    /// <summary>
    /// No chat is stored: a chat lasts only while AI Studio keeps it in memory.
    /// </summary>
    /// <remarks>
    /// The name dates from the time when this also switched workspaces off. Workspaces have a switch of
    /// their own now, see <see cref="DataWorkspace.UsesWorkspaces"/>.
    /// </remarks>
    DISABLE_WORKSPACES,

    /// <summary>
    /// Every chat is stored as it changes.
    /// </summary>
    STORE_CHATS_AUTOMATICALLY,

    /// <summary>
    /// A chat is stored only when the user saves it.
    /// </summary>
    STORE_CHATS_MANUALLY,
}