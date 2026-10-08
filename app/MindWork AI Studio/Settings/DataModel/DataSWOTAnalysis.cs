using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataSWOTAnalysis
{
    /// <summary>
    /// Preselect any SWOT analysis options?
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
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; }

    /// <summary>
    /// Preselect another target language?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect any aspects that the SWOT analysis should focus on?
    /// </summary>
    public string PreselectedImportantAspects { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The preselected provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}