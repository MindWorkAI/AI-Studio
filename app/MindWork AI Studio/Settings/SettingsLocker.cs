using System.Linq.Expressions;

namespace AIStudio.Settings;

public sealed class SettingsLocker
{
    private readonly Dictionary<string, Dictionary<string, Guid>> lockedProperties = new();
    
    /// <summary>
    /// Registers a property of a class to be locked by a specific configuration plugin ID.
    /// </summary>
    /// <param name="propertyExpression">The property expression to lock.</param>
    /// <param name="configurationPluginId">The ID of the configuration plugin that locks the property.</param>
    /// <typeparam name="T">The type of the class that contains the property.</typeparam>
    public void Register<T>(Expression<Func<T, object>> propertyExpression, Guid configurationPluginId)
    {
        var memberExpression = propertyExpression.GetMemberExpression();
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        if (!this.lockedProperties.ContainsKey(className))
            this.lockedProperties[className] = [];
            
        this.lockedProperties[className].TryAdd(propertyName, configurationPluginId);
    }
    
    /// <summary>
    /// Removes the lock for a property of a class.
    /// </summary>
    /// <param name="propertyExpression">The property expression to remove the lock for.</param>
    /// <typeparam name="T">The type of the class that contains the property.</typeparam>
    public void Remove<T>(Expression<Func<T, object>> propertyExpression)
    {
        var memberExpression = propertyExpression.GetMemberExpression();
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        if (this.lockedProperties.TryGetValue(className, out var props))
        {
            if (props.Remove(propertyName))
            {
                // If the property was removed, check if the class has no more locked properties:
                if (props.Count == 0)
                    this.lockedProperties.Remove(className);
            }
        }
    }
    
    /// <summary>
    /// Gets the configuration plugin ID that locks a specific property of a class.
    /// </summary>
    /// <param name="propertyExpression"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Guid GetConfigurationPluginId<T>(Expression<Func<T, object>> propertyExpression)
    {
        var memberExpression = propertyExpression.GetMemberExpression();
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        if (this.lockedProperties.TryGetValue(className, out var props) && props.TryGetValue(propertyName, out var configurationPluginId))
            return configurationPluginId;

        // No configuration plugin ID found for this property:
        return Guid.Empty;
    }
    
    public bool IsLocked<T>(Expression<Func<T, object>> propertyExpression)
    {
        var memberExpression = propertyExpression.GetMemberExpression();
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        return this.lockedProperties.TryGetValue(className, out var props) && props.ContainsKey(propertyName);
    }
}