using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

/// <summary>
/// Shows and saves the settings of one entry of the tool catalog.
/// </summary>
/// <remarks>
/// An entry is a tool collection, see ToolCollectionDefinition. Its tools keep their settings to
/// themselves, so the dialog shows one section per tool, and an organization still addresses each
/// field as "toolId.fieldName". A tool which belongs to no declared collection forms an entry of its
/// own, and the dialog looks as it always did for it: one section without a heading.
/// </remarks>
public partial class ToolSettingsDialog : SettingsDialogBase
{
    /// <summary>
    /// The ID of the collection whose settings to show. The ID of one of its tools stands for the whole collection.
    /// </summary>
    [Parameter]
    public string CollectionId { get; set; } = string.Empty;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private ToolSettingsService ToolSettingsService { get; init; } = null!;

    private ToolCatalogItem? item;
    private IReadOnlyList<ToolSection> sections = [];
    private string validationMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.item = await this.ToolRegistry.GetCatalogItemAsync(this.CollectionId);
        if (this.item is null)
            return;

        var loadedSections = new List<ToolSection>(this.item.Tools.Count);
        foreach (var tool in this.item.Tools)
            loadedSections.Add(new(tool.Definition, tool.Implementation, await this.ToolSettingsService.GetSettingsAsync(tool.Definition), BuildFieldGroups(tool.Definition)));

        this.sections = loadedSections;
    }

    /// <summary>
    /// Whether each section is headed by the name of its tool.
    /// </summary>
    /// <remarks>
    /// Only when the entry holds more than one tool. Otherwise the title of the dialog names the
    /// tool already.
    /// </remarks>
    private bool ShowsToolHeaders => this.sections.Count > 1;

    private static string GetValue(ToolSection section, string fieldName) => section.Values.GetValueOrDefault(fieldName, string.Empty);

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

    /// <summary>
    /// The groups of one tool as they are rendered right now, without the fields the tool is hiding.
    /// </summary>
    /// <remarks>
    /// Which fields make sense can depend on what is filled in, so this is built on every
    /// render rather than once: a field the tool starts to offer has to appear as soon as the
    /// value it depends on changes. A group whose every field is hidden is left out entirely,
    /// so no empty box is rendered.<br/><br/>
    /// Cheap enough to be called more than once per render: a tool has a handful of settings,
    /// and asking the tool about one of them costs a dictionary lookup or two.
    /// </remarks>
    private static IReadOnlyList<FieldGroup> BuildVisibleFieldGroups(ToolSection section)
    {
        var visibleGroups = new List<FieldGroup>();
        foreach (var group in section.FieldGroups)
        {
            var visibleFields = group.Fields.Where(field => section.Implementation.IsSettingsFieldVisible(field.Key, section.Values)).ToList();
            if (visibleFields.Count > 0)
                visibleGroups.Add(new FieldGroup(group.Key, visibleFields));
        }

        return visibleGroups;
    }

    /// <summary>
    /// Whether one group shows a heading above its fields.
    /// </summary>
    /// <remarks>
    /// A tool that declares no groups has a single nameless group holding everything, and a
    /// heading above the only box would say nothing the dialog's title does not say already.
    /// As soon as there is a second box, each of them has to state which one it is — the box
    /// holding the fields that belong to no group in particular included.<br/><br/>
    /// It counts the boxes of the tool that are actually rendered, so a group the tool hides
    /// entirely does not leave the remaining box with a heading it does not need.
    /// </remarks>
    private static bool ShowsGroupHeader(ToolSection section, FieldGroup group) => BuildVisibleFieldGroups(section).Count > 1 || !string.IsNullOrEmpty(group.Key);

    /// <remarks>
    /// The ungrouped fields have no name of their own, so the label hook hands back their
    /// empty group name. A tool may still name them through that same hook; when it does not,
    /// they are simply what is left over next to the named groups.
    /// </remarks>
    private string GetGroupLabel(ToolSection section, string groupKey)
    {
        var label = section.Implementation.GetSettingsGroupLabel(groupKey);
        return string.IsNullOrEmpty(label) ? T("General") : label;
    }

    private static IReadOnlyList<ToolSettingsGroupLink> GetGroupLinks(ToolSection section, string groupKey) => section.Implementation.GetSettingsGroupLinks(groupKey);

    /// <summary>
    /// What the tools want to say about their settings as they stand right now.
    /// </summary>
    /// <remarks>
    /// Asked on every render, so a warning follows the value it is about instead of waiting for
    /// the next save. These are not errors: they describe settings that are allowed and do
    /// something other than what they look like, and the dialog saves them either way.
    /// </remarks>
    private IEnumerable<string> GetSettingsWarnings() => this.sections.SelectMany(section => section.Implementation.GetSettingsWarnings(section.Values));

    private static string GetFieldLabel(ToolSection section, string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        section.Implementation.GetSettingsFieldLabel(fieldName, fieldDefinition);

    private static string GetFieldDefaultValue(ToolSection section, string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        section.Implementation.GetSettingsFieldDefaultValue(fieldName, fieldDefinition) ?? string.Empty;

    private string GetFieldDescription(ToolSection section, string fieldName, ToolSettingsFieldDefinition fieldDefinition)
    {
        var description = section.Implementation.GetSettingsFieldDescription(fieldName, fieldDefinition);
        var defaultValue = GetFieldDefaultValue(section, fieldName, fieldDefinition);
        if (string.IsNullOrWhiteSpace(defaultValue))
            return description;

        return string.Format(T("{0} Default: {1}"), description, defaultValue);
    }

    private bool IsFieldDisabled(ToolSection section, string fieldName) => this.ToolSettingsService.IsFieldLocked(section.Definition, fieldName);

    private static string GetFieldPlaceholder(ToolSection section, string fieldName, ToolSettingsFieldDefinition fieldDefinition) =>
        string.IsNullOrWhiteSpace(GetValue(section, fieldName)) ? GetFieldDefaultValue(section, fieldName, fieldDefinition) : string.Empty;

    private void UpdateValue(ToolSection section, string fieldName, string? value)
    {
        section.Values[fieldName] = value ?? string.Empty;
        this.validationMessage = string.Empty;
    }

    /// <summary>
    /// Checks the settings of every tool, and saves those of the tools which have any.
    /// </summary>
    /// <remarks>
    /// Nothing is saved while one tool is not satisfied, so a collection never ends up with some
    /// of its tools configured and the others not, which would leave it unusable all the same.
    /// </remarks>
    private async Task Save()
    {
        if (this.item is null)
            return;

        foreach (var section in this.sections)
        {
            var validationState = await this.ToolSettingsService.ValidateSettingsAsync(section.Definition, section.Values, section.Implementation);
            if (validationState.IsConfigured)
                continue;

            this.validationMessage = !string.IsNullOrWhiteSpace(validationState.Message)
                ? validationState.Message
                : string.Format(T("Please configure the required settings: {0}"), string.Join(", ", validationState.MissingRequiredFields));
            return;
        }

        foreach (var section in this.sections.Where(section => section.Definition.SettingsSchema.Properties.Count > 0))
            await this.ToolSettingsService.SaveSettingsAsync(section.Definition, section.Values);

        this.MudDialog.Close();
    }

    /// <param name="Definition">The tool.</param>
    /// <param name="Implementation">The implementation of the tool, which labels and checks its fields.</param>
    /// <param name="Values">The values as they are being edited.</param>
    /// <param name="FieldGroups">The groups of its fields, in the order the tool declared them.</param>
    private sealed record ToolSection(ToolDefinition Definition, IToolImplementation Implementation, Dictionary<string, string> Values, IReadOnlyList<FieldGroup> FieldGroups);

    /// <param name="Key">The group's name from the schema, or empty for the ungrouped fields.</param>
    /// <param name="Fields">The fields of this group, in the order the tool declared them.</param>
    private sealed record FieldGroup(string Key, List<KeyValuePair<string, ToolSettingsFieldDefinition>> Fields);
}