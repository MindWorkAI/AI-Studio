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

    /// <summary>
    /// Whether the organization wants this assistant plugin to be enabled, instead of leaving that
    /// to the user.
    /// </summary>
    /// <remarks>
    /// An approval only ever states that a plugin is safe. Enabling it is a separate decision, and
    /// without this field it stays with the user: a rolled-out assistant is approved, but every
    /// colleague still has to switch it on. This field is how an organization makes that decision
    /// instead.
    /// </remarks>
    public bool Activate { get; init; }

    /// <summary>
    /// Whether the user may switch an assistant plugin the organization activated off again.
    /// </summary>
    /// <remarks>
    /// This follows the AllowUserOverride convention of every managed setting: without it, what the
    /// organization set is locked; with it, the organization only provides a default the user may
    /// change. It has no meaning of its own while Activate is false, because there is nothing to
    /// override then.
    /// </remarks>
    public bool AllowUserOverride { get; init; }
}
