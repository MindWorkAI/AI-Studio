namespace AIStudio.Settings.DataModel;

/// <summary>
/// Whether and how long chats are kept, as the settings offer it.
/// </summary>
/// <remarks>
/// Not stored itself: each choice stands for a storage behavior and a deletion policy, which the
/// settings file holds, see <see cref="DataWorkspace.GetChoice"/>.
/// </remarks>
public enum ChatStorageChoice
{
    /// <summary>
    /// Only for chats in workspaces: whatever all other chats get right now.
    /// </summary>
    SAME_AS_ALL_CHATS,

    /// <summary>
    /// No chat is stored.
    /// </summary>
    DO_NOT_STORE,

    /// <summary>
    /// A chat is stored when the user saves it, and kept until the user deletes it.
    /// </summary>
    STORE_ON_SAVE,

    /// <summary>
    /// Every chat is stored as it changes, and deleted some time after its last change.
    /// </summary>
    STORE_TEMPORARILY,

    /// <summary>
    /// Every chat is stored as it changes, and kept until the user deletes it.
    /// </summary>
    STORE_INDEFINITELY,
}