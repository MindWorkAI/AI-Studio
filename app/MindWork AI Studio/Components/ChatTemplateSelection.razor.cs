using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ChatTemplateSelection : MSGComponentBase
{
    [Parameter]
    public ChatTemplate CurrentChatTemplate { get; set; } = ChatTemplate.NO_CHATTEMPLATE;
    
    [Parameter]
    public EventCallback<ChatTemplate> CurrentChatTemplateChanged { get; set; }

    [Parameter]
    public string MarginLeft { get; set; } = "ml-3";

    [Parameter]
    public string MarginRight { get; set; } = string.Empty;
    
    private string MarginClass => $"{this.MarginLeft} {this.MarginRight}";
    
    private async Task SelectionChanged(ChatTemplate chatTemplate)
    {
        this.CurrentChatTemplate = chatTemplate;
        await this.CurrentChatTemplateChanged.InvokeAsync(chatTemplate);
    }
}