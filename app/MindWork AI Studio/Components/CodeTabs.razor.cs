using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class CodeTabs : ComponentBase
{
    [Parameter] 
    public RenderFragment? ChildContent { get; set; }
    
    [Parameter]
    public int SelectedIndex { get; set; } = 0;
    
    private readonly List<CodeTabItem> blocks = new();

    internal void RegisterBlock(string title, RenderFragment fragment)
    {
        this.blocks.Add(new CodeTabItem
        {
            Title = title,
            Fragment = fragment,
        });
        
        this.StateHasChanged();
    }

    private class CodeTabItem
    {
        public string Title { get; init; } = string.Empty;
        
        public RenderFragment Fragment { get; init; } = null!;
    }
}