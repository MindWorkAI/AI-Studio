namespace AIStudio.Settings;

public sealed class ManagedEditableDefaultState
{
    public Guid ConfigPluginId { get; init; } = Guid.Empty;

    public string LastAppliedValue { get; init; } = string.Empty;
}