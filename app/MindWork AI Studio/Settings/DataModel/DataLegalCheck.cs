using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public class DataLegalCheck
{
    /// <summary>
    /// Do you want to preselect any legal check options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// Hide the web content reader?
    /// </summary>
    public bool HideWebContentReader { get; set; }

    /// <summary>
    /// Preselect the web content reader?
    /// </summary>
    public bool PreselectWebContentReader { get; set; }

    /// <summary>
    /// Preselect the content cleaner agent?
    /// </summary>
    public bool PreselectContentCleanerAgent { get; set; }
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// The preselected translator provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
}