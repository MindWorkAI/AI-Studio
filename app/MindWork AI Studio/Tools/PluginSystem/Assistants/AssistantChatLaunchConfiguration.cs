namespace AIStudio.Tools.PluginSystem.Assistants;

/// <param name="WorkspaceName">The workspace the chat is created in. An empty name means the launcher opens a chat without a workspace, which the app shows as a disappearing chat.</param>
/// <param name="ToolIds">The tools preselected for the chat, or null when the launcher names none.</param>
public sealed record AssistantChatLaunchConfiguration(string WorkspaceName, Guid? ProviderId, Guid? ProfileId, Guid? ChatTemplateId, IReadOnlyList<Guid>? DataSourceIds, IReadOnlyList<string>? ToolIds)
{
    /// <summary>
    /// Whether the launcher opens a chat that belongs to no workspace. The missing workspace name is
    /// the whole condition, so the launch behavior and the written plugin follow from it.
    /// </summary>
    public bool OpensTemporaryChat => string.IsNullOrWhiteSpace(this.WorkspaceName);
}
