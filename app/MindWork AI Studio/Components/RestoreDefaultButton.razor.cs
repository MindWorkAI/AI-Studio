using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A small button below a field, which puts the default value back into that field.
/// </summary>
/// <remarks>
/// The button takes a row of its own, aligned to the right. Next to the field, it took away the width
/// the field needs, wrapped or got cut off with a longer text, and every field of a form ended at
/// another place, depending on the length of its button.
/// </remarks>
public partial class RestoreDefaultButton : ComponentBase
{
    /// <summary>
    /// The text on the button, e.g., "Restore default".
    /// </summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Puts the default value back into the field.
    /// </summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// The CSS classes of the row, e.g., for the space below it.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;
}