using AIStudio.Provider;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The settings of the retrieval context validation, as settings files up to version V6 store them.
/// </summary>
public sealed class DataAgentRetrievalContextValidationV6
{
    /// <summary>
    /// Enable the retrieval context validation?
    /// </summary>
    public bool EnableRetrievalContextValidation { get; set; }

    /// <summary>
    /// Preselect any retrieval context validation options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; }

    /// <summary>
    /// Preselect a retrieval context validation provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = string.Empty;

    /// <summary>
    /// The minimum confidence level of the provider which validates the retrieval contexts.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;

    /// <summary>
    /// Configure how many parallel validations to run.
    /// </summary>
    public int NumParallelValidations { get; set; } = 3;
}