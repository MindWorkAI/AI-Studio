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
    
    /// <summary>
    /// Whether to render this switch in its compact form.
    /// </summary>
    /// <remarks>
    /// For places which stack several of these switches above other content, such as the data source
    /// selection the chat opens from its footer. The roomy form stays the default, so that nothing
    /// changes where this was never asked for.
    /// </remarks>
    [Parameter]
    public bool Dense { get; set; }
    
    private string FieldClasses => this.Dense ? "mb-2" : "mb-3";
    
    private Size SwitchSize => this.Dense ? Size.Small : Size.Medium;
}