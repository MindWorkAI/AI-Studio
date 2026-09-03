using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem;

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