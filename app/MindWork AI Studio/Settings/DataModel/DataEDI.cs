using AIStudio.Assistants.EDI;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataEDI
{
    /// <summary>
    /// Preselect any EDI options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
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
    /// Do you want to preselect any other data source?
    /// </summary>
    public string PreselectedOtherDataSource { get; set; } = string.Empty;

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