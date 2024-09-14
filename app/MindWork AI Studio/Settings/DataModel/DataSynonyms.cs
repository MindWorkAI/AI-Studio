using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataSynonyms
{
    /// <summary>
    /// Preselect any rewrite options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// Preselect the language?
    /// </summary>
    public CommonLanguages PreselectedLanguage { get; set; }
    
    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Preselect a provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}