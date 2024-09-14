using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataTranslation
{
    /// <summary>
    /// The live translation interval for debouncing in milliseconds.
    /// </summary>
    public int DebounceIntervalMilliseconds { get; set; } = 1_500;
    
    /// <summary>
    /// Do we want to preselect any translator options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Preselect the live translation?
    /// </summary>
    public bool PreselectLiveTranslation { get; set; }

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
    public CommonLanguages PreselectedTargetLanguage { get; set; } = CommonLanguages.EN_US;

    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// The preselected translator provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}