using AIStudio.Components;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Asks the user whether a local configuration plugin may be deleted, and shows what the deletion
/// takes with it.
/// </summary>
public partial class ConfigurationPluginDeleteDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The name of the configuration plugin about to be deleted.
    /// </summary>
    [Parameter]
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// What the deletion removes besides the plugin directory.
    /// </summary>
    [Parameter]
    public ConfigurationPluginDeleteSummary Summary { get; set; } = ConfigurationPluginDeleteSummary.EMPTY;

    private List<string> Consequences => this.BuildConsequences();

    /// <summary>
    /// Turns the summary into the lines shown to the user. Only what is actually affected is listed,
    /// so the dialog stays short for a configuration plugin that just locks a single setting.
    /// </summary>
    private List<string> BuildConsequences()
    {
        var consequences = new List<string>();
        var summary = this.Summary;

        Add(summary.LlmProviders, this.T("{0} LLM provider"), this.T("{0} LLM providers"));
        Add(summary.TranscriptionProviders, this.T("{0} transcription provider"), this.T("{0} transcription providers"));
        Add(summary.EmbeddingProviders, this.T("{0} embedding provider"), this.T("{0} embedding providers"));
        Add(summary.ChatTemplates, this.T("{0} chat template"), this.T("{0} chat templates"));
        Add(summary.Profiles, this.T("{0} profile"), this.T("{0} profiles"));
        Add(summary.DocumentAnalysisPolicies, this.T("{0} document analysis policy"), this.T("{0} document analysis policies"));
        Add(summary.MandatoryInfos, this.T("{0} mandatory information"), this.T("{0} mandatory informations"));
        Add(summary.Introductions, this.T("{0} introduction on the welcome page"), this.T("{0} introductions on the welcome page"));

        // Data sources are called out separately: removing them also deletes their credentials from
        // the operating system's keychain, which the user cannot undo by reinstalling the plugin.
        Add(summary.DataSources,
            this.T("{0} data source, including its credentials in your operating system's keychain"),
            this.T("{0} data sources, including their credentials in your operating system's keychain"));

        Add(summary.LockedSettings, this.T("{0} setting returns to its default value"), this.T("{0} settings return to their default values"));

        return consequences;

        void Add(int count, string singular, string plural)
        {
            if (count > 0)
                consequences.Add(string.Format(count == 1 ? singular : plural, count));
        }
    }

    private void Cancel() => this.MudDialog.Cancel();

    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}