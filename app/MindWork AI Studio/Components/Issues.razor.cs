using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Issues : ComponentBase
{
    [Parameter]
    public IEnumerable<string> IssuesData { get; set; } = [];
}