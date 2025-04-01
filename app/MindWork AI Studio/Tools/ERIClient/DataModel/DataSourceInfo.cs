namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// Information about the data source.
/// </summary>
/// <param name="Name">The name of the data source, e.g., "Internal Organization Documents."</param>
/// <param name="Description">A short description of the data source. What kind of data does it contain?
/// What is the data source used for?</param>
public readonly record struct DataSourceInfo(string Name, string Description);