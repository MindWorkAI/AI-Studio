using System.Collections.Concurrent;
using System.Linq.Expressions;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

public static partial class ManagedConfiguration
{
    private static readonly ConcurrentDictionary<string, IConfig> METADATA = new();
    
    private static SettingsManager SettingsManagerAccess => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    
    private static ILogger Log => Program.LOGGER_FACTORY.CreateLogger(nameof(ManagedConfiguration));

    /// <summary>
    /// Attempts to retrieve the configuration metadata for a given configuration selection and
    /// property expression (enum-based).
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default
    /// value set to default(TValue). This allows the caller to handle the absence of configuration
    /// gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the
    /// configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata
    /// if found.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>
    public static bool TryGet<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression, out ConfigMeta<TClass, TValue> configMeta)
        where TValue : Enum
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, TValue> meta)
        {
            meta.RestoreLockedConfiguration();
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
    /// Attempts to retrieve the configuration metadata for a given configuration selection and
    /// property expression (string-based).
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default
    /// value set to default(TValue). This allows the caller to handle the absence of configuration
    /// gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the
    /// configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata
    /// if found.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>
    public static bool TryGet<TClass>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, string>> propertyExpression, out ConfigMeta<TClass, string> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, string> meta)
        {
            meta.RestoreLockedConfiguration();
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, string>(configSelection, propertyExpression)
        {
            Default = string.Empty,
        };
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the configuration metadata for a given configuration selection and
    /// property expression (ISpanParsable-based).
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default
    /// value set to default(TValue). This allows the caller to handle the absence of configuration
    /// gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the
    /// configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata
    /// if found.</param>
    /// <param name="_">An optional parameter to help with method overload resolution.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>

    // ReSharper disable MethodOverloadWithOptionalParameter
    public static bool TryGet<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression, out ConfigMeta<TClass, TValue> configMeta, ISpanParsable<TValue>? _ = null)
        where TValue : struct, ISpanParsable<TValue>
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, TValue> meta)
        {
            meta.RestoreLockedConfiguration();
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, TValue>(configSelection, propertyExpression)
        {
            Default = default!,
        };
        return false;
    }

    // ReSharper restore MethodOverloadWithOptionalParameter

    /// <summary>
    /// Attempts to retrieve the configuration metadata for a list-based setting.
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default
    /// value set to an empty list. This allows the caller to handle the absence of configuration
    /// gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the
    /// configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata
    /// if found.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>
    public static bool TryGet<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, IList<TValue>>> propertyExpression, out ConfigMeta<TClass, IList<TValue>> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, IList<TValue>> meta)
        {
            meta.RestoreLockedConfiguration();
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, IList<TValue>>(configSelection, propertyExpression)
        {
            Default = [],
        };
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the configuration metadata for a set-based setting.
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default
    /// value set to an empty set. This allows the caller to handle the absence of configuration
    /// gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the
    /// configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata
    /// if found.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>
    public static bool TryGet<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, ISet<TValue>>> propertyExpression, out ConfigMeta<TClass, ISet<TValue>> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, ISet<TValue>> meta)
        {
            meta.RestoreLockedConfiguration();
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, ISet<TValue>>(configSelection, propertyExpression)
        {
            Default = new HashSet<TValue>(),
        };
        return false;
    }
    
    /// <summary>
    /// Attempts to retrieve the configuration metadata for a string dictionary-based setting.
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default
    /// value set to an empty dictionary. This allows the caller to handle the absence of configuration
    /// gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the
    /// configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata
    /// if found.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>
    public static bool TryGet<TClass>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, IDictionary<string, string>>> propertyExpression, out ConfigMeta<TClass, IDictionary<string, string>> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, IDictionary<string, string>> meta)
        {
            meta.RestoreLockedConfiguration();
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, IDictionary<string, string>>(configSelection, propertyExpression)
        {
            Default = new Dictionary<string, string>(),
        };
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the configuration metadata for an enum dictionary-based setting.
    /// </summary>
    /// <remarks>
    /// When no configuration metadata is found, it returns a NoConfig instance with the default
    /// value set to an empty dictionary. This allows the caller to handle the absence of configuration
    /// gracefully. In such cases, the return value of the method will be false.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the
    /// configuration class.</param>
    /// <param name="configMeta">The output parameter that will hold the configuration metadata
    /// if found.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TKey">The enum type of the dictionary keys.</typeparam>
    /// <typeparam name="TValue">The enum type of the dictionary values.</typeparam>
    /// <returns>True if the configuration metadata was found, otherwise false.</returns>
    public static bool TryGet<TClass, TKey, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, Dictionary<TKey, TValue>>> propertyExpression, out ConfigMeta<TClass, Dictionary<TKey, TValue>> configMeta)
        where TKey : struct, Enum
        where TValue : struct, Enum
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, Dictionary<TKey, TValue>> meta)
        {
            meta.RestoreLockedConfiguration();
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, Dictionary<TKey, TValue>>(configSelection, propertyExpression)
        {
            Default = new Dictionary<TKey, TValue>(),
        };
        return false;
    }

    /// <summary>
    /// Removes all managed states whose configuration plugin is not available anymore.
    /// </summary>
    /// <remarks>
    /// This covers every registered setting, regardless of its type: locked settings, editable
    /// defaults, and additive plugin contributions. Settings do not need to be listed anywhere for
    /// this cleanup to work, so adding a new managed setting cannot be forgotten here.<br/><br/>
    /// A locked setting whose plugin is gone is reset to its default value. That is intended: the
    /// value belonged to the organization, not to the user, and the user might not be able to
    /// change it at all.
    /// </remarks>
    /// <param name="availablePlugins">The collection of available plugins to check against.</param>
    /// <param name="deployedEnterpriseConfigPluginIds">
    /// The IDs of the configuration plugins which an organization deployed on this machine, including
    /// those which could not be loaded. A deployed plugin was not removed, so its settings must stay
    /// untouched.
    /// </param>
    /// <returns>True when at least one setting was changed, otherwise false.</returns>
    public static bool CleanupLeftOverManagedConfigurations(IReadOnlyCollection<IAvailablePlugin> availablePlugins, IReadOnlySet<Guid> deployedEnterpriseConfigPluginIds)
    {
        var wasChanged = false;
        var registeredSettingNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var config in METADATA.Values)
        {
            if (config is not ConfigMetaBase configMeta)
                continue;

            registeredSettingNames.Add(configMeta.SettingName);

            //
            // Restore the persisted ownership first. Otherwise, we would not recognize a left-over
            // lock when nobody has read this setting since the settings were loaded:
            //
            configMeta.RestoreLockedConfiguration();

            // Check the locked state:
            if (configMeta.IsLocked && configMeta.LockedByConfigPluginId != Guid.Empty && !IsPluginPresent(configMeta.LockedByConfigPluginId, availablePlugins, deployedEnterpriseConfigPluginIds))
            {
                Log.LogInformation($"Resetting the setting '{configMeta.SettingName}': it was locked by the configuration plugin '{configMeta.LockedByConfigPluginId}', which is not available anymore.");
                configMeta.ResetLockedConfiguration();
                wasChanged = true;
            }

            // Check the editable default state:
            if (CleanupEditableDefaultState(configMeta, availablePlugins, deployedEnterpriseConfigPluginIds))
                wasChanged = true;

            // Check the additive plugin contribution:
            if (configMeta.HasPluginContribution && configMeta.PluginContributionByConfigPluginId != Guid.Empty && !IsPluginPresent(configMeta.PluginContributionByConfigPluginId, availablePlugins, deployedEnterpriseConfigPluginIds))
            {
                Log.LogInformation($"Clearing the plugin contribution for the setting '{configMeta.SettingName}': the configuration plugin '{configMeta.PluginContributionByConfigPluginId}' is not available anymore.");
                configMeta.ClearPluginContribution();
                wasChanged = true;
            }
        }

        // Remove persisted states which belong to settings that do not exist anymore:
        if (RemoveUnknownManagedStates(registeredSettingNames))
            wasChanged = true;

        return wasChanged;
    }

    /// <summary>
    /// Checks whether a configuration plugin is still present on this machine.
    /// </summary>
    /// <remarks>
    /// A plugin counts as present when it was loaded, or when it is deployed but could not be loaded.
    /// The latter matters for organizations: a broken configuration plugin is still in charge, so we
    /// must not treat its settings as left over.
    /// </remarks>
    private static bool IsPluginPresent(Guid configPluginId, IReadOnlyCollection<IAvailablePlugin> availablePlugins, IReadOnlySet<Guid> deployedEnterpriseConfigPluginIds) => deployedEnterpriseConfigPluginIds.Contains(configPluginId) || availablePlugins.Any(x => x.Id == configPluginId);

    /// <summary>
    /// Removes persisted managed states which belong to settings that are not registered anymore.
    /// </summary>
    /// <remarks>
    /// Without this, states of removed or renamed settings would stay in the settings file forever.
    /// </remarks>
    private static bool RemoveUnknownManagedStates(IReadOnlySet<string> registeredSettingNames)
    {
        var wasChanged = false;
        var configurationData = SettingsManagerAccess.ConfigurationData;

        foreach (var settingName in configurationData.ManagedLockedConfigurations.Keys.Where(x => !registeredSettingNames.Contains(x)).ToList())
        {
            Log.LogInformation($"Removing the persisted lock of the setting '{settingName}': this setting does not exist anymore.");
            configurationData.ManagedLockedConfigurations.Remove(settingName);
            wasChanged = true;
        }

        foreach (var settingName in configurationData.ManagedEditableDefaults.Keys.Where(x => !registeredSettingNames.Contains(x)).ToList())
        {
            Log.LogInformation($"Removing the persisted editable default of the setting '{settingName}': this setting does not exist anymore.");
            configurationData.ManagedEditableDefaults.Remove(settingName);
            wasChanged = true;
        }

        return wasChanged;
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

    private static string SettingName<TClass, TValue>(Expression<Func<TClass, TValue>> propertyExpression) => SettingsManager.ToSettingName(propertyExpression);

    private static bool TryGetEditableDefaultState(string settingName, out ManagedEditableDefaultState editableDefaultState)
    {
        return SettingsManagerAccess.ConfigurationData.ManagedEditableDefaults.TryGetValue(settingName, out editableDefaultState!);
    }

    private static void SetEditableDefaultState(string settingName, Guid pluginId, string lastAppliedValue)
    {
        SettingsManagerAccess.ConfigurationData.ManagedEditableDefaults[settingName] = new()
        {
            ConfigPluginId = pluginId,
            LastAppliedValue = lastAppliedValue,
        };
    }

    private static bool ClearEditableDefaultState(string settingName) => SettingsManagerAccess.ConfigurationData.ManagedEditableDefaults.Remove(settingName);

    private static bool CleanupEditableDefaultState(ConfigMetaBase configMeta, IReadOnlyCollection<IAvailablePlugin> availablePlugins, IReadOnlySet<Guid> deployedEnterpriseConfigPluginIds)
    {
        if (!TryGetEditableDefaultState(configMeta.SettingName, out var editableDefaultState))
        {
            if (configMeta.ManagedMode is not ManagedConfigurationMode.EDITABLE_DEFAULT)
                return false;

            configMeta.ClearEditableDefaultConfiguration();
            return true;
        }

        if (IsPluginPresent(editableDefaultState.ConfigPluginId, availablePlugins, deployedEnterpriseConfigPluginIds))
            return false;

        Log.LogInformation($"Clearing the editable default of the setting '{configMeta.SettingName}': the configuration plugin '{editableDefaultState.ConfigPluginId}' is not available anymore.");
        configMeta.ClearEditableDefaultConfiguration();
        return ClearEditableDefaultState(configMeta.SettingName);
    }
}