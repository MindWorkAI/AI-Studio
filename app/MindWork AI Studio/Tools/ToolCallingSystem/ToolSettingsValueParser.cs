namespace AIStudio.Tools.ToolCallingSystem;

internal static class ToolSettingsValueParser
{
    public static int? ReadOptionalPositiveInt(IReadOnlyDictionary<string, string> settingsValues, string key)
    {
        if (!settingsValues.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, out var parsedValue) && parsedValue > 0 ? parsedValue : null;
    }

    public static bool TryReadOptionalPositiveInt(
        IReadOnlyDictionary<string, string> settingsValues,
        string key,
        string invalidValueErrorFormat,
        out int? value,
        out string error)
    {
        value = null;
        error = string.Empty;

        if (!settingsValues.TryGetValue(key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            return true;

        if (int.TryParse(rawValue, out var parsedValue) && parsedValue > 0)
        {
            value = parsedValue;
            return true;
        }

        error = string.Format(invalidValueErrorFormat, key);
        return false;
    }

    public static bool TryReadBoundedOptionalPositiveInt(
        IReadOnlyDictionary<string, string> settingsValues,
        string key,
        int maximum,
        string invalidValueErrorFormat,
        string maximumErrorFormat,
        out int? value,
        out string error)
    {
        if (!TryReadOptionalPositiveInt(settingsValues, key, invalidValueErrorFormat, out value, out error))
            return false;

        if (value is null || value <= maximum)
            return true;

        error = string.Format(maximumErrorFormat, key, maximum);
        return false;
    }
}
