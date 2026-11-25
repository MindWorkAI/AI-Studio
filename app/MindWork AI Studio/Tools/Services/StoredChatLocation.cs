using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tools.Services;

/// <summary>
/// Where the cleanup of the provenance store at the start finds a chat: in a workspace, among the temporary chats,
/// in the BeyondFacts Lab, or in a comparison series of the Model Comparison assistant.
/// </summary>
/// <remarks>
/// Create it with InWorkspace, InLab, or InComparisonSeries.
/// </remarks>
/// <param name="WorkspaceId">The workspace of a chat in a workspace, Guid.Empty for a temporary chat or a chat of an assistant.</param>
/// <param name="LabThread">Where a chat of the lab belongs, or null for any other chat.</param>
/// <param name="ComparisonSeriesId">The comparison series of a chat of the Model Comparison assistant, or null for any other chat.</param>
public readonly record struct StoredChatLocation(Guid WorkspaceId, BflThreadLocation? LabThread, Guid? ComparisonSeriesId = null)
{
    /// <summary>
    /// Gets the place of a chat in a workspace or among the temporary chats.
    /// </summary>
    /// <param name="workspaceId">The workspace, Guid.Empty for a temporary chat.</param>
    public static StoredChatLocation InWorkspace(Guid workspaceId) => new(workspaceId, null);

    /// <summary>
    /// Gets the place of a chat of the BeyondFacts Lab.
    /// </summary>
    /// <param name="thread">Where the chat belongs in the lab.</param>
    public static StoredChatLocation InLab(BflThreadLocation thread) => new(Guid.Empty, thread);

    /// <summary>
    /// Gets the place of a chat of a comparison series of the Model Comparison assistant.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    public static StoredChatLocation InComparisonSeries(Guid seriesId) => new(Guid.Empty, null, seriesId);
}