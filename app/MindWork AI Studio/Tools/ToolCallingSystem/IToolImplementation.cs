using System.Text.Json;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public interface IToolImplementation
{
    public string ImplementationKey { get; }

    public string Icon => Icons.Material.Filled.Build;

    public IReadOnlySet<string> SensitiveTraceArgumentNames { get; }

    public string GetDisplayName() => this.T("Tool");

    public string GetDescription() => this.T("Tool description");

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        this.T(fieldDefinition.Title);

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        this.T(fieldDefinition.Description);

    public Task<ToolConfigurationState?> ValidateConfigurationAsync(
        ToolDefinition definition,
        IReadOnlyDictionary<string, string> settingsValues,
        CancellationToken token = default) => Task.FromResult<ToolConfigurationState?>(null);

    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default);

    public string FormatTraceResult(string rawResult) => rawResult;

    private string T(string fallbackEN) => I18N.I.T(fallbackEN, this.GetType().Namespace, this.GetType().Name);
}
