using AIStudio.Assistants;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows one card per option with which an assistant can start, e.g., to translate a document or a text,
/// and reports the option the user chose.
/// </summary>
/// <remarks>
/// The assistant shows nothing else until the user chose, so the texts of the options come from the
/// assistant: they tell what it does with each of them.
/// </remarks>
public partial class AssistantModeChoice : ComponentBase
{
    /// <summary>
    /// The options to choose from, in the order the cards show them.
    /// </summary>
    [Parameter]
    public IReadOnlyList<AssistantModeOption> Options { get; set; } = [];

    /// <summary>
    /// Called with the mode of the option the user chose.
    /// </summary>
    [Parameter]
    public EventCallback<AssistantContentMode> ModeChosen { get; set; }

    private async Task Choose(AssistantModeOption option) => await this.ModeChosen.InvokeAsync(option.Mode);
}