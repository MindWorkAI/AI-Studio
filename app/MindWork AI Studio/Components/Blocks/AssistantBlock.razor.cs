using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class AssistantBlock : ComponentBase
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public string Description { get; set; } = string.Empty;
    
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.DisabledByDefault;
    
    [Parameter]
    public string ButtonText { get; set; } = "Start";
    
    [Parameter]
    public string Link { get; set; } = string.Empty;
}