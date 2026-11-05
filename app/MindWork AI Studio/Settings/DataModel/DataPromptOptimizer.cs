using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores the default options for the Prompt Optimizer.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataPromptOptimizer(Expression<Func<Data, DataPromptOptimizer>>? configSelection = null)
{
    /// <summary>
    /// Initializes an unmanaged Prompt Optimizer settings instance.
    /// </summary>
    public DataPromptOptimizer() : this(null)
    {
    }

    /// <summary>
    /// Preselect prompt optimizer options?
    /// </summary>
    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectOptions, false);

    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedTargetLanguage, CommonLanguages.AS_IS);

    /// <summary>
    /// Preselect a custom target language when "Other" is selected?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedOtherLanguage, string.Empty);

    /// <summary>
    /// Preselect important aspects for the optimization.
    /// </summary>
    public string PreselectedImportantAspects { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedImportantAspects, string.Empty);

    /// <summary>
    /// The file with the prompt guide to preselect. An empty path preselects the default prompt guide.
    /// </summary>
    /// <remarks>
    /// The path names a file of the user and therefore stays absolute. The assistant reads its current
    /// content whenever it applies the preselections.
    /// </remarks>
    public string PreselectedCustomPromptGuidePath { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, value => value.MinimumProviderConfidence, ConfidenceLevel.NONE);

    /// <summary>
    /// Preselect a provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedProvider, string.Empty);
}