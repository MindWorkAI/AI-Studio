namespace AIStudio.Settings.DataModel;

public sealed class DataAgentRetrievalContextValidation
{
    /// <summary>
    /// Preselect any retrieval context validation options?
    /// </summary>
    public bool PreselectAgentOptions { get; set; }
    
    /// <summary>
    /// Preselect a retrieval context validation provider?
    /// </summary>
    public string PreselectedAgentProvider { get; set; } = string.Empty;
}