namespace AIStudio.Settings;

/// <summary>
/// The data model for the settings file.
/// </summary>
public sealed class Data
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings,
    /// when a new version is available.
    /// </summary>
    public Version Version { get; init; } = Version.V1;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; init; } = new();
}