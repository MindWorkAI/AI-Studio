namespace AIStudio.Settings.DataModel.PreviousModels;

public sealed class DataV4
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings
    /// when a new version is available.
    /// </summary>
    public Version Version { get; init; }

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; init; } = [];
    
    /// <summary>
    /// Settings concerning the LLM providers.
    /// </summary>
    public DataLLMProviders LLMProviders { get; init; } = new();
    
    /// <summary>
    /// List of configured profiles.
    /// </summary>
    public List<Profile> Profiles { get; init; } = [];

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    /// <summary>
    /// The next profile number to use.
    /// </summary>
    public uint NextProfileNum { get; set; } = 1;

    public DataApp App { get; init; } = new(x => x.App);

    public DataChat Chat { get; init; } = new();

    public DataWorkspace Workspace { get; init; } = new();

    public DataIconFinder IconFinder { get; init; } = new();

    public DataTranslation Translation { get; init; } = new();

    public DataCoding Coding { get; init; } = new();

    public DataTextSummarizer TextSummarizer { get; init; } = new();

    public DataTextContentCleaner TextContentCleaner { get; init; } = new();
    
    public DataAgenda Agenda { get; init; } = new();
    
    public DataGrammarSpelling GrammarSpelling { get; init; } = new();
    
    public DataRewriteImprove RewriteImprove { get; init; } = new();

    public DataEMail EMail { get; set; } = new();
    
    public DataLegalCheck LegalCheck { get; set; } = new();
    
    public DataSynonyms Synonyms { get; set; } = new();

    public DataMyTasks MyTasks { get; set; } = new();
}