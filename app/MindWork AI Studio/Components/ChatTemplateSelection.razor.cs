using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ChatTemplateSelection : MSGComponentBase
{
    [Parameter]
    public ChatTemplate CurrentChatTemplate { get; set; } = ChatTemplate.NO_CHAT_TEMPLATE;
    
    [Parameter]
    public bool CanChatThreadBeUsedForTemplate { get; set; }
    
    [Parameter]
    public ChatThread? CurrentChatThread { get; set; }
    
    [Parameter]
    public EventCallback<ChatTemplate> CurrentChatTemplateChanged { get; set; }

    [Parameter]
    public string MarginLeft { get; set; } = "ml-1";

    [Parameter]
    public string MarginRight { get; set; } = string.Empty;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    private string MarginClass => $"{this.MarginLeft} {this.MarginRight}";
    
    private async Task SelectionChanged(ChatTemplate chatTemplate)
    {
        this.CurrentChatTemplate = chatTemplate;
        await this.CurrentChatTemplateChanged.InvokeAsync(chatTemplate);
    }
    
    private async Task OpenSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        await this.DialogService.ShowAsync<SettingsDialogChatTemplate>(T("Open Chat Template Options"), dialogParameters, DialogOptions.FULLSCREEN);
    }
    
    private async Task CreateNewChatTemplateFromChat()
    {
        var dialogParameters = new DialogParameters<SettingsDialogChatTemplate>
        {
            { x => x.CreateTemplateFromExistingChatThread, true },
            { x => x.ExistingChatThread, this.CurrentChatThread }
        };
        await this.DialogService.ShowAsync<SettingsDialogChatTemplate>(T("Open Chat Template Options"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}