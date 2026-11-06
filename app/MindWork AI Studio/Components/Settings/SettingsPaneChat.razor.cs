namespace AIStudio.Components.Settings;

/// <summary>
/// The pane of the settings window with the options of all chats, see <see cref="AIStudio.Settings.SettingsPath.CHAT"/>.
/// </summary>
public partial class SettingsPaneChat : SettingsPanelBase
{
    /// <summary>
    /// What not storing chats means for the privacy of the user. Chats in workspaces may still be stored,
    /// by a choice of their own, and the note must not promise more than holds.
    /// </summary>
    private string PrivacyNote => this.SettingsManager.ConfigurationData.Workspace.CanChatsGoIntoWorkspaces
        ? T("Private: your chats outside of workspaces are never stored. Such a chat exists only while it is open, and once you start a new chat or close AI Studio, it is gone. Chats in workspaces follow their own choice in the workspace settings.")
        : T("Private: your chats are never stored. A chat exists only while it is open, and once you start a new chat or close AI Studio, it is gone.");
}