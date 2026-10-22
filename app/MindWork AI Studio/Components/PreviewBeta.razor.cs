using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class PreviewBeta : MSGComponentBase
{
    [Parameter]
    public bool ApplyInnerScrollingFix { get; set; }
    
    /// <summary>
    /// Additional class names for the chip itself, separated by space.
    /// </summary>
    /// <remarks>
    /// The default is the margin every caller relied on before this parameter existed, because the
    /// chip usually sits on a line of its own above a heading. A header which puts it beside the
    /// heading instead passes an empty value.
    /// </remarks>
    [Parameter]
    public string ChipClass { get; set; } = "mb-3";
    
    private string Classes => this.ApplyInnerScrollingFix ? "InnerScrollingFix" : string.Empty;
}