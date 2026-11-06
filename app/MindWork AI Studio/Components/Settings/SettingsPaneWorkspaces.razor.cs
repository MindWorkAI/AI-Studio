namespace AIStudio.Components.Settings;

/// <summary>
/// The pane of the settings window with the workspaces, see <see cref="AIStudio.Settings.SettingsPath.WORKSPACES"/>.
/// </summary>
/// <remarks>
/// Workspaces only sort the stored chats into folders. Whether chats are stored at all, and when
/// disappearing chats are deleted, is part of the pane of the chat, see <see cref="SettingsPaneChat"/>.
/// </remarks>
public partial class SettingsPaneWorkspaces : SettingsPanelBase;