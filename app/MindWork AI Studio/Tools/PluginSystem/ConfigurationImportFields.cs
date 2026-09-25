using AIStudio.Tools;

using Lua;

namespace AIStudio.Tools.PluginSystem;

public static class ConfigurationImportFields
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfigurationImportFields).Namespace, nameof(ConfigurationImportFields));

    public static void ValidateExportId(LuaTable table)
    {
        if (!Guid.TryParse(String(table, "Id"), out _))
            throw new FormatException(TB("The exported item has an invalid ID."));
    }

    public static string String(LuaTable table, string name, bool required = true)
    {
        if (table.TryGetValue(name, out var value))
        {
            if (value.Type is LuaValueType.String && value.TryRead<string>(out var text))
                return text;
            throw new FormatException(string.Format(TB("The '{0}' field must be a string."), name));
        }
        if (!required)
            return string.Empty;
        throw new FormatException(string.Format(TB("The '{0}' field must be a string."), name));
    }

    public static LuaTable Table(LuaTable table, string name)
    {
        if (table.TryGetValue(name, out var value) && value.Type is LuaValueType.Table && value.TryRead<LuaTable>(out var nested))
            return nested;
        throw new FormatException(string.Format(TB("The '{0}' field must be a table."), name));
    }

    public static T Enum<T>(LuaTable table, string name) where T : struct, Enum
    {
        var text = String(table, name);
        if (System.Enum.TryParse<T>(text, true, out var result) && System.Enum.IsDefined(result))
            return result;
        throw new FormatException(string.Format(TB("The '{0}' field has an unknown value."), name));
    }

    public static bool Bool(LuaTable table, string name, bool fallback = false)
    {
        if (!table.TryGetValue(name, out var value))
            return fallback;
        if (value.Type is LuaValueType.Boolean && value.TryRead<bool>(out var result))
            return result;
        throw new FormatException(string.Format(TB("The '{0}' field must be true or false."), name));
    }

    public static int Int(LuaTable table, string name, int fallback = 0)
    {
        if (!table.TryGetValue(name, out var value))
            return fallback;
        if (value.Type is LuaValueType.Number && value.TryRead<double>(out var number) && number >= int.MinValue && number <= int.MaxValue && number == Math.Truncate(number))
            return (int)number;
        throw new FormatException(string.Format(TB("The '{0}' field must be a whole number."), name));
    }

    public static List<string> Strings(LuaTable table, string name)
    {
        var nested = Table(table, name);
        var result = new List<string>();
        for (var i = 1; i <= nested.ArrayLength; i++)
        {
            if (nested[i].Type is not LuaValueType.String || !nested[i].TryRead<string>(out var text) || string.IsNullOrWhiteSpace(text))
                throw new FormatException(string.Format(TB("The '{0}' field contains an invalid entry."), name));
            result.Add(text);
        }
        return result;
    }

    public static bool IsExistingLocalFile(string path) => Path.IsPathFullyQualified(path) && File.Exists(path);

    /// <summary>Lists the given references as a warning, or returns an empty text when nothing is missing.</summary>
    public static string UnavailableReferencesIssue(IReadOnlyCollection<string> missing) => missing.Count == 0
        ? string.Empty
        : string.Format(TB("Unavailable references: {0}. Review the selections before saving."), string.Join(", ", missing));

    public static string MissingProviderReference(string id) => string.Format(TB("provider {0}"), id);

    public static string MissingProfileReference(string id) => string.Format(TB("profile {0}"), id);

    public static string MissingToolReference(string id) => string.Format(TB("tool {0}"), id);

    public static string MissingDataSourceReference(string id) => string.Format(TB("data source {0}"), id);

    public static string Credential(LuaTable table, string name, out string issue, EnterpriseEncryption? decryptionService = null)
    {
        issue = string.Empty;
        var encrypted = String(table, name, required: false);
        if (string.IsNullOrEmpty(encrypted))
            return string.Empty;
        if (!EnterpriseEncryption.IsEncrypted(encrypted))
            throw new FormatException(string.Format(TB("The '{0}' field must contain an ENC:v1 credential."), name));
        var encryption = decryptionService ?? PluginFactory.EnterpriseEncryption;
        if (encryption?.IsAvailable == true && encryption.TryDecrypt(encrypted, out var decrypted))
            return decrypted;
        issue = TB("The embedded credential could not be decrypted on this device. Enter your own credential before saving.");
        return string.Empty;
    }
}