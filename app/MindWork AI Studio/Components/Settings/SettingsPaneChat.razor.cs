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
        ? T("Private: chats outside of workspaces are never stored, and each one is deleted once another chat is started or AI Studio is closed. Workspaces have their own setting for this.")
        : T("Private: chats are never stored, and each one is deleted once another chat is started or AI Studio is closed.");
}