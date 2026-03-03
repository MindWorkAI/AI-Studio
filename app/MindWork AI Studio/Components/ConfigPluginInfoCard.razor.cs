using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigPluginInfoCard : ComponentBase
{
    [Parameter]
    public string HeaderIcon { get; set; } = Icons.Material.Filled.Extension;

    [Parameter]
    public string HeaderText { get; set; } = string.Empty;

    [Parameter]
    public IEnumerable<ConfigInfoRowItem> Items { get; set; } = [];

    [Parameter]
    public bool ShowWarning { get; set; }

    [Parameter]
    public string WarningText { get; set; } = string.Empty;

    [Parameter]
    public string Class { get; set; } = "pa-3 mt-2 mb-2";
}
