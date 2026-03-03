using System.Linq.Expressions;

using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

public static partial class ManagedConfiguration
{
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
    public static TValue Register<TClass, TValue>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, TValue>> propertyExpression,
        TValue defaultValue)
        where TValue : struct
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if (configSelection is null)
            return defaultValue;

        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return defaultValue;

        // Not registered yet, so we register it now:
        METADATA[configPath] = new ConfigMeta<TClass, TValue>(configSelection, propertyExpression)
        {
            Default = defaultValue,
        };

        return defaultValue;
    }

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
    /// <returns>The default value.</returns>
    public static string Register<TClass>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, string>> propertyExpression,
        string defaultValue)
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if(configSelection is null)
            return defaultValue;
		
        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return defaultValue;
        
        // Not registered yet, so we register it now:
        METADATA[configPath] = new ConfigMeta<TClass, string>(configSelection, propertyExpression)
        {
            Default = defaultValue,
        };

        return defaultValue;
    }

    /// <summary>
    /// Registers a configuration setting with a default value for a IList of TValue.
    /// </summary>
    /// <remarks>
    /// If the configSelection parameter is null, the method returns a list containing the default value
    /// without registering the configuration setting.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the property within the configuration class.</param>
    /// <param name="defaultValue">The default value to use when the setting is not configured.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the elements in the list within the configuration class.</typeparam>
    /// <returns>A list containing the default value.</returns>
    public static List<TValue> Register<TClass, TValue>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, IList<TValue>>> propertyExpression,
        TValue defaultValue)
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if(configSelection is null)
            return [defaultValue];
		
        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return [defaultValue];
        
        // Not registered yet, so we register it now:
        METADATA[configPath] = new ConfigMeta<TClass, IList<TValue>>(configSelection, propertyExpression)
        {
            Default = [defaultValue],
        };

        return [defaultValue];
    }

    /// <summary>
    /// Registers a configuration setting with multiple default values.
    /// </summary>
    /// <remarks>
    /// When called with a null configSelection parameter, the method ignores the register call and directly returns the default values.
    /// If the configuration path already exists in the metadata, the method also returns the default values without registering new metadata.
    /// </remarks>
    /// <param name="configSelection">The expression used to select the configuration class.</param>
    /// <param name="propertyExpression">The expression used to select the property within the configuration class.</param>
    /// <param name="defaultValues">The list of default values to be used when the configuration setting is not defined.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the elements within the property list.</typeparam>
    /// <returns>The list of default values.</returns>
    public static List<TValue> Register<TClass, TValue>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, IList<TValue>>> propertyExpression,
        IList<TValue> defaultValues)
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if(configSelection is null)
            return [..defaultValues];
		
        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return [..defaultValues];
        
        // Not registered yet, so we register it now:
        METADATA[configPath] = new ConfigMeta<TClass, IList<TValue>>(configSelection, propertyExpression)
        {
            Default = [..defaultValues],
        };

        return [..defaultValues];
    }

    /// <summary>
    /// Registers a configuration setting with a default value.
    /// </summary>
    /// <remarks>
    /// When called with a null configSelection, this method returns the default value without registering the setting.
    /// </remarks>
    /// <param name="configSelection">The expression to select the configuration class.</param>
    /// <param name="propertyExpression">The expression to select the set within the configuration class.</param>
    /// <param name="defaultValue">The default value to use when the setting is not configured.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the values within the set.</typeparam>
    /// <returns>A set containing the default value.</returns>
    public static HashSet<TValue> Register<TClass, TValue>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, ISet<TValue>>> propertyExpression,
        TValue defaultValue)
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if (configSelection is null)
            return [defaultValue];
		
        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return [defaultValue];
        
        // Not registered yet, so we register it now:
        METADATA[configPath] = new ConfigMeta<TClass, ISet<TValue>>(configSelection, propertyExpression)
        {
            Default = new HashSet<TValue> { defaultValue },
        };

        return [defaultValue];
    }

    /// <summary>
    /// Registers a configuration setting with a collection of default values.
    /// </summary>
    /// <remarks>
    /// When the method is invoked with a null configSelection, the configuration path
    /// is ignored, and the specified default values are returned without registration.
    /// </remarks>
    /// <param name="configSelection">The expression that selects the configuration class from the root Data model.</param>
    /// <param name="propertyExpression">The expression to select the property within the configuration class.</param>
    /// <param name="defaultValues">The default collection of values to use when the setting is not configured.</param>
    /// <typeparam name="TClass">The type of the configuration class from which the property is selected.</typeparam>
    /// <typeparam name="TValue">The type of the elements in the collection associated with the configuration property.</typeparam>
    /// <returns>A set containing the default values.</returns>
    public static HashSet<TValue> Register<TClass, TValue>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, ISet<TValue>>> propertyExpression,
        IList<TValue> defaultValues)
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if (configSelection is null)
            return [..defaultValues];
		
        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return [..defaultValues];
        
        // Not registered yet, so we register it now:
        METADATA[configPath] = new ConfigMeta<TClass, ISet<TValue>>(configSelection, propertyExpression)
        {
            Default = new HashSet<TValue>(defaultValues),
        };

        return [..defaultValues];
    }

    /// <summary>
    /// Registers a configuration setting with a default dictionary of string key-value pairs.
    /// </summary>
    /// <remarks>
    /// When the method is invoked with a null configSelection, the configuration path
    /// is ignored, and the specified default values are returned without registration.
    /// </remarks>
    /// <param name="configSelection">The expression that selects the configuration class from the root Data model.</param>
    /// <param name="propertyExpression">The expression to select the property within the configuration class.</param>
    /// <param name="defaultValues">The default dictionary of values to use when the setting is not configured.</param>
    /// <typeparam name="TClass">The type of the configuration class from which the property is selected.</typeparam>
    /// <typeparam name="TDict">>The type of the dictionary within the configuration class.</typeparam>
    /// <returns>A dictionary containing the default values.</returns>
    public static TDict Register<TClass, TDict>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, IDictionary<string, string>>> propertyExpression,
        TDict defaultValues)
        where TDict : IDictionary<string, string>, new()
    {
        // When called from the JSON deserializer by using the standard constructor,
        // we ignore the register call and return the default value:
        if (configSelection is null)
            return new();

        var configPath = Path(configSelection, propertyExpression);

        // If the metadata already exists for this configuration path, we return the default value:
        if (METADATA.ContainsKey(configPath))
            return defaultValues;

        // Not registered yet, so we register it now:
        METADATA[configPath] = new ConfigMeta<TClass, IDictionary<string, string>>(configSelection, propertyExpression)
        {
            Default = defaultValues,
        };
        
        return defaultValues;
    }
}