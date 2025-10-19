using System.Collections.Concurrent;
using System.Linq.Expressions;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

public static partial class ManagedConfiguration
{
    private static readonly ConcurrentDictionary<string, IConfig> METADATA = new();

    /// <summary>
    /// Attempts to retrieve the configuration metadata for a given configuration selection and
    /// property expression.
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
    public static bool TryGet<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, TValue>> propertyExpression,
        out ConfigMeta<TClass, TValue> configMeta)
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
    public static bool TryGet<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, IList<TValue>>> propertyExpression,
        out ConfigMeta<TClass, IList<TValue>> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, IList<TValue>> meta)
        {
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
    public static bool TryGet<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, ISet<TValue>>> propertyExpression,
        out ConfigMeta<TClass, ISet<TValue>> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, ISet<TValue>> meta)
        {
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
    public static bool TryGet<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, IDictionary<string, string>>> propertyExpression,
        out ConfigMeta<TClass, IDictionary<string, string>> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, IDictionary<string, string>> meta)
        {
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, IDictionary<string, string>>(configSelection, propertyExpression)
        {
            Default = [],
        };
        return false;
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