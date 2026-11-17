using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The heading of a category on the assistants page, which collapses and expands the category.
/// </summary>
/// <remarks>
/// The whole heading is the button, so that users find it where they look. The category block keeps the
/// state, since it also decides what the category shows.
/// </remarks>
public partial class AssistantCategoryHeader : MSGComponentBase
{
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
    /// The CSS classes of the heading.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    private string ToggleTooltip => this.IsCollapsed ? T("Expand this category") : T("Collapse this category");
}