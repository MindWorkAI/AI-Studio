using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataDocumentAnalysisPolicy
{
    /// <summary>
    /// Preselect the policy name?
    /// </summary>
    public string PolicyName { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the policy description?
    /// </summary>
    public string PolicyDescription { get; set; } = string.Empty;

    /// <summary>
    /// Is this policy protected? If so, it cannot be deleted or modified by the user.
    /// This is useful for policies that are distributed by the organization.
    /// </summary>
    public bool IsProtected { get; set; }

    /// <summary>
    /// The rules for the document analysis policy.
    /// </summary>
    public string AnalysisRules { get; set; } = string.Empty;

    /// <summary>
    /// The rules for the output of the document analysis, e.g., the desired format, structure, etc.
    /// </summary>
    public string OutputRules { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Which LLM provider should be preselected?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
}