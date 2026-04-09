namespace AIStudio.Settings.DataModel;

public sealed record DataMandatoryInfoAcceptance
{
    /// <summary>
    /// The ID of the mandatory info that was accepted.
    /// </summary>
    public string InfoId { get; init; } = string.Empty;

    /// <summary>
    /// The accepted version string.
    /// </summary>
    public string AcceptedVersion { get; init; } = string.Empty;

    /// <summary>
    /// The UTC time of the acceptance.
    /// </summary>
    public DateTimeOffset AcceptedAtUtc { get; init; }

    /// <summary>
    /// The plugin that provided the accepted info at the time of acceptance.
    /// </summary>
    public Guid EnterpriseConfigurationPluginId { get; init; } = Guid.Empty;
}