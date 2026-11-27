using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Reads seven settings of the AI tasks under the names they had in AI Studio v26.11.1.
/// </summary>
/// <remarks>
/// Compatibility shim, remove after 2027-05-26, see documentation/compatibility-shims/2026-11-ai-task-setting-names.md.
/// Version v26.11.2 took the word "agent" out of the names of these settings. An organization rolls out a
/// new version over weeks, and devices with v26.11.1 read only the former names. A configuration plugin
/// therefore keeps the former names meanwhile, or sets both.
/// </remarks>
public static class FormerSettingNames
{
    private const string USER_OVERRIDE_SUFFIX = ".AllowUserOverride";

    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(FormerSettingNames));

    /// <summary>
    /// The current name of each setting, by the name it had in AI Studio v26.11.1.
    /// </summary>
    /// <remarks>
    /// The settings of the AI tasks which came after v26.11.1 have no former name here, because no
    /// released version knew them.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> CURRENT_NAMES = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DataAgentDataSourceSelection.PreselectAgentOptions"] = "DataDataSourceSelection.PreselectOptions",
        ["DataAgentDataSourceSelection.PreselectedAgentProvider"] = "DataDataSourceSelection.PreselectedProvider",
        ["DataAgentRetrievalContextValidation.EnableRetrievalContextValidation"] = "DataRetrievalContextValidation.EnableRetrievalContextValidation",
        ["DataAgentRetrievalContextValidation.PreselectAgentOptions"] = "DataRetrievalContextValidation.PreselectOptions",
        ["DataAgentRetrievalContextValidation.PreselectedAgentProvider"] = "DataRetrievalContextValidation.PreselectedProvider",
        ["DataAgentRetrievalContextValidation.NumParallelValidations"] = "DataRetrievalContextValidation.NumParallelValidations",
        ["DataAssistantPluginAudit.PreselectedAgentProvider"] = "DataAssistantPluginAudit.PreselectedProvider",
    };

    /// <summary>
    /// Copies the SETTINGS table of a configuration plugin and gives every setting it sets under a
    /// former name its current name.
    /// </summary>
    /// <remarks>
    /// A former name covers its <c>.AllowUserOverride</c> companion as well. When a plugin sets a
    /// setting under both names, as it does while the devices of an organization run both versions,
    /// the current name wins and the former one is left out.
    /// </remarks>
    /// <param name="settingsTable">The SETTINGS table of the plugin.</param>
    /// <param name="pluginName">The name of the plugin, for the log.</param>
    /// <param name="logHints">Whether to log a hint for each setting which the plugin sets under its former name only.</param>
    /// <returns>A new table, which holds every setting under its current name.</returns>
    public static LuaTable Resolve(LuaTable settingsTable, string pluginName, bool logHints)
    {
        var resolvedTable = new LuaTable();
        var previousKey = LuaValue.Nil;
        while (settingsTable.TryGetNext(previousKey, out var pair))
        {
            previousKey = pair.Key;
            if (!pair.Key.TryRead<string>(out var settingName) || !TryGetCurrentName(settingName, out var currentName))
            {
                resolvedTable[pair.Key] = pair.Value;
                continue;
            }

            if (settingsTable.TryGetValue(currentName, out _))
                continue;

            resolvedTable[currentName] = pair.Value;
            if (logHints && !settingName.EndsWith(USER_OVERRIDE_SUFFIX, StringComparison.Ordinal))
                LOG.LogWarning($"The configuration plugin '{pluginName}' sets '{settingName}', the name of the setting '{currentName}' before AI Studio v26.11.2. AI Studio reads the former name until May 2027. Set the setting under both names until every device runs v26.11.2 or newer, then under its new name only.");
        }

        return resolvedTable;
    }

    /// <summary>
    /// Looks up the current name of a setting by its former name, the <c>.AllowUserOverride</c> companion included.
    /// </summary>
    /// <param name="settingName">The name the plugin uses.</param>
    /// <param name="currentName">The current name, when the plugin uses a former name; otherwise empty.</param>
    /// <returns>True, when the plugin uses a former name.</returns>
    private static bool TryGetCurrentName(string settingName, out string currentName)
    {
        var isUserOverride = settingName.EndsWith(USER_OVERRIDE_SUFFIX, StringComparison.Ordinal);
        var baseName = isUserOverride ? settingName[..^USER_OVERRIDE_SUFFIX.Length] : settingName;
        if (!CURRENT_NAMES.TryGetValue(baseName, out var currentBaseName))
        {
            currentName = string.Empty;
            return false;
        }

        currentName = isUserOverride ? $"{currentBaseName}{USER_OVERRIDE_SUFFIX}" : currentBaseName;
        return true;
    }
}