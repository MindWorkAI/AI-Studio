using AIStudio.Layout;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class InnerScrolling : MSGComponentBase
{
    /// <summary>
    /// Set the height of anything above the scrolling content; usually a header.
    /// What we do is calc(100vh - HeaderHeight). Means, you can use multiple measures like
    /// 230px - 3em. Default is 3em.
    /// </summary>
    [Parameter]
    public string HeaderHeight { get; set; } = "3em";
    
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    
    /// <summary>
    /// Optional footer content, shown after the scrolling area.
    /// </summary>
    [Parameter]
    public RenderFragment? FooterContent { get; set; }
    
    [CascadingParameter]
    private MainLayout MainLayout { get; set; } = null!;
    
    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;
    
    private ElementReference AnchorAfterChildContent { get; set; }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.STATE_HAS_CHANGED ]);
        await base.OnInitializedAsync();
    }

    #endregion

    #region Overrides of MSGComponentBase

    public override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.STATE_HAS_CHANGED:
                this.StateHasChanged();
                break;
        }
        
        return Task.CompletedTask;
    }

    public override Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) where TResult : default where TPayload : default
    {
        return Task.FromResult(default(TResult));
    }

    #endregion

    private string Height => $"height: calc(100vh - {this.HeaderHeight} - {this.MainLayout.AdditionalHeight});";
    
    public async Task ScrollToBottom()
    {
        await this.AnchorAfterChildContent.ScrollIntoViewAsync(this.JsRuntime);
    }
}