using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigInfoRow : ComponentBase
{
    [Parameter]
    public ConfigInfoRowItem Item { get; set; } = new(Icons.Material.Filled.ArrowRightAlt, string.Empty, string.Empty, string.Empty, string.Empty);
}