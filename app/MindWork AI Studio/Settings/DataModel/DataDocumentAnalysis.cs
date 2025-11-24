namespace AIStudio.Settings.DataModel;

public sealed class DataDocumentAnalysis
{
    /// <summary>
    /// Configured document analysis policies.
    /// </summary>
    public List<DataDocumentAnalysisPolicy> Policies { get; set; } = [];
}