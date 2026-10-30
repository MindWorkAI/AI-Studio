using System.Collections.Concurrent;

using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Keeps assistant sessions alive while their Blazor components are not mounted.
/// </summary>
/// <param name="messageBus">The message bus used to publish assistant session changes.</param>
public sealed class AssistantSessionService(MessageBus messageBus)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantSessionService).Namespace, nameof(AssistantSessionService));

    /// <summary>
    /// Mutable runtime state owned exclusively by <see cref="AssistantSessionService"/>.
    /// </summary>
    /// <remarks>
    /// This type intentionally exists in addition to <see cref="AssistantSessionSnapshot"/>.
    /// It holds runtime-only data such as the cancellation token source and lock, while
    /// snapshots are copied DTOs for UI components and message bus payloads.
    /// </remarks>
    private sealed class AssistantSessionState
    {
        /// <summary>
        /// Identifies this concrete run of an assistant session.
        /// </summary>
        public required Guid SessionId { get; init; }

        /// <summary>
        /// Identifies the assistant and logical session slot this run belongs to.
        /// </summary>
        public required AssistantSessionKey Key { get; init; }

        /// <summary>
        /// Cancels the active assistant run.
        /// </summary>
        public required CancellationTokenSource CancellationTokenSource { get; init; }

        /// <summary>
        /// Stores when the session run started.
        /// </summary>
        public required DateTimeOffset StartedAt { get; init; }

        /// <summary>
        /// Stores when the session state was last changed.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Stores when the session reached a terminal state.
        /// </summary>
        public DateTimeOffset? FinishedAt { get; set; }

        /// <summary>
        /// Stores the user-visible assistant title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Stores the current lifecycle state of the session.
        /// </summary>
        public AssistantSessionStatus Status { get; set; }

        /// <summary>
        /// Stores the user-visible error message for failed sessions.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Stores the current assistant chat thread, including streamed output.
        /// </summary>
        public ChatThread? ChatThread { get; set; }

        /// <summary>
        /// Stores the assistant component state captured from the running UI instance.
        /// </summary>
        public Dictionary<string, IAssistantSessionSnapshotField> State { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Whether the answer of the session waits for the user to confirm a tool call.
        /// </summary>
        public bool IsWaitingForToolApproval { get; set; }

        /// <summary>
        /// Guards mutable fields while snapshots are created or updates are applied.
        /// </summary>
        public readonly Lock SyncRoot = new();
    }

    /// <summary>
    /// Stores one assistant session per session key.
    /// </summary>
    private readonly ConcurrentDictionary<AssistantSessionKey, AssistantSessionState> sessions = new();

    /// <summary>
    /// Gets whether at least one assistant session is still active.
    /// </summary>
    public bool HasActiveSessions => this.sessions.Values.Any(session => session.Status is AssistantSessionStatus.RUNNING or AssistantSessionStatus.CANCELING);

    /// <summary>
    /// Gets whether at least one assistant session waits for the user to confirm a tool call.
    /// </summary>
    public bool IsAnySessionWaitingForToolApproval => this.sessions.Values.Any(session => session.IsWaitingForToolApproval);

    /// <summary>
    /// How many components show each assistant right now, see SetShown.
    /// </summary>
    private readonly ConcurrentDictionary<AssistantSessionKey, int> shownAssistants = new();

    /// <summary>
    /// Says that a component shows an assistant, or no longer does.
    /// </summary>
    /// <remarks>
    /// A session runs on when the user leaves its assistant, and only then does a tool call which
    /// waits for confirmation need a notification: on the page of the assistant, the user sees it
    /// anyway. Counted rather than flagged, because the next page may show the same assistant
    /// before the previous one is disposed.
    /// </remarks>
    /// <param name="key">The assistant session key the component shows.</param>
    /// <param name="isShown">True when the component appears, false when it goes.</param>
    public void SetShown(AssistantSessionKey key, bool isShown)
    {
        if (isShown)
        {
            this.shownAssistants.AddOrUpdate(key, 1, (_, count) => count + 1);

            // The user comes back to an assistant which waits, and the request stands in front of them again:
            if (this.sessions.TryGetValue(key, out var shownSession) && shownSession.IsWaitingForToolApproval)
                messageBus.HideNotification(ToolApprovalNotificationKey(key)).Observe($"{nameof(AssistantSessionService)}: hiding a notification the user no longer needs");

            return;
        }

        var remaining = this.shownAssistants.AddOrUpdate(key, 0, (_, count) => Math.Max(0, count - 1));

        // The user leaves an assistant which waits for a confirmation, so the request is out of sight from now on:
        if (remaining is 0 && this.sessions.TryGetValue(key, out var session) && session.IsWaitingForToolApproval)
            this.NotifyWaitingForToolApprovalAsync(key, session.Title).Observe($"{nameof(AssistantSessionService)}: notifying about a confirmation the user left");
    }

    /// <summary>
    /// Tells the user that an assistant which is out of sight waits for a confirmation.
    /// </summary>
    /// <remarks>
    /// Sent when the confirmation begins while nobody shows the assistant, and when the user leaves
    /// an assistant which waits: in both cases, only the notification tells them. It stays until the
    /// user closes it or nothing waits anymore: the user answered, came back to the assistant, or
    /// the run ended. One per assistant, identified by ToolApprovalNotificationKey.
    /// </remarks>
    private Task NotifyWaitingForToolApprovalAsync(AssistantSessionKey key, string title) => messageBus.SendWarning(new(Icons.Material.Filled.Warning, string.Format(TB("The assistant '{0}' waits for you to confirm a tool call."), title.Shorten(MAX_TITLE_CHARACTERS_IN_NOTIFICATIONS)), StaysUntilClosed: true, Key: ToolApprovalNotificationKey(key)));

    /// <summary>
    /// The key of the notification that an assistant waits for a confirmation.
    /// </summary>
    private static string ToolApprovalNotificationKey(AssistantSessionKey key) => $"tool-approval-assistant-{key}";

    /// <summary>
    /// How much of the title of an assistant a notification quotes.
    /// </summary>
    private const int MAX_TITLE_CHARACTERS_IN_NOTIFICATIONS = 50;

    /// <summary>
    /// Says that the answer of an assistant waits for the user to confirm a tool call, or no longer does.
    /// </summary>
    /// <remarks>
    /// The counterpart to AIJobService.SetWaitingForToolApprovalAsync for the assistants. The tool
    /// calling loop only knows the thread it runs for, so the session is found by its thread. A
    /// thread no session holds is not an error: chats run the same loop, and so do the threads an
    /// assistant runs out of sight.
    /// </remarks>
    /// <param name="chatId">The ID of the thread whose answer waits.</param>
    /// <param name="isWaiting">True when a call waits for confirmation, false when the user answered.</param>
    public async Task SetWaitingForToolApprovalAsync(Guid chatId, bool isWaiting)
    {
        var session = this.sessions.Values.FirstOrDefault(session => session.ChatThread?.ChatId == chatId && session.Status is AssistantSessionStatus.RUNNING);
        if (session is null)
            return;

        string title;
        lock (session.SyncRoot)
        {
            if (session.IsWaitingForToolApproval == isWaiting)
                return;

            session.IsWaitingForToolApproval = isWaiting;
            session.UpdatedAt = DateTimeOffset.Now;
            title = session.Title;
        }

        await this.NotifyChangedAsync(session);
        if (!isWaiting)
            await messageBus.HideNotification(ToolApprovalNotificationKey(session.Key));
        else if (this.shownAssistants.GetValueOrDefault(session.Key) is 0)
            await this.NotifyWaitingForToolApprovalAsync(session.Key, title);
    }

    /// <summary>
    /// Gets copied snapshots for all known assistant sessions.
    /// </summary>
    /// <returns>Session snapshots ordered by newest update first.</returns>
    public IReadOnlyCollection<AssistantSessionSnapshot> GetSnapshots()
    {
        return this.sessions.Values
            .Select(CreateSnapshot)
            .OrderByDescending(snapshot => snapshot.UpdatedAt)
            .ToList();
    }

    /// <summary>
    /// Tries to get the current snapshot for an assistant session key.
    /// </summary>
    /// <param name="key">The assistant session key to look up.</param>
    /// <returns>The current snapshot, or <c>null</c> when no session exists.</returns>
    public AssistantSessionSnapshot? TryGetSnapshot(AssistantSessionKey key)
    {
        return this.sessions.TryGetValue(key, out var session) ? CreateSnapshot(session) : null;
    }

    /// <summary>
    /// Tries to get and remove an inactive assistant session snapshot.
    /// </summary>
    /// <remarks>
    /// This method intentionally does not publish a change event. It is used when
    /// a UI instance consumes a finished session exactly once and should keep the
    /// restored result locally until the user leaves or resets the assistant.
    /// </remarks>
    /// <param name="key">The assistant session key to look up and remove.</param>
    /// <returns>The removed inactive snapshot, or <c>null</c> when no inactive session exists.</returns>
    public AssistantSessionSnapshot? TryTakeInactiveSnapshot(AssistantSessionKey key)
    {
        if (!this.sessions.TryGetValue(key, out var session))
            return null;

        AssistantSessionSnapshot snapshot;
        lock (session.SyncRoot)
        {
            if (session.Status is AssistantSessionStatus.RUNNING or AssistantSessionStatus.CANCELING)
                return null;

            snapshot = CreateSnapshotWithoutLock(session);
        }

        return ((ICollection<KeyValuePair<AssistantSessionKey, AssistantSessionState>>)this.sessions).Remove(new(key, session)) ? snapshot : null;
    }

    /// <summary>
    /// Starts a new assistant session when no active session exists for the key.
    /// </summary>
    /// <param name="key">The assistant session key.</param>
    /// <param name="title">The user-visible assistant title.</param>
    /// <param name="cancellationTokenSource">The cancellation token source owned by the new runtime session.</param>
    /// <param name="chatThread">The current assistant chat thread, if one already exists.</param>
    /// <param name="state">The initial assistant component state.</param>
    /// <param name="sendingComponent">The component that initiated the session start.</param>
    /// <returns>The new session snapshot, or the existing active session snapshot.</returns>
    public async Task<AssistantSessionSnapshot> TryBeginAsync(AssistantSessionKey key, string title, CancellationTokenSource cancellationTokenSource, ChatThread? chatThread, Dictionary<string, IAssistantSessionSnapshotField> state, ComponentBase? sendingComponent = null)
    {
        if (this.sessions.TryGetValue(key, out var existing) && existing.Status is AssistantSessionStatus.RUNNING or AssistantSessionStatus.CANCELING)
            return CreateSnapshot(existing);

        var now = DateTimeOffset.Now;
        var session = new AssistantSessionState
        {
            SessionId = Guid.NewGuid(),
            Key = key,
            CancellationTokenSource = cancellationTokenSource,
            StartedAt = now,
            UpdatedAt = now,
            Title = title,
            Status = AssistantSessionStatus.RUNNING,
            ChatThread = chatThread,
            State = state,
        };

        this.sessions[key] = session;
        var snapshot = CreateSnapshot(session);
        await this.NotifyChangedAsync(session, sendingComponent);
        return snapshot;
    }

    /// <summary>
    /// Updates a running assistant session with the latest UI and chat state.
    /// </summary>
    /// <param name="key">The assistant session key.</param>
    /// <param name="sessionId">The concrete run ID that is allowed to write the checkpoint.</param>
    /// <param name="title">The current user-visible assistant title.</param>
    /// <param name="chatThread">The current assistant chat thread.</param>
    /// <param name="state">The current assistant component state.</param>
    /// <param name="sendingComponent">The component that initiated the checkpoint.</param>
    public async Task CheckpointAsync(AssistantSessionKey key, Guid sessionId, string title, ChatThread? chatThread, Dictionary<string, IAssistantSessionSnapshotField> state, ComponentBase? sendingComponent = null)
    {
        if (!this.sessions.TryGetValue(key, out var session))
            return;

        lock (session.SyncRoot)
        {
            if (session.SessionId != sessionId)
                return;

            session.Title = title;
            session.ChatThread = chatThread;
            session.State = state;
            session.UpdatedAt = DateTimeOffset.Now;
        }

        await this.NotifyChangedAsync(session, sendingComponent);
    }

    /// <summary>
    /// Requests cancellation for an active assistant session.
    /// </summary>
    /// <param name="key">The assistant session key to cancel.</param>
    /// <param name="sendingComponent">The component that initiated the cancellation.</param>
    public async Task CancelAsync(AssistantSessionKey key, ComponentBase? sendingComponent = null)
    {
        if (!this.sessions.TryGetValue(key, out var session))
            return;

        lock (session.SyncRoot)
        {
            if (session.Status is not AssistantSessionStatus.RUNNING)
                return;

            session.Status = AssistantSessionStatus.CANCELING;
            session.UpdatedAt = DateTimeOffset.Now;
        }

        try
        {
            if (!session.CancellationTokenSource.IsCancellationRequested)
                await session.CancellationTokenSource.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        await this.NotifyChangedAsync(session, sendingComponent);
    }

    /// <summary>
    /// Moves an assistant session into a terminal state and publishes completion.
    /// </summary>
    /// <param name="key">The assistant session key.</param>
    /// <param name="sessionId">The concrete run ID that is allowed to complete the session.</param>
    /// <param name="status">The terminal status to store.</param>
    /// <param name="errorMessage">The user-visible error message for failed sessions.</param>
    /// <param name="chatThread">The final assistant chat thread.</param>
    /// <param name="state">The final assistant component state.</param>
    /// <param name="sendingComponent">The component that initiated the completion.</param>
    public async Task CompleteAsync(AssistantSessionKey key, Guid sessionId, AssistantSessionStatus status, string errorMessage, ChatThread? chatThread, Dictionary<string, IAssistantSessionSnapshotField> state, ComponentBase? sendingComponent = null)
    {
        if (!this.sessions.TryGetValue(key, out var session))
            return;

        lock (session.SyncRoot)
        {
            if (session.SessionId != sessionId)
                return;

            session.Status = status;
            session.IsWaitingForToolApproval = false;
            session.ErrorMessage = errorMessage;
            session.ChatThread = chatThread;
            session.State = state;
            session.UpdatedAt = DateTimeOffset.Now;
            session.FinishedAt = session.UpdatedAt;
        }

        // A confirmation the run still waited for, e.g., when the user stopped it, waits no more:
        await messageBus.HideNotification(ToolApprovalNotificationKey(key));
        await this.NotifyChangedAsync(session, sendingComponent);
        await messageBus.SendMessage(sendingComponent, Event.ASSISTANT_SESSION_FINISHED, CreateSnapshot(session));

        try
        {
            session.CancellationTokenSource.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Clears an inactive assistant session.
    /// </summary>
    /// <param name="key">The assistant session key to clear.</param>
    public async Task ClearAsync(AssistantSessionKey key)
    {
        if (!this.sessions.TryGetValue(key, out var session))
            return;

        if (session.Status is AssistantSessionStatus.RUNNING or AssistantSessionStatus.CANCELING)
            return;

        lock (session.SyncRoot)
        {
            session.Status = AssistantSessionStatus.NONE;
            session.ChatThread = null;
            session.State = new(StringComparer.Ordinal);
            session.UpdatedAt = DateTimeOffset.Now;
            session.FinishedAt = session.UpdatedAt;
        }

        this.sessions.TryRemove(key, out _);
        await messageBus.SendMessage(null, Event.ASSISTANT_SESSION_CHANGED, CreateSnapshot(session));
    }

    /// <summary>
    /// Clears all inactive sessions for a component.
    /// </summary>
    /// <param name="component">The component whose inactive sessions should be cleared.</param>
    public async Task ClearInactiveSessionsForComponentAsync(Components component)
    {
        var matchingKeys = this.sessions
            .Where(pair => pair.Key.Component == component && pair.Value.Status is not AssistantSessionStatus.RUNNING and not AssistantSessionStatus.CANCELING)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in matchingKeys)
            await this.ClearAsync(key);
    }

    /// <summary>
    /// Publishes an assistant session change event.
    /// </summary>
    /// <param name="session">The runtime session whose copied snapshot should be published.</param>
    /// <param name="sendingComponent">The component that initiated the session change.</param>
    private async Task NotifyChangedAsync(AssistantSessionState session, ComponentBase? sendingComponent = null)
    {
        await messageBus.SendMessage(sendingComponent, Event.ASSISTANT_SESSION_CHANGED, CreateSnapshot(session));
    }

    /// <summary>
    /// Creates a copied, external snapshot from the internal runtime state.
    /// </summary>
    /// <param name="session">The runtime session to copy.</param>
    /// <returns>A snapshot safe to send to UI components.</returns>
    private static AssistantSessionSnapshot CreateSnapshot(AssistantSessionState session)
    {
        lock (session.SyncRoot)
        {
            return CreateSnapshotWithoutLock(session);
        }
    }

    /// <summary>
    /// Creates a copied, external snapshot while the caller already holds the session lock.
    /// </summary>
    /// <param name="session">The runtime session to copy.</param>
    /// <returns>A snapshot safe to send to UI components.</returns>
    private static AssistantSessionSnapshot CreateSnapshotWithoutLock(AssistantSessionState session)
    {
        return new()
        {
            SessionId = session.SessionId,
            Key = session.Key,
            Title = session.Title,
            Status = session.Status,
            StartedAt = session.StartedAt,
            UpdatedAt = session.UpdatedAt,
            FinishedAt = session.FinishedAt,
            ErrorMessage = session.ErrorMessage,
            IsWaitingForToolApproval = session.IsWaitingForToolApproval,
            ChatThread = session.ChatThread,
            State = new Dictionary<string, IAssistantSessionSnapshotField>(session.State, StringComparer.Ordinal),
        };
    }
}