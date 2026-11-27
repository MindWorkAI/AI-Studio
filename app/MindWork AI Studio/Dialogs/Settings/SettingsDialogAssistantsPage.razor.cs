using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

/// <summary>
/// The settings of the assistants page: what happens to the assistants of collapsed and hidden categories,
/// whether assistants keep your inputs when you leave them, and which categories the page shows.
/// </summary>
public partial class SettingsDialogAssistantsPage : SettingsDialogBase
{
    /// <summary>
    /// The categories the dialog offers to show or hide, in the order of the page.
    /// </summary>
    /// <remarks>
    /// The caller selects them with AssistantPageSections.SelectForSettings. The list stays the same while
    /// the dialog is open, so that a category the user hides here keeps its switch.
    /// </remarks>
    [Parameter]
    public IReadOnlyList<AssistantCategoryKey> Categories { get; set; } = [];

    private string CollapsedCategoriesHelp => TA("""Collapsing a category keeps the assistants page tidy. Choose whether AI Studio keeps offering its assistants elsewhere, e.g., in the "Send to ..." menu.""");

    private string HiddenCategoriesHelp => TA("""Hiding a category says that you do not need its assistants. Choose whether AI Studio still offers them elsewhere, e.g., in the "Send to ..." menu.""");

    private string InputsWhenLeavingHelp => TA("When you switch to the chat or to another assistant and come back, an assistant can still show your inputs, your options, and its last result. AI Studio keeps them in memory only, never on disk, until you reset the assistant or quit AI Studio. A text which you send to the assistant from elsewhere replaces them. An assistant which is still working keeps working in the background either way.");
}