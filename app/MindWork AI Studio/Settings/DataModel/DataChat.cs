using System.Linq.Expressions;

namespace AIStudio.Settings.DataModel;

public sealed class DataChat(Expression<Func<Data, DataChat>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataChat() : this(null)
    {
    }

    /// <summary>
    /// Shortcuts to send the input to the AI.
    /// </summary>
    public SendBehavior ShortcutSendBehavior { get; set; } = SendBehavior.ENTER_IS_SENDING;

    /// <summary>
    /// Defines the provider behavior for loading a chat.
    /// </summary>
    public LoadingChatProviderBehavior LoadingProviderBehavior { get; set; } = LoadingChatProviderBehavior.USE_CHAT_PROVIDER_IF_AVAILABLE;

    /// <summary>
    /// Defines the provider behavior when adding a chat.
    /// </summary>
    public AddChatProviderBehavior AddChatProviderBehavior { get; set; } = AddChatProviderBehavior.ADDED_CHATS_USE_LATEST_PROVIDER;

    /// <summary>
    /// Defines the data source behavior when sending assistant results to a chat.
    /// </summary>
    public SendToChatDataSourceBehavior SendToChatDataSourceBehavior { get; set; } = ManagedConfiguration.Register(configSelection, n => n.SendToChatDataSourceBehavior, SendToChatDataSourceBehavior.NO_DATA_SOURCES);

    /// <summary>
    /// Preselect any chat options?
    /// </summary>
    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectOptions, false);

    /// <summary>
    /// Should we preselect a provider for the chat?
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProvider, string.Empty);
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProfile, string.Empty);
    
    /// <summary>
    /// Preselect a chat template?
    /// </summary>
    public string PreselectedChatTemplate { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedChatTemplate, string.Empty);
    
    /// <summary>
    /// Whether data sources are disabled by default for new chats.
    /// </summary>
    public bool PreselectedDataSourcesDisabled { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedDataSourcesDisabled, true);

    /// <summary>
    /// Whether data sources should be selected automatically by default for new chats.
    /// </summary>
    public bool PreselectedDataSourcesAutomaticSelection { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedDataSourcesAutomaticSelection, false);

    /// <summary>
    /// Whether retrieved data should be validated automatically by default for new chats.
    /// </summary>
    public bool PreselectedDataSourcesAutomaticValidation { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedDataSourcesAutomaticValidation, false);

    /// <summary>
    /// The data source IDs that should be preselected by default for new chats.
    /// </summary>
    public List<string> PreselectedDataSourceIds { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedDataSourceIds, []);

    /// <summary>
    /// Should we preselect data sources options for a created chat?
    /// </summary>
    // Compatibility shim: legacy settings used this nested object. See documentation/compatibility-shims/2026-07-chat-data-source-options.md; remove after 2027-01-05.
    public DataSourceOptions PreselectedDataSourceOptions
    {
        get => new()
        {
            DisableDataSources = this.PreselectedDataSourcesDisabled,
            AutomaticDataSourceSelection = this.PreselectedDataSourcesAutomaticSelection,
            AutomaticValidation = this.PreselectedDataSourcesAutomaticValidation,
            PreselectedDataSourceIds = [..this.PreselectedDataSourceIds],
        };
        set
        {
            this.PreselectedDataSourcesDisabled = value.DisableDataSources;
            this.PreselectedDataSourcesAutomaticSelection = value.AutomaticDataSourceSelection;
            this.PreselectedDataSourcesAutomaticValidation = value.AutomaticValidation;
            this.PreselectedDataSourceIds = [..value.PreselectedDataSourceIds];
        }
    }

    /// <summary>
    /// Should we show the latest message after loading? When false, we show the first (aka oldest) message.
    /// </summary>
    public bool ShowLatestMessageAfterLoading { get; set; } = true;
}