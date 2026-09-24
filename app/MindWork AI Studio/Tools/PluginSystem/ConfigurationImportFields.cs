using AIStudio.Tools;

using Lua;

namespace AIStudio.Tools.PluginSystem;

public static class ConfigurationImportFields
{
    public static void ValidateExportId(LuaTable table)
    {
        if (!Guid.TryParse(String(table, "Id"), out _))
            throw new FormatException("The exported item has an invalid ID.");
    }

    public static string String(LuaTable table, string name, bool required = true)
    {
        if (table.TryGetValue(name, out var value))
        {
            if (value.Type is LuaValueType.String && value.TryRead<string>(out var text))
                return text;
            throw new FormatException($"The '{name}' field must be a string.");
        }
        if (!required)
            return string.Empty;
        throw new FormatException($"The '{name}' field must be a string.");
    }

    public static LuaTable Table(LuaTable table, string name)
    {
        if (table.TryGetValue(name, out var value) && value.Type is LuaValueType.Table && value.TryRead<LuaTable>(out var nested))
            return nested;
        throw new FormatException($"The '{name}' field must be a table.");
    }

    public static T Enum<T>(LuaTable table, string name) where T : struct, Enum
    {
        var text = String(table, name);
        if (System.Enum.TryParse<T>(text, true, out var result) && System.Enum.IsDefined(result))
            return result;
        throw new FormatException($"The '{name}' field has an unknown value.");
    }

    public static bool Bool(LuaTable table, string name, bool fallback = false)
    {
        if (!table.TryGetValue(name, out var value))
            return fallback;
        if (value.Type is LuaValueType.Boolean && value.TryRead<bool>(out var result))
            return result;
        throw new FormatException($"The '{name}' field must be true or false.");
    }

    public static int Int(LuaTable table, string name, int fallback = 0)
    {
        if (!table.TryGetValue(name, out var value))
            return fallback;
        if (value.Type is LuaValueType.Number && value.TryRead<double>(out var number) && number >= int.MinValue && number <= int.MaxValue && number == Math.Truncate(number))
            return (int)number;
        throw new FormatException($"The '{name}' field must be a whole number.");
    }

    public static List<string> Strings(LuaTable table, string name)
    {
        var nested = Table(table, name);
        var result = new List<string>();
        for (var i = 1; i <= nested.ArrayLength; i++)
        {
            if (nested[i].Type is not LuaValueType.String || !nested[i].TryRead<string>(out var text) || string.IsNullOrWhiteSpace(text))
                throw new FormatException($"The '{name}' field contains an invalid entry.");
            result.Add(text);
        }
        return result;
    }

    public static bool IsExistingLocalFile(string path) => Path.IsPathFullyQualified(path) && File.Exists(path);

    public static string Credential(LuaTable table, string name, out string issue, EnterpriseEncryption? decryptionService = null)
    {
        issue = string.Empty;
        var encrypted = String(table, name, required: false);
        if (string.IsNullOrEmpty(encrypted))
            return string.Empty;
        if (!EnterpriseEncryption.IsEncrypted(encrypted))
            throw new FormatException($"The '{name}' field must contain an ENC:v1 credential.");
        var encryption = decryptionService ?? PluginFactory.EnterpriseEncryption;
        if (encryption?.IsAvailable == true && encryption.TryDecrypt(encrypted, out var decrypted))
            return decrypted;
        issue = "The embedded credential could not be decrypted on this device. Enter your own credential before saving.";
        return string.Empty;
    }
}
