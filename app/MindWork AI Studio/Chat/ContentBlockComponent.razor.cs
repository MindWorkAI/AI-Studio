using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

/// <summary>
/// The UI component for a chat content block, i.e., for any IContent.
/// </summary>
public partial class ContentBlockComponent : MSGComponentBase, IAsyncDisposable
{
    private const string CHAT_MATH_SYNC_FUNCTION = "chatMath.syncContainer";
    private const string CHAT_MATH_DISPOSE_FUNCTION = "chatMath.disposeContainer";
    private const string HTML_START_TAG = "<";
    private const string HTML_END_TAG = "</";
    private const string HTML_SELF_CLOSING_TAG = "/>";
    private const string CODE_FENCE_MARKER_BACKTICK = "```";
    private const string CODE_FENCE_MARKER_TILDE = "~~~";
    private const string MATH_BLOCK_MARKER_DOLLAR = "$$";
    private const string MATH_BLOCK_MARKER_BRACKET_OPEN = """\[""";
    private const string MATH_BLOCK_MARKER_BRACKET_CLOSE = """\]""";
    private const string HTML_CODE_FENCE_PREFIX = "```html";

    private static readonly string[] HTML_TAG_MARKERS =
    [
        "<!doctype",
        "<html",
        "<head",
        "<body",
        "<style",
        "<script",
        "<iframe",
        "<svg",
    ];

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

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    private bool HideContent { get; set; }
    private bool hasRenderHash;
    private int lastRenderHash;
    private string cachedMarkdownRenderPlanInput = string.Empty;
    private MarkdownRenderPlan cachedMarkdownRenderPlan = MarkdownRenderPlan.EMPTY;
    private ElementReference mathContentContainer;
    private string lastMathRenderSignature = string.Empty;
    private bool hasActiveMathContainer;
    private bool isDisposed;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.RegisterStreamingEvents();
        await base.OnInitializedAsync();
    }

    protected override Task OnParametersSetAsync()
    {
        this.RegisterStreamingEvents();
        return base.OnParametersSetAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await this.SyncMathRenderIfNeededAsync();
        await base.OnAfterRenderAsync(firstRender);
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        var currentRenderHash = this.CreateRenderHash();
        if (!this.hasRenderHash || currentRenderHash != this.lastRenderHash)
        {
            this.lastRenderHash = currentRenderHash;
            this.hasRenderHash = true;
            return true;
        }

        return false;
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

    private void RegisterStreamingEvents()
    {
        this.Content.StreamingDone = this.AfterStreaming;
        this.Content.StreamingEvent = () => this.InvokeAsync(this.StateHasChanged);
    }

    private int CreateRenderHash()
    {
        var hash = new HashCode();
        hash.Add(this.Role);
        hash.Add(this.Type);
        hash.Add(this.Time);
        hash.Add(this.Class);
        hash.Add(this.IsLastContentBlock);
        hash.Add(this.IsSecondToLastBlock);
        hash.Add(this.HideContent);
        hash.Add(this.SettingsManager.IsDarkMode);
        hash.Add(this.RegenerateEnabled());
        hash.Add(this.Content.InitialRemoteWait);
        hash.Add(this.Content.IsStreaming);
        hash.Add(this.Content.FileAttachments.Count);
        hash.Add(this.Content.Sources.Count);

        switch (this.Content)
        {
            case ContentText text:
                var textValue = text.Text;
                hash.Add(textValue.Length);
                hash.Add(textValue.GetHashCode(StringComparison.Ordinal));
                hash.Add(text.Sources.Count);
                break;

            case ContentImage image:
                hash.Add(image.SourceType);
                hash.Add(image.Source);
                break;
        }

        return hash.ToHashCode();
    }

    #endregion
    
    private string CardClasses => $"my-2 rounded-lg {this.Class}";

    private CodeBlockTheme CodeColorPalette => this.SettingsManager.IsDarkMode ? CodeBlockTheme.Dark : CodeBlockTheme.Default;

    private MudMarkdownStyling MarkdownStyling => new()
    {
        CodeBlock = { Theme = this.CodeColorPalette },
    };

    private MarkdownRenderPlan GetMarkdownRenderPlan(string text)
    {
        if (ReferenceEquals(this.cachedMarkdownRenderPlanInput, text) || string.Equals(this.cachedMarkdownRenderPlanInput, text, StringComparison.Ordinal))
            return this.cachedMarkdownRenderPlan;

        this.cachedMarkdownRenderPlanInput = text;
        this.cachedMarkdownRenderPlan = BuildMarkdownRenderPlan(text);
        return this.cachedMarkdownRenderPlan;
    }

    private async Task SyncMathRenderIfNeededAsync()
    {
        if (this.isDisposed)
            return;

        if (!this.TryGetCompletedMathRenderState(out var mathRenderSignature))
        {
            await this.DisposeMathContainerIfNeededAsync();
            return;
        }

        if (string.Equals(this.lastMathRenderSignature, mathRenderSignature, StringComparison.Ordinal))
            return;

        await this.JsRuntime.InvokeVoidAsync(CHAT_MATH_SYNC_FUNCTION, this.mathContentContainer, mathRenderSignature);
        this.lastMathRenderSignature = mathRenderSignature;
        this.hasActiveMathContainer = true;
    }

    private async Task DisposeMathContainerIfNeededAsync()
    {
        if (!this.hasActiveMathContainer)
        {
            this.lastMathRenderSignature = string.Empty;
            return;
        }

        try
        {
            await this.JsRuntime.InvokeVoidAsync(CHAT_MATH_DISPOSE_FUNCTION, this.mathContentContainer);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        this.hasActiveMathContainer = false;
        this.lastMathRenderSignature = string.Empty;
    }

    private bool TryGetCompletedMathRenderState(out string mathRenderSignature)
    {
        mathRenderSignature = string.Empty;

        if (this.HideContent || this.Type is not ContentType.TEXT || this.Content.IsStreaming || this.Content is not ContentText textContent || textContent.InitialRemoteWait)
            return false;

        var renderPlan = this.GetMarkdownRenderPlan(textContent.Text);
        mathRenderSignature = CreateMathRenderSignature(renderPlan);
        return !string.IsNullOrEmpty(mathRenderSignature);
    }

    private static string CreateMathRenderSignature(MarkdownRenderPlan renderPlan)
    {
        var hash = new HashCode();
        var mathSegmentCount = 0;

        foreach (var segment in renderPlan.Segments)
        {
            if (segment.Type is not MarkdownRenderSegmentType.MATH_BLOCK)
                continue;

            mathSegmentCount++;
            hash.Add(segment.Start);
            hash.Add(segment.Length);
            hash.Add(segment.GetContent(renderPlan.Source).GetHashCode(StringComparison.Ordinal));
        }

        return mathSegmentCount == 0
            ? string.Empty
            : $"{mathSegmentCount}:{hash.ToHashCode()}";
    }

    private static MarkdownRenderPlan BuildMarkdownRenderPlan(string text)
    {
        var normalized = NormalizeMarkdownForRendering(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return MarkdownRenderPlan.EMPTY;

        var normalizedSpan = normalized.AsSpan();
        var segments = new List<MarkdownRenderSegment>();
        var activeCodeFenceMarker = '\0';
        var activeMathBlockFenceType = MathBlockFenceType.NONE;
        var markdownSegmentStart = 0;
        var mathContentStart = 0;

        for (var lineStart = 0; lineStart < normalizedSpan.Length;)
        {
            var lineEnd = lineStart;
            while (lineEnd < normalizedSpan.Length && normalizedSpan[lineEnd] is not '\r' and not '\n')
                lineEnd++;

            var nextLineStart = lineEnd;
            if (nextLineStart < normalizedSpan.Length)
            {
                if (normalizedSpan[nextLineStart] == '\r')
                    nextLineStart++;

                if (nextLineStart < normalizedSpan.Length && normalizedSpan[nextLineStart] == '\n')
                    nextLineStart++;
            }

            var trimmedLine = TrimWhitespace(normalizedSpan[lineStart..lineEnd]);
            if (activeMathBlockFenceType is MathBlockFenceType.NONE && TryUpdateCodeFenceState(trimmedLine, ref activeCodeFenceMarker))
            {
                lineStart = nextLineStart;
                continue;
            }

            if (activeCodeFenceMarker != '\0')
            {
                lineStart = nextLineStart;
                continue;
            }

            if (activeMathBlockFenceType is MathBlockFenceType.NONE)
            {
                if (trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_DOLLAR.AsSpan()))
                {
                    AddMarkdownSegment(markdownSegmentStart, lineStart);
                    mathContentStart = nextLineStart;
                    activeMathBlockFenceType = MathBlockFenceType.DOLLAR;
                    lineStart = nextLineStart;
                    continue;
                }

                if (trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_BRACKET_OPEN.AsSpan()))
                {
                    AddMarkdownSegment(markdownSegmentStart, lineStart);
                    mathContentStart = nextLineStart;
                    activeMathBlockFenceType = MathBlockFenceType.BRACKET;
                    lineStart = nextLineStart;
                    continue;
                }
            }
            else if (activeMathBlockFenceType is MathBlockFenceType.DOLLAR && trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_DOLLAR.AsSpan()))
            {
                var (start, end) = TrimLineBreaks(normalizedSpan, mathContentStart, lineStart);
                segments.Add(new(MarkdownRenderSegmentType.MATH_BLOCK, start, end - start));

                markdownSegmentStart = nextLineStart;
                activeMathBlockFenceType = MathBlockFenceType.NONE;
                lineStart = nextLineStart;
                continue;
            }
            else if (activeMathBlockFenceType is MathBlockFenceType.BRACKET && trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_BRACKET_CLOSE.AsSpan()))
            {
                var (start, end) = TrimLineBreaks(normalizedSpan, mathContentStart, lineStart);
                segments.Add(new(MarkdownRenderSegmentType.MATH_BLOCK, start, end - start));

                markdownSegmentStart = nextLineStart;
                activeMathBlockFenceType = MathBlockFenceType.NONE;
                lineStart = nextLineStart;
                continue;
            }

            lineStart = nextLineStart;
        }

        if (activeMathBlockFenceType is not MathBlockFenceType.NONE)
            return new(normalized, [new(MarkdownRenderSegmentType.MARKDOWN, 0, normalized.Length)]);

        AddMarkdownSegment(markdownSegmentStart, normalized.Length);
        if (segments.Count == 0)
            segments.Add(new(MarkdownRenderSegmentType.MARKDOWN, 0, normalized.Length));

        return new(normalized, segments);

        void AddMarkdownSegment(int start, int end)
        {
            if (end <= start)
                return;

            segments.Add(new(MarkdownRenderSegmentType.MARKDOWN, start, end - start));
        }
    }

    private static string NormalizeMarkdownForRendering(string text)
    {
        var textWithoutThinkTags = text.RemoveThinkTags();
        var trimmed = TrimWhitespace(textWithoutThinkTags.AsSpan());
        if (trimmed.IsEmpty)
            return string.Empty;

        var cleaned = trimmed.Length == textWithoutThinkTags.Length
            ? textWithoutThinkTags
            : trimmed.ToString();

        if (cleaned.Contains(CODE_FENCE_MARKER_BACKTICK, StringComparison.Ordinal))
            return cleaned;

        if (LooksLikeRawHtml(cleaned))
            return $"{HTML_CODE_FENCE_PREFIX}{Environment.NewLine}{cleaned}{Environment.NewLine}{CODE_FENCE_MARKER_BACKTICK}";

        return cleaned;
    }

    private static bool LooksLikeRawHtml(string text)
    {
        var content = text.AsSpan();
        var start = 0;
        while (start < content.Length && char.IsWhiteSpace(content[start]))
            start++;

        content = content[start..];
        if (!content.StartsWith(HTML_START_TAG.AsSpan(), StringComparison.Ordinal))
            return false;

        foreach (var marker in HTML_TAG_MARKERS)
            if (content.IndexOf(marker.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        return content.IndexOf(HTML_END_TAG.AsSpan(), StringComparison.Ordinal) >= 0
               || content.IndexOf(HTML_SELF_CLOSING_TAG.AsSpan(), StringComparison.Ordinal) >= 0;
    }

    private static bool TryUpdateCodeFenceState(ReadOnlySpan<char> trimmedLine, ref char activeCodeFenceMarker)
    {
        var fenceMarker = '\0';
        if (trimmedLine.StartsWith(CODE_FENCE_MARKER_BACKTICK.AsSpan(), StringComparison.Ordinal))
            fenceMarker = '`';
        else if (trimmedLine.StartsWith(CODE_FENCE_MARKER_TILDE.AsSpan(), StringComparison.Ordinal))
            fenceMarker = '~';

        if (fenceMarker == '\0')
            return false;

        activeCodeFenceMarker = activeCodeFenceMarker == '\0'
            ? fenceMarker
            : activeCodeFenceMarker == fenceMarker
                ? '\0'
                : activeCodeFenceMarker;

        return true;
    }

    private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> text)
    {
        var start = 0;
        var end = text.Length - 1;

        while (start < text.Length && char.IsWhiteSpace(text[start]))
            start++;

        while (end >= start && char.IsWhiteSpace(text[end]))
            end--;

        return start > end ? ReadOnlySpan<char>.Empty : text[start..(end + 1)];
    }

    private static (int Start, int End) TrimLineBreaks(ReadOnlySpan<char> text, int start, int end)
    {
        while (start < end && text[start] is '\r' or '\n')
            start++;

        while (end > start && text[end - 1] is '\r' or '\n')
            end--;

        return (start, end);
    }

    private enum MarkdownRenderSegmentType
    {
        MARKDOWN,
        MATH_BLOCK,
    }

    private enum MathBlockFenceType
    {
        NONE,
        DOLLAR,
        BRACKET,
    }

    private sealed record MarkdownRenderPlan(string Source, IReadOnlyList<MarkdownRenderSegment> Segments)
    {
        public static readonly MarkdownRenderPlan EMPTY = new(string.Empty, []);
    }

    private sealed class MarkdownRenderSegment(MarkdownRenderSegmentType type, int start, int length)
    {
        private string? cachedContent;

        public MarkdownRenderSegmentType Type { get; } = type;

        public int Start { get; } = start;

        public int Length { get; } = length;

        public int RenderKey { get; } = HashCode.Combine(type, start, length);

        public string GetContent(string source)
        {
            if (this.cachedContent is not null)
                return this.cachedContent;

            this.cachedContent = this.Start == 0 && this.Length == source.Length
                ? source
                : source.Substring(this.Start, this.Length);

            return this.cachedContent;
        }
    }
    
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
    
    private async Task ExportToWord()
    {
        await PandocExport.ToMicrosoftWord(this.RustService, this.DialogService, T("Export Chat to Microsoft Word"), this.Content);
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
    
    private async Task OpenAttachmentsDialog()
    {
        var result = await ReviewAttachmentsDialog.OpenDialogAsync(this.DialogService, this.Content.FileAttachments.ToHashSet());
        this.Content.FileAttachments = result.ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (this.isDisposed)
            return;

        this.isDisposed = true;
        await this.DisposeMathContainerIfNeededAsync();
        this.Dispose();
    }
}
