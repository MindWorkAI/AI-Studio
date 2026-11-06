using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Media;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class Workspaces : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<Workspaces> Logger { get; init; } = null!;

    [Inject]
    private AIJobService AIJobService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Parameter]
    public ChatThread? CurrentChatThread { get; set; }
    
    [Parameter]
    public EventCallback<ChatThread> CurrentChatThreadChanged { get; set; }

    /// <summary>
    /// Asks the page to start a new chat in the workspace with the given ID.
    /// </summary>
    /// <remarks>
    /// The page hands the request to its chat component. A message on the bus would not do: the bus
    /// also serves the chat components of windows which were reloaded long ago, and each of them would
    /// start a chat of its own, storing what it still holds on its way.
    /// </remarks>
    [Parameter]
    public EventCallback<Guid> StartNewChatRequested { get; set; }

    /// <summary>
    /// The search, whose field sits in the header of the sidebar. While it holds a text, the list shows
    /// the chats it found instead of all workspaces and chats.
    /// </summary>
    [Parameter]
    public WorkspaceSearch? Search { get; set; }

    private const Placement WORKSPACE_ITEM_TOOLTIP_PLACEMENT = Placement.Bottom;

    /// <summary>
    /// The pause in typing after which the list also searches the contents of the chats.
    /// </summary>
    private static readonly TimeSpan CONTENT_SEARCH_DELAY = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Cuts a name which is too long for its row, instead of breaking it into several lines.
    /// </summary>
    private const string NAME_STYLE = "min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;";

    /// <summary>
    /// The hover effect and pointer of a clickable list item. The lists are read-only, because the
    /// open chat is marked by the classes below rather than by a selection, and read-only lists
    /// may leave these out.
    /// </summary>
    private const string CLICKABLE_ITEM_CLASSES = "mud-list-item-clickable";

    /// <summary>
    /// The mark of the open chat: MudBlazor's own look of a selected item, plus the class the list
    /// scrolls to.
    /// </summary>
    private const string CURRENT_ITEM_CLASSES = "is-current mud-selected-item mud-primary-text mud-primary-hover";

    private readonly SemaphoreSlim treeLoadingSemaphore = new(1, 1);
    private readonly HashSet<Guid> loadingWorkspaceChatLists = [];
    private readonly HashSet<Guid> expandedWorkspaceIds = [];

    private IReadOnlyList<WorkspaceTreeWorkspace> workspaces = [];
    private IReadOnlyList<WorkspaceTreeChat> temporaryChats = [];

    /// <summary>
    /// Whether the list shows the workspaces, as it did when it was built last.
    /// </summary>
    /// <remarks>
    /// While workspaces are switched off, the list holds no workspace at all, so that neither the search
    /// nor stepping through its results finds one. The chats in the workspaces stay where they are.
    /// </remarks>
    private bool areWorkspacesShown = true;

    /// <summary>
    /// Whether a new chat can start in a workspace, which needs stored chats besides workspaces.
    /// </summary>
    private bool CanChatsGoIntoWorkspaces => this.SettingsManager.ConfigurationData.Workspace.CanChatsGoIntoWorkspaces;

    /// <summary>
    /// Whether the list shows what a search found.
    /// </summary>
    /// <remarks>
    /// The list keeps showing all workspaces and chats in their usual places, so that nothing jumps while
    /// the user types. A search only marks what it found: the words in the names, and the icon of a chat
    /// found in its messages. Differs from <see cref="this.HasSearchQuery"/> while the first step of a
    /// search still runs: the list then keeps showing what it showed before.
    /// </remarks>
    private bool isShowingSearchResults;

    /// <summary>
    /// The words of the search whose results the list shows, which the names mark.
    /// </summary>
    private IReadOnlyList<string> searchTerms = [];

    /// <summary>
    /// The workspaces which the search found by their own names.
    /// </summary>
    private readonly HashSet<Guid> nameMatchedWorkspaceIds = [];

    /// <summary>
    /// The chats which the search found, each by what it holds itself: its name, messages, attached files,
    /// and sources.
    /// </summary>
    private readonly HashSet<Guid> searchResultChatIds = [];

    /// <summary>
    /// For the chats which hold words of the search in their messages, attached files, or sources, where
    /// they hold the first one, which shows when the pointer rests on the chat.
    /// </summary>
    private readonly Dictionary<Guid, ChatSearchPassage> chatPassages = [];

    /// <summary>
    /// The chats which the search found by their own names, so that their messages, attached files, or
    /// sources only hold the words as well, rather than being why the chat was found.
    /// </summary>
    private readonly HashSet<Guid> nameMatchedChatIds = [];

    /// <summary>
    /// The workspaces the user expanded or collapsed while the list shows the results of a search. This
    /// lasts until the search ends and leaves the state of the whole list alone.
    /// </summary>
    private readonly Dictionary<Guid, bool> searchExpandedWorkspaces = [];

    /// <summary>
    /// The result of the search which the user stepped to, a workspace or a chat, or null.
    /// </summary>
    private Guid? currentSearchResultId;

    /// <summary>
    /// Whether the list still has to scroll the current result of the search into view.
    /// </summary>
    private bool isScrollToSearchResultPending;

    private ElementReference listElement;

    /// <summary>
    /// The markers right before the titles of the sections, where these titles sit while they do
    /// not stick to the top of the list.
    /// </summary>
    private ElementReference workspacesSectionStart;

    /// <inheritdoc cref="this.workspacesSectionStart"/>
    private ElementReference temporaryChatsSectionStart;

    /// <summary>
    /// The start of a section opened from its title, which the list reveals once it shows the content
    /// of the section, or null. See <see cref="this.TryRevealSectionAsync"/>.
    /// </summary>
    private ElementReference? sectionToReveal;

    /// <summary>
    /// Whether the script already watches which titles of the sections stick to the top of the list.
    /// </summary>
    private bool isWatchingStuckSections;

    /// <summary>
    /// The chat or workspace whose menu is open, or the empty ID.
    /// </summary>
    private Guid itemIdWithOpenMenu;

    /// <summary>
    /// The chat which was open when the list last looked, together with its workspace.
    /// </summary>
    private (Guid ChatId, Guid WorkspaceId) lastCurrentChat;

    /// <summary>
    /// Whether the list still has to scroll the open chat into view.
    /// </summary>
    private bool isScrollToCurrentChatPending;

    private CancellationTokenSource? prefetchCancellationTokenSource;
    private CancellationTokenSource? searchCancellationTokenSource;
    private bool isInitialLoading = true;
    private bool isDisposed;
    private long searchRevision;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnMediaImportStateChanged;
        if (this.Search is not null)
        {
            this.Search.TextChanged += this.OnSearchTextChanged;
            this.Search.StepRequested += this.OnSearchStepRequested;
        }

        await base.OnInitializedAsync();
        this.ApplyFilters([], [ Event.AI_JOB_CHANGED, Event.AI_JOB_FINISHED, Event.CHAT_GENERATION_CHANGED, Event.WORKSPACE_CREATED, Event.CONFIGURATION_CHANGED ]);
        this.LoadTreeItemsAsync(startPrefetch: true).Observe($"{nameof(Workspaces)}: loading the workspace tree");
    }

    /// <summary>
    /// Follows the open chat: expands its workspace and has the list scroll to it.
    /// </summary>
    /// <remarks>
    /// Only a chat which differs from the last one counts, or the same chat in another workspace.
    /// Every other render leaves alone what the user expanded, collapsed, or scrolled to.
    /// </remarks>
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        var currentChat = this.CurrentChatThread is { } chat ? (chat.ChatId, chat.WorkspaceId) : default;
        if (currentChat == this.lastCurrentChat)
            return;

        this.lastCurrentChat = currentChat;
        this.isScrollToCurrentChatPending = currentChat.ChatId != Guid.Empty;
        if (currentChat.WorkspaceId != Guid.Empty && this.expandedWorkspaceIds.Add(currentChat.WorkspaceId))
            await this.EnsureWorkspaceChatsLoadedAsync(currentChat.WorkspaceId);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        //
        // A section opened from its title may unfold out of sight, above the visible part of the
        // list while its title sticks, or with its open chat further below. The script scrolls only
        // in these cases, so a section opened in plain sight leaves the list where it is:
        //
        if (this.sectionToReveal is { } sectionStart)
        {
            this.sectionToReveal = null;
            await this.TryRevealSectionAsync(sectionStart);
        }

        //
        // Only a title which sticks, or whose section hides the open chat, does something on a click,
        // and only such a title takes the pointer. And a title carries the mark of the open chat while
        // that chat is out of sight above or below, as a collapsed one does. CSS can tell neither, so
        // the script marks such titles, as soon as the list exists:
        //
        if (!this.isWatchingStuckSections && !this.isInitialLoading)
        {
            this.isWatchingStuckSections = true;
            try
            {
                await this.JsRuntime.InvokeVoidAsync("watchStuckSections", this.listElement);
            }
            catch (JSDisconnectedException)
            {
                // Nobody is left to see the list.
            }
            catch (JSException ex)
            {
                this.Logger.LogWarning(ex, "Failed to watch the titles of the sections.");
            }
        }

        //
        // The current result of the search scrolls into view when it is out of sight, after the user
        // stepped to it, or when the search found a new first one:
        //
        if (this.isScrollToSearchResultPending && !this.isInitialLoading)
        {
            this.isScrollToSearchResultPending = false;
            try
            {
                await this.JsRuntime.InvokeVoidAsync("scrollFirstMatchIntoView", this.listElement, ".is-search-current");
            }
            catch (JSDisconnectedException)
            {
                // Nobody is left to see the list.
            }
            catch (JSException ex)
            {
                this.Logger.LogWarning(ex, "Failed to scroll the current result of the search into view.");
            }
        }

        //
        // The open chat may not be in the list yet: its workspace may still be loading, or the chat
        // is new and reaches the disk only with its first message. Until then, it stays pending.
        //
        if (!this.isScrollToCurrentChatPending || this.isInitialLoading || !this.IsCurrentChatMarked())
            return;

        this.isScrollToCurrentChatPending = false;

        //
        // Scrolling is a convenience. An exception from here would end the whole circuit, though,
        // and a browser which still runs a cached app.js from an older version lacks the function.
        //
        try
        {
            await this.JsRuntime.InvokeVoidAsync("scrollFirstMatchIntoView", this.listElement, ".is-current");
        }
        catch (JSDisconnectedException)
        {
            // Nobody is left to see the list.
        }
        catch (JSException ex)
        {
            this.Logger.LogWarning(ex, "Failed to scroll the open chat into view.");
        }
    }

    #endregion

    /// <summary>
    /// Loads the workspaces and chats into the list.
    /// </summary>
    /// <param name="startPrefetch">Whether to load the chats of all workspaces in the background afterward.</param>
    /// <param name="forceReload">Whether to read everything from the disk anew.</param>
    /// <param name="searchAgain">Whether a running search looks at the list anew, which may have changed under it.</param>
    private async Task LoadTreeItemsAsync(bool startPrefetch = true, bool forceReload = false, bool searchAgain = true)
    {
        await this.treeLoadingSemaphore.WaitAsync();
        try
        {
            if (this.isDisposed)
                return;

            if (forceReload)
                await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();

            var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
            this.BuildTreeItems(snapshot);
            this.isInitialLoading = false;
        }
        finally
        {
            this.treeLoadingSemaphore.Release();
        }

        await this.SafeStateHasChanged();

        //
        // The list changed under a running search, so the search runs anew. In the background, since
        // reading the contents of the chats may take a while, and whoever reloaded the list should
        // not wait for that:
        //
        if (searchAgain && this.HasSearchQuery)
            this.SearchAsync(isTyping: false).Observe($"{nameof(Workspaces)}: searching the chats anew");

        if (startPrefetch)
            await this.StartPrefetchAsync();
    }

    private bool HasSearchQuery => !string.IsNullOrWhiteSpace(this.Search?.Text);

    private string GetAddChatToWorkspaceTooltip(string workspaceName) => string.Format(T("Start a new chat in workspace '{0}'"), workspaceName);

    private async Task<Func<string?, string?>> CreateWorkspaceNameValidationAsync(Guid excludedWorkspaceId = default, string? originalWorkspaceName = null)
    {
        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        return workspaceName =>
        {
            var normalizedWorkspaceName = WorkspaceBehaviour.NormalizeWorkspaceName(workspaceName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedWorkspaceName))
                return null;

            if (!string.IsNullOrWhiteSpace(originalWorkspaceName) &&
                string.Equals(WorkspaceBehaviour.NormalizeWorkspaceName(originalWorkspaceName), normalizedWorkspaceName, StringComparison.OrdinalIgnoreCase))
                return null;

            var nameExists = snapshot.Workspaces.Any(workspace =>
                workspace.WorkspaceId != excludedWorkspaceId &&
                string.Equals(WorkspaceBehaviour.NormalizeWorkspaceName(workspace.Name), normalizedWorkspaceName, StringComparison.OrdinalIgnoreCase));

            return nameExists ? T("There is already a workspace with this name. Please choose a different name.") : null;
        };
    }

    private void BuildTreeItems(WorkspaceTreeCacheSnapshot snapshot)
    {
        this.areWorkspacesShown = this.SettingsManager.ConfigurationData.Workspace.UsesWorkspaces;
        this.workspaces = !this.areWorkspacesShown ? [] : snapshot.Workspaces
            .Select(workspace => workspace with { Chats = workspace.Chats.OrderByDescending(x => x.LastEditTime).ToList() })
            .ToList();

        this.temporaryChats = snapshot.TemporaryChats.OrderByDescending(x => x.LastEditTime).ToList();
    }

    /// <summary>
    /// Marks what a search found in the list, which keeps showing all workspaces and chats.
    /// </summary>
    /// <remarks>
    /// The result the user stepped to stays the current one, as long as the search still finds it.
    /// Otherwise, the first result becomes the current one, and the list scrolls to it when it is out
    /// of sight; it opens no workspace for it, though, so that nothing moves while the user types. Then
    /// the field learns how many results there are.
    /// </remarks>
    /// <param name="snapshot">What the search found.</param>
    /// <param name="terms">The words of the search.</param>
    private void ShowSearchResults(WorkspaceSearchSnapshot snapshot, IReadOnlyList<string> terms)
    {
        this.isShowingSearchResults = true;
        this.searchTerms = terms;

        this.nameMatchedWorkspaceIds.Clear();
        this.searchResultChatIds.Clear();
        this.chatPassages.Clear();
        this.nameMatchedChatIds.Clear();
        foreach (var workspace in snapshot.Workspaces)
        {
            if (workspace.NameMatched)
                this.nameMatchedWorkspaceIds.Add(workspace.WorkspaceId);

            foreach (var result in workspace.Chats)
                this.AddSearchResult(result);
        }

        foreach (var result in snapshot.TemporaryChats)
            this.AddSearchResult(result);

        var results = this.GetSearchResultsInOrder();
        if (this.currentSearchResultId is not { } current || !results.Contains(current))
        {
            this.currentSearchResultId = results.Count > 0 ? results[0] : null;
            this.isScrollToSearchResultPending = this.currentSearchResultId is not null;
        }

        this.PublishSearchResults(results);
    }

    /// <summary>
    /// Adds a chat which the search found, see <see cref="this.searchResultChatIds"/>.
    /// </summary>
    private void AddSearchResult(WorkspaceSearchResult result)
    {
        this.searchResultChatIds.Add(result.Chat.ChatId);
        if (result.Passage is { } passage)
            this.chatPassages[result.Chat.ChatId] = passage;

        if (result.NameMatched)
            this.nameMatchedChatIds.Add(result.Chat.ChatId);
    }

    /// <summary>
    /// Takes the marks of a search from the list, which shows all workspaces and chats as before.
    /// </summary>
    private void ClearSearchResults()
    {
        this.isShowingSearchResults = false;
        this.searchTerms = [];
        this.nameMatchedWorkspaceIds.Clear();
        this.searchResultChatIds.Clear();
        this.chatPassages.Clear();
        this.nameMatchedChatIds.Clear();
        this.searchExpandedWorkspaces.Clear();
        this.currentSearchResultId = null;
        this.isScrollToSearchResultPending = false;
        this.PublishSearchResults([]);
    }

    /// <summary>
    /// The results of the search in the order the list shows them: each workspace found by its name,
    /// followed by the chats of the workspace which count as results, and then the disappearing chats.
    /// </summary>
    private List<Guid> GetSearchResultsInOrder()
    {
        var results = new List<Guid>();
        foreach (var workspace in this.workspaces)
        {
            if (this.nameMatchedWorkspaceIds.Contains(workspace.WorkspaceId))
                results.Add(workspace.WorkspaceId);

            results.AddRange(workspace.Chats.Where(chat => this.searchResultChatIds.Contains(chat.ChatId)).Select(chat => chat.ChatId));
        }

        results.AddRange(this.temporaryChats.Where(chat => this.searchResultChatIds.Contains(chat.ChatId)).Select(chat => chat.ChatId));
        return results;
    }

    /// <summary>
    /// The words a text of the list marks: the words of the search, but only for a result.
    /// </summary>
    private IReadOnlyList<string> GetTermsToMark(bool isResult) => isResult && this.isShowingSearchResults ? this.searchTerms : [];

    /// <summary>
    /// How many of the results of the search lie in the chats of the given workspace.
    /// </summary>
    private int CountSearchResultsIn(WorkspaceTreeWorkspace workspace) => workspace.Chats.Count(chat => this.searchResultChatIds.Contains(chat.ChatId));

    /// <summary>
    /// Tells the field how many results the search has, which of them is the current one, and why the
    /// search found it.
    /// </summary>
    private void PublishSearchResults(List<Guid> results)
    {
        var position = this.currentSearchResultId is { } current ? results.IndexOf(current) + 1 : 0;
        this.Search?.SetResults(results.Count, position, this.GetCurrentSearchReason());
    }

    /// <summary>
    /// Why the search found the current result, see <see cref="WorkspaceSearchReasonKind"/>.
    /// </summary>
    private WorkspaceSearchReason GetCurrentSearchReason()
    {
        if (this.currentSearchResultId is not { } current)
            return WorkspaceSearchReason.NONE;

        if (this.nameMatchedWorkspaceIds.Contains(current))
            return new(WorkspaceSearchReasonKind.WORKSPACE_NAME);

        if (this.chatPassages.TryGetValue(current, out var passage))
            return new(this.nameMatchedChatIds.Contains(current) ? WorkspaceSearchReasonKind.ALSO_IN_CHAT : WorkspaceSearchReasonKind.IN_CHAT, passage);

        return new(WorkspaceSearchReasonKind.CHAT_NAME);
    }

    /// <summary>
    /// Steps to the next or the previous result of the search, after the last one to the first again.
    /// </summary>
    /// <param name="direction">1 for the next result, -1 for the previous one.</param>
    private async Task StepToSearchResultAsync(int direction)
    {
        var results = this.GetSearchResultsInOrder();
        if (results.Count == 0)
            return;

        var index = this.currentSearchResultId is { } current ? results.IndexOf(current) : -1;
        index = index < 0
            ? direction > 0 ? 0 : results.Count - 1
            : (index + direction + results.Count) % results.Count;

        this.currentSearchResultId = results[index];
        this.isScrollToSearchResultPending = true;
        this.ShowCurrentSearchResultInItsWorkspace();
        this.PublishSearchResults(results);
        await this.SafeStateHasChanged();
    }

    /// <summary>
    /// Expands the workspace of the current result, when the result is a chat in a collapsed workspace.
    /// </summary>
    private void ShowCurrentSearchResultInItsWorkspace()
    {
        if (this.currentSearchResultId is not { } current)
            return;

        foreach (var workspace in this.workspaces)
            if (workspace.Chats.Any(chat => chat.ChatId == current) && !this.IsWorkspaceExpanded(workspace.WorkspaceId))
                this.searchExpandedWorkspaces[workspace.WorkspaceId] = true;
    }

    /// <summary>
    /// Makes the given chat or workspace the current result of the search, when it is one: the user
    /// clicked it, so the counter shows its number, and the arrows go on from there. The list does not
    /// scroll, since the result is where the user just clicked.
    /// </summary>
    private void SelectSearchResult(Guid id)
    {
        if (!this.isShowingSearchResults || this.currentSearchResultId == id)
            return;

        var results = this.GetSearchResultsInOrder();
        if (!results.Contains(id))
            return;

        this.currentSearchResultId = id;
        this.PublishSearchResults(results);
    }

    /// <summary>
    /// Opens a chat the user clicked in the list, which becomes the current result of a search when it is one.
    /// </summary>
    private async Task OpenChatAsync(WorkspaceTreeChat chat)
    {
        this.SelectSearchResult(chat.ChatId);
        await this.LoadChatAsync(chat.ChatPath, true);
    }

    private void OnSearchStepRequested(int direction) => this.InvokeAsync(() => this.StepToSearchResultAsync(direction)).Observe($"{nameof(Workspaces)}: stepping to a result of the search");

    private bool IsWorkspacesSectionCollapsed => !this.isShowingSearchResults && this.SettingsManager.ConfigurationData.Workspace.IsWorkspacesSectionCollapsed;

    private bool IsTemporaryChatsSectionCollapsed => !this.isShowingSearchResults && this.SettingsManager.ConfigurationData.Workspace.IsTemporaryChatsSectionCollapsed;

    private static string GetSectionChevron(bool isCollapsed) => isCollapsed ? Icons.Material.Filled.ChevronRight : Icons.Material.Filled.ExpandMore;

    /// <summary>
    /// The classes of a section's title, which sticks to the top of the list while it scrolls, also
    /// while it shows the results of a search. The second title sticks below the first one.
    /// </summary>
    private static string GetSectionHeaderClasses(bool isSecond) => isSecond
        ? "workspace-section-header is-sticky is-second px-2 pt-2"
        : "workspace-section-header is-sticky px-2 pt-2";

    /// <summary>
    /// Shows what the title of a section points to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A section which holds the open chat out of sight marks its title, and a click on the title
    /// scrolls to that chat. Otherwise, a title which sticks to the top or the bottom of the list
    /// scrolls its section into its usual place. A title in its usual place, whose section shows its
    /// open chat or has none, does nothing: collapsing and expanding is the job of the button beside
    /// the title alone, so that a click on the title only ever moves the list.
    /// </para>
    /// <para>
    /// A collapsed section which holds the open chat opens to show it, as its marked title promises,
    /// and the list reveals the chat once the content of the section is drawn, see
    /// <see cref="this.OnAfterRenderAsync"/>.
    /// </para>
    /// </remarks>
    private async Task ClickSectionTitleAsync(ElementReference sectionStart, bool isCollapsed, bool containsCurrentChat, Func<Task> toggle)
    {
        if (!isCollapsed)
        {
            await this.TryRevealSectionAsync(sectionStart);
            return;
        }

        if (!containsCurrentChat)
            return;

        this.sectionToReveal = sectionStart;
        await toggle();
    }

    /// <summary>
    /// Scrolls the list to the open chat of a section, when it is in the section but out of sight,
    /// or else to the start of the section, when its title sticks to the top or the bottom of the list.
    /// </summary>
    /// <returns>True when the list scrolled, false when there was nothing to reveal.</returns>
    private async Task<bool> TryRevealSectionAsync(ElementReference sectionStart)
    {
        try
        {
            return await this.JsRuntime.InvokeAsync<bool>("revealSection", sectionStart);
        }
        catch (JSDisconnectedException)
        {
            // Nobody is left to see the list.
            return false;
        }
        catch (JSException ex)
        {
            this.Logger.LogWarning(ex, "Failed to reveal a section of the list.");
            return false;
        }
    }

    private async Task ToggleWorkspacesSectionAsync()
    {
        this.SettingsManager.ConfigurationData.Workspace.IsWorkspacesSectionCollapsed = !this.SettingsManager.ConfigurationData.Workspace.IsWorkspacesSectionCollapsed;
        await this.SettingsManager.StoreSettings();
    }

    private async Task ToggleTemporaryChatsSectionAsync()
    {
        this.SettingsManager.ConfigurationData.Workspace.IsTemporaryChatsSectionCollapsed = !this.SettingsManager.ConfigurationData.Workspace.IsTemporaryChatsSectionCollapsed;
        await this.SettingsManager.StoreSettings();
    }

    /// <summary>
    /// Whether the chats of the given workspace are shown.
    /// </summary>
    /// <remarks>
    /// A search leaves every workspace as the user left it, so that nothing moves while they type; a
    /// collapsed one tells how many results it holds. A workspace opens only when the user steps to a
    /// result inside it. Whatever the user expands or collapses during the search counts from then on,
    /// until the search ends, and leaves the state of the whole list alone.
    /// </remarks>
    private bool IsWorkspaceExpanded(Guid workspaceId)
    {
        if (this.isShowingSearchResults && this.searchExpandedWorkspaces.TryGetValue(workspaceId, out var isExpanded))
            return isExpanded;

        return this.expandedWorkspaceIds.Contains(workspaceId);
    }

    /// <summary>
    /// Expands or collapses a workspace, and loads its chats when they are missing.
    /// </summary>
    private async Task ToggleWorkspaceAsync(WorkspaceTreeWorkspace workspace)
    {
        // The results of a search hold the chats of their workspaces already:
        if (this.isShowingSearchResults)
        {
            this.SelectSearchResult(workspace.WorkspaceId);
            this.searchExpandedWorkspaces[workspace.WorkspaceId] = !this.IsWorkspaceExpanded(workspace.WorkspaceId);
            return;
        }

        if (this.expandedWorkspaceIds.Remove(workspace.WorkspaceId))
            return;

        this.expandedWorkspaceIds.Add(workspace.WorkspaceId);
        await this.EnsureWorkspaceChatsLoadedAsync(workspace.WorkspaceId);
    }

    #region Highlighting the open chat

    //
    // The open chat is marked where the user can see it: on its own row, or else on the closest row
    // which hides it -- its collapsed workspace, or the collapsed section. Whatever carries the mark
    // gets the class "is-current", which is also what the list scrolls to.
    //

    private bool IsCurrentChat(WorkspaceTreeChat chat) => this.CurrentChatThread is { } openChat
                                                          && chat.ChatId == openChat.ChatId
                                                          && chat.WorkspaceId == openChat.WorkspaceId;

    private bool IsWorkspaceOfCurrentChat(Guid workspaceId) => workspaceId != Guid.Empty && this.CurrentChatThread?.WorkspaceId == workspaceId;

    private bool IsWorkspaceOfCurrentChatListed => this.workspaces.Any(workspace => this.IsWorkspaceOfCurrentChat(workspace.WorkspaceId));

    private bool IsCurrentChatAmongTemporaryChats => this.temporaryChats.Any(this.IsCurrentChat);

    /// <summary>
    /// Whether some row of the list carries the mark of the open chat.
    /// </summary>
    private bool IsCurrentChatMarked()
    {
        if (this.CurrentChatThread is not { } openChat)
            return false;

        if (openChat.WorkspaceId == Guid.Empty)
            return this.IsCurrentChatAmongTemporaryChats;

        foreach (var workspace in this.workspaces)
        {
            if (workspace.WorkspaceId != openChat.WorkspaceId)
                continue;

            if (this.IsWorkspacesSectionCollapsed || !this.IsWorkspaceExpanded(workspace.WorkspaceId))
                return true;

            return workspace.ChatsLoaded && workspace.Chats.Any(this.IsCurrentChat);
        }

        return false;
    }

    /// <summary>
    /// The classes of a workspace's list item.
    /// </summary>
    /// <remarks>
    /// An expanded workspace of the open chat only takes the primary color, since the chat itself
    /// carries the mark. A collapsed one carries the mark in its place.
    /// </remarks>
    private string GetWorkspaceItemClasses(WorkspaceTreeWorkspace workspace, bool isExpanded)
    {
        var classes = CLICKABLE_ITEM_CLASSES;
        if (this.IsWorkspaceOfCurrentChat(workspace.WorkspaceId))
            classes += isExpanded ? " mud-primary-text" : $" {CURRENT_ITEM_CLASSES}";

        if (this.itemIdWithOpenMenu == workspace.WorkspaceId)
            classes += " is-menu-open";

        if (this.currentSearchResultId == workspace.WorkspaceId)
            classes += " is-search-current";

        return classes;
    }

    /// <summary>
    /// The classes of a chat's list item.
    /// </summary>
    /// <remarks>
    /// A chat inside a workspace is indented by one icon: its own icon starts where the folder icon
    /// of its workspace ends, after the gutter of 16 pixels and the icon of 24.
    /// </remarks>
    private string GetChatItemClasses(WorkspaceTreeChat chat, bool isNested)
    {
        var classes = $"{CLICKABLE_ITEM_CLASSES} workspace-chat";
        if (isNested)
            classes += " pl-10";

        if (this.IsCurrentChat(chat))
            classes += $" {CURRENT_ITEM_CLASSES}";

        if (this.itemIdWithOpenMenu == chat.ChatId)
            classes += " is-menu-open";

        if (this.currentSearchResultId == chat.ChatId)
            classes += " is-search-current";

        return classes;
    }

    /// <summary>
    /// What the icon of a chat shows: its state, or that its messages, attached files, or sources hold
    /// words of the search.
    /// </summary>
    private ChatIndicator GetChatIcon(WorkspaceTreeChat chat, ChatIndicator state) => this.chatPassages.ContainsKey(chat.ChatId) ? ChatIndicator.FoundInChat(state) : state;

    #endregion

    /// <summary>
    /// Keeps the buttons of a row visible while its menu is open, which the pointer leaves for the menu.
    /// </summary>
    /// <param name="itemId">The ID of the chat or workspace whose menu opened or closed.</param>
    /// <param name="isOpen">Whether the menu is open now.</param>
    private void ItemMenuOpenChanged(Guid itemId, bool isOpen)
    {
        if (isOpen)
            this.itemIdWithOpenMenu = itemId;
        else if (this.itemIdWithOpenMenu == itemId)
            this.itemIdWithOpenMenu = Guid.Empty;
    }

    /// <summary>
    /// Whether an answer is being written for the given chat, or a transcript is being made for it.
    /// </summary>
    /// <param name="chatId">The chat to check.</param>
    private bool IsChatBusy(Guid chatId) => this.AIJobService.IsChatGenerationActive(chatId) || this.MediaTranscriptionService.IsBusy(MediaImportOwner.ForChat(chatId));

    /// <summary>
    /// The color of a chat's name, which repeats the color of its icon, such as the one of a transcript's status.
    /// </summary>
    /// <remarks>
    /// The name follows the icon instead of the states, so that a new state needs nothing but its
    /// entry in <see cref="ChatIndicator"/>. Only an unread answer leaves the name alone: it is news,
    /// not a problem, and its icon tells it well enough.
    /// </remarks>
    private static string GetChatNameColorStyle(ChatIndicator indicator) => indicator.Color switch
    {
        Color.Info => "color: var(--mud-palette-info);",
        Color.Success => "color: var(--mud-palette-success);",
        Color.Warning => "color: var(--mud-palette-warning);",
        Color.Error => "color: var(--mud-palette-error);",
        _ => string.Empty,
    };

    /// <summary>
    /// The icon of a chat, which also tells what is going on in it: a transcript, an answer being
    /// written, or an answer which the user has not seen yet.
    /// </summary>
    private ChatIndicator GetChatIndicator(Guid chatId)
    {
        var answer = this.AIJobService.TryGetChatSnapshot(chatId);
        return ChatIndicator.Of(
            this.MediaTranscriptionService.GetSnapshot(MediaImportOwner.ForChat(chatId))?.Status,
            answer?.Status,
            answer?.IsOutcomeSeen ?? true);
    }

    /// <summary>
    /// The color of the dot on the folder icon of a collapsed workspace, or null when it needs none.
    /// </summary>
    /// <remarks>
    /// A collapsed workspace hides the icons of its chats, so the dot tells that one of them holds
    /// an answer the user has not seen yet, in the color of that chat's icon. A failed answer comes
    /// before an unread one, since it needs the user more. An expanded workspace shows the chats'
    /// icons themselves and needs no dot.
    /// </remarks>
    private Color? GetWorkspaceMarkColor(WorkspaceTreeWorkspace workspace, bool isExpanded)
    {
        if (isExpanded || !workspace.ChatsLoaded)
            return null;

        var indicators = workspace.Chats.Select(chat => this.GetChatIndicator(chat.ChatId)).ToHashSet();
        if (indicators.Contains(ChatIndicator.ANSWER_FAILED))
            return ChatIndicator.ANSWER_FAILED.Color;

        if (indicators.Contains(ChatIndicator.ANSWER_UNREAD))
            return ChatIndicator.ANSWER_UNREAD.Color;

        return null;
    }

    /// <summary>
    /// How much a chat's icon stands out.
    /// </summary>
    /// <remarks>
    /// The plain chat icon is in almost every row, so it stays in the background unless its chat is
    /// open. An icon telling a status stands out in full.
    /// </remarks>
    private string? GetChatIconStyle(WorkspaceTreeChat chat, ChatIndicator indicator) => indicator.IsStatus || this.IsCurrentChat(chat) ? null : "opacity: 0.6;";

    private void OnMediaImportStateChanged(MediaImportOwner owner)
    {
        if (owner.Kind is MediaImportOwnerKind.CHAT)
            this.SafeStateHasChanged().Observe($"{nameof(Workspaces)}: rendering a media import change");
    }

    private async Task SafeStateHasChanged()
    {
        if (this.isDisposed)
            return;

        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task StartPrefetchAsync()
    {
        if (this.prefetchCancellationTokenSource is not null)
        {
            await this.prefetchCancellationTokenSource.CancelAsync();
            this.prefetchCancellationTokenSource.Dispose();
        }

        this.prefetchCancellationTokenSource = new CancellationTokenSource();
        await this.PrefetchWorkspaceChatsAsync(this.prefetchCancellationTokenSource.Token);
    }

    private async Task PrefetchWorkspaceChatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WorkspaceBehaviour.TryPrefetchRemainingChatsAsync(async _ =>
            {
                if (this.isDisposed || cancellationToken.IsCancellationRequested)
                    return;

                await this.LoadTreeItemsAsync(startPrefetch: false);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the component is hidden or disposed.
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed while prefetching workspace chats.");
        }
    }

    private void OnSearchTextChanged() => this.InvokeAsync(() => this.SearchAsync(isTyping: true)).Observe($"{nameof(Workspaces)}: searching the chats");

    private async Task CancelSearchAsync()
    {
        this.searchRevision++;
        if (this.searchCancellationTokenSource is not null)
        {
            await this.searchCancellationTokenSource.CancelAsync();
            this.searchCancellationTokenSource.Dispose();
            this.searchCancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Searches the chats for the text of the search, in two steps, and marks what it found.
    /// </summary>
    /// <remarks>
    /// The names come first, right away and with every key the user types, since they are in memory.
    /// The messages of the chats have to be read from the disk, so their search waits until the user
    /// paused typing and runs in the background, while the field shows a progress line; a key typed
    /// meanwhile cancels it. The list itself stays as it is, see <see cref="this.isShowingSearchResults"/>.
    /// An empty text takes the marks away again.
    /// </remarks>
    /// <param name="isTyping">Whether the user changed the text, rather than the list changing under
    /// the search. Only typing waits for a pause before the contents are searched.</param>
    private async Task SearchAsync(bool isTyping)
    {
        await this.CancelSearchAsync();
        if (this.Search is not { } search || !this.HasSearchQuery)
        {
            this.Search?.SetSearchingContents(false);
            this.ClearSearchResults();
            await this.SafeStateHasChanged();
            return;
        }

        var text = search.Text;
        var terms = SearchText.ParseTerms(text);
        this.searchCancellationTokenSource = new CancellationTokenSource();
        var token = this.searchCancellationTokenSource.Token;
        var revision = ++this.searchRevision;
        bool IsOutdated() => this.isDisposed || token.IsCancellationRequested || revision != this.searchRevision;

        try
        {
            var nameMatches = await WorkspaceBehaviour.SearchWorkspaceChatsAsync(text, includeThreadContents: false, token);
            if (IsOutdated())
                return;

            //
            // The search loaded the chats of all workspaces, which the list may not show yet. A workspace
            // which opens to show what was found would otherwise wait for its chats:
            //
            await this.LoadTreeItemsAsync(startPrefetch: false, searchAgain: false);
            if (IsOutdated())
                return;

            this.ShowSearchResults(nameMatches, terms);
            await this.SafeStateHasChanged();

            if (isTyping)
                await Task.Delay(CONTENT_SEARCH_DELAY, token);

            search.SetSearchingContents(true);
            var allMatches = await WorkspaceBehaviour.SearchWorkspaceChatsAsync(text, includeThreadContents: true, token);
            if (IsOutdated())
                return;

            this.ShowSearchResults(allMatches, terms);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user keeps typing or closes the search.
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed while searching workspace chats.");
            if (!IsOutdated())
                this.ShowSearchResults(new([], []), terms);
        }
        finally
        {
            //
            // A newer search tells the field about its own progress, and an empty text ended it:
            //
            if (revision == this.searchRevision)
            {
                search.SetSearchingContents(false);
                await this.SafeStateHasChanged();
            }
        }
    }

    private async Task EnsureWorkspaceChatsLoadedAsync(Guid workspaceId)
    {
        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        var hasWorkspace = false;
        var chatsLoaded = false;
        
        foreach (var workspace in snapshot.Workspaces)
        {
            if (workspace.WorkspaceId != workspaceId)
                continue;

            hasWorkspace = true;
            chatsLoaded = workspace.ChatsLoaded;
            break;
        }

        if (!hasWorkspace || chatsLoaded || !this.loadingWorkspaceChatLists.Add(workspaceId))
            return;

        await this.LoadTreeItemsAsync(startPrefetch: false);

        try
        {
            await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspaceId);
        }
        finally
        {
            this.loadingWorkspaceChatLists.Remove(workspaceId);
        }

        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    public async Task ForceRefreshFromDiskAsync()
    {
        if (this.prefetchCancellationTokenSource is not null)
        {
            await this.prefetchCancellationTokenSource.CancelAsync();
            this.prefetchCancellationTokenSource.Dispose();
            this.prefetchCancellationTokenSource = null;
        }
        
        this.loadingWorkspaceChatLists.Clear();
        this.isInitialLoading = true;
        
        await this.SafeStateHasChanged();
        await this.LoadTreeItemsAsync(startPrefetch: true, forceReload: true);
    }

    public async Task StoreChatAsync(ChatThread chat, bool reloadTreeItems = false)
    {
        await WorkspaceBehaviour.StoreChatAsync(chat);
        
        if (reloadTreeItems)
            this.loadingWorkspaceChatLists.Clear();
        
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    /// <summary>
    /// Copies the given chat, after asking the user for the name of the copy, and shows the copy in the tree.
    /// </summary>
    /// <param name="sourceChat">The chat to copy, as it stands in memory.</param>
    /// <returns>The persisted copy. Null when the user canceled the question, in which case nothing was copied.</returns>
    /// <remarks>
    /// Neither asks about unsaved changes nor opens the copy: which of both a caller needs depends
    /// on where the copy was asked for.
    /// </remarks>
    public async Task<ChatThread?> CopyChatAsync(ChatThread sourceChat)
    {
        var copy = await WorkspaceBehaviour.CopyChatAsync(this.DialogService, sourceChat);
        if (copy is not null)
            await this.LoadTreeItemsAsync(startPrefetch: false);

        return copy;
    }

    private async Task<ChatThread?> LoadChatAsync(string? chatPath, bool switchToChat)
    {
        if (string.IsNullOrWhiteSpace(chatPath))
            return null;

        if (!Directory.Exists(chatPath))
            return null;
        
        if (switchToChat && await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(this, Event.HAS_CHAT_UNSAVED_CHANGES))
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("Are you sure you want to load another chat? All unsaved changes will be lost.") },
            };
            
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Load Chat"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return null;
        }

        try
        {
            var chatData = await File.ReadAllTextAsync(Path.Join(chatPath, "thread.json"), Encoding.UTF8);
            var chat = JsonSerializer.Deserialize<ChatThread>(chatData, WorkspaceBehaviour.JSON_OPTIONS);
            if (chat is not null)
                chat = this.AIJobService.TryGetLiveChatThread(chat.ChatId) ?? chat;

            if (switchToChat)
            {
                this.CurrentChatThread = chat;
                await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            }
            
            return chat;
        }
        catch (Exception e)
        {
            this.Logger.LogError($"Failed to load chat from '{chatPath}': {e.Message}");
        }
        
        return null;
    }

    /// <summary>Deletes the given chat and updates the tree, asking the user to confirm that beforehand.</summary>
    /// <param name="chatPath">Path of the chat to delete.</param>
    /// <param name="askForConfirmation">False skips the question. Only for callers who already asked.</param>
    /// <param name="unloadChat">Whether to take the chat out of the view when it is the one being shown.</param>
    /// <returns>True when the chat is gone, which includes it never having been there. False when it is still there.</returns>
    /// <remarks>
    /// The question itself comes from the workspace behaviour, so that it is worded in one place only.
    /// Callers who do more than deleting have to honor the return value: a chat that is busy is not
    /// deleted either, and then nothing about it may be reset.
    /// </remarks>
    public async Task<bool> DeleteChatAsync(string? chatPath, bool askForConfirmation = true, bool unloadChat = true)
    {
        var chat = await this.LoadChatAsync(chatPath, false);
        
        // There is nothing left to delete, so the caller may go on:
        if (chat is null)
            return true;

        //
        // Deleting a chat while it is being worked on would pull the ground from under that work.
        // We check before asking: nobody should confirm something that cannot happen anyway.
        //
        var mediaOwner = MediaImportOwner.ForChat(chat.ChatId);
        if (this.AIJobService.IsChatGenerationActive(chat.ChatId) || this.MediaTranscriptionService.IsBusy(mediaOwner))
            return false;

        if (!await WorkspaceBehaviour.DeleteChatAsync(this.DialogService, chat.WorkspaceId, chat.ChatId, askForConfirmation))
            return false;

        this.MediaTranscriptionService.ClearOwnerState(mediaOwner);
        await this.LoadTreeItemsAsync(startPrefetch: false);
        
        if (unloadChat && this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread = null;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
        }

        return true;
    }

    private async Task RenameChatAsync(string? chatPath)
    {
        var chat = await this.LoadChatAsync(chatPath, false);
        if (chat is null)
            return;

        if (this.AIJobService.IsChatGenerationActive(chat.ChatId))
            return;
        
        var dialogParameters = new DialogParameters<SingleInputDialog>
        {
            { x => x.Message, string.Format(T("Please enter a new or edit the name for your chat '{0}':"), chat.Name) },
            { x => x.InputHeaderText, T("Chat Name") },
            { x => x.UserInput, chat.Name },
            { x => x.ConfirmText, T("Rename") },
            { x => x.ConfirmColor, Color.Info },
            { x => x.AllowEmptyInput, false },
            { x => x.EmptyInputErrorMessage, T("Please enter a chat name.") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Rename Chat"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        chat.Name = (dialogResult.Data as string)!;
        if (this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread.Name = chat.Name;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
        }
        
        await WorkspaceBehaviour.StoreChatAsync(chat);
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    /// <summary>
    /// Copies the chat behind the copy button of a tree item and opens the copy.
    /// </summary>
    /// <param name="chatPath">The directory of the chat to copy.</param>
    /// <remarks>
    /// The copy itself is done by CopyChatAsync, which the chat toolbar uses as well. What this
    /// handler adds is what only the tree needs: it finds the chat by its directory, and because it
    /// opens the copy afterward, it asks first when the chat open right now has unsaved changes.
    /// The chat toolbar asks nothing, since it copies the chat on the screen and keeps working in it.
    /// </remarks>
    private async Task CopyChatFromTreeAsync(string? chatPath)
    {
        var chat = await this.LoadChatAsync(chatPath, false);
        if (chat is null)
            return;

        var mediaOwner = MediaImportOwner.ForChat(chat.ChatId);
        if (this.AIJobService.IsChatGenerationActive(chat.ChatId) || this.MediaTranscriptionService.IsBusy(mediaOwner))
            return;

        //
        // Copying the chat which is open right now takes its in-memory state, so whatever the user
        // has not saved yet ends up in the copy while the original keeps the state it was stored
        // with. Copying any other chat replaces the open one, so its unsaved changes are gone.
        // Both outcomes are surprising enough to deserve their own wording.
        //
        var openChat = this.CurrentChatThread;
        var isCopyOfOpenChat = openChat is not null && openChat.ChatId == chat.ChatId;
        if (await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(this, Event.HAS_CHAT_UNSAVED_CHANGES))
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                {
                    x => x.Message, isCopyOfOpenChat switch
                    {
                        true => T("Do you want to copy this chat? Your unsaved changes move into the copy, and the original chat keeps the state it was last saved with."),
                        false => T("Do you want to copy this chat? The copy is opened afterwards, so all unsaved changes of the chat you have open right now will be lost."),
                    }
                },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Copy Chat"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }

        var sourceChat = isCopyOfOpenChat ? openChat! : chat;
        var copy = await this.CopyChatAsync(sourceChat);
        if (copy is null)
            return;

        this.CurrentChatThread = copy;
        await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
    }

    private async Task RenameWorkspaceAsync(string? workspacePath)
    {
        if (workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await WorkspaceBehaviour.LoadWorkspaceNameAsync(workspaceId);
        var dialogParameters = new DialogParameters<SingleInputDialog>
        {
            { x => x.Message, string.Format(T("Please enter a new or edit the name for your workspace '{0}':"), workspaceName) },
            { x => x.InputHeaderText, T("Workspace Name") },
            { x => x.UserInput, workspaceName },
            { x => x.ConfirmText, T("Rename") },
            { x => x.ConfirmColor, Color.Info },
            { x => x.AllowEmptyInput, false },
            { x => x.EmptyInputErrorMessage, T("Please enter a workspace name.") },
            { x => x.AdditionalValidation, await this.CreateWorkspaceNameValidationAsync(workspaceId, workspaceName) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Rename Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var alteredWorkspaceName = (dialogResult.Data as string)!;
        if (!await WorkspaceBehaviour.RenameWorkspaceAsync(workspaceId, alteredWorkspaceName))
            return;

        await this.SendMessage(Event.WORKSPACE_RENAMED, workspaceId);
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    private async Task AddWorkspaceAsync()
    {
        var dialogParameters = new DialogParameters<SingleInputDialog>
        {
            { x => x.Message, T("Please name your workspace:") },
            { x => x.InputHeaderText, T("Workspace Name") },
            { x => x.UserInput, string.Empty },
            { x => x.ConfirmText, T("Add workspace") },
            { x => x.ConfirmColor, Color.Info },
            { x => x.AllowEmptyInput, false },
            { x => x.EmptyInputErrorMessage, T("Please enter a workspace name.") },
            { x => x.AdditionalValidation, await this.CreateWorkspaceNameValidationAsync() },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Add Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceName = (dialogResult.Data as string)!;
        var result = await WorkspaceBehaviour.TryCreateWorkspaceAsync(workspaceName);
        if (!result.Success)
            return;
        
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    private async Task DeleteWorkspaceAsync(string? workspacePath)
    {
        if (workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await WorkspaceBehaviour.LoadWorkspaceNameAsync(workspaceId);
        
        //
        // Deleting a workspace while one of its chats is being worked on would pull the ground from
        // under that work. The button cannot tell, since the chats of a workspace may not be loaded
        // yet, so we check before asking: nobody should confirm something that cannot happen anyway.
        //
        var chatIds = this.ReadChatIdsOfWorkspace(workspaceId);
        if (chatIds.Any(this.IsChatBusy))
        {
            await this.WarnAboutBusyWorkspaceAsync(workspaceName);
            return;
        }

        var chatCount = Directory.EnumerateDirectories(workspacePath).Count();
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the workspace '{0}'? This will also delete {1} chat(s) in this workspace."), workspaceName, chatCount) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        if (!await WorkspaceBehaviour.DeleteWorkspaceAsync(workspaceId, this.IsChatBusy))
        {
            await this.WarnAboutBusyWorkspaceAsync(workspaceName);
            return;
        }

        foreach (var chatId in chatIds)
            this.MediaTranscriptionService.ClearOwnerState(MediaImportOwner.ForChat(chatId));

        await this.LoadTreeItemsAsync(startPrefetch: false);

        // The open chat went along with its workspace. Left on the screen, it would be stored again sooner or later:
        if (this.CurrentChatThread?.WorkspaceId == workspaceId)
        {
            this.CurrentChatThread = null;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
        }
    }

    /// <summary>
    /// Reads the chats of the given workspace, together with the open chat when it belongs there.
    /// </summary>
    /// <remarks>
    /// A new chat started in a workspace reaches the disk only with its first message. Until then, it
    /// is missing from the workspace on the disk, but it may already wait for a transcript.
    /// </remarks>
    /// <param name="workspaceId">The workspace to read.</param>
    private HashSet<Guid> ReadChatIdsOfWorkspace(Guid workspaceId)
    {
        var chatIds = WorkspaceBehaviour.ReadChatIdsOfWorkspace(workspaceId).ToHashSet();
        if (this.CurrentChatThread is { } openChat && openChat.WorkspaceId == workspaceId)
            chatIds.Add(openChat.ChatId);

        return chatIds;
    }

    private Task WarnAboutBusyWorkspaceAsync(string workspaceName) => MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.FolderDelete, string.Format(T("The workspace '{0}' cannot be deleted while one of its chats is being answered or transcribed. Please try again once that is finished."), workspaceName)));
    
    /// <summary>
    /// Moves the given chat into another workspace and shows it there in the tree.
    /// </summary>
    /// <param name="chat">The chat to move. It gets the new workspace and the new paths of its attachments.</param>
    /// <param name="targetWorkspaceId">The workspace to move the chat into.</param>
    /// <remarks>
    /// Neither asks for the target workspace nor follows the chat in the chat view: both depend on
    /// where the move was asked for. Whoever moves a chat while this component exists goes through
    /// here, though, since the tree draws what it has built before and would otherwise keep showing
    /// the chat in its old place.<br/><br/>
    ///
    /// What the user did not save reaches the new place only while that place stores chats
    /// automatically, see WorkspaceBehaviour.MoveChatAsync.
    /// </remarks>
    public async Task MoveChatAsync(ChatThread chat, Guid targetWorkspaceId)
    {
        await WorkspaceBehaviour.MoveChatAsync(chat, targetWorkspaceId, this.SettingsManager.ConfigurationData.Workspace.IsStoredAutomatically(targetWorkspaceId));
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    private async Task MoveChatFromTreeAsync(string? chatPath)
    {
        var storedChat = await this.LoadChatAsync(chatPath, false);
        if (storedChat is null)
            return;

        //
        // The open chat is the one which moves, just like with the button in the chat itself. The
        // copy from the disk lacks whatever is not stored yet, such as a message being edited, and
        // the chat would continue with that copy afterward. Whether that reaches the disk in the new
        // place depends on how chats are stored, see MoveChatAsync:
        //
        var chat = this.CurrentChatThread is { } openChat && openChat.ChatId == storedChat.ChatId ? openChat : storedChat;
        if (this.AIJobService.IsChatGenerationActive(chat.ChatId))
            return;
        
        var dialogParameters = new DialogParameters<WorkspaceSelectionDialog>
        {
            { x => x.Message, T("Please select the workspace where you want to move the chat to.") },
            { x => x.SelectedWorkspace, chat.WorkspaceId },
            { x => x.ConfirmText, T("Move chat") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<WorkspaceSelectionDialog>(T("Move Chat to Workspace"), dialogParameters, DialogOptions.FULLSCREEN_MANUAL_ESCAPE);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = dialogResult.Data is Guid id ? id : Guid.Empty;
        if (workspaceId == Guid.Empty)
            return;
        
        await this.MoveChatAsync(chat, workspaceId);
        if (ReferenceEquals(chat, this.CurrentChatThread))
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
    }
    
    /// <summary>
    /// Asks the chat component to start a new chat in the given workspace.
    /// </summary>
    /// <param name="workspacePath">The directory of the workspace.</param>
    /// <remarks>
    /// The chat component starts the chat just like its own button for a new chat in the workspace:
    /// it asks about unsaved changes, applies the chat template with its data sources, tools, and
    /// example conversation, and stores the chat only once it has content. Until then, the chat is
    /// missing from the tree.
    /// </remarks>
    private async Task AddChatAsync(string workspacePath)
    {
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        await this.StartNewChatRequested.InvokeAsync(workspaceId);
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                await this.ForceRefreshFromDiskAsync();
                break;

            case Event.WORKSPACE_CREATED:
                await this.LoadTreeItemsAsync(startPrefetch: false);
                break;

            //
            // The user changed the settings, perhaps the workspaces or the storing of chats. When
            // workspaces were switched on or off, the list builds itself anew from what it read already,
            // and a running search runs anew over what the list holds now. Otherwise, the buttons for a
            // new chat in a workspace may have to show or hide:
            //
            case Event.CONFIGURATION_CHANGED:
                if (this.areWorkspacesShown != this.SettingsManager.ConfigurationData.Workspace.UsesWorkspaces)
                    await this.LoadTreeItemsAsync(startPrefetch: false);
                else
                    await this.SafeStateHasChanged();

                break;

            case Event.AI_JOB_CHANGED:
            case Event.AI_JOB_FINISHED:
            case Event.CHAT_GENERATION_CHANGED:
                await this.SafeStateHasChanged();
                break;
        }
    }

    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnMediaImportStateChanged;
        if (this.Search is not null)
        {
            this.Search.TextChanged -= this.OnSearchTextChanged;
            this.Search.StepRequested -= this.OnSearchStepRequested;
        }

        this.isDisposed = true;
        this.prefetchCancellationTokenSource?.Cancel();
        this.prefetchCancellationTokenSource?.Dispose();
        this.prefetchCancellationTokenSource = null;
        this.searchCancellationTokenSource?.Cancel();
        this.searchCancellationTokenSource?.Dispose();
        this.searchCancellationTokenSource = null;

        base.DisposeResources();
    }

    #endregion
}
