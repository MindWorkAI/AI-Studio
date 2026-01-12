using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class AssistantBlock<TSettings> : MSGComponentBase where TSettings : IComponent
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public string Description { get; set; } = string.Empty;
    
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.DisabledByDefault;
    
    [Parameter]
    public string ButtonText { get; set; } = "Start";
    
    [Parameter]
    public string Link { get; set; } = string.Empty;

    [Parameter]
    public Tools.Components Component { get; set; } = Tools.Components.NONE;

    [Parameter]
    public PreviewFeatures? RequiredPreviewFeature { get; set; }
    
    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<AssistantBlock<TSettings>> Logger { get; init; } = null!;
    
    private async Task OpenSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        
        await this.DialogService.ShowAsync<TSettings>(T("Open Settings"), dialogParameters, DialogOptions.FULLSCREEN);
    }
    
    private string BorderColor => this.SettingsManager.IsDarkMode switch
    {
        true => this.ColorTheme.GetCurrentPalette(this.SettingsManager).GrayLight,
        false => this.ColorTheme.GetCurrentPalette(this.SettingsManager).Primary.Value,
    };

    private string BlockStyle => $"border-width: 2px; border-color: {this.BorderColor}; border-radius: 12px; border-style: solid; max-width: 20em;";

    private bool IsVisible
    {
        get
        {
            // Check if a preview feature is required and enabled:
            if (this.RequiredPreviewFeature is { } previewFeature && !previewFeature.IsEnabled(this.SettingsManager))
            {
                this.Logger.LogInformation("Assistant '{AssistantName}' is not visible because the required preview feature '{PreviewFeature}' is not enabled.", this.Name, previewFeature);
                return false;
            }

            // Check if the assistant is visible based on the configuration:
            return this.IsAssistantVisible();
        }
    }

    /// <summary>
    /// Checks if an assistant should be visible based on configuration.
    /// </summary>
    /// <returns>True if the assistant should be visible, false otherwise.</returns>
    private bool IsAssistantVisible()
    {
        // If no component is specified, it's always visible:
        if (this.Component is Tools.Components.NONE)
        {
            this.Logger.LogWarning("Assistant '{AssistantName}' is visible because no component is specified.", this.Name);
            return true;
        }

        // Map Components enum to ConfigurableAssistant enum:
        var configurableAssistant = this.Component switch
        {
            Tools.Components.GRAMMAR_SPELLING_ASSISTANT => ConfigurableAssistant.GRAMMAR_SPELLING_ASSISTANT,
            Tools.Components.ICON_FINDER_ASSISTANT => ConfigurableAssistant.ICON_FINDER_ASSISTANT,
            Tools.Components.REWRITE_ASSISTANT => ConfigurableAssistant.REWRITE_ASSISTANT,
            Tools.Components.TRANSLATION_ASSISTANT => ConfigurableAssistant.TRANSLATION_ASSISTANT,
            Tools.Components.AGENDA_ASSISTANT => ConfigurableAssistant.AGENDA_ASSISTANT,
            Tools.Components.CODING_ASSISTANT => ConfigurableAssistant.CODING_ASSISTANT,
            Tools.Components.TEXT_SUMMARIZER_ASSISTANT => ConfigurableAssistant.TEXT_SUMMARIZER_ASSISTANT,
            Tools.Components.EMAIL_ASSISTANT => ConfigurableAssistant.EMAIL_ASSISTANT,
            Tools.Components.LEGAL_CHECK_ASSISTANT => ConfigurableAssistant.LEGAL_CHECK_ASSISTANT,
            Tools.Components.SYNONYMS_ASSISTANT => ConfigurableAssistant.SYNONYMS_ASSISTANT,
            Tools.Components.MY_TASKS_ASSISTANT => ConfigurableAssistant.MY_TASKS_ASSISTANT,
            Tools.Components.JOB_POSTING_ASSISTANT => ConfigurableAssistant.JOB_POSTING_ASSISTANT,
            Tools.Components.BIAS_DAY_ASSISTANT => ConfigurableAssistant.BIAS_DAY_ASSISTANT,
            Tools.Components.ERI_ASSISTANT => ConfigurableAssistant.ERI_ASSISTANT,
            Tools.Components.DOCUMENT_ANALYSIS_ASSISTANT => ConfigurableAssistant.DOCUMENT_ANALYSIS_ASSISTANT,
            Tools.Components.I18N_ASSISTANT => ConfigurableAssistant.I18N_ASSISTANT,

            _ => ConfigurableAssistant.UNKNOWN,
        };

        // If the component doesn't map to a configurable assistant, it's always visible:
        if (configurableAssistant is ConfigurableAssistant.UNKNOWN)
        {
            this.Logger.LogWarning("Assistant '{AssistantName}' is visible because its component '{Component}' does not map to a configurable assistant.", this.Name, this.Component);
            return true;
        }

        // Check if the assistant is hidden by any configuration plugin:
        var isHidden = this.SettingsManager.ConfigurationData.App.HiddenAssistants.Contains(configurableAssistant);
        if (isHidden)
            this.Logger.LogInformation("Assistant '{AssistantName}' is hidden based on configuration.", this.Name);
        
        return !isHidden;
    }
}