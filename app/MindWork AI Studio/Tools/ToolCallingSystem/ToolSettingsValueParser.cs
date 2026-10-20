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

    /// <summary>
    /// Reads an optional count which may be zero, such as a number of items where zero switches
    /// something off. Null when it is not set or cannot be read.
    /// </summary>
    public static int? ReadOptionalNonNegativeInt(IReadOnlyDictionary<string, string> settingsValues, string key)
    {
        if (!settingsValues.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, out var parsedValue) && parsedValue >= 0 ? parsedValue : null;
    }

    /// <summary>
    /// Checks an optional count which may be zero and has an upper bound, see ReadOptionalNonNegativeInt.
    /// </summary>
    /// <param name="invalidValueErrorFormat">The error for a value which is no whole number of zero or more. Receives the key as {0}.</param>
    /// <param name="maximumErrorFormat">The error for a value above the maximum. Receives the key as {0} and the maximum as {1}.</param>
    public static bool TryReadBoundedOptionalNonNegativeInt(
        IReadOnlyDictionary<string, string> settingsValues,
        string key,
        int maximum,
        string invalidValueErrorFormat,
        string maximumErrorFormat,
        out string error)
    {
        error = string.Empty;
        if (!settingsValues.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return true;

        if (!int.TryParse(value, out var parsedValue) || parsedValue < 0)
        {
            error = string.Format(invalidValueErrorFormat, key);
            return false;
        }

        if (parsedValue <= maximum)
            return true;

        error = string.Format(maximumErrorFormat, key, maximum);
        return false;
    }

    /// <summary>
    /// Checks that a stored value is one the option source still offers.
    /// </summary>
    /// <remarks>
    /// An empty value passes: whether the field may be empty is decided by the settings schema's
    /// required list, which the tool settings service checks before this method runs.<br/><br/>
    /// The error text comes from the caller, like the other checks here, so that it is translated
    /// in the tool which reports it. It receives the key as {0} and the stored value as {1}.
    /// </remarks>
    public static bool TryValidateOptionValue(IReadOnlyDictionary<string, string> settingsValues, string key, string optionSource, string invalidValueErrorFormat, out string error)
    {
        error = string.Empty;
        var value = settingsValues.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(value) || ToolSettingsOptionSources.GetValues(optionSource).Contains(value))
            return true;

        error = string.Format(invalidValueErrorFormat, key, value);
        return false;
    }
}
