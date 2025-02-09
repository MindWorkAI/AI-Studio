namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// Known types of providers that can process data.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// The related data is not allowed to be sent to any provider.
    /// </summary>
    NONE,

    /// <summary>
    /// The related data can be sent to any provider.
    /// </summary>
    ANY,

    /// <summary>
    /// The related data can be sent to a provider that is hosted by the same organization, either on-premises or locally.
    /// </summary>
    SELF_HOSTED,
}