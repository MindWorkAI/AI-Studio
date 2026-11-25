using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows one card per mode with which an assistant can start, and reports the mode the user chose.
/// </summary>
/// <remarks>
/// The assistant shows nothing else until the user chose, so the texts of the options come from the assistant: they
/// tell what it does in each mode. Generic, so that every assistant names its modes with an enum of its own.
/// </remarks>
/// <typeparam name="TMode">The type of the modes of the assistant.</typeparam>
public partial class AssistantModeChoice<TMode> : ComponentBase
{
    /// <summary>
    /// The options to choose from, in the order the cards show them.
    /// </summary>
    [Parameter]
    public IReadOnlyList<AssistantModeOption<TMode>> Options { get; set; } = [];

    /// <summary>
    /// Called with the mode of the option the user chose.
    /// </summary>
    [Parameter]
    public EventCallback<TMode> ModeChosen { get; set; }

    private async Task Choose(AssistantModeOption<TMode> option) => await this.ModeChosen.InvokeAsync(option.Mode);
}