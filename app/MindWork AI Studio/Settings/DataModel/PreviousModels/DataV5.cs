using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Settings.DataModel.PreviousModels;

public sealed class DataV5
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings
    /// when a new version is available.
    /// </summary>
    public Version Version { get; set; } = Version.V5;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<AIStudio.Settings.Provider> Providers { get; set; } = [];
    
    /// <summary>
    /// Settings concerning the LLM providers.
    /// </summary>
    public DataLLMProvidersV5 LLMProviders { get; set; } = new();

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
    /// Cached audit results for assistant plugins.
    /// </summary>
    public List<PluginAssistantAudit> AssistantPluginAudits { get; set; } = [];

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

    public DataChat Chat { get; set; } = new();

    public DataWorkspace Workspace { get; set; } = new();

    public DataIconFinder IconFinder { get; set; } = new();

    public DataTranslation Translation { get; set; } = new();

    public DataCoding Coding { get; set; } = new();
    
    public DataERI ERI { get; set; } = new();
    
    public DataDocumentAnalysis DocumentAnalysis { get; set; } = new();

    public DataMandatoryInformation MandatoryInformation { get; set; } = new();

    public DataTextSummarizer TextSummarizer { get; set; } = new();

    public DataTextContentCleaner TextContentCleaner { get; set; } = new();
    
    public DataAgentDataSourceSelection AgentDataSourceSelection { get; set; } = new();
    
    public DataAgentRetrievalContextValidation AgentRetrievalContextValidation { get; set; } = new();

    public DataAssistantPluginAudit AssistantPluginAudit { get; set; } = new(x => x.AssistantPluginAudit);
    
    public DataAgenda Agenda { get; set; } = new();
    
    public DataGrammarSpelling GrammarSpelling { get; set; } = new();
    
    public DataRewriteImprove RewriteImprove { get; set; } = new();
    
    public DataPromptOptimizer PromptOptimizer { get; set; } = new();

    public DataEMail EMail { get; set; } = new();
    
    public DataSlideBuilder SlideBuilder { get; set; } = new();
    
    public DataLegalCheck LegalCheck { get; set; } = new();
    
    public DataSynonyms Synonyms { get; set; } = new();

    public DataMyTasks MyTasks { get; set; } = new();
    
    public DataJobPostings JobPostings { get; set; } = new();
    
    public DataBiasOfTheDay BiasOfTheDay { get; set; } = new();
    
    public DataI18N I18N { get; set; } = new();
}