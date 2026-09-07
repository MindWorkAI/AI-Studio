using System.Linq.Expressions;

using AIStudio.Settings.DataModel;

using LuaTable = Lua.LuaTable;
using LuaValueType = Lua.LuaValueType;

namespace AIStudio.Settings;

public static partial class ManagedConfiguration
{
    public static bool TryProcessProfileIds<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, ISet<string>>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        if (!TryGet(configSelection, propertyExpression, out var configMeta))
            return false;

        var successful = false;
        ISet<string> configuredValue = configMeta.Default;
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            successful = TryReadProfileIds(valueTable, out var values);
            configuredValue = values;
        }

        return HandleParsedScalarValue(
            configPluginId,
            dryRun,
            successful,
            configMeta,
            configuredValue,
            ReadManagedConfigurationMode(propertyExpression, settings),
            SettingsManager.ToSettingName(propertyExpression));
    }

    public static bool TryProcessLegacyProfileIds<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, ISet<string>>> propertyExpression,
        string legacySettingName,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        if (!TryGet(configSelection, propertyExpression, out var configMeta) ||
            !settings.TryGetValue(legacySettingName, out var legacyValue) ||
            !legacyValue.TryRead<string>(out var legacyProfileId))
            return false;

        if (!string.IsNullOrWhiteSpace(legacyProfileId) && !Guid.TryParse(legacyProfileId, out _))
            return false;

        ISet<string> configuredValue = Guid.TryParse(legacyProfileId, out var profileId) && profileId != Guid.Empty
            ? new HashSet<string> { profileId.ToString() }
            : new HashSet<string>();

        return HandleParsedScalarValue(
            configPluginId,
            dryRun,
            true,
            configMeta,
            configuredValue,
            ReadManagedConfigurationMode(propertyExpression, settings),
            SettingsManager.ToSettingName(propertyExpression));
    }

    public static HashSet<string>? RegisterProfilePreselection<TClass>(
        Expression<Func<Data, TClass>>? configSelection,
        Expression<Func<TClass, HashSet<string>?>> propertyExpression)
    {
        if (configSelection is null)
            return null;

        var configPath = Path(configSelection, propertyExpression);
        if (!METADATA.ContainsKey(configPath))
        {
            METADATA[configPath] = new ConfigMeta<TClass, HashSet<string>?>(configSelection, propertyExpression)
            {
                Default = null,
                AllowNullSnapshot = true,
            };
        }

        return null;
    }

    public static bool TryGetProfilePreselection<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, HashSet<string>?>> propertyExpression,
        out ConfigMeta<TClass, HashSet<string>?> configMeta)
    {
        var configPath = Path(configSelection, propertyExpression);
        if (METADATA.TryGetValue(configPath, out var value) && value is ConfigMeta<TClass, HashSet<string>?> meta)
        {
            meta.RestoreLockedConfiguration();
            configMeta = meta;
            return true;
        }

        configMeta = new NoConfig<TClass, HashSet<string>?>(configSelection, propertyExpression)
        {
            Default = null,
        };
        return false;
    }

    public static bool TryProcessProfilePreselection<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, HashSet<string>?>> propertyExpression,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        if (!TryGetProfilePreselection(configSelection, propertyExpression, out var configMeta))
            return false;

        var successful = false;
        HashSet<string>? configuredValue = null;
        if (settings.TryGetValue(SettingsManager.ToSettingName(propertyExpression), out var configuredLuaList) &&
            configuredLuaList.Type is LuaValueType.Table &&
            configuredLuaList.TryRead<LuaTable>(out var valueTable))
        {
            successful = TryReadProfileIds(valueTable, out var values);

            if (successful)
                configuredValue = values;
        }

        if (dryRun)
            return successful;

        return HandleParsedScalarValue(
            configPluginId,
            dryRun,
            successful,
            configMeta,
            configuredValue,
            ReadManagedConfigurationMode(propertyExpression, settings),
            SettingsManager.ToSettingName(propertyExpression));
    }

    public static bool TryProcessLegacyProfilePreselection<TClass>(
        Expression<Func<Data, TClass>> configSelection,
        Expression<Func<TClass, HashSet<string>?>> propertyExpression,
        string legacySettingName,
        Guid configPluginId,
        LuaTable settings,
        bool dryRun)
    {
        if (!TryGetProfilePreselection(configSelection, propertyExpression, out var configMeta) ||
            !settings.TryGetValue(legacySettingName, out var legacyValue) ||
            !legacyValue.TryRead<string>(out var legacyProfileId))
            return false;

        HashSet<string>? configuredValue;
        if (string.IsNullOrWhiteSpace(legacyProfileId))
            configuredValue = null;
        else if (!Guid.TryParse(legacyProfileId, out var parsedProfileId))
            return false;
        else
            configuredValue = parsedProfileId == Guid.Empty ? [] : [parsedProfileId.ToString()];

        return HandleParsedScalarValue(
            configPluginId,
            dryRun,
            true,
            configMeta,
            configuredValue,
            ReadManagedConfigurationMode(propertyExpression, settings),
            SettingsManager.ToSettingName(propertyExpression));
    }

    private static bool TryReadProfileIds(LuaTable valueTable, out HashSet<string> values)
    {
        values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index <= valueTable.ArrayLength; index++)
        {
            if (!valueTable[index].TryRead<string>(out var value) ||
                !Guid.TryParse(value, out var profileId) ||
                profileId == Guid.Empty ||
                !values.Add(profileId.ToString()))
                return false;
        }

        return true;
    }
}
