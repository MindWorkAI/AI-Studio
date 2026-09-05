using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

/// <summary>
/// The UI component for a chat content block, i.e., for any IContent.
/// </summary>
public partial class ContentBlockComponent : MSGComponentBase
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

    /// <summary>
    /// What the export offers, used both as the label of the export button and as the title of
    /// the save dialog.
    /// </summary>
    /// <remarks>
    /// Only AI blocks can be exported, so this always names something the AI produced. In the chat
    /// that is its response, whereas in an assistant it is the result, and there the user sees no
    /// chat at all. Whoever renders this block knows which of the two it is. Null falls back to
    /// the chat wording.
    /// </remarks>
    [Parameter]
    public string? ExportTitle { get; set; }
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private ILogger<ContentBlockComponent> Logger { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailability { get; init; } = null!;

    private bool HideContent { get; set; }
    private bool hasRenderHash;
    private int lastRenderHash;
    private string cachedMarkdownRenderPlanInput = string.Empty;
    private MarkdownRenderPlan cachedMarkdownRenderPlan = MarkdownRenderPlan.EMPTY;
    private string cachedMessageTablesInput = string.Empty;
    private IReadOnlyList<MessageTable> cachedMessageTables = [];
    private char csvSeparator = ',';
    private ElementReference mathContentContainer;
    private string lastMathRenderSignature = string.Empty;
    private bool hasActiveMathContainer;
    private bool isDisposed;
    private bool showToolTrace;
    private readonly HashSet<int> expandedToolInvocations = [];

    /// <summary>
    /// Whether this block can be exported.
    /// </summary>
    /// <remarks>
    /// We wait for the stream to finish: half an answer is nothing anybody wants in a document,
    /// and waiting keeps us from searching for a text which still grows with every token. Only text
    /// can be completely exported; an image, for example, has no representation our formats could write.
    /// </remarks>
    private bool CanExport => this.Content is { InitialRemoteWait: false, IsStreaming: false } && this.Content.TryGetMarkdownText(out _);

    /// <summary>
    /// The tables this block holds so that the export menu can offer each of them.
    /// </summary>
    /// <remarks>
    /// Cached the same way the Markdown render plan is: reading the tables means parsing the whole
    /// message, and a block re-renders for reasons which have nothing to do with its text, such as
    /// switching the theme, which would parse every message of a long chat again.
    /// </remarks>
    private IReadOnlyList<MessageTable> MessageTables
    {
        get
        {
            if (!this.Content.TryGetMarkdownText(out var markdown))
                return [];

            if (ReferenceEquals(this.cachedMessageTablesInput, markdown) || string.Equals(this.cachedMessageTablesInput, markdown, StringComparison.Ordinal))
                return this.cachedMessageTables;

            this.cachedMessageTablesInput = markdown;
            this.cachedMessageTables = PlainFileExport.ExtractTables(markdown, this.csvSeparator);
            return this.cachedMessageTables;
        }
    }

    /// <summary>
    /// Names one table in the export menu.
    /// </summary>
    /// <remarks>
    /// With a single table the format alone says everything. As soon as an answer holds more than
    /// one, the user has to be able to tell them apart: the heading above a table does that, unless
    /// it is missing or two tables share one, and then we count them.
    /// </remarks>
    private string ExportLabel(MessageTable table)
    {
        var tables = this.MessageTables;
        if (tables.Count < 2)
            return table.Format.ToName();

        var captionIsTelling = !string.IsNullOrWhiteSpace(table.Caption)
                               && tables.Where(entry => entry.Ordinal != table.Ordinal).All(entry => !string.Equals(entry.Caption, table.Caption, StringComparison.Ordinal));

        //
        // The caption is the heading the model wrote, so it already carries the language of the
        // answer and needs no translation of ours. Only the fallback, where we have to count the
        // tables ourselves, is our own wording.
        //
        return captionIsTelling
            ? $"{table.Caption} ({table.Format.ToFileExtension()})"
            : string.Format(this.T("Table {0} ({1})"), table.Ordinal, table.Format.ToFileExtension());
    }

    /// <summary>
    /// What the export offers, falling back to the chat wording when nobody named it.
    /// </summary>
    private string EffectiveExportTitle => this.ExportTitle ?? this.T("Export AI response");

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.RegisterStreamingEvents();
        await base.OnInitializedAsync();

        //
        // Which separator a CSV needs depends on the language, and asking for the language means
        // waiting for the settings. The first render therefore uses the comma we start with; once
        // we know better, we ask for another render. Nobody can have opened the export menu in
        // between, so no file is ever written with the wrong separator.
        //
        var languagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        var separator = CsvWriter.SeparatorFor(languagePlugin.IETFTag);
        if (separator == this.csvSeparator)
            return;

        this.csvSeparator = separator;
        this.cachedMessageTablesInput = string.Empty;
        this.cachedMessageTables = [];
        await this.InvokeAsync(this.StateHasChanged);
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
                hash.Add(text.ToolInvocations.Count);
                hash.Add(text.ToolRuntimeStatus.IsRunning);
                hash.Add(text.ToolRuntimeStatus.Message);
                hash.Add(this.showToolTrace);
                hash.Add(this.expandedToolInvocations.Count);
                foreach (var expandedInvocation in this.expandedToolInvocations.Order())
                    hash.Add(expandedInvocation);
                foreach (var invocation in text.ToolInvocations)
                {
                    hash.Add(invocation.Order);
                    hash.Add(invocation.ToolId);
                    hash.Add(invocation.Status);
                    hash.Add(invocation.StatusMessage);
                    hash.Add(invocation.Result);
                    hash.Add(invocation.JsonResult is not null);
                    hash.Add(invocation.Arguments.Count);
                    foreach (var argument in invocation.Arguments)
                    {
                        hash.Add(argument.Key);
                        hash.Add(argument.Value);
                    }
                }
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

    private bool HasToolTrace => this.Role is ChatRole.AI && this.GetToolInvocations().Count > 0;

    private CodeBlockTheme CodeColorPalette => this.SettingsManager.IsDarkMode ? CodeBlockTheme.Dark : CodeBlockTheme.Default;

    private static Color GetTraceColor(ToolInvocationTraceStatus status) => status switch
    {
        ToolInvocationTraceStatus.SUCCESS => Color.Success,
        ToolInvocationTraceStatus.ERROR => Color.Error,
        ToolInvocationTraceStatus.BLOCKED => Color.Warning,
        _ => Color.Default,
    };

    private string GetTraceStatusText(ToolInvocationTrace trace) => trace.Status switch
    {
        ToolInvocationTraceStatus.SUCCESS => this.T("Executed"),
        ToolInvocationTraceStatus.ERROR => this.T("Failed"),
        ToolInvocationTraceStatus.BLOCKED => this.T("Blocked"),
        _ => this.T("Unknown"),
    };

    private IReadOnlyList<ToolInvocationTrace> GetToolInvocations() => this.Content is ContentText textContent
        ? textContent.ToolInvocations.OrderBy(x => x.Order).ToList()
        : [];

    private string GetToolTraceTooltip()
    {
        var invocations = this.GetToolInvocations();
        return invocations.Count switch
        {
            0 => this.T("No tool calls"),
            1 => string.Format(this.T("Show tool call for {0}"), invocations[0].ToolName),
            _ => string.Format(this.T("Show {0} tool calls"), invocations.Count),
        };
    }

    private void ToggleToolTrace() => this.showToolTrace = !this.showToolTrace;

    private bool IsToolInvocationExpanded(int order) => this.expandedToolInvocations.Contains(order);

    private void ToggleToolInvocation(int order)
    {
        if (!this.expandedToolInvocations.Add(order))
            this.expandedToolInvocations.Remove(order);
    }

    private string GetToolInvocationResult(ToolInvocationTrace invocation) => string.IsNullOrWhiteSpace(invocation.Result)
        ? this.T("No result")
        : invocation.Result;

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

        //
        // Remember what the browser shows only when it really got the call: otherwise, a call which was
        // lost while the connection was down would make us skip the math rendering after the reconnect.
        //
        if (!await this.JsRuntime.TryInvokeVoidAsync(this.CircuitState, CHAT_MATH_SYNC_FUNCTION, this.mathContentContainer, mathRenderSignature))
            return;

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

        await this.JsRuntime.TryInvokeVoidAsync(this.CircuitState, CHAT_MATH_DISPOSE_FUNCTION, this.mathContentContainer);

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
                }
            }
            else if (activeMathBlockFenceType is MathBlockFenceType.DOLLAR && trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_DOLLAR.AsSpan()))
            {
                var (start, end) = TrimLineBreaks(normalizedSpan, mathContentStart, lineStart);
                segments.Add(new(MarkdownRenderSegmentType.MATH_BLOCK, start, end - start));

                markdownSegmentStart = nextLineStart;
                activeMathBlockFenceType = MathBlockFenceType.NONE;
            }
            else if (activeMathBlockFenceType is MathBlockFenceType.BRACKET && trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_BRACKET_CLOSE.AsSpan()))
            {
                var (start, end) = TrimLineBreaks(normalizedSpan, mathContentStart, lineStart);
                segments.Add(new(MarkdownRenderSegmentType.MATH_BLOCK, start, end - start));

                markdownSegmentStart = nextLineStart;
                activeMathBlockFenceType = MathBlockFenceType.NONE;
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
    
    /// <summary>
    /// Exports the entire message.
    /// </summary>
    private async Task ExportDocument(FileExportFormat format)
    {
        try
        {
            //
            // The format itself knows who writes it, so we do not have to keep a list of formats
            // here which would fall out of sync with the one in FileExportFormatExtensions.
            //
            if (format.UsesPandoc())
                await PandocExport.ToDocument(this.RustService, this.PandocAvailability, this.EffectiveExportTitle, format, this.Content);
            else if (this.Content.TryGetMarkdownText(out var markdown))
                await PlainFileExport.ToFile(this.RustService, this.EffectiveExportTitle, format, markdown);
        }
        catch (ArgumentOutOfRangeException e)
        {
            await this.ReportUnknownExportFormat(e, format);
        }
    }

    /// <summary>
    /// Exports one table out of the message, exactly as the menu offered it.
    /// </summary>
    private async Task ExportTable(MessageTable table)
    {
        try
        {
            await PlainFileExport.ToFile(this.RustService, this.EffectiveExportTitle, table.Format, table.Content, table.Caption);
        }
        catch (ArgumentOutOfRangeException e)
        {
            await this.ReportUnknownExportFormat(e, table.Format);
        }
    }

    private async Task ReportUnknownExportFormat(ArgumentOutOfRangeException exception, FileExportFormat format)
    {
        await this.MessageBus.SendError(new(Icons.Material.Filled.Error, string.Format(this.T("Failed to export this message, because the file format '{0}' is unknown."), format)));
        this.Logger.LogError(exception, "Failed to export the content, because no exporter writes the format {ExportFormat}.", format);
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
        this.Content.FileAttachments = [.. result];
    }

    protected override async ValueTask DisposeResourcesAsync()
    {
        if (this.isDisposed)
            return;

        this.isDisposed = true;

        //
        // Our handlers close over this component, while the content belongs to the chat thread and
        // outlives us. We only detach what is still ours, though: when this content is streaming
        // again, another component has registered its own handlers in the meantime.
        //
        if (this.Content.StreamingDone == this.AfterStreaming)
            this.Content.ResetStreamingHandlers();

        await this.DisposeMathContainerIfNeededAsync();
    }
}