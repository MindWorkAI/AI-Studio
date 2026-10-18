using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// One row of a table that shows information: an icon and a label in the first cell, the value in the
/// second.
/// </summary>
/// <remarks>
/// Place it inside the body of a MudSimpleTable. The value is free content, so a row may hold buttons
/// next to its text.
/// </remarks>
public partial class InfoTableRow : ComponentBase
{
    /// <summary>
    /// The icon in front of the label.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;

    /// <summary>
    /// The label, which names what the row shows.
    /// </summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The value of the row.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}