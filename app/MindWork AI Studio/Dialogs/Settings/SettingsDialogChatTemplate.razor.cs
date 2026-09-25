using AIStudio.Chat;
using AIStudio.Settings;
using Microsoft.AspNetCore.Components;

using Lua;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogChatTemplate : SettingsDialogBase
{
    private bool isPluginDirectoryDialogOpen;

    [Parameter]
    public bool CreateTemplateFromExistingChatThread { get; set; }

    [Parameter]
    public ChatThread? ExistingChatThread { get; set; }

    #region Overrides of ComponentBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (this.CreateTemplateFromExistingChatThread)
            await this.AddChatTemplate();
    }

    #endregion

    private Task AddChatTemplate() => this.AddChatTemplate(null);

    private async Task ImportChatTemplate()
    {
        if (!this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("CHAT_TEMPLATES"))
            return;
        var table = await ConfigurationSnippetImportDialog.ShowAsync(this.DialogService, "CHAT_TEMPLATES", T("Import Chat Template"));
        if (table is not null && this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("CHAT_TEMPLATES"))
            await this.AddChatTemplate(table);
    }

    private async Task AddChatTemplate(LuaTable? importedConfiguration)
    {
        if (importedConfiguration is not null && !this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("CHAT_TEMPLATES"))
            return;

        var dialogParameters = new DialogParameters<ChatTemplateDialog>
        {
            { x => x.IsEditing, false },
        };
        if (importedConfiguration is not null)
            dialogParameters.Add(x => x.ImportedConfiguration, importedConfiguration);

        if (this.CreateTemplateFromExistingChatThread && importedConfiguration is null)
        {
            dialogParameters.Add(x => x.CreateFromExistingChatThread, this.CreateTemplateFromExistingChatThread);
            dialogParameters.Add(x => x.ExistingChatThread, this.ExistingChatThread);
        }

        var dialogReference = await this.DialogService.ShowAsync<ChatTemplateDialog>(T("Add Chat Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled ||
            (importedConfiguration is not null && !this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("CHAT_TEMPLATES")))
            return;

        var addedChatTemplate = (ChatTemplate)dialogResult.Data!;
        addedChatTemplate = addedChatTemplate with { Num = this.SettingsManager.ConfigurationData.NextChatTemplateNum++ };

        this.SettingsManager.ConfigurationData.ChatTemplates.Add(addedChatTemplate);

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task EditChatTemplate(ChatTemplate chatTemplate)
    {
        if (chatTemplate == ChatTemplate.NO_CHAT_TEMPLATE || chatTemplate.IsEnterpriseConfiguration)
            return;

        var dialogParameters = new DialogParameters<ChatTemplateDialog>
        {
            { x => x.DataNum, chatTemplate.Num },
            { x => x.DataId, chatTemplate.Id },
            { x => x.DataName, chatTemplate.Name },
            { x => x.DataSystemPrompt, chatTemplate.SystemPrompt },
            { x => x.PredefinedUserPrompt, chatTemplate.PredefinedUserPrompt },
            { x => x.IsEditing, true },
            { x => x.ExampleConversation, chatTemplate.ExampleConversation },
            { x => x.FileAttachments, chatTemplate.FileAttachments },
            { x => x.AllowProfileUsage, chatTemplate.AllowProfileUsage },
            { x => x.ToolIds, chatTemplate.ToolIds },
            { x => x.DataSourceOptions, chatTemplate.DataSourceOptions },
        };

        var dialogReference = await this.DialogService.ShowAsync<ChatTemplateDialog>(T("Edit Chat Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedChatTemplate = (ChatTemplate)dialogResult.Data!;
        this.SettingsManager.ConfigurationData.ChatTemplates[this.SettingsManager.ConfigurationData.ChatTemplates.IndexOf(chatTemplate)] = editedChatTemplate;

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ViewChatTemplate(ChatTemplate chatTemplate)
    {
        var dialogParameters = new DialogParameters<ChatTemplateDialog>
        {
            { x => x.DataNum, chatTemplate.Num },
            { x => x.DataId, chatTemplate.Id },
            { x => x.DataName, chatTemplate.Name },
            { x => x.DataSystemPrompt, chatTemplate.SystemPrompt },
            { x => x.PredefinedUserPrompt, chatTemplate.PredefinedUserPrompt },
            { x => x.IsEditing, true },
            { x => x.IsReadOnly, true },
            { x => x.ExampleConversation, chatTemplate.ExampleConversation },
            { x => x.FileAttachments, chatTemplate.FileAttachments },
            { x => x.AllowProfileUsage, chatTemplate.AllowProfileUsage },
            { x => x.ToolIds, chatTemplate.ToolIds },
            { x => x.DataSourceOptions, chatTemplate.DataSourceOptions },
        };

        await this.DialogService.ShowAsync<ChatTemplateDialog>(T("View Chat Template"), dialogParameters, DialogOptions.FULLSCREEN);
    }

    private async Task DeleteChatTemplate(ChatTemplate chatTemplate)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the chat template '{0}'?"), chatTemplate.Name) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Chat Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        this.SettingsManager.ConfigurationData.ChatTemplates.Remove(chatTemplate);
        await this.SettingsManager.StoreSettings();

        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ExportChatTemplateWithSharedAttachmentPaths(ChatTemplate chatTemplate)
    {
        if (!this.SettingsManager.ConfigurationData.App.ShowAdminSettings)
            return;

        if (chatTemplate == ChatTemplate.NO_CHAT_TEMPLATE || chatTemplate.IsEnterpriseConfiguration)
            return;

        if (!await this.ConfirmExportOfLocalDataSources(chatTemplate))
            return;

        await this.CopyChatTemplateLuaToClipboard(chatTemplate);
    }

    private async Task ExportChatTemplateWithPackagedAttachments(ChatTemplate chatTemplate)
    {
        if (!this.SettingsManager.ConfigurationData.App.ShowAdminSettings || this.isPluginDirectoryDialogOpen)
            return;

        if (chatTemplate == ChatTemplate.NO_CHAT_TEMPLATE || chatTemplate.IsEnterpriseConfiguration)
            return;

        if (chatTemplate.FileAttachments.Count == 0)
        {
            // That way asks about the local data sources itself, so we must not ask twice:
            await this.ExportChatTemplateWithSharedAttachmentPaths(chatTemplate);
            return;
        }

        if (!await this.ConfirmExportOfLocalDataSources(chatTemplate))
            return;

        this.isPluginDirectoryDialogOpen = true;
        try
        {
            var pluginDirectoryResponse = await this.RustService.SelectDirectory(T("Select configuration plugin folder"));
            if (pluginDirectoryResponse.UserCancelled)
                return;

            await this.CopyPackagedChatTemplateLuaToClipboard(chatTemplate, pluginDirectoryResponse.SelectedDirectory);
        }
        finally
        {
            this.isPluginDirectoryDialogOpen = false;
        }
    }

    /// <summary>
    /// Asks whether to export a template although it preselects data sources of this machine.
    /// </summary>
    /// <remarks>
    /// The export writes the preselected data source IDs unchanged, which is what makes a template
    /// usable across an organization — but a local file or folder exists here and nowhere else, so
    /// its ID points at nothing on the machine reading the plugin. Nothing breaks, the chat simply
    /// starts without that source, and that is precisely why it has to be said beforehand: nobody
    /// would notice it afterwards. Exporting anyway is a fair choice, because the rest of the
    /// template is worth rolling out.
    /// </remarks>
    /// <param name="chatTemplate">The chat template about to be exported.</param>
    /// <returns>True when the export may go ahead.</returns>
    private async Task<bool> ConfirmExportOfLocalDataSources(ChatTemplate chatTemplate)
    {
        var localDataSourceNames = ChatTemplate.GetPreselectedLocalDataSourceNames(chatTemplate, this.SettingsManager.ConfigurationData.DataSources);
        if (localDataSourceNames.Count == 0)
            return true;

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("This chat template preselects data sources which exist on this machine only: {0}. They cannot be rolled out, so a chat started with this template elsewhere begins without them. Do you want to export the template anyway?"), string.Join(", ", localDataSourceNames)) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Export Chat Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        return dialogResult is { Canceled: false };
    }

    private async Task CopyChatTemplateLuaToClipboard(ChatTemplate chatTemplate)
    {
        if (!chatTemplate.TryExportAsConfigurationSection(out var luaCode, out var issue))
        {
            await this.DialogService.ShowMessageBox(
                T("Export Chat Template"),
                issue,
                T("Close"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(luaCode))
            await this.RustService.CopyText2Clipboard(luaCode);
    }

    private async Task CopyPackagedChatTemplateLuaToClipboard(ChatTemplate chatTemplate, string pluginDirectory)
    {
        if (!chatTemplate.TryExportAsConfigurationSectionWithPackagedAttachments(pluginDirectory, out var luaCode, out var issue))
        {
            await this.DialogService.ShowMessageBox(
                T("Export Chat Template"),
                issue,
                T("Close"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(luaCode))
            await this.RustService.CopyText2Clipboard(luaCode);
    }
}
