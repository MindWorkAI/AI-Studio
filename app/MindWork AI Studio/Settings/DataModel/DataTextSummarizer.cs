using AIStudio.Assistants.TextSummarizer;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataTextSummarizer
{
    /// <summary>
    /// Preselect any text summarizer options?
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
    /// Preselect any other language?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect the complexity?
    /// </summary>
    public Complexity PreselectedComplexity { get; set; }
    
    /// <summary>
    /// Preselect any expertise in a field?
    /// </summary>
    public string PreselectedExpertInField { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Preselect a text summarizer provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}