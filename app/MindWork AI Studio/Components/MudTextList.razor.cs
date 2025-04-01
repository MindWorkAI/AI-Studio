using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MudTextList : ComponentBase
{
    [Parameter]
    public bool Clickable { get; set; }

    [Parameter]
    public IList<TextItem> Items { get; set; } = [];

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.CheckCircle;
    
    [Parameter]
    public string Class { get; set; } = string.Empty;
    
    private string Classes => $"mud-text-list {this.Class}";
}

public readonly record struct TextItem(string Header, string Text);