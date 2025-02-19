namespace AIStudio.Agents;

/// <summary>
/// Represents a selected data source, chosen by the agent.
/// </summary>
/// <param name="Id">The data source ID.</param>
/// <param name="Reason">The reason for selecting the data source.</param>
/// <param name="Confidence">The confidence of the agent in the selection.</param>
public readonly record struct SelectedDataSource(string Id, string Reason, float Confidence) : IConfidence;