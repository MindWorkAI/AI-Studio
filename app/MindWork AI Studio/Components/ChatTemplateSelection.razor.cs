using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

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

    private Task OpenSettingsDialog() => SettingsWindow.OpenAsync(this.DialogService, SettingsPath.CHAT_TEMPLATES);

    /// <summary>
    /// Opens a new template filled in from the current chat, without the list of templates in between.
    /// </summary>
    private async Task CreateNewChatTemplateFromChat()
    {
        if (!this.CanChatThreadBeUsedForTemplate)
            return;

        var dialogParameters = new DialogParameters<ChatTemplateDialog>
        {
            { x => x.IsEditing, false },
            { x => x.CreateFromExistingChatThread, true },
            { x => x.ExistingChatThread, this.CurrentChatThread },
        };
        await ChatTemplateDialog.AddAsync(this.DialogService, this.SettingsManager, dialogParameters, isImport: false);
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