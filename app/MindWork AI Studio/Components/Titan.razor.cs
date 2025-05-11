using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Titan : ComponentBase
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public string Acknowledgment { get; set; } = string.Empty;
    
    [Parameter]
    public string? URL { get; set; }

    [Parameter]
    public SupporterType Type { get; set; }
    
    [Parameter]
    public string? ImageSrc { get; set; }
}