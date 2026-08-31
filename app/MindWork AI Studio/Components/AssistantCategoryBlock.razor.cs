using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Renders one category of assistants together with its heading.
/// </summary>
/// <remarks>
/// The heading is derived from the assistant blocks inside this category: it is rendered only when
/// at least one of them is visible. Thus, hiding assistants by configuration can never leave an
/// empty category heading behind.
/// </remarks>
public partial class AssistantCategoryBlock : ComponentBase
{
    private readonly HashSet<IAssistantCategoryMember> members = [];

    /// <summary>
    /// The heading of this category.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The CSS classes used for the heading.
    /// </summary>
    [Parameter]
    public string HeaderClass { get; set; } = "mb-2 mr-3 mt-6";

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Adds an assistant block to this category.
    /// </summary>
    /// <remarks>
    /// Assistant blocks call this while they initialize, i.e. after this category was rendered for
    /// the first time. Hence, we have to render again to show the heading.
    /// </remarks>
    /// <param name="member">The assistant block which belongs to this category.</param>
    internal void RegisterAssistant(IAssistantCategoryMember member)
    {
        if (this.members.Add(member))
            this.StateHasChanged();
    }

    /// <summary>
    /// Removes an assistant block from this category.
    /// </summary>
    /// <param name="member">The assistant block which no longer belongs to this category.</param>
    internal void UnregisterAssistant(IAssistantCategoryMember member) => this.members.Remove(member);

    /// <summary>
    /// Gets whether at least one assistant of this category is visible right now.
    /// </summary>
    /// <remarks>
    /// We evaluate this live instead of caching it. That way, changes to the configuration take
    /// effect as soon as the assistants page renders again.
    /// </remarks>
    private bool HasVisibleAssistant => this.members.Any(member => member.IsVisible);

    /// <summary>
    /// Gets the CSS classes used for the assistant stack.
    /// </summary>
    /// <remarks>
    /// The stack must be rendered even when no assistant is visible, because the assistant blocks
    /// register themselves while rendering. Without any visible assistant, we drop the margin so
    /// that a hidden category leaves no gap behind.
    /// </remarks>
    private string StackClass => this.HasVisibleAssistant ? "mb-3" : string.Empty;
}