using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MudJustifiedMarkdown
{
    [Parameter]
    public string Value { get; set; } = string.Empty;
}