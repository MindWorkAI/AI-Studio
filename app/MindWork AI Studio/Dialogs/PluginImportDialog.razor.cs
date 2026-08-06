using AIStudio.Components;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Asks the user whether a plugin archive may be installed. It shows the metadata the archive
/// declares about itself, so the user can judge the plugin before its code runs.
/// </summary>
public partial class PluginImportDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The metadata of the plugin archive about to be installed.
    /// </summary>
    [Parameter]
    public PluginImportPreview Preview { get; set; } = null!;

    /// <summary>
    /// Names the kind of plugin the user is about to install. Each plugin type gets its own
    /// sentence instead of a placeholder because articles and word order differ between languages.
    /// </summary>
    private string IntroductionText => this.Preview.Plugin.Type switch
    {
        PluginType.LANGUAGE => this.T("You are about to install a language plugin from a file."),
        PluginType.ASSISTANT => this.T("You are about to install an assistant plugin from a file."),
        PluginType.CONFIGURATION => this.T("You are about to install a configuration plugin from a file."),
        PluginType.THEME => this.T("You are about to install a theme plugin from a file."),

        _ => this.T("You are about to install a plugin from a file."),
    };

    private string TypeLabel => this.Preview.Plugin.Type.GetName();

    private string AuthorsLabel => this.Preview.Plugin.Authors.Length > 0
        ? string.Join(", ", this.Preview.Plugin.Authors)
        : this.T("Unknown");

    private void Cancel() => this.MudDialog.Cancel();

    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}