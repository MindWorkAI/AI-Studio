using System.Linq.Expressions;

using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

/// <summary>
/// Represents configuration metadata for a specific class and property.
/// </summary>
/// <typeparam name="TClass">The class type that contains the configuration property.</typeparam>
/// <typeparam name="TValue">The type of the configuration property value.</typeparam>
public record ConfigMeta<TClass, TValue> : ConfigMetaBase
{
    public ConfigMeta(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression)
    {
        this.ConfigSelection = configSelection;
        this.PropertyExpression = propertyExpression;
    }

    /// <summary>
    /// The expression to select the configuration class from the settings data.
    /// </summary>
    private Expression<Func<Data, TClass>> ConfigSelection { get; }

    /// <summary>
    /// The expression to select the property within the configuration class.
    /// </summary>
    private Expression<Func<TClass, TValue>> PropertyExpression { get; }
	
    /// <summary>
    /// Indicates whether the configuration is managed by a plugin and is therefore locked.
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// The ID of the plugin that manages this configuration. This is set when the configuration is locked.
    /// </summary>
    public Guid MangedByConfigPluginId { get; private set; }
    
    /// <summary>
    /// The default value for the configuration property. This is used when resetting the property to its default state.
    /// </summary>
    public required TValue Default { get; init; }

    /// <summary>
    /// Locks the configuration state, indicating that it is managed by a specific plugin.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin that is managing this configuration.</param>
    public void LockManagedState(Guid pluginId)
    {
        this.IsLocked = true;
        this.MangedByConfigPluginId = pluginId;
    }
    
    /// <summary>
    /// Resets the managed state of the configuration, allowing it to be modified again.
    /// This will also reset the property to its default value.
    /// </summary>
    public void ResetManagedState()
    {
        this.IsLocked = false;
        this.MangedByConfigPluginId = Guid.Empty;
        this.Reset();
    }
    
    /// <summary>
    /// Resets the configuration property to its default value.
    /// </summary>
    public void Reset()
    {
        var configInstance = this.ConfigSelection.Compile().Invoke(SETTINGS_MANAGER.ConfigurationData);
        var memberExpression = this.PropertyExpression.GetMemberExpression();
        if (memberExpression.Member is System.Reflection.PropertyInfo propertyInfo)
            propertyInfo.SetValue(configInstance, this.Default);
    }
    
    /// <summary>
    /// Sets the value of the configuration property to the specified value.
    /// </summary>
    /// <param name="value">The value to set for the configuration property.</param>
    public void SetValue(TValue value)
    {
        var configInstance = this.ConfigSelection.Compile().Invoke(SETTINGS_MANAGER.ConfigurationData);
        var memberExpression = this.PropertyExpression.GetMemberExpression();
        if (memberExpression.Member is System.Reflection.PropertyInfo propertyInfo)
            propertyInfo.SetValue(configInstance, value);
    }
}