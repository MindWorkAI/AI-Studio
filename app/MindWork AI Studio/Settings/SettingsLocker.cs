using System.Linq.Expressions;

namespace AIStudio.Settings;

public sealed class SettingsLocker
{
    private static readonly ILogger<SettingsLocker> LOGGER = Program.LOGGER_FACTORY.CreateLogger<SettingsLocker>();
    private readonly Dictionary<string, Dictionary<string, Guid>> lockedProperties = new();
    
    public void Register<T>(Expression<Func<T, object>> propertyExpression, Guid configurationPluginId)
    {
        var memberExpression = GetMemberExpression(propertyExpression);
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        if (!this.lockedProperties.ContainsKey(className))
            this.lockedProperties[className] = [];
            
        this.lockedProperties[className].TryAdd(propertyName, configurationPluginId);
    }
    
    public void Remove<T>(Expression<Func<T, object>> propertyExpression)
    {
        var memberExpression = GetMemberExpression(propertyExpression);
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
        var memberExpression = GetMemberExpression(propertyExpression);
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        if (this.lockedProperties.TryGetValue(className, out var props) && props.TryGetValue(propertyName, out var configurationPluginId))
            return configurationPluginId;

        // No configuration plugin ID found for this property:
        return Guid.Empty;
    }
    
    public bool IsLocked<T>(Expression<Func<T, object>> propertyExpression)
    {
        var memberExpression = GetMemberExpression(propertyExpression);
        var className = typeof(T).Name;
        var propertyName = memberExpression.Member.Name;
        
        return this.lockedProperties.TryGetValue(className, out var props) && props.ContainsKey(propertyName);
    }
    
    private static MemberExpression GetMemberExpression<T>(Expression<Func<T, object>> expression)
    {
        switch (expression.Body)
        {
            // Case for value types, which are wrapped in UnaryExpression:
            case UnaryExpression { NodeType: ExpressionType.Convert } unaryExpression:
                return (MemberExpression)unaryExpression.Operand;

            // Case for reference types, which are directly MemberExpressions:
            case MemberExpression memberExpression:
                return memberExpression;
            
            default:
                LOGGER.LogError($"Expression '{expression}' is not a valid property expression.");
                throw new ArgumentException($"Expression '{expression}' is not a valid property expression.", nameof(expression));
        }
    }
}