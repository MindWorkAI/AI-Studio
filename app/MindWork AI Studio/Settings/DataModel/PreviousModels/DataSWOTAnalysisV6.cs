using AIStudio.Provider;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The settings of the SWOT analysis assistant, as settings files up to version V6 store them.
/// </summary>
public sealed class DataSWOTAnalysisV6
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
    /// Preselect the text content cleaner?
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