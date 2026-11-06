using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Finds the words of the chat search in the open chat: it marks every match in the messages, counts
/// them, and steps from one to the next.
/// </summary>
/// <remarks>
/// <para>
/// It shows while the search holds a text, and listens to the search itself, so the chat does not
/// render anew with every key the user types. The marks come from the script in app.js, which paints
/// them without changing the messages, see <c>setChatFind</c>.
/// </para>
/// <para>
/// It never scrolls by itself: typing changes the marks only, and the arrows step to a match. Where a
/// chat stands when it opens is up to the chat, which shows its last match when it holds one, see
/// <c>positionOpenedChat</c> in app.js.
/// </para>
/// </remarks>
public partial class ChatFindBar : MSGComponentBase
{
    /// <summary>
    /// The search of the sidebar, whose words this chat looks for.
    /// </summary>
    [Parameter]
    public WorkspaceSearch? Search { get; set; }

    /// <summary>
    /// The ID of the open chat. Another chat finds anew.
    /// </summary>
    [Parameter]
    public Guid ChatId { get; set; }

    /// <summary>
    /// The element which holds the messages of the open chat. It exists once the chat rendered.
    /// </summary>
    [Parameter]
    public Func<ElementReference>? Messages { get; set; }

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private ILogger<ChatFindBar> Logger { get; init; } = null!;

    private DotNetObjectReference<ChatFindBar>? dotNetReference;

    /// <summary>
    /// The chat whose matches the script marks, or the empty ID.
    /// </summary>
    private Guid findChatId;

    /// <summary>
    /// The search text whose words the script marks.
    /// </summary>
    private string findText = string.Empty;

    private int matchCount;

    /// <summary>
    /// The current match, counted from 0, or -1 while there is none.
    /// </summary>
    private int currentMatch = -1;

    /// <summary>
    /// Whether the script could find at all. A browser which still runs a cached app.js from an older
    /// version lacks its functions; the row then hides, rather than claim the chat holds no match.
    /// </summary>
    private bool canFind = true;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        if (this.Search is not null)
            this.Search.TextChanged += this.OnSearchTextChanged;

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        //
        // The chat renders often, such as while an answer is written. The script follows such changes
        // by itself, so only another search text or another chat needs it to find anew:
        //
        var searchText = this.Search?.Text ?? string.Empty;
        if (searchText == this.findText && this.ChatId == this.findChatId)
            return;

        // Before the chat rendered its messages, there is nothing to find in yet; the next render tries again:
        if (this.MessagesElement is null)
            return;

        this.findText = searchText;
        this.findChatId = this.ChatId;
        await this.FindAsync();
    }

    #endregion

    private bool IsSearching => this.canFind && !string.IsNullOrWhiteSpace(this.Search?.Text);

    private string Words => string.Join(' ', SearchText.ParseTerms(this.Search?.Text));

    /// <summary>
    /// What the row says: how many matches this chat holds, and which one is the current one. A dash
    /// stands in for the current one while there is none, such as for a match which showed up after the
    /// chat opened: the user sees that the chat holds matches and that none is selected yet.
    /// </summary>
    private string FindText => this.matchCount == 0
        ? string.Format(T("“{0}” is not in this chat"), this.Words)
        : string.Format(T("“{0}” in this chat: {1} of {2}"), this.Words, this.currentMatch < 0 ? "–" : (this.currentMatch + 1).ToString(), this.matchCount);

    /// <summary>
    /// The element of the messages, once the chat rendered it.
    /// </summary>
    private ElementReference? MessagesElement => this.Messages?.Invoke() is { Id: not null } messages ? messages : null;

    private void OnSearchTextChanged() => this.InvokeAsync(this.StateHasChanged).Observe($"{nameof(ChatFindBar)}: following the search");

    /// <summary>
    /// Marks the words of the search in the messages, or takes the marks away when the search ended.
    /// </summary>
    private async Task FindAsync()
    {
        if (this.MessagesElement is not { } messages)
            return;

        var terms = SearchText.ParseTerms(this.findText);

        //
        // Finding is a convenience. An exception from here would end the whole circuit, though, and a
        // browser which still runs a cached app.js from an older version lacks the functions:
        //
        try
        {
            if (terms.Count == 0)
            {
                await this.JsRuntime.InvokeVoidAsync("clearChatFind", messages);
                this.SetMatches(0, -1);
                return;
            }

            this.dotNetReference ??= DotNetObjectReference.Create(this);
            var result = await this.JsRuntime.InvokeAsync<int[]>("setChatFind", messages, terms, this.dotNetReference);
            this.SetMatches(result[0], result[1]);
        }
        catch (JSDisconnectedException)
        {
            // Nobody is left to see the chat.
        }
        catch (JSException ex)
        {
            this.Logger.LogWarning(ex, "Failed to find the words of the search in the chat.");
            this.canFind = false;
            this.StateHasChanged();
        }
    }

    private async Task StepAsync(int direction)
    {
        if (this.MessagesElement is not { } messages)
            return;

        try
        {
            var result = await this.JsRuntime.InvokeAsync<int[]>("stepChatFind", messages, direction);
            this.SetMatches(result[0], result[1]);
        }
        catch (JSDisconnectedException)
        {
            // Nobody is left to see the chat.
        }
        catch (JSException ex)
        {
            this.Logger.LogWarning(ex, "Failed to step to a match in the chat.");
        }
    }

    /// <summary>
    /// Learns from the script that the messages changed, such as while an answer is written.
    /// </summary>
    /// <param name="count">How many matches there are now.</param>
    /// <param name="current">The current match, counted from 0, or -1.</param>
    [JSInvokable]
    public Task MatchesChangedAsync(int count, int current) => this.InvokeAsync(() => this.SetMatches(count, current));

    private void SetMatches(int count, int current)
    {
        if (count == this.matchCount && current == this.currentMatch)
            return;

        this.matchCount = count;
        this.currentMatch = current;
        this.StateHasChanged();
    }

    /// <summary>
    /// Takes the marks away when the chat goes, such as when the user leaves the chat page.
    /// </summary>
    private async Task ClearAsync(ElementReference messages)
    {
        try
        {
            await this.JsRuntime.InvokeVoidAsync("clearChatFind", messages);
        }
        catch (JSDisconnectedException)
        {
            // Nobody is left to see the chat.
        }
        catch (JSException ex)
        {
            this.Logger.LogWarning(ex, "Failed to take the marks of the search away.");
        }
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        if (this.Search is not null)
            this.Search.TextChanged -= this.OnSearchTextChanged;

        if (this.MessagesElement is { } messages)
            this.ClearAsync(messages).Observe($"{nameof(ChatFindBar)}: taking the marks away");

        this.dotNetReference?.Dispose();
        base.DisposeResources();
    }

    #endregion
}