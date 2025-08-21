using System.Linq.Expressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents metadata for a configuration object from a configuration plugin. These are
/// complex objects such as configured LLM providers, chat templates, etc.
/// </summary>
public sealed record PluginConfigurationObject
{
    private static readonly SettingsManager SETTINGS_MANAGER = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger<PluginConfigurationObject>();
    
    /// <summary>
    /// The id of the configuration plugin to which this configuration object belongs.
    /// </summary>
    public required Guid ConfigPluginId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// The id of the configuration object, e.g., the id of a chat template.
    /// </summary>
    public required Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The type of the configuration object.
    /// </summary>
    public required PluginConfigurationObjectType Type { get; init; } = PluginConfigurationObjectType.NONE;

    /// <summary>
    /// Parses Lua table entries into configuration objects of the specified type, populating the
    /// provided list with results.
    /// </summary>
    /// <typeparam name="TClass">The type of configuration object to parse, which must
    /// inherit from <see cref="ConfigurationBaseObject"/>.</typeparam>
    /// <param name="configObjectType">The type of configuration object to process, as specified
    /// in <see cref="PluginConfigurationObjectType"/>.</param>
    /// <param name="configObjectSelection">An expression to retrieve existing configuration objects from
    /// the main configuration data.</param>
    /// <param name="nextConfigObjectNumSelection">An expression to retrieve the next available configuration
    /// object number from the main configuration data.</param>
    /// <param name="mainTable">The Lua table containing entries to parse into configuration objects.</param>
    /// <param name="configPluginId">The unique identifier of the plugin associated with the configuration
    /// objects being parsed.</param>
    /// <param name="configObjects">The list to populate with the parsed configuration objects.
    /// This parameter is passed by reference.</param>
    /// <param name="dryRun">Specifies whether to perform the operation as a dry run, where changes
    /// are not persisted.</param>
    /// <returns>Returns true if parsing succeeds and configuration objects are added
    /// to the list; otherwise, false.</returns>
    public static bool TryParse<TClass>(
        PluginConfigurationObjectType configObjectType,
        Expression<Func<Data, List<TClass>>> configObjectSelection,
        Expression<Func<Data, uint>> nextConfigObjectNumSelection,
        LuaTable mainTable,
        Guid configPluginId,
        ref List<PluginConfigurationObject> configObjects,
        bool dryRun
    ) where TClass : ConfigurationBaseObject
    {
        var luaTableName = configObjectType switch
        {
            PluginConfigurationObjectType.LLM_PROVIDER => "LLM_PROVIDERS",
            PluginConfigurationObjectType.CHAT_TEMPLATE => "CHAT_TEMPLATES",
            PluginConfigurationObjectType.DATA_SOURCE => "DATA_SOURCES",
            PluginConfigurationObjectType.EMBEDDING_PROVIDER => "EMBEDDING_PROVIDERS",
            PluginConfigurationObjectType.PROFILE => "PROFILES",
            
            _ => null,
        };

        if (luaTableName is null)
        {
            LOG.LogError($"The configuration object type '{configObjectType}' is not supported yet.");
            return false;
        }

        if (!mainTable.TryGetValue(luaTableName, out var luaValue) || !luaValue.TryRead<LuaTable>(out var luaTable))
        {
            LOG.LogWarning($"The {luaTableName} table does not exist or is not a valid table.");
            return false;
        }

        var storedObjects = configObjectSelection.Compile()(SETTINGS_MANAGER.ConfigurationData);
        var numberObjects = luaTable.ArrayLength;
        ThreadSafeRandom? random = null;
        for (var i = 1; i <= numberObjects; i++)
        {
            var luaObjectTableValue = luaTable[i];
            if (!luaObjectTableValue.TryRead<LuaTable>(out var luaObjectTable))
            {
                LOG.LogWarning($"The {luaObjectTable} table at index {i} is not a valid table.");
                continue;
            }

            var (wasParsingSuccessful, configObject) = configObjectType switch
            {
                PluginConfigurationObjectType.LLM_PROVIDER => (Settings.Provider.TryParseProviderTable(i, luaObjectTable, configPluginId, out var configurationObject) && configurationObject != Settings.Provider.NONE, configurationObject),
                PluginConfigurationObjectType.CHAT_TEMPLATE => (ChatTemplate.TryParseChatTemplateTable(i, luaObjectTable, configPluginId, out var configurationObject) && configurationObject != ChatTemplate.NO_CHAT_TEMPLATE, configurationObject),

                _ => (false, NoConfigurationObject.INSTANCE)
            };

            if (wasParsingSuccessful)
            {
                // Store it in the config object list:
                configObjects.Add(new()
                {
                    ConfigPluginId = configPluginId,
                    Id = Guid.Parse(configObject.Id),
                    Type = configObjectType,
                });

                if (dryRun)
                    continue;

                var objectIndex = storedObjects.FindIndex(t => t.Id == configObject.Id);
                
                // Case: The object already exists, we update it:
                if (objectIndex > -1)
                {
                    var existingObject = storedObjects[objectIndex];
                    configObject = configObject with { Num = existingObject.Num };
                    storedObjects[objectIndex] = (TClass)configObject;
                }
                
                // Case: The object does not exist, we have to add it
                else
                {
                    // Case: Increment the next number was successful
                    if (nextConfigObjectNumSelection.TryIncrement(SETTINGS_MANAGER.ConfigurationData) is { Success: true, UpdatedValue: var nextNum })
                    {
                        configObject = configObject with { Num = nextNum };
                        storedObjects.Add((TClass)configObject);
                    }
                    else
                    {
                        // Case: The next number could not be incremented, we use a random number
                        random ??= new ThreadSafeRandom();
                        configObject = configObject with { Num = (uint)random.Next(500_000, 1_000_000) };
                        storedObjects.Add((TClass)configObject);
                        LOG.LogWarning($"The next number for the configuration object '{configObject.Name}' (id={configObject.Id}) could not be incremented. Using a random number instead.");
                    }
                }
            }
            else
                LOG.LogWarning($"The {luaObjectTable} table at index {i} does not contain a valid chat template configuration.");
        }
        
        return true;
    }
    
    /// <summary>
    /// Cleans up configuration objects of a specified type that are no longer associated with any available plugin.
    /// </summary>
    /// <typeparam name="TClass">The type of configuration object to clean up.</typeparam>
    /// <param name="configObjectType">The type of configuration object to process.</param>
    /// <param name="configObjectSelection">A selection expression to retrieve the configuration objects from the main configuration.</param>
    /// <param name="availablePlugins">A list of currently available plugins.</param>
    /// <param name="configObjectList">A list of all existing configuration objects.</param>
    /// <returns>Returns true if the configuration was altered during cleanup; otherwise, false.</returns>
    public static bool CleanLeftOverConfigurationObjects<TClass>(
        PluginConfigurationObjectType configObjectType,
        Expression<Func<Data, List<TClass>>> configObjectSelection,
        IList<IAvailablePlugin> availablePlugins,
        IList<PluginConfigurationObject> configObjectList) where TClass : IConfigurationObject
    {
        var configuredObjects = configObjectSelection.Compile()(SETTINGS_MANAGER.ConfigurationData);
        var leftOverObjects = new List<TClass>();
        foreach (var configuredObject in configuredObjects)
        {
            if(!configuredObject.IsEnterpriseConfiguration)
                continue;

            var configObjectSourcePluginId = configuredObject.EnterpriseConfigurationPluginId;
            if(configObjectSourcePluginId == Guid.Empty)
                continue;
            
            var templateSourcePlugin = availablePlugins.FirstOrDefault(plugin => plugin.Id == configObjectSourcePluginId);
            if(templateSourcePlugin is null)
            {
                LOG.LogWarning($"The configured object '{configuredObject.Name}' (id={configuredObject.Id}) is based on a plugin that is not available anymore. Removing the chat template from the settings.");
                leftOverObjects.Add(configuredObject);
            }
            
            if(!configObjectList.Any(configObject =>
                   configObject.Type == configObjectType &&
                   configObject.ConfigPluginId == configObjectSourcePluginId &&
                   configObject.Id.ToString() == configuredObject.Id))
            {
                LOG.LogWarning($"The configured object '{configuredObject.Name}' (id={configuredObject.Id}) is not present in the configuration plugin anymore. Removing the chat template from the settings.");
                leftOverObjects.Add(configuredObject);
            }
        }
        
        // Remove collected items after enumeration to avoid modifying the collection during iteration:
        var wasConfigurationChanged = leftOverObjects.Count > 0;
        foreach (var item in leftOverObjects.Distinct())
            configuredObjects.Remove(item);
        
        return wasConfigurationChanged;
    }
}