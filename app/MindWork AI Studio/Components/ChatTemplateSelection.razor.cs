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

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    #endregion

    private string ChatTemplateIcon(ChatTemplate chatTemplate)
    {
        if (chatTemplate.IsEnterpriseConfiguration)
            return Icons.Material.Filled.Business;

        return Icons.Material.Filled.RateReview;
    }

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

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            this.StateHasChanged();

        return Task.CompletedTask;
    }

    #endregion
}