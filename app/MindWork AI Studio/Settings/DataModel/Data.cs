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
    public Version Version { get; init; } = Version.V5;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; init; } = [];
    
    /// <summary>
    /// Settings concerning the LLM providers.
    /// </summary>
    public DataLLMProviders LLMProviders { get; init; } = new();

    /// <summary>
    /// A collection of embedding providers configured.
    /// </summary>
    public List<EmbeddingProvider> EmbeddingProviders { get; init; } = [];

    /// <summary>
    /// A collection of data sources configured.
    /// </summary>
    public List<IDataSource> DataSources { get; set; } = [];
    
    /// <summary>
    /// List of configured profiles.
    /// </summary>
    public List<Profile> Profiles { get; init; } = [];
    
    /// <summary>
    /// List of configured chat templates.
    /// </summary>
    public List<ChatTemplate> ChatTemplates { get; init; } = [];

    /// <summary>
    /// List of enabled plugins.
    /// </summary>
    public List<Guid> EnabledPlugins { get; set; } = [];

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    /// <summary>
    /// The next embedding number to use.
    /// </summary>
    public uint NextEmbeddingNum { get; set; } = 1;

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

    public DataApp App { get; init; } = new(x => x.App);

    public DataChat Chat { get; init; } = new();

    public DataWorkspace Workspace { get; init; } = new();

    public DataIconFinder IconFinder { get; init; } = new();

    public DataTranslation Translation { get; init; } = new();

    public DataCoding Coding { get; init; } = new();
    
    public DataERI ERI { get; init; } = new();

    public DataTextSummarizer TextSummarizer { get; init; } = new();

    public DataTextContentCleaner TextContentCleaner { get; init; } = new();
    
    public DataAgentDataSourceSelection AgentDataSourceSelection { get; init; } = new();
    
    public DataAgentRetrievalContextValidation AgentRetrievalContextValidation { get; init; } = new();
    
    public DataAgenda Agenda { get; init; } = new();
    
    public DataGrammarSpelling GrammarSpelling { get; init; } = new();
    
    public DataRewriteImprove RewriteImprove { get; init; } = new();

    public DataEMail EMail { get; init; } = new();
    
    public DataLegalCheck LegalCheck { get; init; } = new();
    
    public DataSynonyms Synonyms { get; init; } = new();

    public DataMyTasks MyTasks { get; init; } = new();
    
    public DataJobPostings JobPostings { get; init; } = new();
    
    public DataBiasOfTheDay BiasOfTheDay { get; init; } = new();
    
    public DataI18N I18N { get; init; } = new();
}