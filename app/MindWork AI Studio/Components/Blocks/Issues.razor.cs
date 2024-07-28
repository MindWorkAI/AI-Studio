using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class Issues : ComponentBase
{
    [Parameter]
    public IEnumerable<string> IssuesData { get; set; } = [];
}