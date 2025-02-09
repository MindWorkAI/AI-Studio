namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// Represents the security requirements for this data source.
/// </summary>
/// <param name="AllowedProviderType">Which provider types are allowed to process the data?</param>
public readonly record struct SecurityRequirements(ProviderType AllowedProviderType);