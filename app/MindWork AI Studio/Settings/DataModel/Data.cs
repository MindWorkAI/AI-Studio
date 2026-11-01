using AIStudio.Tools.PluginSystem.Assistants;

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
    public Version Version { get; set; } = Version.V6;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; set; } = [];
    
    /// <summary>
    /// Settings concerning confidence levels.
    /// </summary>
    public DataConfidence Confidence { get; set; } = new(x => x.Confidence);

    /// <summary>
    /// Settings concerning data source security checks.
    /// </summary>
    public DataSourceSecuritySettings DataSourceSecurity { get; set; } = new(x => x.DataSourceSecurity);

    /// <summary>
    /// A collection of embedding providers configured.
    /// </summary>
    public List<EmbeddingProvider> EmbeddingProviders { get; set; } = [];
    
    /// <summary>
    /// A collection of speech providers configured.
    /// </summary>
    public List<TranscriptionProvider> TranscriptionProviders { get; set; } = [];

    /// <summary>
    /// A collection of data sources configured.
    /// </summary>
    public List<IDataSource> DataSources { get; set; } = [];

    /// <summary>
    /// A collection of mailboxes configured.
    /// </summary>
    /// <remarks>
    /// A list of its own rather than a part of DataSources: classic RAG, Semantic Search and the
    /// agents read DataSources, and none of them is meant to see a mailbox. It also keeps the
    /// settings readable for older versions of AI Studio, so the settings version stays the same.
    /// An older version skips a property it does not know, but would fail to read the whole file
    /// over a data source type it does not know. It drops the list, though, the next time it stores
    /// the settings. The numbers come from NextDataSourceNum, which the data sources use as well.
    /// </remarks>
    public List<DataSourceMailbox> Mailboxes { get; set; } = [];

    /// <summary>
    /// Settings which apply to all mailboxes.
    /// </summary>
    public DataMailboxes MailboxSettings { get; set; } = new(x => x.MailboxSettings);

    /// <summary>
    /// List of configured profiles.
    /// </summary>
    public List<Profile> Profiles { get; set; } = [];
    
    /// <summary>
    /// List of configured chat templates.
    /// </summary>
    public List<ChatTemplate> ChatTemplates { get; set; } = [];

    /// <summary>
    /// List of enabled plugins.
    /// </summary>
    public List<Guid> EnabledPlugins { get; set; } = [];

    /// <summary>
    /// Metadata for managed settings that use a plugin-provided editable default.
    /// </summary>
    public Dictionary<string, ManagedEditableDefaultState> ManagedEditableDefaults { get; set; } = [];

    /// <summary>
    /// The configuration plugin that owns each locked managed setting.
    /// </summary>
    public Dictionary<string, Guid> ManagedLockedConfigurations { get; set; } = [];

    /// <summary>
    /// The value each managed setting had before a configuration plugin took it over, as JSON.
    /// </summary>
    /// <remarks>
    /// A configuration plugin might be removed later, e.g. when a test configuration ends or when an
    /// organization withdraws its configuration. The value the user had chosen before belongs to the
    /// user, so we keep it here and restore it instead of falling back to the app's default value.
    /// The snapshot is taken once, when a setting becomes managed, and is consumed when no
    /// configuration plugin manages that setting anymore.
    /// </remarks>
    public Dictionary<string, string> ManagedUserValueSnapshots { get; set; } = [];

    /// <summary>
    /// Cached audit results for assistant plugins.
    /// </summary>
    public List<PluginAssistantAudit> AssistantPluginAudits { get; set; } = [];

    /// <summary>
    /// The assistant plugin hashes whose organization default for the activation was already applied.
    /// </summary>
    /// <remarks>
    /// An organization may enable an assistant plugin it approved while still letting the user switch
    /// it off again. That is a default, not a rule, so it must be applied exactly once: applying it on
    /// every start would keep switching the assistant back on against the user's decision. We remember
    /// the hashes it was applied for, and forget one as soon as no approval asks for it anymore, so a
    /// later rollout of the same plugin takes effect again. Activations the user may not override are
    /// not listed here: those are decided live and never touch the list of enabled plugins.
    /// </remarks>
    public List<string> AppliedEnterpriseAssistantActivations { get; set; } = [];

    /// <summary>
    /// Whether AI Studio already stored the chats and the settings once more, so that every path below
    /// the data directory in them is relative.
    /// </summary>
    /// <remarks>
    /// Compatibility shim, see documentation/compatibility-shims/2026-10-relative-data-paths.md.
    /// Remove after 2027-04-14.
    /// </remarks>
    public bool StoredDataPathsAreRelative { get; set; }

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    /// <summary>
    /// The next embedding provider number to use.
    /// </summary>
    public uint NextEmbeddingNum { get; set; } = 1;
    
    /// <summary>
    /// The next transcription provider number to use.
    /// </summary>
    public uint NextTranscriptionNum { get; set; } = 1;

    /// <summary>
    /// The next data source number to use.
    /// </summary>
    public uint NextDataSourceNum { get; set; } = 1;

    /// <summary>
    /// The next profile number to use.
    /// </summary>
    public uint NextProfileNum { get; set; } = 1;
    
    /// <summary>
    /// The next chat template number to use.
    /// </summary>
    public uint NextChatTemplateNum { get; set; } = 1;
    
    /// <summary>
    /// The next document analysis policy number to use.
    /// </summary>
    public uint NextDocumentAnalysisPolicyNum { get; set; } = 1;

    public DataApp App { get; set; } = new(x => x.App);

    public DataChat Chat { get; set; } = new(x => x.Chat);

    public DataWorkspace Workspace { get; set; } = new();

    public DataIconFinder IconFinder { get; set; } = new();

    public DataTranslation Translation { get; set; } = new();

    public DataCoding Coding { get; set; } = new();
    
    public DataERI ERI { get; set; } = new();
    
    public DataDocumentAnalysis DocumentAnalysis { get; set; } = new();

    /// <summary>
    /// Gets the managed Batch Processing Assistant defaults.
    /// </summary>
    public DataBatchProcessing BatchProcessing { get; set; } = new(x => x.BatchProcessing);

    public DataMandatoryInformation MandatoryInformation { get; set; } = new();

    public DataTextSummarizer TextSummarizer { get; set; } = new();

    public DataSWOTAnalysis SWOTAnalysis { get; set; } = new();

    public DataTextContentCleaner TextContentCleaner { get; set; } = new();
    
    public DataAgentDataSourceSelection AgentDataSourceSelection { get; set; } = new(x => x.AgentDataSourceSelection);
    
    public DataAgentRetrievalContextValidation AgentRetrievalContextValidation { get; set; } = new(x => x.AgentRetrievalContextValidation);

    public DataAssistantPluginAudit AssistantPluginAudit { get; set; } = new(x => x.AssistantPluginAudit);
    
    public DataAgenda Agenda { get; set; } = new();
    
    public DataGrammarSpelling GrammarSpelling { get; set; } = new();
    
    public DataRewriteImprove RewriteImprove { get; set; } = new();
    
    public DataPromptOptimizer PromptOptimizer { get; set; } = new();

    public DataEMail EMail { get; set; } = new();
    
    public DataSlideBuilder SlideBuilder { get; set; } = new(x => x.SlideBuilder);

    /// <summary>
    /// Gets the managed Assistant Builder defaults.
    /// </summary>
    public DataAssistantBuilder AssistantBuilder { get; set; } = new(x => x.AssistantBuilder);

    /// <summary>
    /// Gets the managed Visual Briefing Assistant defaults.
    /// </summary>
    public DataVisualBriefing VisualBriefing { get; set; } = new(x => x.VisualBriefing);
    
    public DataLegalCheck LegalCheck { get; set; } = new();
    
    public DataSynonyms Synonyms { get; set; } = new();

    public DataMyTasks MyTasks { get; set; } = new();
    
    public DataJobPostings JobPostings { get; set; } = new();
    
    public DataBiasOfTheDay BiasOfTheDay { get; set; } = new();
    
    public DataI18N I18N { get; set; } = new();

    public DataTools Tools { get; set; } = new(x => x.Tools);
}
