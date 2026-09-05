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
    /// Reads the saved, effective configuration and exports the selected areas. Incomplete tools
    /// may be exported too: administrators can finish the configuration in their Lua plugin.
    /// </summary>
    /// <remarks>
    /// Uses the same organization overrides and keyring values as tool execution, without saving
    /// settings or writing to the keyring. The caller provides the admin-only UI and copies a
    /// successful, nonempty result to the clipboard.<br/><br/>
    /// Only explicitly selected areas are included. Missing values stay absent, explicitly empty
    /// non-secret values stay empty, and runtime defaults are not filled in. Secrets require
    /// opt-in and enterprise encryption, and are always locked, even in a default-value export.
    /// The optional minimum provider confidence is also always a fixed requirement.
    /// </remarks>
    public async Task<ToolSettingsExportResult> ExportAsync(ToolDefinition definition, IToolImplementation implementation, ToolSettingsExportOptions options)
    {
        var areas = implementation.GetExportableSettings(definition);
        var values = await this.GetSettingsAsync(definition);
        var confidence = settingsManager.GetMinimumProviderConfidenceForTool(definition.Id, definition.MinimumProviderConfidence);
        return BuildConfigurationSection(definition, areas, values, options, confidence, PluginFactory.EnterpriseEncryption);
    }

    /// <summary>
    /// Resolves selected areas to known fields in schema order. Overlapping areas include a
    /// field only once; unknown field names are ignored. Form visibility does not limit exports.
    /// </summary>
    private static IReadOnlyList<string> GetSelectedFieldNames(ToolDefinition definition, IReadOnlyList<ExportableSettings> areas, IReadOnlySet<string> selectedAreaIds)
    {
        var selectedIds = new HashSet<string>(selectedAreaIds, StringComparer.Ordinal);
        var selectedFields = areas.Where(area => selectedIds.Contains(area.Id))
            .SelectMany(area => area.FieldNames)
            .ToHashSet(StringComparer.Ordinal);

        return definition.SettingsSchema.Properties.Keys.Where(selectedFields.Contains).ToList();
    }

    /// <summary>
    /// Builds a fragment from one snapshot. A failed encryption returns no Lua, even when other
    /// fields have already been processed, so the caller cannot copy a partial export by accident.
    /// </summary>
    private static ToolSettingsExportResult BuildConfigurationSection(ToolDefinition definition, IReadOnlyList<ExportableSettings> areas, IReadOnlyDictionary<string, string> values,
        ToolSettingsExportOptions options, ConfidenceLevel minimumProviderConfidence, EnterpriseEncryption? encryption)
    {
        if (!Enum.IsDefined(options.Mode))
            return new(ErrorMessage: TB("The selected tool configuration export mode is invalid."));

        var lockedValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var defaultValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fieldName in GetSelectedFieldNames(definition, areas, options.SelectedAreaIds))
        {
            if (!values.TryGetValue(fieldName, out var value))
                continue;

            var key = ManagedSettingKey(definition.Id, fieldName);
            if (definition.SettingsSchema.Properties[fieldName].Secret)
            {
                if (!options.IncludeSecrets || string.IsNullOrWhiteSpace(value))
                    continue;

                if (encryption?.IsAvailable is not true)
                    return new(ErrorMessage: TB("Cannot export encrypted tool secrets: No enterprise encryption secret is configured."));

                if (!encryption.TryEncrypt(value, out var encrypted))
                    return new(ErrorMessage: TB("The tool secrets could not be encrypted. Nothing was exported."));

                lockedValues[key] = encrypted;
            }
            else if (options.Mode is ToolSettingsExportMode.LOCKED)
                lockedValues[key] = value;
            else
                defaultValues[key] = value;
        }

        if (lockedValues.Count is 0 && defaultValues.Count is 0 && !options.IncludeMinimumProviderConfidence)
            return new();

        if (options.IncludeMinimumProviderConfidence && (!Enum.IsDefined(minimumProviderConfidence) || minimumProviderConfidence is ConfidenceLevel.UNKNOWN))
            return new(ErrorMessage: TB("The tool's minimum provider confidence level is invalid."));

        var lua = new StringBuilder();
        lua.AppendLine("CONFIG = CONFIG or {}");
        lua.AppendLine("CONFIG[\"SETTINGS\"] = CONFIG[\"SETTINGS\"] or {}");
        AppendSettings(lua, LOCKED_SETTINGS, lockedValues);
        AppendSettings(lua, DEFAULT_SETTINGS, defaultValues);

        if (options.IncludeMinimumProviderConfidence)
        {
            AppendSettings(lua, MINIMUM_CONFIDENCE, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [definition.Id] = minimumProviderConfidence.ToString(),
            });

            // The existing plugin contract locks the entire confidence dictionary, not one entry:
            lua.AppendLine($"CONFIG[\"SETTINGS\"][\"{MINIMUM_CONFIDENCE}.AllowUserOverride\"] = false");
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
        lua.AppendLine();
        lua.AppendLine($"{table} = {table} or {{}}");
        foreach (var (key, value) in values)
            lua.AppendLine($"{table}[\"{LuaTools.EscapeLuaString(key)}\"] = \"{LuaTools.EscapeLuaString(value)}\"");
    }
}