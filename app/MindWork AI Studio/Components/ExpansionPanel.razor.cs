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
    
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool IsExpanded { get; set; }
}