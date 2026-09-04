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
    private IReadOnlyList<FieldGroup> fieldGroups = [];
    private string validationMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.toolDefinition = this.ToolRegistry.GetDefinition(this.ToolId);
        if (this.toolDefinition is not null)
        {
            this.implementation = this.ToolRegistry.GetImplementation(this.toolDefinition.ImplementationKey);
            this.values = await this.ToolSettingsService.GetSettingsAsync(this.toolDefinition);
            this.fieldGroups = BuildFieldGroups(this.toolDefinition);
        }
    }

    private string GetValue(string fieldName) => this.values.GetValueOrDefault(fieldName, string.Empty);

    /// <summary>
    /// Splits the tool's settings fields into the groups the tool declared for them.
    /// </summary>
    /// <remarks>
    /// Groups appear in the order in which their first field appears in the schema, and the
    /// fields keep the order the tool wrote them in. That is the order the fields have always
    /// been rendered in, so a tool without groups looks exactly as it did before: one group
    /// with an empty name, holding everything.<br/><br/>
    /// A schema does not change while the dialog is open, so this runs once rather than on
    /// every render.
    /// </remarks>
    private static IReadOnlyList<FieldGroup> BuildFieldGroups(ToolDefinition definition)
    {
        var groups = new List<FieldGroup>();
        var groupIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in definition.SettingsSchema.Properties)
        {
            if (!groupIndexByKey.TryGetValue(property.Value.Group, out var groupIndex))
            {
                groupIndex = groups.Count;
                groupIndexByKey[property.Value.Group] = groupIndex;
                groups.Add(new FieldGroup(property.Value.Group, []));
            }

            groups[groupIndex].Fields.Add(property);
        }

        return groups;
    }

    private string GetGroupLabel(string groupKey) => this.implementation?.GetSettingsGroupLabel(groupKey) ?? groupKey;

    private IReadOnlyList<ToolSettingsGroupLink> GetGroupLinks(string groupKey) => this.implementation?.GetSettingsGroupLinks(groupKey) ?? [];

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
        this.toolDefinition is not null && this.ToolSettingsService.IsFieldLocked(this.toolDefinition, fieldName);

    private string GetFieldPlaceholder(string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        string.IsNullOrWhiteSpace(this.GetValue(fieldName)) ? this.GetFieldDefaultValue(fieldName, fieldDefinition) : string.Empty;

    private void UpdateValue(string fieldName, string? value)
    {
        this.values[fieldName] = value ?? string.Empty;
        this.validationMessage = string.Empty;
    }

    private async Task Save()
    {
        if (this.toolDefinition is null)
            return;

        var validationState = await this.ToolSettingsService.ValidateSettingsAsync(this.toolDefinition, this.values, this.implementation);
        if (!validationState.IsConfigured)
        {
            this.validationMessage = !string.IsNullOrWhiteSpace(validationState.Message)
                ? validationState.Message
                : string.Format(T("Please configure the required settings: {0}"), string.Join(", ", validationState.MissingRequiredFields));
            return;
        }

        await this.ToolSettingsService.SaveSettingsAsync(this.toolDefinition, this.values);
        this.MudDialog.Close();
    }

    /// <param name="Key">The group's name from the schema, or empty for the ungrouped fields.</param>
    /// <param name="Fields">The fields of this group, in the order the tool declared them.</param>
    private sealed record FieldGroup(string Key, List<KeyValuePair<string, ToolSettingsFieldDefinition>> Fields);
}
