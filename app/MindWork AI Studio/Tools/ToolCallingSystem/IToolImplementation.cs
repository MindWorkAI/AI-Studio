using System.Text.Json;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public interface IToolImplementation
{
    public string ImplementationKey { get; }

    /// <summary>
    /// Describes this tool: what the model may call, which settings it needs, and where it may
    /// be used.
    /// </summary>
    /// <remarks>
    /// For a tool written in C#, the definition and the implementation are one object. Tools that
    /// arrive from elsewhere — a plugin, an assistant — get their definition from their own
    /// definition source instead, and are matched to an implementation by their implementation key.
    /// </remarks>
    public ToolDefinition GetDefinition();

    public string Icon => Icons.Material.Filled.Build;

    public IReadOnlySet<string> SensitiveTraceArgumentNames { get; }

    /// <summary>
    /// Whether this tool returns content it fetched from outside AI Studio, such as a web page.
    /// </summary>
    /// <remarks>
    /// Such content is attacker-controlled and must be filtered for prompt injections before a
    /// model sees it. A tool that returns it filters it itself, because only the tool knows which
    /// of its fields came from where — see the web search and read web page tools, which do so
    /// through the web page content sanitizer.<br/><br/>
    /// Declaring it here keeps the obligation visible in one place, and gives tools that cannot
    /// carry it out themselves, such as tools defined by plugin authors, a flag the tool executor
    /// can act on for them.
    /// </remarks>
    public bool ReturnsUntrustedExternalContent => false;

    public string GetDisplayName() => TB("Tool");

    public string GetDescription() => TB("Tool description");

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        TB(fieldDefinition.Title);

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        TB(fieldDefinition.Description);

    public string? GetSettingsFieldDefaultValue(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => null;

    /// <summary>
    /// The heading shown above one group of settings.
    /// </summary>
    /// <remarks>
    /// The group name in the schema is an identifier, so it is not what the user should read.
    /// A tool that declares groups translates their headings here, the same way it does for
    /// its field labels.
    /// </remarks>
    public string GetSettingsGroupLabel(string groupKey) => groupKey;

    /// <summary>
    /// Links offered next to one group of settings, such as where to create an account.
    /// </summary>
    public IReadOnlyList<ToolSettingsGroupLink> GetSettingsGroupLinks(string groupKey) => [];

    public Task<ToolConfigurationState?> ValidateConfigurationAsync(
        ToolDefinition definition,
        IReadOnlyDictionary<string, string> settingsValues,
        CancellationToken token = default) => Task.FromResult<ToolConfigurationState?>(null);

    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(IToolImplementation).Namespace, nameof(IToolImplementation));
}
