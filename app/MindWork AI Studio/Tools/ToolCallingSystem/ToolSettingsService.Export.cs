using System.Text;

using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

using SharedTools;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed partial class ToolSettingsService
{
    private const string LOCKED_SETTINGS = "DataTools.LockedToolSettings";
    private const string DEFAULT_SETTINGS = "DataTools.DefaultToolSettings";
    private const string MINIMUM_CONFIDENCE = "DataTools.MinimumProviderConfidenceByToolId";

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ToolSettingsService).Namespace, nameof(ToolSettingsService));

    /// <summary>
    /// The areas an administrator can choose from when exporting a tool collection: those of each of its tools.
    /// </summary>
    /// <remarks>
    /// Each tool divides its settings itself, see IToolImplementation.GetExportableSettings. The ID
    /// of an area is unique only within its tool, so the ID here adds the tool's. The label names the
    /// tool as soon as more than one tool of the collection has settings to export.
    /// </remarks>
    /// <param name="tools">The tools of the collection.</param>
    public static IReadOnlyList<ToolSettingsExportArea> GetExportAreas(IReadOnlyList<ToolCatalogTool> tools)
    {
        var areasByTool = tools
            .Select(tool => (Tool: tool, Areas: tool.Implementation.GetExportableSettings(tool.Definition)))
            .Where(entry => entry.Areas.Count > 0)
            .ToList();

        var namesTheTool = areasByTool.Count > 1;
        return areasByTool
            .SelectMany(entry => entry.Areas.Select(area => new ToolSettingsExportArea(
                $"{entry.Tool.Definition.Id}/{area.Id}",
                namesTheTool ? $"{entry.Tool.Implementation.GetDisplayName()}: {area.Label}" : area.Label,
                entry.Tool,
                area)))
            .ToList();
    }

    /// <summary>
    /// Reads the saved, effective configuration and exports the selected areas. Incomplete tools
    /// may be exported too: administrators can finish the configuration in their Lua plugin.
    /// </summary>
    /// <remarks>
    /// Uses the same organization overrides and keyring values as tool execution, without saving
    /// settings or writing to the keyring. The caller provides the admin-only UI and copies a
    /// successful, nonempty result to the clipboard.<br/><br/>
    /// Only explicitly selected areas are included, see GetExportAreas. Missing values stay absent,
    /// explicitly empty non-secret values stay empty, and runtime defaults are not filled in.
    /// Secrets require opt-in and enterprise encryption, and are always locked, even in a
    /// default-value export. The optional minimum provider confidence is also always a fixed
    /// requirement.<br/><br/>
    /// That confidence belongs to the collection, which only the tool registry knows. The registry
    /// depends on this service, so the caller asks it and passes it in.
    /// </remarks>
    /// <param name="tools">The tools of the collection.</param>
    /// <param name="options">What the administrator chose to export.</param>
    /// <param name="collectionId">The ID of the collection, under which the confidence is exported.</param>
    /// <param name="minimumProviderConfidence">The confidence the collection needs, as the tool registry resolves it.</param>
    public async Task<ToolSettingsExportResult> ExportAsync(IReadOnlyList<ToolCatalogTool> tools, ToolSettingsExportOptions options, string collectionId, ConfidenceLevel minimumProviderConfidence)
    {
        var lockedValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var defaultValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var selectedAreas = GetExportAreas(tools).Where(area => options.SelectedAreaIds.Contains(area.Id)).ToList();
        foreach (var tool in tools)
        {
            var areasOfTool = selectedAreas.Where(area => area.Tool.Definition.Id == tool.Definition.Id).Select(area => area.Area).ToList();
            if (areasOfTool.Count == 0)
                continue;

            var values = await this.GetSettingsAsync(tool.Definition);
            var issue = CollectSettings(tool.Definition, areasOfTool, values, options, PluginFactory.EnterpriseEncryption, lockedValues, defaultValues);
            if (issue is not null)
                return new(ErrorMessage: issue);
        }

        return BuildConfigurationSection(lockedValues, defaultValues, options, collectionId, minimumProviderConfidence);
    }

    /// <summary>
    /// Resolves selected areas to known fields in schema order. Overlapping areas include a
    /// field only once; unknown field names are ignored. Form visibility does not limit exports.
    /// </summary>
    private static IReadOnlyList<string> GetSelectedFieldNames(ToolDefinition definition, IEnumerable<ExportableSettings> selectedAreas)
    {
        var selectedFields = selectedAreas
            .SelectMany(area => area.FieldNames)
            .ToHashSet(StringComparer.Ordinal);

        return definition.SettingsSchema.Properties.Keys.Where(selectedFields.Contains).ToList();
    }

    /// <summary>
    /// Adds the selected settings of one tool to the values to export, by their managed key.
    /// </summary>
    /// <returns>Why the export failed, or null when it may go on.</returns>
    private static string? CollectSettings(ToolDefinition definition, IEnumerable<ExportableSettings> selectedAreas, IReadOnlyDictionary<string, string> values, ToolSettingsExportOptions options, EnterpriseEncryption? encryption, Dictionary<string, string> lockedValues, Dictionary<string, string> defaultValues)
    {
        foreach (var fieldName in GetSelectedFieldNames(definition, selectedAreas))
        {
            if (!values.TryGetValue(fieldName, out var value))
                continue;

            var key = ManagedSettingKey(definition.Id, fieldName);
            if (definition.SettingsSchema.Properties[fieldName].Secret)
            {
                if (!options.IncludeSecrets || string.IsNullOrWhiteSpace(value))
                    continue;

                if (encryption?.IsAvailable is not true)
                    return TB("Cannot export encrypted tool secrets: No enterprise encryption secret is configured.");

                if (!encryption.TryEncrypt(value, out var encrypted))
                    return TB("The tool secrets could not be encrypted. Nothing was exported.");

                lockedValues[key] = encrypted;
            }
            else if (options.Mode is ToolSettingsExportMode.LOCKED)
                lockedValues[key] = value;
            else
                defaultValues[key] = value;
        }

        return null;
    }

    /// <summary>
    /// Builds a fragment from the collected values. A failed encryption returned before this, even
    /// when other fields had already been processed, so the caller cannot copy a partial export by
    /// accident.
    /// </summary>
    private static ToolSettingsExportResult BuildConfigurationSection(IReadOnlyDictionary<string, string> lockedValues, IReadOnlyDictionary<string, string> defaultValues, ToolSettingsExportOptions options, string collectionId, ConfidenceLevel minimumProviderConfidence)
    {
        if (lockedValues.Count is 0 && defaultValues.Count is 0 && !options.IncludeMinimumProviderConfidence)
            return new();

        if (options.IncludeMinimumProviderConfidence && (!Enum.IsDefined(minimumProviderConfidence) || minimumProviderConfidence is ConfidenceLevel.UNKNOWN))
            return new(ErrorMessage: TB("The tool's minimum provider confidence level is invalid."));

        var lua = new StringBuilder();
        AppendSettings(lua, LOCKED_SETTINGS, lockedValues);
        AppendSettings(lua, DEFAULT_SETTINGS, defaultValues);

        if (options.IncludeMinimumProviderConfidence)
        {
            AppendSettings(lua, MINIMUM_CONFIDENCE, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [collectionId] = minimumProviderConfidence.ToString(),
            });

            //
            // A managed setting without an AllowUserOverride flag is locked anyway, so writing
            // "= false" here would only restate the default — and would silently undo an
            // administrator's own "= true" further up in the same plugin, for the whole
            // dictionary rather than this tool's entry. A comment says the same thing without
            // overwriting anything:
            //
            lua.AppendLine($"-- The whole table is locked unless you set CONFIG[\"SETTINGS\"][\"{MINIMUM_CONFIDENCE}.AllowUserOverride\"] = true");
        }

        return new(LuaCode: lua.ToString());
    }

    /// <summary>
    /// Adds entries without replacing the table, so administrators can combine export fragments
    /// in one plugin. Later assignments to the same key win. This does not merge dictionaries
    /// across separate configuration plugins; those still follow managed-setting precedence.
    /// </summary>
    private static void AppendSettings(StringBuilder lua, string settingName, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count is 0)
            return;

        var table = $"CONFIG[\"SETTINGS\"][\"{settingName}\"]";
        if (lua.Length > 0)
            lua.AppendLine();
        lua.AppendLine($"{table} = {table} or {{}}");
        foreach (var (key, value) in values)
            lua.AppendLine($"{table}[\"{LuaTools.EscapeLuaString(key)}\"] = \"{LuaTools.EscapeLuaString(value)}\"");
    }
}