using AIStudio.Settings;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class ToolSettingsDialog : SettingsDialogBase
{
    [Parameter]
    public string ToolId { get; set; } = string.Empty;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private ToolSettingsService ToolSettingsService { get; init; } = null!;

    private ToolDefinition? toolDefinition;
    private IToolImplementation? implementation;
    private Dictionary<string, string> values = new(StringComparer.Ordinal);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.toolDefinition = this.ToolRegistry.GetDefinition(this.ToolId);
        if (this.toolDefinition is not null)
        {
            this.implementation = this.ToolRegistry.GetImplementation(this.toolDefinition.ImplementationKey);
            this.values = await this.ToolSettingsService.GetSettingsAsync(this.toolDefinition);
        }
    }

    private string GetValue(string fieldName) => this.values.GetValueOrDefault(fieldName, string.Empty);

    private string GetFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        this.implementation?.GetSettingsFieldLabel(fieldName, fieldDefinition) ?? fieldDefinition.Title;

    private string GetFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        this.GetFieldDescriptionWithDefault(fieldName, fieldDefinition);

    private string GetFieldDefaultValue(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        this.implementation?.GetSettingsFieldDefaultValue(fieldName, fieldDefinition) ?? string.Empty;

    private string GetFieldDescriptionWithDefault(string fieldName, ToolSettingsFieldDefinition fieldDefinition)
    {
        var description = this.implementation?.GetSettingsFieldDescription(fieldName, fieldDefinition) ?? fieldDefinition.Description;
        var defaultValue = this.GetFieldDefaultValue(fieldName, fieldDefinition);
        if (string.IsNullOrWhiteSpace(defaultValue))
            return description;

        return string.Format(T("{0} Default: {1}"), description, defaultValue);
    }

    private bool IsFieldDisabled(string fieldName) =>
        this.toolDefinition?.Id.Equals(ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, StringComparison.Ordinal) is true &&
        fieldName.Equals("allowedPrivateHosts", StringComparison.Ordinal) &&
        ManagedConfiguration.TryGet(x => x.Tools, x => x.ReadWebPageAllowedPrivateHosts, out var meta) &&
        meta.IsLocked;

    private string GetFieldPlaceholder(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        string.IsNullOrWhiteSpace(this.GetValue(fieldName)) ? this.GetFieldDefaultValue(fieldName, fieldDefinition) : string.Empty;

    private void UpdateValue(string fieldName, string? value) => this.values[fieldName] = value ?? string.Empty;

    private async Task Save()
    {
        if (this.toolDefinition is null)
            return;

        await this.ToolSettingsService.SaveSettingsAsync(this.toolDefinition, this.values);
        this.MudDialog.Close();
    }
}
