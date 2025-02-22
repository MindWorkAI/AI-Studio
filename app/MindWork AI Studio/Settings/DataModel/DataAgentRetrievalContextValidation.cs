namespace AIStudio.Settings.DataModel;

public sealed class DataAgentRetrievalContextValidation
{
    /// <summary>
    /// Enable the retrieval context validation agent?
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
    /// Configure how many parallel validations to run.
    /// </summary>
    public int NumParallelValidations { get; set; } = 3;
}