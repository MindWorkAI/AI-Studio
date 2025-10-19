using System.Globalization;
using System.Linq.Expressions;

using AIStudio.Settings.DataModel;

using Lua;

namespace AIStudio.Settings;

public static partial class ManagedConfiguration
{
    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for enum types.
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
    /// <param name="_">An unused parameter to help with type inference for enum types. You might ignore it when calling the method.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, TValue>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun,
        TValue? _ = default)
        where TValue : Enum
    {
        //
        // Handle configured enum values
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;

        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value out of the Lua table:
        if(settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredEnumValue))
        {
            // Step 2 -- try to read the Lua value as a string:
            if(configuredEnumValue.TryRead<string>(out var configuredEnumText))
            {
                // Step 3 -- try to parse the string as the enum type:
                if (Enum.TryParse(typeof(TValue), configuredEnumText, true, out var configuredEnum))
                {
                    configuredValue = (TValue)configuredEnum;
                    successful = true;
                }
            }
        }
        
        if(dryRun)
            return successful;
        
        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }

    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for ISpanParsable types.
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
    /// <param name="_">An unused parameter to help with type inference. You might ignore it when calling the method.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, TValue>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun,
        ISpanParsable<TValue>? _ = null)
        where TValue : struct, ISpanParsable<TValue>
    {
        //
        // Handle configured ISpanParsable values
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaValue))
        {
            // Step 2a -- try to read the Lua value as a string:
            if (configuredLuaValue.Type is LuaValueType.String && configuredLuaValue.TryRead<string>(out var configuredLuaValueText))
            {
                // Step 3 -- try to parse the string as the target type:
                if (TValue.TryParse(configuredLuaValueText, CultureInfo.InvariantCulture, out var configuredParsedValue))
                {
                    configuredValue = configuredParsedValue;
                    successful = true;
                }
            }
            
            // Step 2b -- try to read the Lua value:
            if(configuredLuaValue.TryRead<TValue>(out var configuredLuaValueInstance))
            {
                configuredValue = configuredLuaValueInstance;
                successful = true;
            }
        }

        if(dryRun)
            return successful;
        
        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }
    
    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for string values.
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
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, string>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        //
        // Handle configured string values
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value out of the Lua table:
        if(settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredTextValue))
        {
            // Step 2 -- try to read the Lua value as a string:
            if(configuredTextValue.TryRead<string>(out var configuredText))
            {
                configuredValue = configuredText;
                successful = true;
            }
        }
        
        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }

    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for ISpanParsable list types.
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
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <param name="_">An unused parameter to help with type inference for ISpanParsable types. You might ignore it when calling the method.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class. It is also the type of the list
    /// elements, which must implement ISpanParsable.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>

    // ReSharper disable MethodOverloadWithOptionalParameter
    public static bool TryProcessConfiguration<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, IList<TValue>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun,
        ISpanParsable<TValue>? _ = null)
        where TValue : ISpanParsable<TValue>
    {
        //
        // Handle configured ISpanParsable lists
        //

        // Check if that configuration was registered:
        if (!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;

        var successful = false;
        var configuredValue = configMeta.Default;

        // Step 1 -- try to read the Lua value (we expect a table) out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            // Determine the length of the Lua table and prepare a list to hold the parsed values:
            var len = valueTable.ArrayLength;
            var list = new List<TValue>(len);
            
            // Iterate over each entry in the Lua table:
            for (var index = 1; index <= len; index++)
            {
                // Retrieve the Lua value at the current index:
                var value = valueTable[index];
                
                // Step 2a -- try to read the Lua value as a string:
                if (value.Type is LuaValueType.String && value.TryRead<string>(out var configuredLuaValueText))
                {
                    // Step 3 -- try to parse the string as the target type:
                    if (TValue.TryParse(configuredLuaValueText, CultureInfo.InvariantCulture, out var configuredParsedValue))
                        list.Add(configuredParsedValue);
                }

                // Step 2b -- try to read the Lua value:
                if (value.TryRead<TValue>(out var configuredLuaValueInstance))
                    list.Add(configuredLuaValueInstance);
            }

            configuredValue = list;
            successful = true;
        }

        if (dryRun)
            return successful;

        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }

    // ReSharper restore MethodOverloadWithOptionalParameter
    
    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for enum list types.
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
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class. It is also the type of the list
    /// elements, which must be an enum.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, IList<TValue>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
        where TValue : Enum
    {
        //
        // Handle configured enum lists
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value (we expect a table) out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            // Determine the length of the Lua table and prepare a list to hold the parsed values:
            var len = valueTable.ArrayLength;
            var list = new List<TValue>(len);
            
            // Iterate over each entry in the Lua table:
            for (var index = 1; index <= len; index++)
            {
                // Retrieve the Lua value at the current index:
                var value = valueTable[index];
                
                // Step 2 -- try to read the Lua value as a string:
                if (value.Type is LuaValueType.String && value.TryRead<string>(out var configuredLuaValueText))
                {
                    // Step 3 -- try to parse the string as the target type:
                    if (Enum.TryParse(typeof(TValue), configuredLuaValueText, true, out var configuredEnum))
                        list.Add((TValue)configuredEnum);
                }
            }
		
            configuredValue = list;
            successful = true;
        }
        
        if(dryRun)
            return successful;
        
        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }
    
    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for string list types.
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
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, IList<string>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        //
        // Handle configured string lists
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value (we expect a table) out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            // Determine the length of the Lua table and prepare a list to hold the parsed values:
            var len = valueTable.ArrayLength;
            var list = new List<string>(len);
            
            // Iterate over each entry in the Lua table:
            for (var index = 1; index <= len; index++)
            {
                // Retrieve the Lua value at the current index:
                var value = valueTable[index];
                
                // Step 2 -- try to read the Lua value as a string:
                if (value.Type is LuaValueType.String && value.TryRead<string>(out var configuredLuaValueText))
                    list.Add(configuredLuaValueText);
            }
		
            configuredValue = list;
            successful = true;
        }
        
        if(dryRun)
            return successful;
        
        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }

    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for ISpanParsable set types.
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
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <param name="_">An unused parameter to help with type inference for ISpanParsable types. You might ignore it when calling the method.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class. It is also the type of the set
    /// elements, which must implement ISpanParsable.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>

    // ReSharper disable MethodOverloadWithOptionalParameter
    public static bool TryProcessConfiguration<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, ISet<TValue>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun,
        ISpanParsable<TValue>? _ = null)
        where TValue : ISpanParsable<TValue>
    {
        //
        // Handle configured ISpanParsable sets
        //

        // Check if that configuration was registered:
        if (!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;

        var successful = false;
        var configuredValue = configMeta.Default;

        // Step 1 -- try to read the Lua value (we expect a table) out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            // Determine the length of the Lua table and prepare a set to hold the parsed values:
            var len = valueTable.ArrayLength;
            var set = new HashSet<TValue>(len);
            
            // Iterate over each entry in the Lua table:
            for (var index = 1; index <= len; index++)
            {
                // Retrieve the Lua value at the current index:
                var value = valueTable[index];
                
                // Step 2a -- try to read the Lua value as a string:
                if (value.Type is LuaValueType.String && value.TryRead<string>(out var configuredLuaValueText))
                {
                    // Step 3 -- try to parse the string as the target type:
                    if (TValue.TryParse(configuredLuaValueText, CultureInfo.InvariantCulture, out var configuredParsedValue))
                        set.Add(configuredParsedValue);
                }

                // Step 2b -- try to read the Lua value:
                if (value.TryRead<TValue>(out var configuredLuaValueInstance))
                    set.Add(configuredLuaValueInstance);
            }

            configuredValue = set;
            successful = true;
        }

        if (dryRun)
            return successful;

        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }

    // ReSharper restore MethodOverloadWithOptionalParameter
    
    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for enum set types.
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
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the property within the configuration class. It is also the type of the set
    /// elements, which must be an enum.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass, TValue>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, ISet<TValue>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
        where TValue : Enum
    {
        //
        // Handle configured enum sets
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value (we expect a table) out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            // Determine the length of the Lua table and prepare a set to hold the parsed values:
            var len = valueTable.ArrayLength;
            var set = new HashSet<TValue>(len);
            
            // Iterate over each entry in the Lua table:
            for (var index = 1; index <= len; index++)
            {
                // Retrieve the Lua value at the current index:
                var value = valueTable[index];
                
                // Step 2 -- try to read the Lua value as a string:
                if (value.Type is LuaValueType.String && value.TryRead<string>(out var configuredLuaValueText))
                {
                    // Step 3 -- try to parse the string as the target type:
                    if (Enum.TryParse(typeof(TValue), configuredLuaValueText, true, out var configuredEnum))
                        set.Add((TValue)configuredEnum);
                }
            }
		
            configuredValue = set;
            successful = true;
        }
        
        if(dryRun)
            return successful;
        
        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }
    
    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for string set types.
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
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, ISet<string>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        //
        // Handle configured string sets
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value (we expect a table) out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            // Determine the length of the Lua table and prepare a set to hold the parsed values:
            var len = valueTable.ArrayLength;
            var set = new HashSet<string>(len);
            
            // Iterate over each entry in the Lua table:
            for (var index = 1; index <= len; index++)
            {
                // Retrieve the Lua value at the current index:
                var value = valueTable[index];
                
                // Step 2 -- try to read the Lua value as a string:
                if (value.Type is LuaValueType.String && value.TryRead<string>(out var configuredLuaValueText))
                    set.Add(configuredLuaValueText);
            }
		
            configuredValue = set;
            successful = true;
        }
        
        if(dryRun)
            return successful;
        
        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }
    
    /// <summary>
    /// Attempts to process the configuration settings from a Lua table for string dictionary types.
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
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    public static bool TryProcessConfiguration<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, IDictionary<string, string>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        //
        // Handle configured string dictionaries (both keys and values are strings)
        //
        
        // Check if that configuration was registered:
        if(!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;
        
        var successful = false;
        var configuredValue = configMeta.Default;
        
        // Step 1 -- try to read the Lua value (we expect a table) out of the Lua table:
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            // Determine the length of the Lua table and prepare a dictionary to hold the parsed key-value pairs.
            // Instead of using ArrayLength, we use HashMapCount to get the number of key-value pairs:
            var len = valueTable.HashMapCount;
            if (len > 0)
                configuredValue.Clear();
            
            // In order to iterate over all key-value pairs in the Lua table, we have to use TryGetNext.
            // Thus, we initialize the previous key variable to Nil and keep calling TryGetNext until
            // there are no more pairs:
            var previousKey = LuaValue.Nil;
            while(valueTable.TryGetNext(previousKey, out var pair))
            {
                // Update the previous key for the next iteration:
                previousKey = pair.Key;
                
                // Try to read both the key and the value as strings:
                var hadKey = pair.Key.TryRead<string>(out var key);
                var hadValue = pair.Value.TryRead<string>(out var value);
                
                // If both key and value were read successfully, add them to the dictionary:
                if (hadKey && hadValue)
                    configuredValue[key] = value;
            }
		
            successful = true;
        }
        
        if(dryRun)
            return successful;

        return HandleParsedValue(configPluginId, dryRun, successful, configMeta, configuredValue);
    }

    /// <summary>
    /// Handles the parsed configuration value based on whether the parsing was successful and whether it's a dry run.
    /// </summary>
    /// <param name="configPluginId">The ID of the related configuration plugin.</param>
    /// <param name="dryRun">When true, no changes will be applied.</param>
    /// <param name="successful">Indicates whether the configuration value was successfully parsed.</param>
    /// <param name="configMeta">The configuration metadata.</param>
    /// <param name="configuredValue">>The parsed configuration value.</param>
    /// <typeparam name="TClass">The type of the configuration class.</typeparam>
    /// <typeparam name="TValue">The type of the configuration property value.</typeparam>
    /// <returns>True when the configuration was successfully processed, otherwise false.</returns>
    private static bool HandleParsedValue<TClass, TValue>(
        Guid configPluginId,
        bool dryRun,
        bool successful,
        ConfigMeta<TClass, TValue> configMeta,
        TValue configuredValue)
    {
        if(dryRun)
            return successful;
        
        switch (successful)
        {
            case true:
                //
                // Case: the setting was configured, and we could read the value successfully.
                //
                
                // Set the configured value and lock the managed state:
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
}