namespace AIStudio.Settings.DataModel.PreviousModels;

public sealed class DataV4
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings
    /// when a new version is available.
    /// </summary>
    public Version Version { get; set; }

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; set; } = [];
    
    /// <summary>
    /// Settings concerning the LLM providers.
    /// </summary>
    public DataLLMProvidersV5 LLMProviders { get; set; } = new();
    
    /// <summary>
    /// List of configured profiles.
    /// </summary>
    public List<Profile> Profiles { get; set; } = [];

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    /// <summary>
    /// The next profile number to use.
    /// </summary>
    public uint NextProfileNum { get; set; } = 1;

    public DataApp App { get; set; } = new(x => x.App);

    public DataChat Chat { get; set; } = new();

    public DataWorkspace Workspace { get; set; } = new();

    public DataIconFinder IconFinder { get; set; } = new();

    public DataTranslation Translation { get; set; } = new();

    public DataCoding Coding { get; set; } = new();

    public DataTextSummarizer TextSummarizer { get; set; } = new();

    public DataTextContentCleaner TextContentCleaner { get; set; } = new();
    
    public DataAgenda Agenda { get; set; } = new();
    
    public DataGrammarSpelling GrammarSpelling { get; set; } = new();
    
    public DataRewriteImprove RewriteImprove { get; set; } = new();

    public DataEMail EMail { get; set; } = new();
    
    public DataLegalCheck LegalCheck { get; set; } = new();
    
    public DataSynonyms Synonyms { get; set; } = new();

    public DataMyTasks MyTasks { get; set; } = new();
}