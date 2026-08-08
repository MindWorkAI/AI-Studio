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
    public ConfigMeta(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression) : base(SettingsManager.ToSettingName(propertyExpression))
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
    /// The default value for the configuration property. This is used when resetting the property to its default state.
    /// </summary>
    public required TValue Default { get; init; }

    /// <summary>
    /// The additive value contributions, one per contributing configuration plugin.
    /// </summary>
    /// <remarks>
    /// Every configuration plugin keeps its own contribution, so removing one of them leaves the
    /// contributions of the others intact. Callers that need the overall contribution combine the
    /// values themselves: only they know how to combine the concrete type.
    /// </remarks>
    public IReadOnlyDictionary<Guid, TValue> PluginContributions => this.pluginContributions;

    /// <inheritdoc/>
    public override IReadOnlyCollection<Guid> ContributingConfigPluginIds => this.pluginContributions.Keys;

    private readonly Dictionary<Guid, TValue> pluginContributions = [];

    /// <summary>
    /// Stores the additive contribution of one configuration plugin, replacing its previous one.
    /// </summary>
    /// <param name="value">The contributed value.</param>
    /// <param name="pluginId">The contributing configuration plugin.</param>
    public void SetPluginContribution(TValue value, Guid pluginId) => this.pluginContributions[pluginId] = value;

    /// <inheritdoc/>
    public override bool RemovePluginContribution(Guid configPluginId) => this.pluginContributions.Remove(configPluginId);

    /// <inheritdoc/>
    protected override void Reset()
    {
        var configInstance = this.ConfigSelection.Compile().Invoke(SettingsManagerAccess.ConfigurationData);
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
        var configInstance = this.ConfigSelection.Compile().Invoke(SettingsManagerAccess.ConfigurationData);
        var memberExpression = this.PropertyExpression.GetMemberExpression();
        if (memberExpression.Member is System.Reflection.PropertyInfo propertyInfo)
            propertyInfo.SetValue(configInstance, value);
    }

    /// <summary>
    /// Gets the current value of the configuration property.
    /// </summary>
    public TValue GetValue()
    {
        var configInstance = this.ConfigSelection.Compile().Invoke(SettingsManagerAccess.ConfigurationData);
        var memberExpression = this.PropertyExpression.GetMemberExpression();
        if (memberExpression.Member is System.Reflection.PropertyInfo propertyInfo && propertyInfo.GetValue(configInstance) is TValue value)
            return value;

        return default!;
    }
}