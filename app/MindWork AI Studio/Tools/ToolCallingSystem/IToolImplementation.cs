using System.Text.Json;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public interface IToolImplementation
{
    public string ImplementationKey { get; }

    public string Icon => Icons.Material.Filled.Build;

    public IReadOnlySet<string> SensitiveTraceArgumentNames { get; }

    public string GetDisplayName() => TB("Tool");

    public string GetDescription() => TB("Tool description");

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        TB(fieldDefinition.Title);

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        TB(fieldDefinition.Description);

    public string? GetSettingsFieldDefaultValue(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => null;

    public Task<ToolConfigurationState?> ValidateConfigurationAsync(
        ToolDefinition definition,
        IReadOnlyDictionary<string, string> settingsValues,
        CancellationToken token = default) => Task.FromResult<ToolConfigurationState?>(null);

    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(IToolImplementation).Namespace, nameof(IToolImplementation));
}
