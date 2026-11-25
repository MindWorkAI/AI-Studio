using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// An assistant plugin, reduced to its metadata.
/// </summary>
/// <remarks>
/// Enough for everything which decides by the categories of a plugin, e.g., the sections of the
/// assistants page and the categories your organization disabled.
/// </remarks>
internal sealed class FakeAssistantPlugin(string name, PluginCategory[] categories, string[]? customCategories = null) : IPluginMetadata
{
    public string IconDataUrl => string.Empty;

    public PluginType Type => PluginType.ASSISTANT;

    public Guid Id { get; } = Guid.NewGuid();

    public string Name => name;

    public string Description => string.Empty;

    public PluginVersion Version => new(1, 0, 0);

    public DateTimeOffset? LastChanged => null;

    public string[] Authors => [];

    public string SupportContact => string.Empty;

    public string SourceURL => string.Empty;

    public PluginCategory[] Categories => categories;

    public string[] CustomCategories => customCategories ?? [];

    public PluginTargetGroup[] TargetGroups => [];

    public bool IsMaintained => true;

    public string DeprecationMessage => string.Empty;

    public bool IsInternal => false;
}