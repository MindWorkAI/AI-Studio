using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Components;

public partial class CodeBlock : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    
    [Parameter] 
    public string? Title { get; set; } = string.Empty;
    
    [Parameter] 
    public bool IsInline { get; set; }
    
    [CascadingParameter]
    public CodeTabs? ParentTabs { get; set; }

    protected override void OnInitialized()
    {
        if (this.ParentTabs is not null && this.Title is not null)
        {
            void BlockSelf(RenderTreeBuilder builder)
            {
                builder.OpenComponent<CodeBlock>(0);
                builder.AddAttribute(1, "Title", this.Title);
                builder.AddAttribute(2, "ChildContent", this.ChildContent);
                builder.CloseComponent();
            }

            this.ParentTabs.RegisterBlock(this.Title, BlockSelf);
        }
    }

    private string BlockPadding => this.ParentTabs is null ? "padding: 16px !important;" : "padding: 8px !important";
}