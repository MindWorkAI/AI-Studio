using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class PreviewReleaseCandidate : MSGComponentBase
{
    [Parameter]
    public bool ApplyInnerScrollingFix { get; set; }
    
    private string Classes => this.ApplyInnerScrollingFix ? "InnerScrollingFix" : string.Empty;
}