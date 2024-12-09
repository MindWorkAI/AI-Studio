using AIStudio.Assistants.EDI;
using AIStudio.Provider;

using OperatingSystem = AIStudio.Assistants.EDI.OperatingSystem;

namespace AIStudio.Settings.DataModel;

public sealed class DataEDI
{
    /// <summary>
    /// Preselect any EDI options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Preselect the server name?
    /// </summary>
    public string PreselectedServerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the server description?
    /// </summary>
    public string PreselectedServerDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the EDI version?
    /// </summary>
    public EDIVersion PreselectedEDIVersion { get; set; } = EDIVersion.NONE;
    
    /// <summary>
    /// Preselect the language for implementing the EDI?
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
    /// Do you want to preselect a retrieval description?
    /// </summary>
    public string PreselectedRetrievalDescription { get; set; } = string.Empty;

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