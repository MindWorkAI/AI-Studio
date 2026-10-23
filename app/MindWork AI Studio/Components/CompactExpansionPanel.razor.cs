using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A single, dense panel which hides content most users do not need, such as advanced options
/// or the source code of a generated plugin.
/// </summary>
/// <remarks>
/// Unlike the ExpansionPanel component, it brings its own panel list, so it stands anywhere in a
/// form. The header text comes from the caller, which keeps the translation with the caller as
/// well.
/// </remarks>
public partial class CompactExpansionPanel : ComponentBase
{
    [Parameter]
    public string HeaderText { get; set; } = string.Empty;

    [Parameter]
    public string HeaderIcon { get; set; } = Icons.Material.Filled.Tune;

    [Parameter]
    public Color IconColor { get; set; } = Color.Inherit;

    /// <summary>
    /// Class names for the surrounding panel list, separated by space, e.g., for its margins.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public bool Expanded { get; set; }

    [Parameter]
    public EventCallback<bool> ExpandedChanged { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}