using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Manages the stored format templates. Used by the format template settings dialog, which the
/// export menu of an answer opens, and by the format template panel in the app settings. Adding
/// and importing a format template is up to FormatTemplateAddButtons, which each of them places on
/// its own.
/// </summary>
public partial class FormatTemplateManagement : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    /// <summary>
    /// The IDs of the format templates whose file cannot be found.
    /// </summary>
    private IReadOnlySet<string> templateIdsWithMissingFile = new HashSet<string>();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await this.FindMissingFilesAsync();
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                await this.FindMissingFilesAsync();
                this.StateHasChanged();
                break;
        }
    }

    #endregion

    /// <summary>
    /// Looks up which template files cannot be found.
    /// </summary>
    /// <remarks>
    /// Done once after each change rather than with each render, and on a background thread: a
    /// template on a network share which is not connected can take a while to answer.
    /// </remarks>
    private async Task FindMissingFilesAsync()
    {
        var formatTemplates = this.SettingsManager.ConfigurationData.FormatTemplates.ToList();
        this.templateIdsWithMissingFile = await Task.Run(() => formatTemplates.Where(formatTemplate => !File.Exists(formatTemplate.FilePath)).Select(formatTemplate => formatTemplate.Id).ToHashSet());
    }

    private async Task EditFormatTemplate(FormatTemplate formatTemplate)
    {
        if (formatTemplate.IsEnterpriseConfiguration)
            return;

        var dialogReference = await this.DialogService.ShowAsync<FormatTemplateDialog>(T("Edit Format Template"), FormatTemplateDialog.ParametersOf(formatTemplate, isEditing: true), DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedFormatTemplate = (FormatTemplate)dialogResult.Data!;
        this.SettingsManager.ConfigurationData.FormatTemplates[this.SettingsManager.ConfigurationData.FormatTemplates.IndexOf(formatTemplate)] = editedFormatTemplate;

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ViewFormatTemplate(FormatTemplate formatTemplate)
    {
        await this.DialogService.ShowAsync<FormatTemplateDialog>(T("View Format Template"), FormatTemplateDialog.ParametersOf(formatTemplate, isEditing: true, isReadOnly: true), DialogOptions.FULLSCREEN);
    }

    private async Task ExportFormatTemplate(FormatTemplate formatTemplate)
    {
        if (!this.SettingsManager.ConfigurationData.App.ShowAdminSettings)
            return;

        if (formatTemplate.IsEnterpriseConfiguration)
            return;

        var luaCode = formatTemplate.ExportAsConfigurationSection();
        if (!string.IsNullOrWhiteSpace(luaCode))
            await this.RustService.CopyText2Clipboard(luaCode);
    }

    private async Task DeleteFormatTemplate(FormatTemplate formatTemplate)
    {
        if (formatTemplate.IsEnterpriseConfiguration)
            return;

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the format template '{0}'?"), formatTemplate.Name) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Format Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        this.SettingsManager.ConfigurationData.FormatTemplates.Remove(formatTemplate);
        await this.SettingsManager.StoreSettings();

        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}