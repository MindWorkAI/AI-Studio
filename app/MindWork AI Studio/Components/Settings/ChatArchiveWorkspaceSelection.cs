namespace AIStudio.Components.Settings;

/// <summary>
/// One workspace as shown in the export list.
/// </summary>
public sealed class ChatArchiveWorkspaceSelection
{
    /// <summary>
    /// The unique identifier of the workspace.
    /// </summary>
    public Guid WorkspaceId { get; init; }

    /// <summary>
    /// The name of the workspace.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The number of chats in this workspace.
    /// </summary>
    public int ChatCount { get; init; }

    /// <summary>
    /// Whether the user selected this workspace for the export.
    /// </summary>
    public bool IsSelected { get; set; }
}