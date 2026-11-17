using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

/// <summary>
/// The notification that the user hid a category of the assistants page, with the way back.
/// </summary>
/// <remarks>
/// It stays until the user closes it or opens the settings: whoever hid a category by mistake should not
/// have to search for where it went. All notifications of this kind share one key, so hiding several
/// categories in a row leaves one notification, the latest.
/// </remarks>
/// <param name="Categories">The categories the settings dialog offers, see AssistantPageSections.SelectForSettings.</param>
public readonly record struct DataAssistantCategoryHiddenMessage(IReadOnlyList<AssistantCategoryKey> Categories)
{
    /// <summary>
    /// The key of every notification of this kind.
    /// </summary>
    public const string KEY = "assistant-category-hidden";

    /// <param name="snackbar">The notifications of the window.</param>
    public void Show(ISnackbar snackbar)
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(AssistantCategoryHiddenMessage.Categories)] = this.Categories
        };

        snackbar.RemoveByKey(KEY);
        snackbar.Add<AssistantCategoryHiddenMessage>(parameters, Severity.Info, config =>
        {
            config.Icon = AppIcons.HIDDEN;
            config.IconSize = Size.Large;
            config.RequireInteraction = true;
            config.HideTransitionDuration = 600;
        }, KEY);
    }
}