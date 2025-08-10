using System.Collections.Concurrent;
using System.Linq.Expressions;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Settings;

public static class ManagedConfiguration
{
    private static readonly ConcurrentDictionary<string, IConfig> METADATA = new();
    
    /// <summary>
    /// Registers a configuration setting with a default value.
    /// </summary>
    /// <remarks>
    /// When called from the JSON deserializer, the configSelection parameter will be null.
    /// In this case, the method will return the default value without registering the setting.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the configuration class.</param>
    /// <param name="defaultValue">The default value to use when the setting is not configured.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>The default value.</returns>
    public static TValue Register<TClass, TValue>(Expression<Func<Data, TClass>>? configSelection, Expression<Func<TClass, TValue>> propertyExpression, TValue defaultValue)
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if(configSelection is null)
            return defaultValue;
		
        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return defaultValue;
        
        METADATA[configPath] = new ConfigMeta<TClass, TValue>(configSelection, propertyExpression)
        {
            Default = defaultValue,
        };

        return defaultValue;
    }

    /// <summary>
    /// Attempts to retrieve the configuration metadata for a given configuration selection and property expression.
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default value set to default(TValue).
    /// This allows the caller to handle the absence of configuration gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata if found.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>
    public static bool TryGet<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression, out ConfigMeta<TClass, TValue> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, TValue> meta)
        {
            configMeta = meta;
            return true;
        }
        
        configMeta = new NoConfig<TClass, TValue>(configSelection, propertyExpression) 
        {
            Default = default!,
        };
        
        return false;
    }

    /// <summary>
    /// Attempts to process the configuration settings from a Lua table.
    /// </summary>
    /// <remarks>
    /// When the configuration is successfully processed, it updates the configuration metadata with the configured value.
    /// Furthermore, it locks the managed state of the configuration metadata to the provided configuration plugin ID.
    /// The setting's value is set to the configured value.
    /// </remarks>
    /// <param name="configPluginId">The ID of the related configuration plugin.</param>
    /// <param name="settings">The Lua table containing the settings to process.</param>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the configuration class.</param>
    /// <param name="dryRun">When true, the method will not apply any changes, but only check if the configuration can be read.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression, Guid configPluginId, LuaTable settings, bool dryRun)
    {
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var (configuredValue, successful) = configMeta.Default switch
        {
            Enum => settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredEnumValue) && configuredEnumValue.TryRead<string>(out var configuredEnumText) && Enum.TryParse(typeof(TValue), configuredEnumText, true, out var configuredEnum) ? ((TValue)configuredEnum, true) : (configMeta.Default, false),
            Guid => settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredGuidValue) && configuredGuidValue.TryRead<string>(out var configuredGuidText) && Guid.TryParse(configuredGuidText, out var configuredGuid) ? ((TValue)(object)configuredGuid, true) : (configMeta.Default, false),

            string => settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredTextValue) && configuredTextValue.TryRead<string>(out var configuredText) ? ((TValue)(object)configuredText, true) : (configMeta.Default, false),
            bool => settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredBoolValue) && configuredBoolValue.TryRead<bool>(out var configuredState) ? ((TValue)(object)configuredState, true) : (configMeta.Default, false),

            int => settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredIntValue) && configuredIntValue.TryRead<int>(out var configuredInt) ? ((TValue)(object)configuredInt, true) : (configMeta.Default, false),
            double => settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredDoubleValue) && configuredDoubleValue.TryRead<double>(out var configuredDouble) ? ((TValue)(object)configuredDouble, true) : (configMeta.Default, false),
            float => settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredFloatValue) && configuredFloatValue.TryRead<float>(out var configuredFloat) ? ((TValue)(object)configuredFloat, true) : (configMeta.Default, false),

            _ => (configMeta.Default, false),
        };

        if(dryRun)
            return successful;
        
        switch (successful)
        {
            case true:
                //
                // Case: the setting was configured, and we could read the value successfully.
                //
                configMeta.SetValue(configuredValue);
                configMeta.LockManagedState(configPluginId);
                break;

            case false when configMeta.IsLocked && configMeta.MangedByConfigPluginId == configPluginId:
                //
                // Case: the setting was configured previously, but we could not read the value successfully.
                // This happens when the setting was removed from the configuration plugin. We handle that
                // case only when the setting was locked and managed by the same configuration plugin.
                //
                // The other case, when the setting was locked and managed by a different configuration plugin,
                // is handled by the IsConfigurationLeftOver method, which checks if the configuration plugin
                // is still available. If it is not available, it resets the managed state of the
                // configuration setting, allowing it to be reconfigured by a different plugin or left unchanged.
                //
                configMeta.ResetManagedState();
                break;
            
            case false:
                //
                // Case: the setting was not configured, or we could not read the value successfully.
                // We do not change the setting, and it remains at whatever value it had before.
                //
                break;
        }

        return successful;
    }

    /// <summary>
    /// Checks if a configuration setting is left over from a configuration plugin that is no longer available.
    /// If the configuration setting is locked and managed by a configuration plugin that is not available,
    /// it resets the managed state of the configuration setting and returns true.
    /// Otherwise, it returns false.
    /// </summary>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the configuration class.</param>
    /// <param name="availablePlugins">The collection of available plugins to check against.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True if the configuration setting is left over and was reset, otherwise false.</returns>
    public static bool IsConfigurationLeftOver<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression, IEnumerable<IAvailablePlugin> availablePlugins)
    {
        if (!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;

        if(configMeta.MangedByConfigPluginId == Guid.Empty || !configMeta.IsLocked)
            return false;
        
        // Check if the configuration plugin ID is valid against the available plugin IDs:
        var plugin = availablePlugins.FirstOrDefault(x => x.Id == configMeta.MangedByConfigPluginId);
        if (plugin is null)
        {
            // Remove the locked state:
            configMeta.ResetManagedState();
            return true;
        }
        
        return false;
    }
    
    private static string Path<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression)
    {
        var className = typeof(TClass).Name;
		
        var memberExpressionConfig = configSelection.GetMemberExpression();
        var configName = memberExpressionConfig.Member.Name;
		
        var memberExpressionProperty = propertyExpression.GetMemberExpression();
        var propertyName = memberExpressionProperty.Member.Name;

        var configPath = $"{configName}.{className}.{propertyName}";
        return configPath;
    }
}