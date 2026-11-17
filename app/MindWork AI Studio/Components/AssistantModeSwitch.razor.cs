using AIStudio.Assistants;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The button next to the settings of an assistant, which switches to the other option it offers at the
/// start, e.g., from translating a document to translating a text.
/// </summary>
public partial class AssistantModeSwitch : ComponentBase
{
    /// <summary>
    /// The option the button switches to, whose icon and tooltip it shows.
    /// </summary>
    [Parameter, EditorRequired]
    public AssistantModeOption Option { get; set; }

    /// <summary>
    /// Whether the button is disabled, e.g., while the assistant works.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Called with the mode of the option when the user clicks the button.
    /// </summary>
    [Parameter]
    public EventCallback<AssistantContentMode> ModeChosen { get; set; }

    private async Task Switch() => await this.ModeChosen.InvokeAsync(this.Option.Mode);
}