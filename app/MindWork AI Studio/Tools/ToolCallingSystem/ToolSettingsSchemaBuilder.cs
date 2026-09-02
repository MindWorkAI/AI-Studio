namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Builds the schema describing a tool's settings.
/// </summary>
/// <remarks>
/// Settings are stored as text throughout, so there is no field type to choose here. What a
/// field declares instead is whether it must be set, whether it holds a secret, and whether it
/// offers a fixed choice.<br/><br/>
/// Titles and descriptions are deliberately absent: they come from the implementation, which can
/// translate them. See the settings field label and description hooks on the tool interface.
/// </remarks>
public sealed class ToolSettingsSchemaBuilder
{
    private readonly Dictionary<string, ToolSettingsFieldDefinition> properties = new(StringComparer.Ordinal);
    private readonly HashSet<string> requiredNames = new(StringComparer.Ordinal);

    public static ToolSettingsSchemaBuilder Create() => new();

    /// <summary>
    /// A field the tool cannot work without.
    /// </summary>
    /// <remarks>
    /// The tool counts as unconfigured while a required field is empty, which keeps it out of the
    /// model's reach instead of letting it run and fail.
    /// </remarks>
    public ToolSettingsSchemaBuilder Required(string name) => this.Add(name, isRequired: true);

    public ToolSettingsSchemaBuilder Optional(string name) => this.Add(name, isRequired: false);

    /// <summary>
    /// A required field whose value is picked from one of the app's option lists.
    /// </summary>
    public ToolSettingsSchemaBuilder RequiredChoice(string name, string optionSource) => this.Add(name, isRequired: true, optionSource: optionSource);

    public ToolSettingsSchemaBuilder OptionalChoice(string name, string optionSource) => this.Add(name, isRequired: false, optionSource: optionSource);

    /// <summary>
    /// A field kept in the operating system's keyring rather than in the settings file.
    /// </summary>
    public ToolSettingsSchemaBuilder OptionalSecret(string name) => this.Add(name, isRequired: false, isSecret: true);

    public ToolSettingsSchemaBuilder RequiredSecret(string name) => this.Add(name, isRequired: true, isSecret: true);

    public ToolSettingsSchema Build() => new()
    {
        Properties = new(this.properties, StringComparer.Ordinal),
        Required = [..this.requiredNames],
    };

    private ToolSettingsSchemaBuilder Add(string name, bool isRequired, string optionSource = "", bool isSecret = false)
    {
        this.properties[name] = new ToolSettingsFieldDefinition
        {
            OptionSource = optionSource,
            Secret = isSecret,
        };

        if (isRequired)
            this.requiredNames.Add(name);

        return this;
    }
}