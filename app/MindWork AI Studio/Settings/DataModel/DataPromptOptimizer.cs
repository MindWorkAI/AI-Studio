using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataPromptOptimizer
{
    /// <summary>
    /// Preselect prompt optimizer options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; } = CommonLanguages.AS_IS;

    /// <summary>
    /// Preselect a custom target language when "Other" is selected?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect important aspects for the optimization.
    /// </summary>
    public string PreselectedImportantAspects { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;

    /// <summary>
    /// Preselect a provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}
