using Microsoft.AspNetCore.Components;

using RustService = AIStudio.Tools.RustService;

namespace AIStudio.Chat;

/// <summary>
/// The UI component for a chat content block, i.e., for any IContent.
/// </summary>
public partial class ContentBlockComponent : ComponentBase
{
    /// <summary>
    /// The role of the chat content block.
    /// </summary>
    [Parameter]
    public ChatRole Role { get; init; } = ChatRole.NONE;

    /// <summary>
    /// The content.
    /// </summary>
    [Parameter]
    public IContent Content { get; init; } = new ContentText();
    
    /// <summary>
    /// The content type.
    /// </summary>
    [Parameter]
    public ContentType Type { get; init; } = ContentType.NONE;
    
    /// <summary>
    /// When was the content created?
    /// </summary>
    [Parameter]
    public DateTimeOffset Time { get; init; }
    
    /// <summary>
    /// Optional CSS classes.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    private bool HideContent { get; set; }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Register the streaming events:
        this.Content.StreamingDone = this.AfterStreaming;
        this.Content.StreamingEvent = () => this.InvokeAsync(this.StateHasChanged);
        
        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Gets called when the content stream ended.
    /// </summary>
    private async Task AfterStreaming()
    {
        // Might be called from a different thread, so we need to invoke the UI thread:
        await this.InvokeAsync(() =>
        {
            //
            // Issue we try to solve: When the content changes during streaming,
            // Blazor might fail to see all changes made to the render tree.
            // This happens mostly when Markdown code blocks are streamed.
            //
            
            // Hide the content for a short time:
            this.HideContent = true;
            
            // Let Blazor update the UI, i.e., to see the render tree diff:
            this.StateHasChanged();
            
            // Show the content again:
            this.HideContent = false;
            
            // Let Blazor update the UI, i.e., to see the render tree diff:
            this.StateHasChanged();
        });
    }

    #endregion

    /// <summary>
    /// Copy this block's content to the clipboard.
    /// </summary>
    private async Task CopyToClipboard()
    {
        switch (this.Type)
        {
            case ContentType.TEXT:
                var textContent = (ContentText) this.Content;
                await this.RustService.CopyText2Clipboard(this.Snackbar, textContent.Text);
                break;
            
            default:
                this.Snackbar.Add("Cannot copy this content type to clipboard!", Severity.Error, config =>
                {
                    config.Icon = Icons.Material.Filled.ContentCopy;
                    config.IconSize = Size.Large;
                    config.IconColor = Color.Error;
                });
                break;
        }
    }
    
    private string CardClasses => $"my-2 rounded-lg {this.Class}";
}