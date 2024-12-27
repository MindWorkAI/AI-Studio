using AIStudio.Assistants.ERI;
using AIStudio.Provider;

using OperatingSystem = AIStudio.Assistants.ERI.OperatingSystem;

namespace AIStudio.Settings.DataModel;

public sealed class DataERI
{
    /// <summary>
    /// Should we automatically save any input made in the ERI assistant?
    /// </summary>
    public bool AutoSaveChanges { get; set; } = true;

    /// <summary>
    /// Preselect any ERI options?
    /// </summary>
    public bool PreselectOptions { get; set; } = true;

    /// <summary>
    /// Preselect the server name?
    /// </summary>
    public string PreselectedServerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the server description?
    /// </summary>
    public string PreselectedServerDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the ERI version?
    /// </summary>
    public ERIVersion PreselectedERIVersion { get; set; } = ERIVersion.NONE;
    
    /// <summary>
    /// Preselect the language for implementing the ERI?
    /// </summary>
    public ProgrammingLanguages PreselectedProgrammingLanguage { get; set; }

    /// <summary>
    /// Do you want to preselect any other language?
    /// </summary>
    public string PreselectedOtherProgrammingLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect a data source?
    /// </summary>
    public DataSources PreselectedDataSource { get; set; }

    /// <summary>
    /// Do you want to preselect a product name for the data source?
    /// </summary>
    public string PreselectedDataSourceProductName { get; set; } = string.Empty;

    /// <summary>
    /// Do you want to preselect any other data source?
    /// </summary>
    public string PreselectedOtherDataSource { get; set; } = string.Empty;

    /// <summary>
    /// Do you want to preselect a hostname for the data source?
    /// </summary>
    public string PreselectedDataSourceHostname { get; set; } = string.Empty;

    /// <summary>
    /// Do you want to preselect a port for the data source?
    /// </summary>
    public int? PreselectedDataSourcePort { get; set; }

    /// <summary>
    /// Did the user type the port number?
    /// </summary>
    public bool UserTypedPort { get; set; } = false;

    /// <summary>
    /// Preselect any authentication methods?
    /// </summary>
    public HashSet<Auth> PreselectedAuthMethods { get; set; } = [];

    /// <summary>
    /// Do you want to preselect any authentication description?
    /// </summary>
    public string PreselectedAuthDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Do you want to preselect an operating system? This is necessary when SSO with Kerberos is used.
    /// </summary>
    public OperatingSystem PreselectedOperatingSystem { get; set; } = OperatingSystem.NONE;

    /// <summary>
    /// Do you want to preselect which LLM providers are allowed?
    /// </summary>
    public AllowedLLMProviders PreselectedAllowedLLMProviders { get; set; } = AllowedLLMProviders.NONE;

    /// <summary>
    /// Do you want to predefine any embedding information?
    /// </summary>
    public List<EmbeddingInfo> PreselectedEmbeddingInfos { get; set; } = new();

    /// <summary>
    /// Do you want to predefine any retrieval information?
    /// </summary>
    public List<RetrievalInfo> PreselectedRetrievalInfos { get; set; } = new();
    
    /// <summary>
    /// Do you want to preselect any additional libraries?
    /// </summary>
    public string PreselectedAdditionalLibraries { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Which coding provider should be preselected?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
}