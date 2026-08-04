using AIStudio.Chat;

namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Immutable-style view of an assistant session for UI consumers and message bus events.
/// </summary>
/// <remarks>
/// The service creates snapshots by copying its internal runtime state. Consumers must
/// treat the contained chat thread and state objects as read-only views of the session.
/// </remarks>
public sealed record AssistantSessionSnapshot
{
    /// <summary>
    /// Identifies the concrete run represented by this snapshot.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Identifies the assistant and logical session slot represented by this snapshot.
    /// </summary>
    public required AssistantSessionKey Key { get; init; }

    /// <summary>
    /// Gets the user-visible assistant title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the current lifecycle status.
    /// </summary>
    public required AssistantSessionStatus Status { get; init; }

    /// <summary>
    /// Gets when the session run started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets when the session was last changed.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets when the session reached a terminal state.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>
    /// Gets the user-visible error message for failed sessions.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assistant chat thread captured for this session.
    /// </summary>
    public ChatThread? ChatThread { get; init; }

    /// <summary>
    /// Gets the assistant component state captured for this session.
    /// </summary>
    public IReadOnlyDictionary<string, IAssistantSessionSnapshotField> State { get; init; } = new Dictionary<string, IAssistantSessionSnapshotField>();

    /// <summary>
    /// Gets whether the session is still running or canceling.
    /// </summary>
    public bool IsActive => this.Status is AssistantSessionStatus.RUNNING or AssistantSessionStatus.CANCELING;
}