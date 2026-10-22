using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ExpansionPanel : ComponentBase
{
    [Parameter]
    public string HeaderIcon { get; set; } = Icons.Material.Filled.BugReport;
    
    [Parameter]
    public Size IconSize { get; set; } = Size.Medium;
    
    [Parameter]
    public Color IconColor { get; set; } = Color.Primary;
    
    [Parameter]
    public string HeaderText { get; set; } = "n/a";

    /// <summary>
    /// The typography of the header text.
    /// </summary>
    /// <remarks>
    /// Worth lowering for a panel which sits inside another one, together with the compact header
    /// class below: two headers of the same size give no clue about which one contains the other.
    /// </remarks>
    [Parameter]
    public Typo HeaderTypo { get; set; } = Typo.h6;

    /// <summary>
    /// Additional class names for the header, separated by space.
    /// </summary>
    /// <remarks>
    /// The one this exists for is expansion-panel-header-compact, which takes the height of the
    /// header down for a nested panel.
    /// </remarks>
    [Parameter]
    public string HeaderClass { get; set; } = string.Empty;

    [Parameter]
    public int? MaxHeight { get; set; }
    
    [Parameter]
    public Func<bool, Task> ExpandedChanged { get; set; } = _ => Task.CompletedTask;
    
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool IsExpanded { get; set; }
    
    [Parameter]
    public bool ShowEndButton { get; set; }
    
    [Parameter]
    public Func<ValueTask> EndButtonClickAsync { get; set; } = () => ValueTask.CompletedTask;
    
    [Parameter]
    public string EndButtonIcon { get; set; } = Icons.Material.Filled.Delete;

    [Parameter]
    public Color EndButtonColor { get; set; } = Color.Error;

    [Parameter]
    public string EndButtonTooltip { get; set; } = string.Empty;
}