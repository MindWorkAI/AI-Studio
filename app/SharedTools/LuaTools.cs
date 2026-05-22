namespace SharedTools;

public static class LuaTools
{
    public static string EscapeLuaString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Replace backslashes with double backslashes and escape double quotes:
        return value
            .Replace("\\", @"\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    public static string ToLuaStringLiteral(string? value, bool forceLongString = false, int longStringLengthThreshold = 80)
    {
        value ??= string.Empty;
        if (!forceLongString &&
            value.Length <= longStringLengthThreshold &&
            !value.Contains('\n') &&
            !value.Contains('\r'))
            return $"\"{EscapeLuaString(value)}\"";

        return $"{CreateLongStringOpeningDelimiter(value)}{value}{CreateLongStringClosingDelimiter(value)}";
    }

    private static string CreateLongStringOpeningDelimiter(string value)
    {
        var equals = CreateLongStringEquals(value);
        return $"[{equals}[";
    }

    private static string CreateLongStringClosingDelimiter(string value)
    {
        var equals = CreateLongStringEquals(value);
        return $"]{equals}]";
    }

    private static string CreateLongStringEquals(string value)
    {
        var equalsCount = 3;
        while (value.Contains($"]{new string('=', equalsCount)}]"))
            equalsCount++;

        return new string('=', equalsCount);
    }
}