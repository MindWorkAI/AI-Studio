using AIStudio.Dialogs;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

/// <summary>
/// The chat templates below the options of new chats, as a <see cref="CollectionSection"/>.
/// </summary>
/// <remarks>
/// A template overrides the defaults above it, as far as it sets them, for every chat started from it, so
/// the templates come last, right below the choice of a preselected template. The preview names the user's own
/// templates in the order of the page of the templates, see
/// <see cref="ChatTemplateExtensions.SortedForSettings"/>, and a click opens one of them. The templates of
/// an organization stay out of the preview, since the user cannot change them.
/// </remarks>
public partial class ChatTemplatesSection : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    /// <summary>
    /// The templates the user can change, in the order of the page of the templates.
    /// </summary>
    private List<ChatTemplate> OwnChatTemplates => this.SettingsManager.ConfigurationData.ChatTemplates
        .Where(chatTemplate => !chatTemplate.IsEnterpriseConfiguration)
        .SortedForSettings()
        .ToList();

    private CollectionPreview Preview
    {
        get
        {
            var ownChatTemplates = this.OwnChatTemplates;
            var shown = ownChatTemplates
                .Take(CollectionSection.MAX_PREVIEW_ITEMS)
                .Select(chatTemplate => new CollectionPreviewItem(chatTemplate.Id, chatTemplate.GetSafeName(), () => ChatTemplateDialog.EditAsync(this.DialogService, this.SettingsManager, chatTemplate)))
                .ToList();

            return new CollectionPreview(shown, ownChatTemplates.Count - shown.Count);
        }
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.PLUGINS_RELOADED ]);
        await base.OnInitializedAsync();
    }

    #endregion

    private async Task AddChatTemplate()
    {
        var dialogParameters = new DialogParameters<ChatTemplateDialog>
        {
            { x => x.IsEditing, false },
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