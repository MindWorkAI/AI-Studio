using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Adds and imports format templates. It is kept apart from FormatTemplateManagement, so that the
/// format template settings dialog can place these buttons in its footer, where they stay in reach
/// however long the table of format templates grows. The format template panel in the app settings
/// places them below the table.
/// </summary>
public partial class FormatTemplateAddButtons : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            // The rules of the organization decide whether the import is offered:
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }

    #endregion

    private async Task AddFormatTemplate()
    {
        var dialogParameters = new DialogParameters<FormatTemplateDialog>
        {
            { x => x.IsEditing, false },
        };

        await this.AddFormatTemplate(dialogParameters, isImport: false);
    }

    private async Task ImportFormatTemplate()
    {
        var import = await ConfigurationSnippetImportDialog.ShowAsync(this.DialogService, PluginConfigurationObjectType.FORMAT_TEMPLATE, T("Import Format Template"));
        if (import?.ConfigurationObject is not FormatTemplate formatTemplate)
            return;

        await this.AddFormatTemplate(FormatTemplateDialog.ParametersOf(formatTemplate, isEditing: false), isImport: true);
    }

    private async Task AddFormatTemplate(DialogParameters<FormatTemplateDialog> dialogParameters, bool isImport)
    {
        var dialogReference = await this.DialogService.ShowAsync<FormatTemplateDialog>(T("Add Format Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        //
        // An organization can lock the import while the user is still filling in the form, e.g.,
        // with a new version of its configuration plugin. Hence, the permission is checked once
        // more before the imported format template is stored:
        //
        if (isImport && !this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet(PluginConfigurationObjectType.FORMAT_TEMPLATE))
            return;

        var addedFormatTemplate = (FormatTemplate)dialogResult.Data!;
        addedFormatTemplate = addedFormatTemplate with { Num = this.SettingsManager.ConfigurationData.NextFormatTemplateNum++ };

        this.SettingsManager.ConfigurationData.FormatTemplates.Add(addedFormatTemplate);

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}