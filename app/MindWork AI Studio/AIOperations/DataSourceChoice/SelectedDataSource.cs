namespace AIStudio.AIOperations.DataSourceChoice;

/// <summary>
/// Represents a selected data source, chosen by the model.
/// </summary>
/// <param name="Id">The data source ID.</param>
/// <param name="Reason">The reason for selecting the data source.</param>
/// <param name="Confidence">The confidence of the model in the selection.</param>
public readonly record struct SelectedDataSource(string Id, string Reason, float Confidence) : IConfidence;