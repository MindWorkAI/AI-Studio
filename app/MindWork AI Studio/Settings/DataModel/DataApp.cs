using System.Linq.Expressions;

using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public sealed class DataApp(Expression<Func<Data, DataApp>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataApp() : this(null)
    {
    }
    
    /// <summary>
    /// The language behavior.
    /// </summary>
    public LangBehavior LanguageBehavior { get; set; } = LangBehavior.AUTO;
    
    /// <summary>
    /// The language plugin ID to use.
    /// </summary>
    public Guid LanguagePluginId { get; set; } = Guid.Empty;
    
    /// <summary>
    /// The preferred theme to use.
    /// </summary>
    public Themes PreferredTheme { get; set; } = Themes.SYSTEM;
    
    /// <summary>
    /// Should we save energy? When true, we will update content streamed
    /// from the server, i.e., AI, less frequently.
    /// </summary>
    public bool IsSavingEnergy { get; set; } = ManagedConfiguration.Register(configSelection, n => n.IsSavingEnergy, false);
    
    /// <summary>
    /// Should we enable spellchecking for all input fields?
    /// </summary>
    public bool EnableSpellchecking { get; set; }

    /// <summary>
    /// Should the message about an exported file close after a few seconds? When false, it stays until
    /// the user closes it, so that its buttons to the file remain at hand.
    /// </summary>
    public bool CloseExportMessagesAutomatically { get; set; }

    /// <summary>
    /// If and when we should look for updates.
    /// </summary>
    public UpdateInterval UpdateInterval { get; set; } = ManagedConfiguration.Register(configSelection, n => n.UpdateInterval, UpdateInterval.HOURLY);

    /// <summary>
    /// How updates should be installed.
    /// </summary>
    public UpdateInstallation UpdateInstallation { get; set; } = ManagedConfiguration.Register(configSelection, n => n.UpdateInstallation, UpdateInstallation.MANUAL);
    
    /// <summary>
    /// The navigation behavior.
    /// </summary>
    public NavBehavior NavigationBehavior { get; set; } = NavBehavior.NEVER_EXPAND_USE_TOOLTIPS;

    /// <summary>
    /// Which page should be opened first when the app starts?
    /// </summary>
    public StartPage StartPage { get; set; } = ManagedConfiguration.Register(configSelection, n => n.StartPage, StartPage.HOME);

    /// <summary>
    /// Whether an alert dialog should be shown when prompt-injection content is blocked.
    /// </summary>
    public bool ShowPromptInjectionAlert { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowPromptInjectionAlert, true);

    /// <summary>
    /// Should the built-in introduction be visible on the home page?
    /// </summary>
    public bool ShowIntroduction { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowIntroduction, true);

    /// <summary>
    /// Should the quick start guide be visible on the home page?
    /// </summary>
    public bool ShowQuickStartGuide { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowQuickStartGuide, true);

    /// <summary>
    /// Should the last changelog be visible on the home page?
    /// </summary>
    public bool ShowLastChangelog { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowLastChangelog, true);

    /// <summary>
    /// Should the vision panel be visible on the home page?
    /// </summary>
    public bool ShowVision { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowVision, true);

    /// <summary>
    /// The visibility setting for previews features.
    /// </summary>
    public PreviewVisibility PreviewVisibility { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreviewVisibility, PreviewVisibility.NONE);

    /// <summary>
    /// The enabled preview features.
    /// </summary>
    public HashSet<PreviewFeatures> EnabledPreviewFeatures { get; set; } = ManagedConfiguration.Register(configSelection, n => n.EnabledPreviewFeatures, []);
    
    /// <summary>
    /// Should we preselect a provider for the entire app?
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProvider, string.Empty);
    
    /// <summary>
    /// Should we preselect a profile for the entire app?
    /// </summary>
    public string PreselectedProfile { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProfile, string.Empty);
    
    /// <summary>
    /// Should we preselect a chat template for the entire app?
    /// </summary>
    public string PreselectedChatTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Which transcription provider should be used?
    /// </summary>
    public string UseTranscriptionProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.UseTranscriptionProvider, string.Empty);

    /// <summary>
    /// The Opus bitrate used when normalizing uploaded audio/video for transcription.
    /// </summary>
    /// <remarks>
    /// Every recording is re-encoded to mono Opus before it goes to the transcription provider. That
    /// encoding used to be fixed at 32 kbps, which cost the transcription models entire quiet passages:
    /// a greeting spoken softly at the start of a recording was simply missing from the transcript. The
    /// same recording compared at 64 and 128 kbps came back complete, which is why 128 kbps is the
    /// default here. Users trading accuracy for a smaller upload can still pick a lower bitrate.
    /// </remarks>
    public TranscriptionOpusBitrate OpusBitrate { get; set; } = ManagedConfiguration.Register(configSelection, n => n.OpusBitrate, TranscriptionOpusBitrate.KBPS_128);

    /// <summary>
    /// The global keyboard shortcut for toggling voice recording.
    /// Uses Tauri's shortcut format, e.g., "CmdOrControl+1" (Cmd+1 on macOS, Ctrl+1 on Windows/Linux).
    /// Set to empty string to disable the global shortcut.
    /// </summary>
    public string ShortcutVoiceRecording { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShortcutVoiceRecording, string.Empty);

    /// <summary>
    /// The user-facing label for the voice recording shortcut, based on the user's keyboard layout.
    /// </summary>
    public string ShortcutVoiceRecordingDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The canonical voice recording shortcut value this display label belongs to.
    /// </summary>
    public string ShortcutVoiceRecordingDisplaySource { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP timeout in seconds for external HTTP clients.
    /// </summary>
    public int HttpClientTimeoutSeconds { get; set; } = ManagedConfiguration.Register(configSelection, n => n.HttpClientTimeoutSeconds, ExternalHttpClientTimeout.DEFAULT_HTTP_CLIENT_TIMEOUT_SECONDS);

    /// <summary>
    /// Should requests to self-hosted servers, i.e., self-hosted providers and LiteLLM, name the feature that sent them?
    /// </summary>
    /// <remarks>
    /// Whoever operates such a server learns about it, which usually is an organization. Off by default:
    /// AI Studio then sends no User-Agent at all. Cloud providers never learn about it either way.
    /// </remarks>
    public bool ShareFeatureUsageWithSelfHostedServerOperators { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShareFeatureUsageWithSelfHostedServerOperators, false);

    /// <summary>
    /// Should external HTTP clients trust additional root certificates from a configured PEM bundle?
    /// </summary>
    public bool ExternalHttpCustomRootCertificatesEnabled { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ExternalHttpCustomRootCertificatesEnabled, false);

    /// <summary>
    /// Path to a PEM bundle containing additional root certificates for external HTTP clients.
    /// </summary>
    public string ExternalHttpCustomRootCertificateBundlePath { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ExternalHttpCustomRootCertificateBundlePath, string.Empty);

    /// <summary>
    /// Hostnames for which external HTTP clients may use the additional root certificates.
    /// </summary>
    public HashSet<string> ExternalHttpCustomRootCertificateAllowedHosts { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ExternalHttpCustomRootCertificateAllowedHosts, []);

    /// <summary>
    /// Should the user be allowed to add providers?
    /// </summary>
    public bool AllowUserToAddProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToAddProvider, true);

    /// <summary>
    /// Should the user be allowed to add LLM providers?
    /// </summary>
    public bool AllowUserToAddLLMProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToAddLLMProvider, true);

    /// <summary>
    /// Should the user be allowed to add embedding providers?
    /// </summary>
    public bool AllowUserToAddEmbeddingProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToAddEmbeddingProvider, true);

    /// <summary>
    /// Should the user be allowed to add transcription providers?
    /// </summary>
    public bool AllowUserToAddTranscriptionProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToAddTranscriptionProvider, true);

    /// <summary>
    /// The providers an organization prohibited, for LLMs, embeddings, and transcriptions alike.
    /// </summary>
    /// <remarks>
    /// A prohibition names the operator of an API, not a family of models: prohibiting the API of a
    /// model maker leaves its models reachable through a hub or a self-hosted server. Providers the
    /// user set up before stay in the settings, so they are back once the prohibition ends, but
    /// AI Studio neither offers nor contacts them, see SettingsManager.IsProviderProhibited.
    /// </remarks>
    public HashSet<LLMProviders> ProhibitedProviders { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ProhibitedProviders, []);

    /// <summary>
    /// Should the user be allowed to add mailboxes?
    /// </summary>
    /// <remarks>
    /// Independent of AllowUserToAddProvider, because a mailbox is a data source, not a provider.
    /// Mailboxes the user added before stay, and the user may still edit or delete them.
    /// </remarks>
    public bool AllowUserToAddMailbox { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToAddMailbox, true);

    /// <summary>
    /// Should the user be allowed to import configurations which others exported?
    /// </summary>
    /// <remarks>
    /// The master switch of the import: the switch of each type allows its import only while this
    /// one does as well. Use CanImportConfigurationSnippet rather than reading the switches one by one.
    /// </remarks>
    public bool AllowUserToImportConfigurationSnippets { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportConfigurationSnippets, true);

    /// <summary>
    /// Should the user be allowed to import profiles?
    /// </summary>
    public bool AllowUserToImportProfile { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportProfile, true);

    /// <summary>
    /// Should the user be allowed to import LLM providers?
    /// </summary>
    /// <remarks>
    /// An import adds a provider, so AllowUserToAddProvider and AllowUserToAddLLMProvider must allow it as well.
    /// </remarks>
    public bool AllowUserToImportLLMProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportLLMProvider, true);

    /// <summary>
    /// Should the user be allowed to import embedding providers?
    /// </summary>
    /// <remarks>
    /// An import adds a provider, so AllowUserToAddProvider and AllowUserToAddEmbeddingProvider must allow it as well.
    /// </remarks>
    public bool AllowUserToImportEmbeddingProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportEmbeddingProvider, true);

    /// <summary>
    /// Should the user be allowed to import transcription providers?
    /// </summary>
    /// <remarks>
    /// An import adds a provider, so AllowUserToAddProvider and AllowUserToAddTranscriptionProvider must allow it as well.
    /// </remarks>
    public bool AllowUserToImportTranscriptionProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportTranscriptionProvider, true);

    /// <summary>
    /// Should the user be allowed to import chat templates?
    /// </summary>
    public bool AllowUserToImportChatTemplate { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportChatTemplate, true);

    /// <summary>
    /// Should the user be allowed to import external data sources (ERI servers)?
    /// </summary>
    public bool AllowUserToImportERIDataSource { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportERIDataSource, true);

    /// <summary>
    /// Should the user be allowed to import document analysis policies?
    /// </summary>
    public bool AllowUserToImportDocumentAnalysisPolicy { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportDocumentAnalysisPolicy, true);

    /// <summary>
    /// Should the user be allowed to import format templates?
    /// </summary>
    public bool AllowUserToImportFormatTemplate { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportFormatTemplate, true);

    /// <summary>
    /// May the user import a configuration of this type, which somebody else exported?
    /// </summary>
    /// <param name="type">The type of configuration object to import.</param>
    /// <returns>True when the master switch and the switch of the type allow it, and for providers also the permission to add one.</returns>
    public bool CanImportConfigurationSnippet(PluginConfigurationObjectType type) => this.AllowUserToImportConfigurationSnippets && type switch
    {
        PluginConfigurationObjectType.PROFILE => this.AllowUserToImportProfile,
        PluginConfigurationObjectType.LLM_PROVIDER => this.AllowUserToImportLLMProvider && this.AllowUserToAddProvider && this.AllowUserToAddLLMProvider,
        PluginConfigurationObjectType.EMBEDDING_PROVIDER => this.AllowUserToImportEmbeddingProvider && this.AllowUserToAddProvider && this.AllowUserToAddEmbeddingProvider,
        PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER => this.AllowUserToImportTranscriptionProvider && this.AllowUserToAddProvider && this.AllowUserToAddTranscriptionProvider,
        PluginConfigurationObjectType.CHAT_TEMPLATE => this.AllowUserToImportChatTemplate,
        PluginConfigurationObjectType.DATA_SOURCE => this.AllowUserToImportERIDataSource,
        PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY => this.AllowUserToImportDocumentAnalysisPolicy,
        PluginConfigurationObjectType.FORMAT_TEMPLATE => this.AllowUserToImportFormatTemplate,

        _ => false,
    };

    /// <summary>
    /// Should the user be allowed to import plugin archives from disk?
    /// </summary>
    public bool AllowUserToImportPlugins { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportPlugins, true);

    /// <summary>
    /// Should the user be allowed to import configuration plugin archives from disk?
    /// </summary>
    /// <remarks>
    /// This is a second gate on top of AllowUserToImportPlugins, and both must allow the import.
    /// Configuration plugins deserve their own switch because they are far more powerful than an
    /// assistant: they define LLM providers and data sources, and they lock settings.
    /// </remarks>
    public bool AllowUserToImportConfigurationPlugins { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToImportConfigurationPlugins, true);

    /// <summary>
    /// Should the user be allowed to share or export plugins as archives?
    /// </summary>
    public bool AllowUserToSharePlugins { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToSharePlugins, true);

    /// <summary>
    /// Should the user be allowed to back up the data of AI Studio into a file?
    /// </summary>
    /// <remarks>
    /// A backup holds chats and other confidential content in a single file, which can leave the
    /// computer on any stick or cloud drive. Secrets never go into it.
    /// </remarks>
    public bool AllowUserToBackUpData { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToBackUpData, true);

    /// <summary>
    /// Should the user be allowed to restore the data of AI Studio from a backup file, or to add the chats of one?
    /// </summary>
    /// <remarks>
    /// A backup brings along everything its chats and settings name, including the locations of
    /// attached documents and of local data sources, which are read again afterward.
    /// </remarks>
    public bool AllowUserToRestoreData { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToRestoreData, true);

    /// <summary>
    /// Should administration settings be visible in the UI?
    /// </summary>
    public bool ShowAdminSettings { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowAdminSettings, false);

    /// <summary>
    /// In which format do exports encrypt API keys, tokens, and passwords with the enterprise encryption secret?
    /// </summary>
    /// <remarks>
    /// New installations write the authenticated format v2. Installations which existed before
    /// v26.10.1 keep writing v1, which older versions read as well: large organizations package
    /// updates themselves, so their administrators run a new version weeks before everybody else.
    /// SettingsManager tells the two kinds of installations apart.
    /// </remarks>
    public EnterpriseEncryptionFormat EnterpriseEncryptionExportFormat { get; set; } = ManagedConfiguration.Register(configSelection, n => n.EnterpriseEncryptionExportFormat, EnterpriseEncryptionFormat.V2);

    /// <summary>
    /// Should AI answers which are copied or exported end with a notice that AI generated them?
    /// </summary>
    /// <remarks>
    /// Off by default. AIGeneratedContentDisclosureService explains which content gets the notice and why.
    /// </remarks>
    public bool AddAIGeneratedContentDisclosure { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AddAIGeneratedContentDisclosure, false);

    /// <summary>
    /// Settings for indexing local data sources.
    /// </summary>
    public DataDataSourceIndexing DataSourceIndexing { get; init; } = new(x => x.App.DataSourceIndexing);

    /// <summary>
    /// List of assistants that should be hidden from the UI.
    /// </summary>
    public HashSet<ConfigurableAssistant> HiddenAssistants { get; set; } = ManagedConfiguration.Register(configSelection, n => n.HiddenAssistants, []);
}