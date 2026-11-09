using System.Text.Json.Serialization;

namespace AIStudio.Settings.DataModel;

public sealed class DataWorkspace
{
    /// <summary>
    /// How long a chat stays when it is stored temporarily and the user did not choose a duration yet.
    /// </summary>
    private const WorkspaceStorageTemporaryMaintenancePolicy DEFAULT_TEMPORARY_POLICY = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS;

    /// <summary>
    /// How the chats outside of workspaces are stored: automatically, only when the user saves them, or
    /// not at all.
    /// </summary>
    /// <remarks>
    /// The chat settings offer it, together with <see cref="this.StorageTemporaryMaintenancePolicy"/>, as one
    /// choice, see <see cref="this.GetChoice"/>. It lives here and keeps the names of its values, so that
    /// older versions of AI Studio still read the settings file.
    /// </remarks>
    public WorkspaceStorageBehavior StorageBehavior { get; set; } = WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY;

    /// <summary>
    /// When disappearing chats are deleted, that is, the stored chats outside of a workspace, counted from
    /// their last change.
    /// </summary>
    /// <remarks>
    /// While chats are stored only when the user saves them, no chat is deleted, whatever this says, see
    /// <see cref="this.GetDeletionPolicy"/>.
    /// </remarks>
    public WorkspaceStorageTemporaryMaintenancePolicy StorageTemporaryMaintenancePolicy { get; set; } = DEFAULT_TEMPORARY_POLICY;

    /// <summary>
    /// How the chats in workspaces are stored, or null while the user never chose.
    /// </summary>
    /// <remarks>
    /// Null in every settings file written before chats in workspaces had a choice of their own. They
    /// are then stored as all other chats are, but never deleted, as before. Changing the choice for all
    /// other chats does not change the one for chats in workspaces, see <see cref="this.SetChoice"/>.
    /// </remarks>
    public WorkspaceStorageBehavior? WorkspaceChatsStorageBehavior { get; set; }

    /// <summary>
    /// When chats in workspaces are deleted, counted from their last change, or null while the user never
    /// chose: then never.
    /// </summary>
    public WorkspaceStorageTemporaryMaintenancePolicy? WorkspaceChatsMaintenancePolicy { get; set; }

    /// <summary>
    /// How a chat is stored: by the choice for chats in workspaces when it lies in one, otherwise by the
    /// choice for all other chats.
    /// </summary>
    /// <param name="isInWorkspace">Whether the chat lies in a workspace.</param>
    public WorkspaceStorageBehavior GetStorageBehavior(bool isInWorkspace) => isInWorkspace
        ? this.WorkspaceChatsStorageBehavior ?? this.StorageBehavior
        : this.StorageBehavior;

    /// <inheritdoc cref="this.GetStorageBehavior(bool)"/>
    /// <param name="workspaceId">The workspace the chat lies in, or <see cref="Guid.Empty"/> for a chat outside of a workspace.</param>
    public WorkspaceStorageBehavior GetStorageBehavior(Guid workspaceId) => this.GetStorageBehavior(isInWorkspace: workspaceId != Guid.Empty);

    /// <summary>
    /// Whether a chat is stored as it changes, by the choice for the place it lies in.
    /// </summary>
    /// <param name="workspaceId">The workspace the chat lies in, or <see cref="Guid.Empty"/> for a chat outside of a workspace.</param>
    public bool IsStoredAutomatically(Guid workspaceId) => this.GetStorageBehavior(workspaceId) is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY;

    /// <summary>
    /// When the maintenance deletes the stored chats of a place, counted from their last change.
    /// </summary>
    /// <remarks>
    /// A chat the user saved on purpose stays until the user deletes it. Otherwise, the duration of the
    /// place applies to all its chats, however they were stored back then. While no chat is stored, it
    /// still applies to the chats stored before.
    /// </remarks>
    /// <param name="isInWorkspace">Whether to ask about the chats in workspaces, or about all other chats.</param>
    public WorkspaceStorageTemporaryMaintenancePolicy GetDeletionPolicy(bool isInWorkspace) => this.GetStorageBehavior(isInWorkspace) is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY
        ? WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE
        : this.GetDuration(isInWorkspace);

    /// <summary>
    /// How long the chats of a place stay while they are stored temporarily.
    /// </summary>
    /// <param name="isInWorkspace">Whether to ask about the chats in workspaces, or about all other chats.</param>
    public WorkspaceStorageTemporaryMaintenancePolicy GetDuration(bool isInWorkspace) => isInWorkspace
        ? this.WorkspaceChatsMaintenancePolicy ?? WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE
        : this.StorageTemporaryMaintenancePolicy;

    /// <summary>
    /// Sets how long the chats of a place stay while they are stored temporarily.
    /// </summary>
    /// <param name="isInWorkspace">Whether to set it for the chats in workspaces, or for all other chats.</param>
    /// <param name="duration">The duration, counted from the last change of a chat.</param>
    public void SetDuration(bool isInWorkspace, WorkspaceStorageTemporaryMaintenancePolicy duration)
    {
        if (isInWorkspace)
            this.WorkspaceChatsMaintenancePolicy = duration;
        else
            this.StorageTemporaryMaintenancePolicy = duration;
    }

    /// <summary>
    /// Whether and how long the chats of a place are kept, as the settings offer it.
    /// </summary>
    /// <param name="isInWorkspace">Whether to ask about the chats in workspaces, or about all other chats.</param>
    public ChatStorageChoice GetChoice(bool isInWorkspace) => this.GetStorageBehavior(isInWorkspace) switch
    {
        WorkspaceStorageBehavior.DISABLE_WORKSPACES => ChatStorageChoice.DO_NOT_STORE,
        WorkspaceStorageBehavior.STORE_CHATS_MANUALLY => ChatStorageChoice.STORE_ON_SAVE,
        _ => this.GetDuration(isInWorkspace) is WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE
            ? ChatStorageChoice.STORE_INDEFINITELY
            : ChatStorageChoice.STORE_TEMPORARILY,
    };

    /// <summary>
    /// Sets whether and how long the chats of a place are kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chats in workspaces keep their choice when the one for all other chats changes, also when they
    /// had the same so far: whoever changes how chats are kept in general does not mean to change the
    /// workspaces as well. "Same as for all chats" takes over what all other chats get at that moment.
    /// </para>
    /// <para>
    /// Not storing, or storing on save, leaves the duration as it was: it still decides about the chats
    /// stored before, or applies again once the user stores temporarily again.
    /// </para>
    /// </remarks>
    /// <param name="isInWorkspace">Whether to set it for the chats in workspaces, or for all other chats.</param>
    /// <param name="choice">The choice.</param>
    public void SetChoice(bool isInWorkspace, ChatStorageChoice choice)
    {
        if (isInWorkspace)
        {
            if (choice is ChatStorageChoice.SAME_AS_ALL_CHATS)
            {
                choice = this.GetChoice(isInWorkspace: false);
                if (choice is ChatStorageChoice.STORE_TEMPORARILY)
                    this.WorkspaceChatsMaintenancePolicy = this.StorageTemporaryMaintenancePolicy;
            }
        }
        else
        {
            this.WorkspaceChatsStorageBehavior ??= this.StorageBehavior;
            this.WorkspaceChatsMaintenancePolicy ??= WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE;
        }

        var behavior = choice switch
        {
            ChatStorageChoice.DO_NOT_STORE => WorkspaceStorageBehavior.DISABLE_WORKSPACES,
            ChatStorageChoice.STORE_ON_SAVE => WorkspaceStorageBehavior.STORE_CHATS_MANUALLY,
            _ => WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY,
        };

        var duration = this.GetDuration(isInWorkspace);
        duration = choice switch
        {
            ChatStorageChoice.STORE_INDEFINITELY => WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE,
            ChatStorageChoice.STORE_TEMPORARILY when duration is WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE => DEFAULT_TEMPORARY_POLICY,
            _ => duration,
        };

        if (isInWorkspace)
            this.WorkspaceChatsStorageBehavior = behavior;
        else
            this.StorageBehavior = behavior;

        this.SetDuration(isInWorkspace, duration);
    }

    /// <summary>
    /// Whether the user switched workspaces on or off, or null while the user never did.
    /// </summary>
    /// <remarks>
    /// Null in every settings file written before workspaces had a switch of their own. Workspaces then
    /// follow the storing of chats, as they did before: whoever chose to store no chat had switched
    /// workspaces off with it, and must not find them back. Ask <see cref="this.UsesWorkspaces"/>.
    /// </remarks>
    public bool? AreWorkspacesEnabled { get; set; }

    /// <summary>
    /// Whether the user uses workspaces, whatever happens to new chats.
    /// </summary>
    /// <remarks>
    /// Switched off, the sidebar lists the disappearing chats only, while the chats in workspaces stay
    /// where they are and come back once the user switches workspaces on again. Switched on while chats in
    /// workspaces are not stored, the workspaces keep what they hold, but no new chat goes into one, see
    /// <see cref="this.CanChatsGoIntoWorkspaces"/>.
    /// </remarks>
    [JsonIgnore]
    public bool UsesWorkspaces => this.AreWorkspacesEnabled ?? this.AreChatsStored;

    /// <summary>
    /// Whether the chats outside of workspaces are stored at all.
    /// </summary>
    /// <remarks>
    /// Otherwise, such a chat lasts only while AI Studio keeps it in memory, which is a matter of privacy.
    /// </remarks>
    [JsonIgnore]
    public bool AreChatsStored => this.StorageBehavior is not WorkspaceStorageBehavior.DISABLE_WORKSPACES;

    /// <summary>
    /// Whether a chat which may not be stored yet can go into a workspace: the open chat, or a chat a
    /// template starts. Putting it there stores it, so this needs workspaces which store chats.
    /// </summary>
    [JsonIgnore]
    public bool CanChatsGoIntoWorkspaces => this.UsesWorkspaces && this.GetStorageBehavior(isInWorkspace: true) is not WorkspaceStorageBehavior.DISABLE_WORKSPACES;

    /// <summary>
    /// Whether the sidebar has something to list: the stored chats outside of workspaces, or the
    /// workspaces.
    /// </summary>
    [JsonIgnore]
    public bool IsChatListShown => this.AreChatsStored || this.UsesWorkspaces;

    /// <summary>
    /// The behavior used for displaying the workspace.
    /// </summary>
    public WorkspaceDisplayBehavior DisplayBehavior { get; set; } = WorkspaceDisplayBehavior.TOGGLE_SIDEBAR;

    /// <summary>
    /// Indicates whether the sidebar is currently visible.
    /// </summary>
    public bool IsSidebarVisible { get; set; } = true;

    /// <summary>
    /// Whether the section with the workspaces is collapsed in the sidebar.
    /// </summary>
    public bool IsWorkspacesSectionCollapsed { get; set; }

    /// <summary>
    /// Whether the section with the disappearing chats is collapsed in the sidebar.
    /// </summary>
    public bool IsTemporaryChatsSectionCollapsed { get; set; }

    /// <summary>
    /// The width of the workspace sidebar beside the chat, in CSS pixels, as the user dragged it.
    /// </summary>
    /// <remarks>
    /// The chat page keeps the sidebar within limits whatever the stored width, see Chat.razor.css.
    /// </remarks>
    public double SidebarWidth { get; set; } = 288;
}