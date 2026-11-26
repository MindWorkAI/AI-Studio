using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The data model of settings files with version V6.
/// </summary>
public sealed class DataV6
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings
    /// when a new version is available.
    /// </summary>
    public Version Version { get; set; } = Version.V6;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<AIStudio.Settings.Provider> Providers { get; set; } = [];

    /// <summary>
    /// Settings concerning confidence levels.
    /// </summary>
    public DataConfidence Confidence { get; set; } = new();

    /// <summary>
    /// Settings concerning data source security checks.
    /// </summary>
    public DataSourceSecuritySettings DataSourceSecurity { get; set; } = new();

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
    public List<DataSourceMailbox> Mailboxes { get; set; } = [];

    /// <summary>
    /// Settings which apply to all mailboxes.
    /// </summary>
    public DataMailboxes MailboxSettings { get; set; } = new();

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
    public Dictionary<string, string> ManagedUserValueSnapshots { get; set; } = [];

    /// <summary>
    /// Cached audit results for assistant plugins.
    /// </summary>
    public List<PluginAssistantAudit> AssistantPluginAudits { get; set; } = [];

    /// <summary>
    /// The assistant plugin hashes whose organization default for the activation was already applied.
    /// </summary>
    public List<string> AppliedEnterpriseAssistantActivations { get; set; } = [];

    /// <summary>
    /// Whether AI Studio already stored the chats and the settings once more, so that every path below
    /// the data directory in them is relative.
    /// </summary>
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

    public DataApp App { get; set; } = new();

    public DataChat Chat { get; set; } = new();

    public DataAssistantsPage AssistantsPage { get; set; } = new();

    public DataWorkspace Workspace { get; set; } = new();

    public DataIconFinder IconFinder { get; set; } = new();

    public DataTranslationV6 Translation { get; set; } = new();

    public DataCoding Coding { get; set; } = new();

    public DataERI ERI { get; set; } = new();

    public DataDocumentAnalysis DocumentAnalysis { get; set; } = new();

    public DataBatchProcessing BatchProcessing { get; set; } = new();

    public DataMandatoryInformation MandatoryInformation { get; set; } = new();

    public DataTextSummarizerV6 TextSummarizer { get; set; } = new();

    public DataSWOTAnalysisV6 SWOTAnalysis { get; set; } = new();

    public DataTextContentCleanerV6 TextContentCleaner { get; set; } = new();

    public DataAgentDataSourceSelectionV6 AgentDataSourceSelection { get; set; } = new();

    public DataAgentRetrievalContextValidationV6 AgentRetrievalContextValidation { get; set; } = new();

    public DataAssistantPluginAuditV6 AssistantPluginAudit { get; set; } = new();

    public DataAgenda Agenda { get; set; } = new();

    public DataGrammarSpelling GrammarSpelling { get; set; } = new();

    public DataRewriteImprove RewriteImprove { get; set; } = new();

    public DataPromptOptimizer PromptOptimizer { get; set; } = new();

    public DataEMail EMail { get; set; } = new();

    public DataSlideBuilder SlideBuilder { get; set; } = new();

    public DataAssistantBuilder AssistantBuilder { get; set; } = new();

    public DataVisualBriefing VisualBriefing { get; set; } = new();

    public DataBeyondFactsLab BeyondFactsLab { get; set; } = new();

    public DataModelComparison ModelComparison { get; set; } = new();

    public DataLegalCheckV6 LegalCheck { get; set; } = new();

    public DataSynonyms Synonyms { get; set; } = new();

    public DataMyTasks MyTasks { get; set; } = new();

    public DataJobPostings JobPostings { get; set; } = new();

    public DataBiasOfTheDay BiasOfTheDay { get; set; } = new();

    public DataI18N I18N { get; set; } = new();

    public DataTools Tools { get; set; } = new();
}