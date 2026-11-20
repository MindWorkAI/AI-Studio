using AIStudio.Dialogs;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// The heading of a category on the assistants page, which collapses and expands the category, and hides it.
/// </summary>
/// <remarks>
/// The whole heading is the button which collapses the category, so that users find it where they look.
/// The eye next to it hides the category, after the user confirmed it. The category block keeps the state,
/// since it also decides what the category shows.
/// </remarks>
public partial class AssistantCategoryHeader : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    /// <summary>
    /// The category whose name is the heading.
    /// </summary>
    [Parameter, EditorRequired]
    public AssistantCategoryKey Category { get; set; } = null!;

    /// <summary>
    /// Whether the user collapsed the category.
    /// </summary>
    [Parameter]
    public bool IsCollapsed { get; set; }

    /// <summary>
    /// Called when the user clicks the heading to collapse or expand the category.
    /// </summary>
    [Parameter]
    public EventCallback OnToggleCollapsed { get; set; }

    /// <summary>
    /// Called when the user hides the category and confirmed it.
    /// </summary>
    [Parameter]
    public EventCallback OnHide { get; set; }

    /// <summary>
    /// The CSS classes of the heading.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    private string ToggleTooltip => this.IsCollapsed ? T("Expand this category") : T("Collapse this category");

    /// <summary>
    /// Asks the user whether to hide the category, and hides it on a yes.
    /// </summary>
    /// <remarks>
    /// Many users click the eye just to see what it does. A category gone without a word would startle them,
    /// so the dialog says what happens, where to show the category again, and that collapsing it might be all
    /// they want. What happens to the assistants elsewhere depends on the option of the user, so the dialog
    /// describes the one in effect.
    /// </remarks>
    private async Task ConfirmHideAsync()
    {
        var effect = this.SettingsManager.ConfigurationData.AssistantsPage.HiddenCategoryAssistants is AssistantOfferBehavior.KEEP_OFFERING
            ? TA("""The assistants page will no longer show this category. AI Studio keeps offering its assistants elsewhere, e.g., in the "Send to ..." menu.""")
            : TA("""The assistants page will no longer show this category, and AI Studio will treat its assistants as if they did not exist, e.g., they will not appear in the "Send to ..." menu.""");

        var wayBack = TA("You can show the category again at any time in the assistant settings, which the gear next to the heading of the page opens.");
        var alternative = TA("If you only want a better overview, click the heading of the category instead: that collapses it.");

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.MarkdownBody, $"{effect}\n\n{wayBack}\n\n{alternative}" },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(string.Format(T("Hide the category '{0}'?"), this.Category.Name), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        await this.OnHide.InvokeAsync();
    }
}