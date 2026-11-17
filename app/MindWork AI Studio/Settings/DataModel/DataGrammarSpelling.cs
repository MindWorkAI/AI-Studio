using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataGrammarSpelling
{
    /// <summary>
    /// Whether the grammar & spelling checker asks on start to check a document or a text, or starts with one of them right away.
    /// </summary>
    public AssistantStartMode StartMode { get; set; } = AssistantStartMode.ASK_EVERY_TIME;

    /// <summary>
    /// Preselect any rewrite options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; }
    
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