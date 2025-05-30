using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class CodeTabs : ComponentBase
{
    [Parameter] 
    public RenderFragment? ChildContent { get; set; }
    
    [Parameter]
    public int SelectedIndex { get; set; }
    
    [Parameter]
    public EventCallback<int> SelectedIndexChanged { get; set; }
    
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

    private async Task TabChanged(int index)
    {
        this.SelectedIndex = index;
        await this.SelectedIndexChanged.InvokeAsync(index);
        await this.InvokeAsync(this.StateHasChanged);
    }
}