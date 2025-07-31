using System.Linq.Expressions;

namespace AIStudio.Settings;

public sealed class SettingsLocker
{
    private readonly Dictionary<string, Dictionary<string, Guid>> lockedProperties = new();
    
    public void Register<T>(Expression<Func<T, object>> propertyExpression, Guid configurationPluginId)
    {
        var memberExpression = propertyExpression.GetMemberExpression();
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        if (!this.lockedProperties.ContainsKey(className))
            this.lockedProperties[className] = [];
            
        this.lockedProperties[className].TryAdd(propertyName, configurationPluginId);
    }
    
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