namespace AIStudio.Settings.DataModel;

/// <summary>
/// The data model for the settings file.
/// </summary>
public sealed class Data
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings
    /// when a new version is available.
    /// </summary>
    public Version Version { get; init; } = Version.V3;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; init; } = [];

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    /// <summary>
    /// Should we save energy? When true, we will update content streamed
    /// from the server, i.e., AI, less frequently.
    /// </summary>
    public bool IsSavingEnergy { get; set; }

    /// <summary>
    /// Shortcuts to send the input to the AI.
    /// </summary>
    public SendBehavior ShortcutSendBehavior { get; set; } = SendBehavior.MODIFER_ENTER_IS_SENDING;
    
    /// <summary>
    /// Should we enable spellchecking for all input fields?
    /// </summary>
    public bool EnableSpellchecking { get; set; }

    /// <summary>
    /// If and when we should look for updates.
    /// </summary>
    public UpdateBehavior UpdateBehavior { get; set; } = UpdateBehavior.ONCE_STARTUP;
    
    /// <summary>
    /// The chat storage behavior.
    /// </summary>
    public WorkspaceStorageBehavior WorkspaceStorageBehavior { get; set; } = WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY;
    
    /// <summary>
    /// The chat storage maintenance behavior.
    /// </summary>
    public WorkspaceStorageTemporaryMaintenancePolicy WorkspaceStorageTemporaryMaintenancePolicy { get; set; } = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS;
    
    /// <summary>
    /// The navigation behavior.
    /// </summary>
    public NavBehavior NavigationBehavior { get; set; } = NavBehavior.EXPAND_ON_HOVER;
}