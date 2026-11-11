using AIStudio.Dialogs.Settings;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

/// <summary>
/// A button which leads to a deeper pane of the settings window, such as "Manage" from the options of all
/// chats to the chat templates. Its arrow says that it leads further.
/// </summary>
/// <remarks>
/// Inside the window, it shows that pane in place, where the back button leads back; anywhere else, it
/// opens the window at that pane.
/// </remarks>
public partial class SettingsPaneLink : ComponentBase
{
    /// <summary>
    /// The path of the pane the button leads to.
    /// </summary>
    [Parameter]
    public SettingsPath Path { get; set; }

    /// <summary>
    /// What the button says, such as "Manage".
    /// </summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The window the button sits in, if any.
    /// </summary>
    [CascadingParameter]
    private SettingsWindow? Window { get; set; }

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private async Task OpenAsync()
    {
        if (this.Window is { } window)
        {
            window.NavigateTo(this.Path);
            return;
        }

        await SettingsWindow.OpenAsync(this.DialogService, this.Path);
    }
}