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

    /// <summary>
    /// Names the kind of a destination a configuration plugin brings.
    /// </summary>
    private string DestinationTypeLabel(PluginConfigurationObjectType objectType) => objectType switch
    {
        PluginConfigurationObjectType.LLM_PROVIDER => this.T("LLM provider"),
        PluginConfigurationObjectType.EMBEDDING_PROVIDER => this.T("Embedding provider"),
        PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER => this.T("Transcription provider"),
        PluginConfigurationObjectType.DATA_SOURCE => this.T("Data source"),

        _ => this.T("Unknown"),
    };

    /// <summary>
    /// Everything a configuration plugin brings besides its providers and data sources. Only what is
    /// actually there gets listed, so the dialog stays short for a small configuration.
    /// </summary>
    private List<string> ConfigurationContents
    {
        get
        {
            var contents = new List<string>();
            if (this.Preview.ConfigurationSummary is not { } summary)
                return contents;

            Add(summary.DeclaredSettings, this.T("{0} setting it takes control of"), this.T("{0} settings it takes control of"));
            Add(summary.ChatTemplates, this.T("{0} chat template"), this.T("{0} chat templates"));
            Add(summary.Profiles, this.T("{0} profile"), this.T("{0} profiles"));
            Add(summary.DocumentAnalysisPolicies, this.T("{0} document analysis policy"), this.T("{0} document analysis policies"));
            Add(summary.MandatoryInfos, this.T("{0} mandatory information you have to accept before using AI Studio"), this.T("{0} mandatory information you have to accept before using AI Studio"));
            Add(summary.Introductions, this.T("{0} introduction on the welcome page"), this.T("{0} introductions on the welcome page"));

            return contents;

            void Add(int count, string singular, string plural)
            {
                if (count > 0)
                    contents.Add(string.Format(count == 1 ? singular : plural, count));
            }
        }
    }

    private void Cancel() => this.MudDialog.Cancel();

    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}