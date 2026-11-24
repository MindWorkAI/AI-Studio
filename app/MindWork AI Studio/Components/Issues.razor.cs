using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Issues : MSGComponentBase
{
    [Parameter]
    public IEnumerable<string> IssuesData { get; set; } = [];
}