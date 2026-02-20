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
    /// Indicates whether the configuration is locked by a configuration plugin.
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// The ID of the plugin that locked this configuration.
    /// </summary>
    public Guid LockedByConfigPluginId { get; private set; }
    
    /// <summary>
    /// The default value for the configuration property. This is used when resetting the property to its default state.
    /// </summary>
    public required TValue Default { get; init; }

    /// <summary>
    /// Indicates whether a plugin contribution is available.
    /// </summary>
    public bool HasPluginContribution { get; private set; }

    /// <summary>
    /// The additive value contribution provided by a configuration plugin.
    /// </summary>
    public TValue PluginContribution { get; private set; } = default!;

    /// <summary>
    /// The ID of the plugin that provided the additive value contribution.
    /// </summary>
    public Guid PluginContributionByConfigPluginId { get; private set; }

    /// <summary>
    /// Locks the configuration state, indicating that it is controlled by a specific plugin.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin that is locking this configuration.</param>
    public void LockConfiguration(Guid pluginId)
    {
        this.IsLocked = true;
        this.LockedByConfigPluginId = pluginId;
    }
    
    /// <summary>
    /// Resets the locked state of the configuration, allowing it to be modified again.
    /// This will also reset the property to its default value.
    /// </summary>
    public void ResetLockedConfiguration()
    {
        this.IsLocked = false;
        this.LockedByConfigPluginId = Guid.Empty;
        this.Reset();
    }

    /// <summary>
    /// Unlocks the configuration state without changing the current value.
    /// </summary>
    public void UnlockConfiguration()
    {
        this.IsLocked = false;
        this.LockedByConfigPluginId = Guid.Empty;
    }

    /// <summary>
    /// Stores an additive plugin contribution.
    /// </summary>
    public void SetPluginContribution(TValue value, Guid pluginId)
    {
        this.PluginContribution = value;
        this.PluginContributionByConfigPluginId = pluginId;
        this.HasPluginContribution = true;
    }

    /// <summary>
    /// Clears the additive plugin contribution without changing the current value.
    /// </summary>
    public void ClearPluginContribution()
    {
        this.PluginContribution = default!;
        this.PluginContributionByConfigPluginId = Guid.Empty;
        this.HasPluginContribution = false;
    }
    
    /// <summary>
    /// Resets the configuration property to its default value.
    /// </summary>
    private void Reset()
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