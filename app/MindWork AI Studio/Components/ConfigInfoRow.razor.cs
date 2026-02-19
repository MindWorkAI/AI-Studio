using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigInfoRow : ComponentBase
{
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.ArrowRightAlt;

    [Parameter]
    public string Text { get; set; } = string.Empty;

    [Parameter]
    public string CopyValue { get; set; } = string.Empty;

    [Parameter]
    public string CopyTooltip { get; set; } = string.Empty;

    [Parameter]
    public string Style { get; set; } = string.Empty;
}