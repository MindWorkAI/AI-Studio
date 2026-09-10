using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Marks an area whose drops end up at its default target when they hit no specific zone.
/// </summary>
/// <remarks>
/// <para>
/// This is how the habitual behaviour survives the move to hit testing: a file dropped anywhere in
/// the chat, in an assistant, or in a dialog still arrives where it used to, while a file dropped
/// on a specific zone now arrives exactly there. No code decides between the two -- the browser
/// does, because the specific zone lies deeper in the DOM than the area around it, and the hit test
/// resolves from the inside out.
/// </para>
/// <para>
/// The component replaces an existing element rather than adding one: it takes the class and the
/// style of the element it stands in for. Where the areas already had a wrapper, which is the case
/// for every page and every assistant, nothing about the layout changes.
/// </para>
/// </remarks>
public partial class DropZoneScope : ComponentBase
{
    /// <summary>
    /// The content of the area.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// The CSS classes of the element this scope renders.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// The inline style of the element this scope renders.
    /// </summary>
    [Parameter]
    public string Style { get; set; } = string.Empty;

    /// <summary>
    /// The state to cascade, for hosts which want to be the default target of their own area.
    /// </summary>
    /// <remarks>
    /// A host cannot read what it cascades itself, so a page which is its own drop target has to own
    /// the state instead. The plugins page is such a case: it accepts an archive anywhere on it and
    /// has no inner zone to hand the role to. Everybody else leaves this alone and lets the scope
    /// keep its own state.
    /// </remarks>
    [Parameter]
    public DropZoneScopeState? State { get; set; }

    private readonly DropZoneScopeState ownState = new($"drop-zone-scope-{Guid.NewGuid():N}");

    private DropZoneScopeState EffectiveState => this.State ?? this.ownState;
}