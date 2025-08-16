using AIStudio.Chat;
using AIStudio.Settings;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogChatTemplate : SettingsDialogBase
{
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
    
    private async Task AddChatTemplate()
    {
        var dialogParameters = new DialogParameters<ChatTemplateDialog>
        {
            { x => x.IsEditing, false },
        };

        if (this.CreateTemplateFromExistingChatThread)
        {
            dialogParameters.Add(x => x.CreateFromExistingChatThread, this.CreateTemplateFromExistingChatThread);
            dialogParameters.Add(x => x.ExistingChatThread, this.ExistingChatThread);
        }

        var dialogReference = await this.DialogService.ShowAsync<ChatTemplateDialog>(T("Add Chat Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
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
            { x => x.AllowProfileUsage, chatTemplate.AllowProfileUsage },
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

    private async Task DeleteChatTemplate(ChatTemplate chatTemplate)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", string.Format(T("Are you sure you want to delete the chat template '{0}'?"), chatTemplate.Name) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Chat Template"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.SettingsManager.ConfigurationData.ChatTemplates.Remove(chatTemplate);
        await this.SettingsManager.StoreSettings();
        
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}