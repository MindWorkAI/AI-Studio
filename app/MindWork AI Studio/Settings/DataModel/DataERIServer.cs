using AIStudio.Assistants.ERI;

using OperatingSystem = AIStudio.Assistants.ERI.OperatingSystem;

namespace AIStudio.Settings.DataModel;

public sealed class DataERIServer
{
    /// <summary>
    /// Preselect the server name?
    /// </summary>
    public string ServerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the server description?
    /// </summary>
    public string ServerDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the ERI version?
    /// </summary>
    public ERIVersion ERIVersion { get; set; } = ERIVersion.NONE;
    
    /// <summary>
    /// Preselect the language for implementing the ERI?
    /// </summary>
    public ProgrammingLanguages ProgrammingLanguage { get; set; }

    /// <summary>
    /// Do you want to preselect any other language?
    /// </summary>
    public string OtherProgrammingLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect a data source?
    /// </summary>
    public DataSources DataSource { get; set; }

    /// <summary>
    /// Do you want to preselect a product name for the data source?
    /// </summary>
    public string DataSourceProductName { get; set; } = string.Empty;

    /// <summary>
    /// Do you want to preselect any other data source?
    /// </summary>
    public string OtherDataSource { get; set; } = string.Empty;

    /// <summary>
    /// Do you want to preselect a hostname for the data source?
    /// </summary>
    public string DataSourceHostname { get; set; } = string.Empty;

    /// <summary>
    /// Do you want to preselect a port for the data source?
    /// </summary>
    public int? DataSourcePort { get; set; }

    /// <summary>
    /// Did the user type the port number?
    /// </summary>
    public bool UserTypedPort { get; set; } = false;

    /// <summary>
    /// Preselect any authentication methods?
    /// </summary>
    public HashSet<Auth> AuthMethods { get; set; } = [];

    /// <summary>
    /// Do you want to preselect any authentication description?
    /// </summary>
    public string AuthDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Do you want to preselect an operating system? This is necessary when SSO with Kerberos is used.
    /// </summary>
    public OperatingSystem OperatingSystem { get; set; } = OperatingSystem.NONE;

    /// <summary>
    /// Do you want to preselect which LLM providers are allowed?
    /// </summary>
    public AllowedLLMProviders AllowedLLMProviders { get; set; } = AllowedLLMProviders.NONE;

    /// <summary>
    /// Do you want to predefine any embedding information?
    /// </summary>
    public List<EmbeddingInfo> EmbeddingInfos { get; set; } = new();

    /// <summary>
    /// Do you want to predefine any retrieval information?
    /// </summary>
    public List<RetrievalInfo> RetrievalInfos { get; set; } = new();
    
    /// <summary>
    /// Do you want to preselect any additional libraries?
    /// </summary>
    public string AdditionalLibraries { get; set; } = string.Empty;

    /// <summary>
    /// Do you want to write all generated code to the filesystem?
    /// </summary>
    public bool WriteToFilesystem { get; set; }

    /// <summary>
    /// The base directory where to write the generated code to.
    /// </summary>
    public string BaseDirectory { get; set; } = string.Empty;

    /// <summary>
    /// We save which files were generated previously.
    /// </summary>
    public List<string> PreviouslyGeneratedFiles { get; set; } = new();
}