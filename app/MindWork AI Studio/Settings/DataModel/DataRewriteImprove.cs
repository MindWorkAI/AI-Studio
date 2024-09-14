using AIStudio.Assistants.RewriteImprove;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataRewriteImprove
{
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
    /// Preselect any writing style?
    /// </summary>
    public WritingStyles PreselectedWritingStyle { get; set; }

    /// <summary>
    /// Preselect any voice style?
    /// </summary>
    public SentenceStructure PreselectedSentenceStructure { get; set; }
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Preselect a provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}