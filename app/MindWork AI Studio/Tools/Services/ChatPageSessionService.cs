using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services;

public sealed record ChatPageLayoutSnapshot(
    bool WorkspaceOverlayVisible,
    bool WorkspaceSearchVisible,
    string CurrentWorkspaceName,
    double SplitterPosition);

public sealed record ChatPageComponentSnapshot(
    ChatThread? ChatThread,
    string ProviderId,
    string CurrentProfileId,
    string CurrentChatTemplateId,
    string UserInput,
    bool HasUserDraft,
    IReadOnlyCollection<FileAttachment> FileAttachments,
    bool HasUnsavedChanges,
    bool AutoSaveEnabled,
    DataSourceOptions EarlyDataSourceOptions,
    DataSourceOptions LastAppliedStandardDataSourceOptions,
    string CurrentWorkspaceName,
    Guid CurrentWorkspaceId,
    Guid CurrentChatThreadId);

/// <summary>
/// Stores chat page state so the active chat page can be rebuilt after reconnect recovery.
/// </summary>
public sealed class ChatPageSessionService
{
    private readonly Lock syncRoot = new();
    private ChatPageLayoutSnapshot? layoutSnapshot;
    private ChatPageComponentSnapshot? componentSnapshot;

    public void StoreLayoutSnapshot(ChatPageLayoutSnapshot snapshot)
    {
        lock (this.syncRoot)
            this.layoutSnapshot = snapshot;
    }

    public ChatPageLayoutSnapshot? GetLayoutSnapshot()
    {
        lock (this.syncRoot)
            return this.layoutSnapshot;
    }

    public void StoreComponentSnapshot(ChatPageComponentSnapshot snapshot)
    {
        var normalizedAttachments = snapshot.FileAttachments
            .Select(attachment => attachment.Normalize())
            .ToArray();

        lock (this.syncRoot)
        {
            this.componentSnapshot = snapshot with
            {
                FileAttachments = normalizedAttachments,
                EarlyDataSourceOptions = snapshot.EarlyDataSourceOptions.CreateCopy(),
                LastAppliedStandardDataSourceOptions = snapshot.LastAppliedStandardDataSourceOptions.CreateCopy(),
            };
        }
    }

    public ChatPageComponentSnapshot? GetComponentSnapshot()
    {
        lock (this.syncRoot)
        {
            if (this.componentSnapshot is null)
                return null;

            return this.componentSnapshot with
            {
                FileAttachments = this.componentSnapshot.FileAttachments.ToArray(),
                EarlyDataSourceOptions = this.componentSnapshot.EarlyDataSourceOptions.CreateCopy(),
                LastAppliedStandardDataSourceOptions = this.componentSnapshot.LastAppliedStandardDataSourceOptions.CreateCopy(),
            };
        }
    }
}
