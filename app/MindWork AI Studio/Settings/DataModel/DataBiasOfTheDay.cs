using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataBiasOfTheDay
{
    /// <summary>
    /// A list of bias IDs that have been used.
    /// </summary>
    public List<int> UsedBias { get; set; } = new();

    /// <summary>
    /// When was the last bias drawn?
    /// </summary>
    public DateOnly DateLastBiasDrawn { get; set; } = DateOnly.MinValue;

    /// <summary>
    /// Which bias is the bias of the day? This isn't the bias id, but rather the chat id in the bias workspace.
    /// </summary>
    public Guid BiasOfTheDayChatId { get; set; } = Guid.Empty;

    /// <summary>
    /// Which bias is the bias of the day?
    /// </summary>
    public Guid BiasOfTheDayId { get; set; } = Guid.Empty;

    /// <summary>
    /// Restrict to one bias per day?
    /// </summary>
    public bool RestrictOneBiasPerDay { get; set; } = true;
    
    /// <summary>
    /// Preselect any rewrite options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// Preselect the language?
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
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}