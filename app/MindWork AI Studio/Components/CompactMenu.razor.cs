using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A menu with the spacing of a dense menu and the text of a normal one, for the compact rows of the
/// chat page.
/// </summary>
/// <remarks>
/// A dense MudMenu sets the text of its items a size smaller, but leaves their icons at the usual 24
/// pixels, so the text looks too small next to them. This menu keeps the dense spacing, gives the text
/// the size it has in every other menu of the app, and draws the icons at 20 pixels, see the rules for
/// <see cref="LIST_CLASS"/> in app.css. The rules live there rather than in styles of this component: a
/// menu opens in a popover, outside of every component.
/// </remarks>
public partial class CompactMenu : ComponentBase
{
    /// <summary>
    /// The class of the menu's list, whose text and icons app.css sizes.
    /// </summary>
    private const string LIST_CLASS = "compact-menu-list";

    /// <summary>
    /// The class of the menu's popover, which app.css outlines.
    /// </summary>
    /// <remarks>
    /// A compact menu opens over rows which look just like its own entries, and MudBlazor's shadow
    /// falls mostly below a popover, so the menu would hardly stand out at its upper edge.
    /// </remarks>
    private const string POPOVER_CLASS = "compact-menu-popover";

    /// <summary>
    /// The icon of the button which opens the menu.
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// The size of the button which opens the menu.
    /// </summary>
    [Parameter]
    public Size Size { get; set; } = Size.Medium;

    /// <summary>
    /// What a screen reader announces for the button which opens the menu.
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// The point of the button the menu opens at.
    /// </summary>
    [Parameter]
    public Origin? AnchorOrigin { get; set; }

    /// <summary>
    /// The point of the menu which meets the anchor.
    /// </summary>
    [Parameter]
    public Origin TransformOrigin { get; set; } = Origin.TopLeft;

    /// <summary>
    /// Called with true when the menu opens and with false when it closes.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>
    /// The items of the menu.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Every other parameter of MudMenu, handed on as it is, such as MaxHeight.
    /// </summary>
    /// <remarks>
    /// These reach MudMenu without their type, so give each one as an expression with @, as in
    /// MaxHeight="@MenuDefaults.MAX_HEIGHT": a plain value such as Disabled="true" arrives as text, which
    /// MudMenu cannot take. A parameter needing a type of its own, as an enumeration or a callback does,
    /// gets a parameter of this component instead.
    /// </remarks>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}