using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MudTextSwitch : ComponentBase
{
    [Parameter]
    public string Label { get; set; } = string.Empty;
    
    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool Value { get; set; }
    
    [Parameter]
    public EventCallback<bool> ValueChanged { get; set; }
    
    [Parameter]
    public Color Color { get; set; } = Color.Primary;
    
    [Parameter]
    public Func<bool, string?> Validation { get; set; } = _ => null;
    
    [Parameter]
    public string LabelOn { get; set; } = string.Empty;
    
    [Parameter]
    public string LabelOff { get; set; } = string.Empty;
}