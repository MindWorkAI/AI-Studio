using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// Represents the resolved security state for an assistant plugin.
/// The state intentionally separates two axes:
/// 1. The audit risk classification, such as Safe, Concerning, or Dangerous.
/// 2. The availability state imposed by local settings, such as Blocked, Audit Required, or Changed.
/// This keeps the semantic audit outcome stable even when settings allow or deny usage independently.
/// </summary>
public sealed class PluginAssistantSecurityState
{
    public PluginAssistants Plugin { get; init; } = null!;
    public PluginAssistantAudit? Audit { get; init; }
    public DataAssistantPluginAudit Settings { get; init; } = new();
    public string CurrentHash { get; init; } = string.Empty;
    public bool HasAudit => this.Audit is not null;
    public bool HashMatches { get; init; }
    public bool HasHashMismatch { get; init; }
    public bool IsBelowMinimum { get; init; }
    public bool MeetsMinimumLevel { get; init; }
    public bool RequiresAudit { get; init; }
    public bool IsBlocked { get; init; }
    public bool CanOverride { get; init; }
    public bool CanActivatePlugin { get; init; }
    public bool CanStartAssistant { get; init; }
    public string AuditLabel { get; init; } = string.Empty;
    public Color AuditColor { get; init; } = Color.Info;
    public string AuditIcon { get; init; } = MudBlazor.Icons.Material.Filled.HelpOutline;
    public string AvailabilityLabel { get; init; } = string.Empty;
    public Color AvailabilityColor { get; init; } = Color.Info;
    public string AvailabilityIcon { get; init; } = MudBlazor.Icons.Material.Filled.Lock;
    public string StatusLabel { get; init; } = string.Empty;
    public string Headline { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Color StatusColor { get; init; } = Color.Info;
    public string StatusIcon { get; init; } = MudBlazor.Icons.Material.Filled.Lock;
    public string ActionLabel { get; init; } = string.Empty;
    public string BadgeIcon { get; init; } = MudBlazor.Icons.Material.Outlined.Shield;
}
