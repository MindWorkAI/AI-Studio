namespace AIStudio.Settings.DataModel;

/// <summary>
/// How the list of workspaces and chats shows beside the chat.
/// </summary>
public enum WorkspaceDisplayBehavior
{
    // Toggling the sidebar is deliberately the member with the underlying value 0: when the settings
    // file holds a value TolerantEnumConverter cannot read, it falls back to that member. Settings
    // stored before the option "Toggle the overlay" was removed hold TOGGLE_OVERLAY, which therefore
    // becomes the sidebar the toggle of the chat page shows and hides, as the overlay was.

    TOGGLE_SIDEBAR,
    SIDEBAR_ALWAYS_VISIBLE,
}