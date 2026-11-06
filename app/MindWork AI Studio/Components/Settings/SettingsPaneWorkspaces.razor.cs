using AIStudio.Settings.DataModel;

namespace AIStudio.Components.Settings;

/// <summary>
/// The pane of the settings window with the workspaces, see <see cref="AIStudio.Settings.SettingsPath.WORKSPACES"/>.
/// </summary>
/// <remarks>
/// Chats in workspaces are kept by a choice of their own. The choice for all other chats is part of the
/// pane of the chat, see <see cref="SettingsPaneChat"/>.
/// </remarks>
public partial class SettingsPaneWorkspaces : SettingsPanelBase
{
    /// <summary>
    /// What all chats outside of workspaces get right now.
    /// </summary>
    private ChatStorageChoice ChoiceOfAllChats => this.SettingsManager.ConfigurationData.Workspace.GetChoice(isInWorkspace: false);

    /// <summary>
    /// The choice for chats in workspaces as the list shows it: "Same as for all chats" whenever it is,
    /// since the list leaves out the choice it covers.
    /// </summary>
    private ChatStorageChoice WorkspaceChoiceShown
    {
        get
        {
            var choice = this.SettingsManager.ConfigurationData.Workspace.GetChoice(isInWorkspace: true);
            return choice == this.ChoiceOfAllChats ? ChatStorageChoice.SAME_AS_ALL_CHATS : choice;
        }
    }
}