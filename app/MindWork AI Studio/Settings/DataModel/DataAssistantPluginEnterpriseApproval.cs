namespace AIStudio.Settings.DataModel;

/// <summary>
/// Enterprise-managed approval entry for an assistant plugin hash.
/// </summary>
public sealed class DataAssistantPluginEnterpriseApproval
{
    public string PluginHash { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public DateTimeOffset? ApprovedAtUtc { get; init; }
}
