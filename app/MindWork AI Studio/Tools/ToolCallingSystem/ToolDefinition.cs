using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public string Id { get; init; } = string.Empty;

    public string ImplementationKey { get; init; } = string.Empty;

    public ToolVisibilityDefinition VisibleIn { get; init; } = new();

    public ToolSettingsSchema SettingsSchema { get; init; } = new();

    public string SystemPromptInstructions { get; init; } = string.Empty;

    public ToolFunctionDefinition Function { get; init; } = new();
}

public sealed class ToolVisibilityDefinition
{
    public bool Chat { get; init; } = true;

    public bool Assistants { get; init; } = true;

    public List<Components> AllowedComponents { get; init; } = [];

    public List<Components> DeniedComponents { get; init; } = [];

    public bool IsVisibleIn(Components component)
    {
        if (this.AllowedComponents.Count == 0 && this.DeniedComponents.Count == 0)
            return component is Components.CHAT ? this.Chat : this.Assistants;

        var isAllowed = this.AllowedComponents.Count == 0 || this.AllowedComponents.Contains(component);
        return isAllowed && !this.DeniedComponents.Contains(component);
    }
}

public sealed class ToolFunctionDefinition
{
    public string Name { get; init; } = string.Empty;

    public string DescriptionForLLM { get; init; } = string.Empty;

    public bool Strict { get; init; } = true;

    public JsonElement Parameters { get; init; }
}

public sealed class ToolSettingsSchema
{
    public string Type { get; init; } = "object";

    public Dictionary<string, ToolSettingsFieldDefinition> Properties { get; init; } = [];

    public HashSet<string> Required { get; init; } = [];
}

public sealed class ToolSettingsFieldDefinition
{
    public string Type { get; init; } = "string";

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("enum")]
    public List<string> EnumValues { get; init; } = [];

    /// <summary>
    /// Name of a list of options the app maintains, as an alternative to spelling them out in
    /// the enum field. See the tool settings option sources for the available names.
    /// </summary>
    /// <remarks>
    /// Use this for values the app already knows, such as languages: it keeps the list in one
    /// place and gives the user readable names instead of raw values. Mutually exclusive with
    /// the enum field.
    /// </remarks>
    public string OptionSource { get; init; } = string.Empty;

    public bool Secret { get; init; }

    /// <summary>
    /// The values and names to offer for this field, from whichever way it declares them.
    /// </summary>
    public IReadOnlyList<ToolSettingsOption> GetOptions() => string.IsNullOrWhiteSpace(this.OptionSource)
        ? this.EnumValues.Select(value => new ToolSettingsOption(value, value)).ToList()
        : ToolSettingsOptionSources.Resolve(this.OptionSource);
}
