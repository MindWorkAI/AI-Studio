using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class InnerScrolling : MSGComponentBase
{
    [Parameter]
    public bool FillEntireHorizontalSpace { get; set; }
    
    [Parameter]
    public RenderFragment? HeaderContent { get; set; }
    
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    
    /// <summary>
    /// Optional footer content, shown after the scrolling area.
    /// </summary>
    [Parameter]
    public RenderFragment? FooterContent { get; set; }

    [Parameter]
    public string Class { get; set; } = string.Empty;
    
    [Parameter]
    public string? MinWidth { get; set; }

    [Parameter]
    public string Style { get; set; } = string.Empty;
    
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
    
    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.STATE_HAS_CHANGED:
                this.StateHasChanged();
                break;
        }
        
        return Task.CompletedTask;
    }

    #endregion

    private string MinWidthStyle => string.IsNullOrWhiteSpace(this.MinWidth) ? string.Empty : $"min-width: {this.MinWidth}; ";
    
    private string TerminatedStyles => string.IsNullOrWhiteSpace(this.Style) ? string.Empty : $"{this.Style}; ";
    
    private string Classes => this.FillEntireHorizontalSpace ? $"{this.Class} d-flex flex-column flex-grow-1" : $"{this.Class} d-flex flex-column";
    
    private string Styles => $"flex-grow: 1; overflow: hidden; {this.TerminatedStyles}{this.MinWidthStyle}";
    
    public async Task ScrollToBottom()
    {
        await this.AnchorAfterChildContent.ScrollIntoViewAsync(this.JsRuntime);
    }
}