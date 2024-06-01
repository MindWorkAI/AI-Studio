using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class InnerScrolling : ComponentBase
{
    /// <summary>
    /// Set the height of anything above the scrolling content; usually a header.
    /// What we do is calc(100vh - THIS). Means, you can use multiple measures like
    /// 230px - 3em. Default is 3em.
    /// </summary>
    [Parameter]
    public string HeaderHeight { get; set; } = "3em";
    
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    
    /// <summary>
    /// Optional footer content, shown after the scrolling area.
    /// </summary>
    [Parameter]
    public RenderFragment? FooterContent { get; set; }
    
    private string Height => $"height: calc(100vh - {this.HeaderHeight});";
}