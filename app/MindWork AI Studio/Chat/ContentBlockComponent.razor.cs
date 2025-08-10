using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

/// <summary>
/// The UI component for a chat content block, i.e., for any IContent.
/// </summary>
public partial class ContentBlockComponent : MSGComponentBase
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
    
    [Parameter]
    public bool IsLastContentBlock { get; set; }
    
    [Parameter]
    public bool IsSecondToLastBlock { get; set; }

    [Parameter]
    public Func<IContent, Task>? RemoveBlockFunc { get; set; }
    
    [Parameter]
    public Func<IContent, Task>? RegenerateFunc { get; set; }
    
    [Parameter]
    public Func<IContent, Task>? EditLastBlockFunc { get; set; }
    
    [Parameter]
    public Func<IContent, Task>? EditLastUserBlockFunc { get; set; }
    
    [Parameter]
    public Func<bool> RegenerateEnabled { get; set; } = () => false;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

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
        await this.InvokeAsync(async () =>
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
            
            // Inform the chat that the streaming is done:
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.CHAT_STREAMING_DONE);
        });
    }

    #endregion
    
    private string CardClasses => $"my-2 rounded-lg {this.Class}";

    private CodeBlockTheme CodeColorPalette => this.SettingsManager.IsDarkMode ? CodeBlockTheme.Dark : CodeBlockTheme.Default;

    private MudMarkdownStyling MarkdownStyling => new()
    {
        CodeBlock = { Theme = this.CodeColorPalette },
    };
    
    private async Task RemoveBlock()
    {
        if (this.RemoveBlockFunc is null)
            return;
        
        var remove = await this.DialogService.ShowMessageBox(
            T("Remove Message"),
            T("Do you really want to remove this message?"),
            T("Yes, remove it"),
            T("No, keep it"));
        
        if (remove.HasValue && remove.Value)
            await this.RemoveBlockFunc(this.Content);
    }
    
    private async Task RegenerateBlock()
    {
        if (this.RegenerateFunc is null)
            return;
        
        if(this.Role is not ChatRole.AI)
            return;
        
        var regenerate = await this.DialogService.ShowMessageBox(
            T("Regenerate Message"),
            T("Do you really want to regenerate this message?"),
            T("Yes, regenerate it"),
            T("No, keep it"));
        
        if (regenerate.HasValue && regenerate.Value)
            await this.RegenerateFunc(this.Content);
    }
    
    private async Task EditLastBlock()
    {
        if (this.EditLastBlockFunc is null)
            return;
        
        if(this.Role is not ChatRole.USER)
            return;
        
        await this.EditLastBlockFunc(this.Content);
    }
    
    private async Task EditLastUserBlock()
    {
        if (this.EditLastUserBlockFunc is null)
            return;
        
        if(this.Role is not ChatRole.USER)
            return;
        
        var edit = await this.DialogService.ShowMessageBox(
            T("Edit Message"),
            T("Do you really want to edit this message? In order to edit this message, the AI response will be deleted."),
            T("Yes, remove the AI response and edit it"),
            T("No, keep it"));
        
        if (edit.HasValue && edit.Value)
            await this.EditLastUserBlockFunc(this.Content);
    }
}