namespace AIStudio.Settings;

public sealed class ManagedEditableDefaultState
{
    public Guid ConfigPluginId { get; set; } = Guid.Empty;

    public string LastAppliedValue { get; set; } = string.Empty;
}