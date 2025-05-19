using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace AIStudio.Components;

public partial class CodeBlock : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    
    [Parameter] 
    public string? Title { get; set; } = string.Empty;
    
    [Parameter] 
    public bool IsInline { get; set; } = false;
    
    [CascadingParameter]
    public CodeTabs? ParentTabs { get; set; }
    
    private static readonly string DARK_BACKGROUND_COLOR = "#2d2d2d";
    private static readonly string DARK_FOREGROUND_COLOR = "#f8f8f2";

    protected override void OnInitialized()
    {
        if (this.ParentTabs is not null && this.Title is not null)
        {
            RenderFragment blockSelf = builder =>
            {
                builder.OpenComponent<CodeBlock>(0);
                builder.AddAttribute(1, "Title", this.Title);
                builder.AddAttribute(2, "ChildContent", this.ChildContent);
                builder.CloseComponent();
            };
            this.ParentTabs.RegisterBlock(this.Title, blockSelf);
        }
    }

    private string BlockPadding()
    {
        return this.ParentTabs is null ? "padding: 16px !important;" : "padding: 8px !important";
    }
}