using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Supporter : ComponentBase
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public string Acknowledgment { get; set; } = string.Empty;
    
    [Parameter]
    public string? URL { get; set; }

    [Parameter]
    public SupporterType Type { get; set; }

    private string Icon => this.Type switch
    {
        SupporterType.INDIVIDUAL => AppIcons.USER,
        SupporterType.ORGANIZATION => AppIcons.BUSINESS_SUPPORTER,
        
        _ => AppIcons.USER,
    };
}