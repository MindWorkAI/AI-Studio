using AIStudio.Dialogs;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelFormatTemplates : SettingsPanelBase
{
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

    private async Task AddFormatTemplate()
    {
        var dialogParameters = new DialogParameters<FormatTemplateDialog>
        {
            { x => x.IsEditing, false },
        };

        var dialogReference = await this.DialogService.ShowAsync<FormatTemplateDialog>(T("Add Format Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedFormatTemplate = (FormatTemplate)dialogResult.Data!;
        addedFormatTemplate = addedFormatTemplate with { Num = this.SettingsManager.ConfigurationData.NextFormatTemplateNum++ };

        this.SettingsManager.ConfigurationData.FormatTemplates.Add(addedFormatTemplate);

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task EditFormatTemplate(FormatTemplate formatTemplate)
    {
        if (formatTemplate.IsEnterpriseConfiguration)
            return;

        var dialogReference = await this.DialogService.ShowAsync<FormatTemplateDialog>(T("Edit Format Template"), CreateDialogParameters(formatTemplate, isEditing: true), DialogOptions.FULLSCREEN);
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
        await this.DialogService.ShowAsync<FormatTemplateDialog>(T("View Format Template"), CreateDialogParameters(formatTemplate, isEditing: true, isReadOnly: true), DialogOptions.FULLSCREEN);
    }

    private static DialogParameters<FormatTemplateDialog> CreateDialogParameters(FormatTemplate formatTemplate, bool isEditing, bool isReadOnly = false) => new()
    {
        { x => x.DataNum, formatTemplate.Num },
        { x => x.DataId, formatTemplate.Id },
        { x => x.DataName, formatTemplate.Name },
        { x => x.DataFilePath, formatTemplate.FilePath },
        { x => x.IsEditing, isEditing },
        { x => x.IsReadOnly, isReadOnly },
    };

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